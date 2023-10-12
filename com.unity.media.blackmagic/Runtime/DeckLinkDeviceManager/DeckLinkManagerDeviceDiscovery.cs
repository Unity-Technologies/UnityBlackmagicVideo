using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Media.Blackmagic
{
    using MappingDictionary = Dictionary<int, DeckLinkCard>;

    class DeckLinkCard
    {
        public string name;
        public Int64 groupID;

        public List<DeckLinkConnectorMapping> compatibleConnectorMappings;
        public List<string> availableLogicalDevices;

        public int usedInputDevices;
        public int usedOutputDevices;

        public Int32 compatibleLinkModes;
        public Int32 compatibleKeyingModes;

        public bool isKeyingCompatible;
        public bool isLinkModeCompatible;

        public DeckLinkCard(string deviceName)
        {
            name = deviceName;
            compatibleConnectorMappings = new List<DeckLinkConnectorMapping>();
            availableLogicalDevices = new List<string>();
            compatibleLinkModes = 0;
            compatibleKeyingModes = 0;
            groupID = 0;
            isKeyingCompatible = true;
            isLinkModeCompatible = true;
        }

        public void ResetValues()
        {
            compatibleConnectorMappings.Clear();
            compatibleLinkModes = 0;
            compatibleKeyingModes = 0;
            groupID = 0;
        }
    }

    /// <summary>
    /// The partial class which contains the callbacks when a device arrived or a device is removed.
    /// </summary>
    partial class DeckLinkManager
    {
        internal static IntPtr s_DeckLinkDeviceDiscovery = IntPtr.Zero;
        static DeckLinkDeviceDiscoveryPlugin.CallbackDevice s_ArrivedCallback;
        static DeckLinkDeviceDiscoveryPlugin.CallbackDevice s_RemovedCallback;
        static string k_DetectionError = "Detection error";

        string m_APIVersion;
        string m_HardwareModel;
        bool m_CacheCardCapacityOnStart = true;

        internal bool m_NoCardDetected = false;

        /// <summary>
        /// Triggers when then Connector Mapping profile(s) has changed.
        /// </summary>
        public event Action OnMappingProfilesChanged = delegate { };

        /// <summary>
        /// Determines if the current Connector Mapping profile is compatible with the card.
        /// </summary>
        /// <returns>True if the current Connector Mapping profile is compatible, otherwise false.</returns>
        public bool IsCurrentMappingCompatible()
        {
            if (m_DeckLinkCards.ContainsKey(m_DeckLinkCardIndex))
            {
                var deckLinkCard = activeDeckLinkCard;
                return deckLinkCard.compatibleConnectorMappings.Contains(connectorMapping);
            }
            return false;
        }

        /// <summary>
        /// The DeckLink API version currently used.
        /// </summary>
        public string ApiVersion => m_APIVersion;

        internal MappingDictionary m_DeckLinkCards = new MappingDictionary();

        internal DeckLinkCard activeDeckLinkCard => m_DeckLinkCards[m_DeckLinkCardIndex];

        void InitializeDeckLinkDeviceDiscovery()
        {
            s_DeckLinkDeviceDiscovery = DeckLinkDeviceDiscoveryPlugin.CreateDeckLinkDeviceDiscoveryInstance();
            if (s_DeckLinkDeviceDiscovery == IntPtr.Zero)
            {
                throw new InvalidOperationException("[DeckLinkDeviceDiscovery] - Failed to create native instance.");
            }

            m_APIVersion = GetBlackmagicAPIVersionPlugin();

            if (InitializeAndRetrieveDeckLinkCards() == 0)
            {
                m_DeckLinkCards.Add(0, new DeckLinkCard(k_DetectionError));
                m_NoCardDetected = true;
            }

            var devicesArray = m_DevicesConnectorMapping.Select((x) => (int)x).ToArray();
            var devicesCount = devicesArray.Length;
            var groupIDs = m_DeckLinkCards.Select((x) => x.Value.groupID).ToArray();

            DeckLinkDeviceDiscoveryPlugin.AddConnectorMapping(s_DeckLinkDeviceDiscovery, groupIDs, devicesArray, devicesCount);

            s_ArrivedCallback = OnDeviceArrived;
            DeckLinkDeviceDiscoveryPlugin.SetDeckLinkOnDeviceArrived(s_DeckLinkDeviceDiscovery,
                Marshal.GetFunctionPointerForDelegate(s_ArrivedCallback));

            s_RemovedCallback = OnDeviceRemoved;
            DeckLinkDeviceDiscoveryPlugin.SetDeckLinkOnDeviceRemoved(s_DeckLinkDeviceDiscovery,
                Marshal.GetFunctionPointerForDelegate(s_RemovedCallback));
        }

        static void ClearDeckLinkDiscoveryDevice()
        {
            s_ArrivedCallback = null;
            s_RemovedCallback = null;

            if (s_DeckLinkDeviceDiscovery != IntPtr.Zero)
            {
                DeckLinkDeviceDiscoveryPlugin.DestroyDeckLinkDeviceDiscoveryInstance(s_DeckLinkDeviceDiscovery);
                s_DeckLinkDeviceDiscovery = IntPtr.Zero;
            }
        }

        [MonoPInvokeCallback(typeof(DeckLinkDeviceDiscoveryPlugin.CallbackDevice))]
        static void OnDeviceArrived(IntPtr deviceName, int deviceType)
        {
            Assert.IsFalse(deviceName == IntPtr.Zero);

            // We cannot use the 'TryGetInstance' method as it must be called from the Main Thread only.
            if (DeckLinkManager.s_VideoIOManagerInstance is var manager && manager == null)
                return;

            var deviceNameStr = Marshal.PtrToStringAnsi(deviceName);

            if (!String.IsNullOrEmpty(deviceNameStr))
            {
                var logMessage = "Unused";
                var videoDeviceType = (VideoDeviceType)deviceType;
                var isInput = videoDeviceType.HasFlag(VideoDeviceType.Input);
                var isOutput = videoDeviceType.HasFlag(VideoDeviceType.Output);

                if (isInput)
                {
                    manager.m_InputDeviceNames.Add(deviceNameStr);
                    manager.m_InputDeviceNames.Sort();

                    foreach (var name in manager.m_InputDeviceNames)
                    {
                        manager.TryToAssociateOldVideoDeviceIndex(name, VideoDeviceType.Input);
                    }
                    logMessage = VideoDeviceType.Input.ToString();
                }

                if (isOutput)
                {
                    manager.m_OutputDeviceNames.Add(deviceNameStr);
                    manager.m_OutputDeviceNames.Sort();

                    foreach (var name in manager.m_OutputDeviceNames)
                    {
                        manager.TryToAssociateOldVideoDeviceIndex(name, VideoDeviceType.Output);
                    }
                    logMessage += VideoDeviceType.Output.ToString();
                }

                for (int i = 0; i < manager.m_DeckLinkCards.Count; ++i)
                {
                    manager.m_CacheCardCapacityOnStart = true;
                    manager.AddCompatibleConnectorMappingProfiles(i);

                    if (isInput)
                    {
                        manager.CheckIfInputDeviceIsUsed(i, deviceNameStr);
                    }

                    if (isOutput)
                    {
                        manager.CheckIfOutputDeviceIsUsed(i, deviceNameStr);
                    }
                }

                Debug.Log($"[DeckLinkDeviceDiscovery] (OnDeviceArrived) - New {logMessage} device arrived: {deviceNameStr}");
            }
            else
            {
                Debug.Log("[DeckLinkDeviceDiscovery] (OnDeviceArrived), device is null.");
            }
        }

        [MonoPInvokeCallback(typeof(DeckLinkDeviceDiscoveryPlugin.CallbackDevice))]
        static void OnDeviceRemoved(IntPtr deviceName, int deviceType)
        {
            Assert.IsFalse(deviceName == IntPtr.Zero);

            // We cannot use the 'TryGetInstance' method as it must be called from the Main Thread only.
            if (DeckLinkManager.s_VideoIOManagerInstance is var manager && manager == null)
                return;

            var deviceNameStr = Marshal.PtrToStringAnsi(deviceName);
            if (!String.IsNullOrEmpty(deviceNameStr))
            {
                var videoDeviceType = (VideoDeviceType)deviceType;
                if (videoDeviceType.HasFlag(VideoDeviceType.Input))
                {
                    manager.m_InputDeviceNames.Remove(deviceNameStr);
                }

                if (videoDeviceType.HasFlag(VideoDeviceType.Output))
                {
                    manager.m_OutputDeviceNames.Remove(deviceNameStr);
                }

                for (int i = 0; i < manager.m_DeckLinkCards.Count; ++i)
                {
                    manager.AddCompatibleConnectorMappingProfiles(i);
                }

                Debug.Log($"[DeckLinkDeviceDiscovery] OnDeviceRemoved - {(VideoDeviceType)deviceType} device removed: {deviceNameStr}");
            }
            else
            {
                Debug.Log("[DeckLinkDeviceDiscovery] (OnDeviceRemoved), device received is null.");
            }
        }

        void AddCompatibleConnectorMappingProfiles(int deckLinkCardIndex)
        {
            var deckLinkCard = m_DeckLinkCards[deckLinkCardIndex];
            deckLinkCard.ResetValues();

            foreach (var profile in (DeckLinkConnectorMapping[])Enum.GetValues(typeof(DeckLinkConnectorMapping)))
            {
                if (DeckLinkDeviceDiscoveryPlugin.IsConnectorMappingProfileCompatible(s_DeckLinkDeviceDiscovery, (int)profile)
                    && !deckLinkCard.compatibleConnectorMappings.Contains(profile))
                {
                    deckLinkCard.compatibleConnectorMappings.Add(profile);
                }
            }

            var groupID = DeckLinkHardwareDiscoveryPlugin.GetDeckLinkDeviceGroupIDByIndex(deckLinkCardIndex);

            CacheCompatibleLinkModes(deckLinkCardIndex, groupID);
            CacheCompatibleKeyingModes(deckLinkCardIndex, groupID);

            if (m_CacheCardCapacityOnStart)
            {
                CacheLinkModeCompabilities(deckLinkCardIndex, deckLinkCard.compatibleLinkModes > 0);
                CacheKeyingModeCompabilities(deckLinkCardIndex, deckLinkCard.compatibleKeyingModes > 0);
                m_CacheCardCapacityOnStart = false;
            }

            OnMappingProfilesChanged.Invoke();
        }

        void CheckIfInputDeviceIsUsed(int deckLinkCardIndex, string deviceName)
        {
            var deckLinkCard = m_DeckLinkCards[deckLinkCardIndex];
            foreach (var device in deckLinkCard.availableLogicalDevices)
            {
                if (String.Compare(deviceName, device) == 0)
                {
                    deckLinkCard.usedInputDevices++;
                    return;
                }
            }
        }

        void CheckIfOutputDeviceIsUsed(int deckLinkCardIndex, string deviceName)
        {
            var deckLinkCard = m_DeckLinkCards[deckLinkCardIndex];
            foreach (var device in deckLinkCard.availableLogicalDevices)
            {
                if (String.Compare(deviceName, device) == 0)
                {
                    deckLinkCard.usedOutputDevices++;
                    return;
                }
            }
        }

        void CacheCompatibleLinkModes(int deckLinkCardIndex, Int64 groupID)
        {
            var deckLinkCard = m_DeckLinkCards[deckLinkCardIndex];
            deckLinkCard.compatibleLinkModes = 0;

            // Link modes are not compatible for all profiles, we need to cache them again.
            foreach (var linkMode in (LinkMode[])Enum.GetValues(typeof(LinkMode)))
            {
                if (DeckLinkDeviceDiscoveryPlugin.IsLinkModeCompatible(s_DeckLinkDeviceDiscovery, (int)linkMode, groupID))
                {
                    deckLinkCard.compatibleLinkModes |= (int)linkMode;
                }
            }
        }

        void CacheCompatibleKeyingModes(int deckLinkCardIndex, Int64 groupID)
        {
            var deckLinkCard = m_DeckLinkCards[deckLinkCardIndex];
            deckLinkCard.compatibleKeyingModes = 0;

            // Keying modes are not compatible for all profiles, we need to cache them again.
            foreach (var keyingMode in (KeyingMode[])Enum.GetValues(typeof(KeyingMode)))
            {
                if (keyingMode == KeyingMode.None)
                    continue;

                if (DeckLinkDeviceDiscoveryPlugin.IsKeyingModeCompatible(s_DeckLinkDeviceDiscovery, (int)keyingMode, groupID))
                {
                    deckLinkCard.compatibleKeyingModes |= (int)keyingMode;
                }
            }
        }

        // Optimization used in the UI code, allowing us to not show the Single/Dual/Quad Link properties
        // if they are always incompatible on the current Connector Mapping profile. This way, a user cannot create a profile
        // with settings that will always be invalid or not possible to use.
        void CacheLinkModeCompabilities(int deckLinkCardIndex, bool connectorMappingChanged = false)
        {
            var deckLinkCard = m_DeckLinkCards[deckLinkCardIndex];
            var connectorMapping = getConnectorMapping(deckLinkCardIndex);

            // There are several cards (e.g 4K Extreme 12G) that don't have any Connector Mapping, so we can't assume
            // that the Link Mode feature is incompatible or not, based on the current Connector Mapping profile.
            // We have another mechanism later in our UI code, to detect if the Single/Dual/Quad is really supported.
            deckLinkCard.isLinkModeCompatible = (!connectorMappingChanged || deckLinkCard.compatibleConnectorMappings.Count == 0)
                ? true
                : OutputLinkModeUtilities.IsLinkModeCompatible(connectorMapping);
        }

        // Same mechanism as above but for the Keying feature.
        void CacheKeyingModeCompabilities(int deckLinkCardIndex, bool connectorMappingChanged = false)
        {
            var deckLinkCard = m_DeckLinkCards[deckLinkCardIndex];
            var connectorMapping = getConnectorMapping(deckLinkCardIndex);

            deckLinkCard.isKeyingCompatible = (!connectorMappingChanged && deckLinkCard.compatibleConnectorMappings.Count == 0)
                ? true
                : OutputKeyingModeUtilities.IsKeyingModeCompatible(connectorMapping);
        }

        void ChangedDevicesDuplexMode(DeckLinkConnectorMapping connectorMapping, int index)
        {
            if (!m_DeckLinkCards.ContainsKey(index))
            {
                Debug.LogError("Invalid DeckLink card index.");
                return;
            }

            foreach (var card in m_DeckLinkCards)
            {
                card.Value.usedInputDevices = 0;
                card.Value.usedOutputDevices = 0;
            }

            var deckLinkCard = m_DeckLinkCards[index];
            var connectorMappingChanged = false;
            var groupID = DeckLinkHardwareDiscoveryPlugin.GetDeckLinkDeviceGroupIDByIndex(index);

            if (deckLinkCard.compatibleConnectorMappings.Contains(connectorMapping))
            {
                connectorMappingChanged = DeckLinkDeviceDiscoveryPlugin.ChangeAllDevicesConnectorMapping(s_DeckLinkDeviceDiscovery,
                    (int)connectorMapping,
                    groupID);
                Debug.Log($"[DeckLinkDeviceDiscovery] - Set all devices to {connectorMapping.ToString()} mode: {(connectorMappingChanged ? "succeed" : "failed")}");
            }
            else
            {
                DeckLinkDeviceDiscoveryPlugin.ReloadAllDeckLinkDevicesEvent(s_DeckLinkDeviceDiscovery);
                Debug.LogWarning($"[DeckLinkDeviceDiscovery] - The DeckLink card used is not compatible with the selected Mapping Connector profile.");
            }

            CacheLinkModeCompabilities(index, connectorMappingChanged);
            CacheKeyingModeCompabilities(index, connectorMappingChanged);
        }

        internal bool IsLogicalDeviceBoundTwice(int index)
        {
            var twoSubDevicesFullDuplexMode = connectorMapping == DeckLinkConnectorMapping.TwoSubDevicesFullDuplex;
            return twoSubDevicesFullDuplexMode && GetDeviceDataFromVideoDeviceIndex(index, VideoDeviceType.Input, out var _);
        }

        int InitializeAndRetrieveDeckLinkCards()
        {
            m_DeckLinkCards.Clear();
            m_CacheCardCapacityOnStart = true;

            DeckLinkHardwareDiscoveryPlugin.InitializeDeckLinkCards();

            var cardsCount = DeckLinkHardwareDiscoveryPlugin.GetDeckLinkCardsCount();
            for (int i = 0; i < cardsCount; ++i)
            {
                var hardwareModel = DeckLinkHardwareDiscoveryPlugin.GetDeckLinkCardNameByIndex(i);
                if (hardwareModel == IntPtr.Zero)
                    continue;

                var cardName = Marshal.PtrToStringAnsi(hardwareModel);
                var deckLinkCard = new DeckLinkCard(cardName);
                deckLinkCard.groupID = DeckLinkHardwareDiscoveryPlugin.GetDeckLinkDeviceGroupIDByIndex(i);

                var logicalDevicesCount = DeckLinkHardwareDiscoveryPlugin.GetDeckLinkCardLogicalDevicesCount(i);
                for (int l = 0; l < logicalDevicesCount; ++l)
                {
                    var logicalDeviceName = DeckLinkHardwareDiscoveryPlugin.GetDeckLinkCardLogicalDeviceName(i, l);
                    if (logicalDeviceName == IntPtr.Zero)
                        continue;

                    deckLinkCard.availableLogicalDevices.Add(Marshal.PtrToStringAnsi(logicalDeviceName));
                }

                m_DeckLinkCards.Add(i, deckLinkCard);

                if (i >= m_DevicesConnectorMapping.Count)
                {
                    m_DevicesConnectorMapping.Add(DeckLinkConnectorMapping.FourSubDevicesHalfDuplex);
                }
            }

            if (m_DeckLinkCardIndex >= m_DeckLinkCards.Count)
            {
                m_DeckLinkCardIndex = 0;
                m_DevicesConnectorMapping.Add(DeckLinkConnectorMapping.FourSubDevicesHalfDuplex);
            }

            return cardsCount;
        }

        internal (bool, bool) IsKeyingAndLinkModeSupported(int logicalDeviceIndex)
        {
            if (logicalDeviceIndex == -1 || logicalDeviceIndex > m_OutputDeviceNames.Count)
                return (false, false);

            var logicalDeviceName = m_OutputDeviceNames[logicalDeviceIndex];

            foreach (var card in m_DeckLinkCards)
            {
                foreach (var logicalDevice in card.Value.availableLogicalDevices)
                {
                    if (String.Compare(logicalDeviceName, logicalDevice) == 0)
                    {
                        return (card.Value.isKeyingCompatible, card.Value.isLinkModeCompatible);
                    }
                }
            }

            return (false, false);
        }

        internal DeckLinkCard GetDeckLinkCardFromLogicalDevice(int logicalDeviceIndex)
        {
            if (logicalDeviceIndex == -1 || logicalDeviceIndex >= m_OutputDeviceNames.Count)
                return null;

            var logicalDeviceName = m_OutputDeviceNames[logicalDeviceIndex];

            foreach (var card in m_DeckLinkCards)
            {
                foreach (var logicalDevice in card.Value.availableLogicalDevices)
                {
                    if (String.Compare(logicalDeviceName, logicalDevice) == 0)
                    {
                        return card.Value;
                    }
                }
            }

            return null;
        }

        static string GetBlackmagicAPIVersionPlugin()
        {
            var apiVersion = DeckLinkHardwareDiscoveryPlugin.GetBlackmagicAPIVersionPlugin();
            if (apiVersion == IntPtr.Zero)
                return null;

            return Marshal.PtrToStringAnsi(apiVersion);
        }

        internal int GetOffsetFromDeviceIndex(int deviceIndex, VideoDeviceType deviceType)
        {
            if (deviceIndex < 0)
                return 0;
            if (deviceType == VideoDeviceType.Input && deviceIndex >= m_InputDeviceNames.Count)
                return 0;
            if (deviceType == VideoDeviceType.Output && deviceIndex >= m_OutputDeviceNames.Count)
                return 0;

            string deviceName = null;

            switch (deviceType)
            {
                case VideoDeviceType.Input:
                    deviceName = m_InputDeviceNames[deviceIndex];
                    break;
                case VideoDeviceType.Output:
                    deviceName = m_OutputDeviceNames[deviceIndex];
                    break;
            }

            int offset = 0;
            foreach (var card in m_DeckLinkCards)
            {
                if (card.Value.availableLogicalDevices.Contains(deviceName))
                    break;

                if (deviceType == VideoDeviceType.Input)
                    offset += (card.Value.availableLogicalDevices.Count - card.Value.usedInputDevices);
                else
                    offset += (card.Value.availableLogicalDevices.Count - card.Value.usedOutputDevices);
            }

            return offset;
        }
    }
}
