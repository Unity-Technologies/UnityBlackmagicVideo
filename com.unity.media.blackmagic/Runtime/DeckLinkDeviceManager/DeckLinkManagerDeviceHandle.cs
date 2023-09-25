using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

using static Unity.Media.Blackmagic.BaseDeckLinkDevice;

namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// The class used to manage the lifecycle of the DeckLink devices for the current scene.
    /// </summary>
    /// <remarks>
    /// It contains all the current input and output devices instantiated, and many callbacks for the device management.
    /// </remarks>
    partial class DeckLinkManager
    {
        [SerializeField]
        internal List<VideoDeviceGameObjectData> m_InputDevices = new List<VideoDeviceGameObjectData>();

        [SerializeField]
        internal List<VideoDeviceGameObjectData> m_OutputDevices = new List<VideoDeviceGameObjectData>();

        internal readonly List<string> m_InputDeviceNames = new List<string>();
        internal readonly List<string> m_OutputDeviceNames = new List<string>();

        GameObject m_MappingGameObject;

        /// <summary>
        /// Retrieves an array of all available input or output devices.
        /// </summary>
        /// <param name="deviceType">The video device type to retrieve.</param>
        /// <returns>The array which contains all available devices.</returns>
        public string[] GetAvailableDeviceNames(VideoDeviceType deviceType)
        {
            return GetDevices(deviceType).Select(x => x.Name).ToArray();
        }

        /// <summary>
        /// Retrieves the data of a video device from a specified name.
        /// </summary>
        /// <param name="deviceName">The name of the device to retrieve.</param>
        /// <param name="deviceType">The video type of the device to retrieve.</param>
        /// <param name="device">The variable that holds the data of the device to retrieve.</param>
        /// <returns>True if the device has been retrieved; false otherwise.</returns>
        internal bool GetDeviceDataByName(string deviceName, VideoDeviceType deviceType, out IVideoDeviceData device)
        {
            VideoDeviceGameObjectData deviceData;
            if (GetDeviceGameObjectData(deviceType, deviceName, out deviceData))
            {
                device = deviceData.VideoData;
                return true;
            }

            device = default;
            return false;
        }

        bool GetDeviceGameObjectData(VideoDeviceType deviceType, string deviceName, out VideoDeviceGameObjectData deviceData)
        {
            // 'GetDevices(x).Find()' is not used, at it's creating a nasty GC.Alloc
            var devices = GetDevices(deviceType);
            foreach (var d in devices)
            {
                if (d.Name.CompareTo(deviceName) == 0)
                {
                    deviceData = d;
                    return true;
                }
            }
            deviceData = null;
            return false;
        }

        internal VideoDeviceGameObjectData GetOrCreateDeviceInstance(string deviceName, VideoDeviceType deviceType)
        {
            Assert.IsFalse(String.IsNullOrEmpty(deviceName));

            if (GetExistingVideoDevice(deviceName, deviceType, out var existingDevice))
            {
                return existingDevice;
            }

            var parentGameObject = GetOrCreateConnectorMappingGameObject(connectorMapping);

            Assert.IsNotNull(parentGameObject);

            var newDeviceGameObject = new GameObject(deviceName);
            newDeviceGameObject.transform.SetParent(parentGameObject.transform);

            VideoDeviceGameObjectData deviceData;

            if (deviceType == VideoDeviceType.Input)
            {
                deviceData = LoadDevice<DeckLinkInputDevice>(newDeviceGameObject, newDeviceGameObject.name);
            }
            else if (deviceType == VideoDeviceType.Output)
            {
                deviceData = LoadDevice<DeckLinkOutputDevice>(newDeviceGameObject, newDeviceGameObject.name);
            }
            else
            {
                throw new InvalidOperationException($"Unexpected video device type {deviceType}.");
            }

            GetDevices(deviceType).Add(deviceData);

            return deviceData;
        }

        internal string RemoveDeviceInstance(int deviceIndex, VideoDeviceType deviceType)
        {
            var deviceInstances = GetDevices(deviceType);

            Assert.IsFalse(deviceIndex < 0 || deviceIndex > deviceInstances.Count);

            var existingDevice = deviceInstances[deviceIndex];
            var deviceName = existingDevice.Name;

            existingDevice.Dispose();

            RemoveCurrentConnectorMappingGameObject();
            deviceInstances.Remove(existingDevice);

            return deviceName;
        }

        internal void RemoveAllDevices(VideoDeviceType deviceType)
        {
            var devicesToRemove = GetDevices(deviceType);

            foreach (var device in devicesToRemove)
            {
                device.Dispose();
                Debug.Log($"{deviceType.ToString()} device removed from the Video I/O Manager: {device.Name}");
            }

            devicesToRemove.Clear();
        }

        internal bool GetExistingVideoDevice(string deviceName, VideoDeviceType deviceType, out VideoDeviceGameObjectData deviceData)
        {
            deviceData = GetDevices(deviceType).Find(x => x.Name == deviceName);
            return deviceData != null;
        }

        internal bool StopTheVideoDeviceIfInUse(int index, VideoDeviceType deviceType)
        {
            VideoDeviceGameObjectData deviceFound;

            // Only one device (Input or Output) can use the current index (a.k.a the 'current logical SDI device').
            // So if the current index is already used by any device, we want to stop this device.
            if (IsUniqueIndexDevice(m_DevicesConnectorMapping[m_DeckLinkCardIndex]))
            {
                if (GetDeviceDataFromVideoDeviceIndex(index, VideoDeviceType.Input, out deviceFound) &&
                    deviceFound.VideoDevice.UpdateInEditor)
                {
                    deviceFound.VideoDevice.UpdateInEditor = false;
                    return true;
                }

                if (GetDeviceDataFromVideoDeviceIndex(index, VideoDeviceType.Output, out deviceFound) &&
                    deviceFound.VideoDevice.UpdateInEditor)
                {
                    deviceFound.VideoDevice.UpdateInEditor = false;
                    return true;
                }

                return false;
            }

            // The current index (a.k.a 'logical SDI device') can be use on an Output device and an Input device at the same time.
            // So if the current index is already used by a device of the same type as the one we are trying to change,
            // we want to stop this device (as only one device of the same type can use a 'logical SDI device').
            if (GetDeviceDataFromVideoDeviceIndex(index, deviceType, out deviceFound) &&
                deviceFound.VideoDevice.UpdateInEditor)
            {
                deviceFound.VideoDevice.UpdateInEditor = false;
                return true;
            }

            return false;
        }

        internal bool IsUniqueIndexDevice(DeckLinkConnectorMapping connectorMapping)
        {
            return connectorMapping == DeckLinkConnectorMapping.FourSubDevicesHalfDuplex ||
                connectorMapping == DeckLinkConnectorMapping.OneSubDeviceHalfDuplex ||
                connectorMapping == DeckLinkConnectorMapping.TwoSubDevicesHalfDuplex;
        }

        internal void ChangeVideoDeviceNameData(string deviceName, VideoDeviceType deviceType, int index)
        {
            if (GetExistingVideoDevice(deviceName, deviceType, out var videoDeviceData))
            {
                if (IsUniqueIndexDevice(m_DevicesConnectorMapping[m_DeckLinkCardIndex]))
                {
                    if (GetDeviceDataFromVideoDeviceIndex(index, VideoDeviceType.Input, out var inputDevice))
                    {
                        inputDevice.SetCurrentVideoDevice(-1);
                    }
                    else if (GetDeviceDataFromVideoDeviceIndex(index, VideoDeviceType.Output, out var outputDevice))
                    {
                        outputDevice.SetCurrentVideoDevice(-1);
                    }
                }
                else
                {
                    if (GetDeviceDataFromVideoDeviceIndex(index, deviceType, out var device))
                    {
                        device.SetCurrentVideoDevice(-1);
                    }
                }

                videoDeviceData.SetCurrentVideoDevice(index, index);
            }
        }

        internal StatusType GetInputVideoSignalStatus(VideoDeviceGameObjectData device, out string status)
        {
            var inputDevice = device.VideoDevice as DeckLinkInputDevice;

            if (inputDevice == null || !inputDevice.IsActive)
            {
                status = String.Empty;
                return StatusType.Unused;
            }

            if (inputDevice.FrameStatus != default)
            {
                var frameStatus = inputDevice.FrameStatus;
                status = frameStatus.Item1;
                return frameStatus.Item2;
            }

            status = String.Empty;
            return StatusType.Ok;
        }

        internal StatusType GetOutputVideoSignalStatus(VideoDeviceGameObjectData device, out string status)
        {
            var outputDevice = device.VideoDevice as DeckLinkOutputDevice;

            if (outputDevice == null || !outputDevice.IsActive)
            {
                status = null;
                return StatusType.Unused;
            }

            if (outputDevice.FrameStatus != default)
            {
                var frameStatus = outputDevice.FrameStatus;
                status = frameStatus.Item1;
                return frameStatus.Item2;
            }

            status = null;
            return StatusType.Ok;
        }

        bool InitializeExistingMappingGameObject(DeckLinkConnectorMapping connectorMapping)
        {
            if (m_MappingGameObject != null &&
                !connectorMapping.ToString().Equals(m_MappingGameObject.name))
            {
                m_MappingGameObject.SetActive(false);
                m_MappingGameObject = null;
            }

            var mappingName = connectorMapping.ToString();
            int countIndex = 0;
            for (; countIndex < transform.childCount; ++countIndex)
            {
                var deviceGameObject = transform.GetChild(countIndex).gameObject;
                if (deviceGameObject.name.Equals(mappingName))
                {
                    m_MappingGameObject = deviceGameObject;
                    m_MappingGameObject.SetActive(true);
                    break;
                }
            }

            return m_MappingGameObject != null;
        }

        void UpdateDeviceIndex(VideoDeviceGameObjectData videoDeviceData)
        {
            if (IsDeviceAlreadyUsed(videoDeviceData))
                return;

            var videoDeviceIndex = videoDeviceData.VideoDevice.DeviceSelection;
            if (videoDeviceIndex < 0)
            {
                videoDeviceData.SetCurrentVideoDevice(-1, videoDeviceData.VideoDevice.OldDeviceSelection);
                return;
            }

            var listNames = GetDeviceNames(videoDeviceData.VideoDevice.DeviceType);
            if (videoDeviceIndex < listNames.Count)
            {
                videoDeviceData.CurrentDeviceIndex = videoDeviceIndex;
                return;
            }

            videoDeviceData.SetCurrentVideoDevice(-1, videoDeviceData.VideoDevice.OldDeviceSelection);
        }

        bool GetDeviceDataFromVideoDeviceIndex(int deviceIndex, VideoDeviceType deviceType, out VideoDeviceGameObjectData deviceData)
        {
            deviceData = GetDevices(deviceType).Find(x => x.CurrentDeviceIndex == deviceIndex);
            return deviceData != null;
        }

        bool IsDeviceAlreadyUsed(VideoDeviceGameObjectData videoDeviceData)
        {
            if (m_DeckLinkCardIndex > 0 && IsUniqueIndexDevice(m_DevicesConnectorMapping[m_DeckLinkCardIndex]))
            {
                if (videoDeviceData.VideoDevice.DeviceType == VideoDeviceType.Input)
                {
                    if (GetDevices(VideoDeviceType.Output).Find(x => x.CurrentDeviceIndex == videoDeviceData.VideoDevice.DeviceSelection) != null)
                    {
                        videoDeviceData.SetCurrentVideoDevice(-1, videoDeviceData.VideoDevice.OldDeviceSelection);
                        return true;
                    }
                }
                else if (videoDeviceData.VideoDevice.DeviceType == VideoDeviceType.Output)
                {
                    if (GetDevices(VideoDeviceType.Input).Find(x => x.CurrentDeviceIndex == videoDeviceData.VideoDevice.DeviceSelection) != null)
                    {
                        videoDeviceData.SetCurrentVideoDevice(-1, videoDeviceData.VideoDevice.OldDeviceSelection);
                        return true;
                    }
                }
            }
            return false;
        }

        void ResetDeviceData(bool resetNames = true)
        {
            m_InputDevices.Clear();
            m_OutputDevices.Clear();

            if (resetNames)
            {
                m_InputDeviceNames.Clear();
                m_OutputDeviceNames.Clear();
            }
        }

        void InitializeDevices(DeckLinkConnectorMapping connectorMapping)
        {
            var mappedGameObject = InitializeExistingMappingGameObject(connectorMapping);
            if (!mappedGameObject)
            {
                m_MappingGameObject = GetOrCreateConnectorMappingGameObject(connectorMapping);
            }

            if (transform.childCount == 0)
                return;

            var transformMapping = m_MappingGameObject.transform;

            for (var i = 0; i < transformMapping.childCount; ++i)
            {
                var gameObject = transformMapping.GetChild(i).gameObject;
                var inputDevice = gameObject.GetComponent<DeckLinkInputDevice>();
                var outputDevice = gameObject.GetComponent<DeckLinkOutputDevice>();
                var deviceName = gameObject.name;

                VideoDeviceGameObjectData videoDevice;

                if (inputDevice != null)
                {
                    videoDevice = LoadDevice(gameObject, deviceName, inputDevice);
                }
                else if (outputDevice != null)
                {
                    videoDevice = LoadDevice(gameObject, deviceName, outputDevice);
                }
                else
                {
                    throw new InvalidOperationException($"Couldn't load device from GameObject: {deviceName}");
                }

                UpdateDeviceIndex(videoDevice);
                var videoDevicesList = GetDevices(videoDevice.VideoDevice.DeviceType);
                videoDevicesList.Add(videoDevice);

                Debug.Log($"An {videoDevice.VideoDevice.DeviceType} device has been restored in the Video I/O Manager: {deviceName}");
            }
        }

        static VideoDeviceGameObjectData LoadDevice<T>(GameObject deviceGameObject, string deviceName, T device = null)
            where T : BaseDeckLinkDevice, IVideoDeviceData
        {
            device = deviceGameObject.GetComponent<T>();
            if (device == null)
            {
                device = deviceGameObject.AddComponent<T>();
            }

            return new VideoDeviceGameObjectData()
            {
                Name = deviceName,
                VideoDevice = device,
                VideoData = device,
                CurrentDeviceIndex = device.DeviceSelection,
                OldDeviceIndex = device.OldDeviceSelection
            };
        }

        List<VideoDeviceGameObjectData> GetDevices(VideoDeviceType deviceType)
        {
            return deviceType == VideoDeviceType.Input ? m_InputDevices : m_OutputDevices;
        }

        List<string> GetDeviceNames(VideoDeviceType deviceType)
        {
            return deviceType == VideoDeviceType.Input ? m_InputDeviceNames : m_OutputDeviceNames;
        }

        GameObject GetOrCreateConnectorMappingGameObject(DeckLinkConnectorMapping connectorMapping)
        {
            if (m_MappingGameObject == null)
            {
                m_MappingGameObject = new GameObject(connectorMapping.ToString());
                m_MappingGameObject.transform.SetParent(transform);
            }

            return m_MappingGameObject;
        }

        void RemoveCurrentConnectorMappingGameObject()
        {
            if (m_MappingGameObject.transform.childCount == 0)
            {
                DestroyImmediate(m_MappingGameObject);
                m_MappingGameObject = null;
            }
        }

        void TryToAssociateOldVideoDeviceIndex(string deviceName, VideoDeviceType deviceType)
        {
            Assert.IsFalse(String.IsNullOrEmpty(deviceName));

            var index = GetDeviceNames(deviceType).IndexOf(deviceName);
            var devices = GetDevices(deviceType);

            if (devices != null && devices.Count > 0)
            {
                var device = devices.Find(x => x?.OldDeviceIndex == index);
                if (device != null)
                {
                    device.SetCurrentVideoDevice(index, index);
                    Debug.Log($"{deviceName} successfully reloaded index {index}");
                }
            }
        }
    }
}
