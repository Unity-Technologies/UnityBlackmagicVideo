using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

using UnityEditor;
using UnityEditor.SceneManagement;

using static Unity.Media.Blackmagic.DeckLinkConnectorMapping;
using DeviceData = System.Tuple<string, int>;

namespace Unity.Media.Blackmagic.Tests
{
    [Category(Contents.k_DefaultCategory)]
    class DeckLinkInputTestsHardware
    {
        class InputFourSubDevicesConfiguration
        {
            public readonly DeviceData[] DevicesData = new DeviceData[]
            {
                new Tuple<string, int>("Device 1", 2),
                new Tuple<string, int>("Device 2", 0),
                new Tuple<string, int>("Device 3", -1)
            };

            public int DevicesCount = 3;
            public int LogicalDevicesCount = 4;
            public List<DeckLinkInputDevice> Devices = new List<DeckLinkInputDevice>();
        }

        class InputOneSubDeviceConfiguration
        {
            public readonly DeviceData[] DevicesData = new DeviceData[]
            {
                new Tuple<string, int>("Device 1", 0),
                new Tuple<string, int>("Device 2", -1)
            };

            public int DevicesCount = 2;
            public int LogicalDevicesCount = 1;
            public List<DeckLinkInputDevice> Devices = new List<DeckLinkInputDevice>();
        }

        class InputContent
        {
            public const VideoDeviceType k_DeviceType = VideoDeviceType.Input;
        }

        DeckLinkManager m_DeckLinkManager;
        InputFourSubDevicesConfiguration m_FourSubDevicesConfig;
        InputOneSubDeviceConfiguration m_OneSubDeviceConfig;
        DeckLinkInputDevice m_CurrentDevice;

        int m_AudioCountFrames = 0;
        int m_VideoCountFrames = 0;
        int m_FrameErrorCountFrames = 0;

        void ChangeConnectorMapping(DeckLinkConnectorMapping mapping)
        {
            var cardIndex = m_DeckLinkManager.deckLinkCardIndex;
            m_DeckLinkManager.m_DevicesConnectorMapping[cardIndex] = mapping;
            m_DeckLinkManager.MappingConnectorProfileChanged(mapping, cardIndex);
        }

        [OneTimeSetUp]
        public void SetUp()
        {
            m_FourSubDevicesConfig = new InputFourSubDevicesConfiguration();
            m_OneSubDeviceConfig = new InputOneSubDeviceConfiguration();

            var scene = EditorSceneManager.OpenScene(AssetDatabase.GetAllAssetPaths().FirstOrDefault(x => x.EndsWith(Contents.k_DefaultScene)));
            Assert.IsNotNull(scene);
        }

        [Test, Order(1)]
        public void IsVideoManagerValid()
        {
            var managerFound = DeckLinkManager.TryGetInstance(out var manager);
            if (managerFound)
            {
                m_DeckLinkManager = manager;
            }

            Assert.IsTrue(managerFound);
            Assert.IsNotNull(m_DeckLinkManager);
        }

        [UnityTest, Order(2)]
        public IEnumerator CanChangeTheInitialConnectorMappingToFourSubDevice()
        {
            ChangeConnectorMapping(FourSubDevicesHalfDuplex);

            // Necessary, so all logical devices are retrieved from the C++ API callback.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsTrue(m_DeckLinkManager.connectorMapping == FourSubDevicesHalfDuplex);
        }

        [Test, Order(3)]
        public void HasTheCorrectNumberOfInputDevicesConfigured_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            var inputDevicesCount = m_DeckLinkManager.m_InputDevices.Count;
            Assert.IsTrue(inputDevicesCount == m_FourSubDevicesConfig.DevicesCount);
        }

        [UnityTest, Order(4)]
        public IEnumerator HasTheCorrectNumberOfLogicalDevicesDetected_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            // Necessary, so all logical devices are retrieved from the C++ API callback.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            var logicalDevicesCount = m_DeckLinkManager.m_InputDeviceNames.Count;
            Assert.IsTrue(logicalDevicesCount == m_FourSubDevicesConfig.LogicalDevicesCount);
        }

        [Test, Order(5)]
        public void CanRetrieveInputDevicesWithoutErrors_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            // Retrieve the 3 input devices configured in the Blackmagic window.
            for (int i = 0; i < m_FourSubDevicesConfig.DevicesCount; ++i)
            {
                var success = m_DeckLinkManager.GetExistingVideoDevice(
                    m_FourSubDevicesConfig.DevicesData[i].Item1,
                    InputContent.k_DeviceType,
                    out var videoDeviceGameObjectData);

                Assert.IsTrue(success);
                Assert.IsNotNull(videoDeviceGameObjectData);
                Assert.IsNotNull(videoDeviceGameObjectData.VideoDevice);

                var inputDevice = videoDeviceGameObjectData.VideoDevice as DeckLinkInputDevice;
                Assert.IsNotNull(m_FourSubDevicesConfig.Devices);

                m_FourSubDevicesConfig.Devices.Add(inputDevice);
                Assert.IsTrue(m_FourSubDevicesConfig.Devices.Count == i + 1);
            }
        }

        [Test, Order(6)]
        public void DoesInputDevicesHaveTheCorrectLogicalDeviceBound_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_FourSubDevicesConfig.Devices.Count == m_FourSubDevicesConfig.DevicesCount);

            // Retrieve the 3 input devices configured in the Blackmagic window.
            for (int i = 0; i < m_FourSubDevicesConfig.DevicesCount; ++i)
            {
                var inputDevice = m_FourSubDevicesConfig.Devices[i];
                var indexLogicalDevice = m_FourSubDevicesConfig.DevicesData[i].Item2;

                Assert.IsNotNull(inputDevice);
                Assert.IsTrue(inputDevice.DeviceSelection == indexLogicalDevice);
            }
        }

        /// ---------------------------------------------------------------------------------------------
        /// Unit Tests for the first Input Device configured in the Blackmagic Window (Four Sub Devices).
        /// ---------------------------------------------------------------------------------------------
        [Test, Order(7)]
        public void CanSubscribeToTheFirstInputDeviceCallbacks_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            m_CurrentDevice = m_FourSubDevicesConfig.Devices.First();
            Assert.IsNotNull(m_CurrentDevice);

            m_CurrentDevice.AddAudioFrameCallback((_) =>
            {
                m_AudioCountFrames++;
            });
            m_CurrentDevice.AddVideoFrameCallback((_) =>
            {
                m_VideoCountFrames++;
            });
        }

        [UnityTest, Order(8)]
        public IEnumerator CanStartAndUseTheFirstInputDevice_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);

            m_CurrentDevice.UpdateInEditor = true;
            m_CurrentDevice.m_RequiresReinit = false;

            EditorApplication.QueuePlayerLoopUpdate();

            // Necessary, because the input device is not valid immediately after it's initialization.
            // It takes time to the Blackmagic API callback to be triggered, allowing to get the valid video resolution.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsNotNull(m_CurrentDevice.m_Plugin);
            Assert.IsNotNull(m_CurrentDevice.TargetTexture);
            Assert.IsTrue(m_CurrentDevice.TargetTexture.wrapMode == TextureWrapMode.Clamp);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);
            Assert.IsTrue(m_CurrentDevice.m_Initialized);
        }

        [UnityTest, Order(9)]
        public IEnumerator IsTheFirstInputDeviceCallbacksCorrectlyTriggered_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);

            // Callbacks are called by the C++ API, it's non-deterministic.
            // This delay should be much enough to have our callbacks triggered.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsTrue(m_VideoCountFrames > 0);
            Assert.IsTrue(m_AudioCountFrames > 0);
        }

        [UnityTest, Order(10)]
        public IEnumerator IsTheFirstInputDevicePixelFormatYUV10_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);

            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            // By Default, the Pixel Format selected (without any changes) is YUV10 bit (best quality).
            Assert.IsTrue(m_CurrentDevice.PixelFormat == BMDPixelFormat.YUV10Bit);
        }

        [UnityTest, Order(11)]
        public IEnumerator IsTheFirstInputDeviceColorSpaceBT709_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);

            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            //The Pixel Format selected from the Blackmagic Window is BT709.
            Assert.IsTrue(m_CurrentDevice.m_RequestedColorSpace == BMDColorSpace.UseDeviceSignal);
            Assert.IsTrue(m_CurrentDevice.InColorSpace == BMDColorSpace.BT709);
        }

        [Test, Order(12)]
        public void IsTheFirstInputDeviceVideoResolutionValid_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);

            // When the input signal is invalid, it gives us the default initial resolution value,
            // which is NTSC (525i59.94 NTSC).
            var result = m_CurrentDevice.TryGetVideoResolution(out var resolution);
            Assert.IsTrue(result);
            result = m_CurrentDevice.TryGetVideoFrameRate(out var framerate);
            Assert.IsTrue(result);

            Assert.IsTrue(resolution.HasValue && resolution.Value != VideoMode.Resolution.fNTSC);
            Assert.IsTrue(resolution.HasValue && framerate.Value != VideoMode.FrameRate.f59_94);
            Assert.IsTrue(String.Compare(m_CurrentDevice.FormatName, Contents.k_SignalNotDefined) != 0);

            Assert.IsNotNull(m_CurrentDevice.m_FrameStatus);
            Assert.IsTrue(m_CurrentDevice.m_FrameStatus.Item2 == BaseDeckLinkDevice.StatusType.Ok);
        }

        [UnityTest, Order(13)]
        public IEnumerator IsTheFirstInputDeviceRenderTextureValid_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);

            // Texture is not resized immediately (same constraint at the video resolution).
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            var frameDimensions = m_CurrentDevice.FrameDimensions;
            var textureWidth = m_CurrentDevice.TargetTexture.width;
            var textureHeight = m_CurrentDevice.TargetTexture.height;

            Assert.IsTrue(frameDimensions.x > 1 && frameDimensions.y > 1);
            Assert.IsTrue(textureWidth == frameDimensions.x);
            Assert.IsTrue(textureHeight == frameDimensions.y);
        }

        [UnityTest, Order(14)]
        public IEnumerator CanchangeTheFirstInputDeviceColorSpace_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);

            m_CurrentDevice.ChangeColorSpace(BMDColorSpace.BT2020);

            // Must be false, because we are not reinitializing the device
            // (the Shader conversion is done in the C# code).
            Assert.IsTrue(!m_CurrentDevice.UpdateSettings);
            yield return null;

            Assert.IsTrue(m_CurrentDevice.m_RequestedColorSpace != BMDColorSpace.UseDeviceSignal);
            Assert.IsTrue(m_CurrentDevice.m_RequestedColorSpace == BMDColorSpace.BT2020);
        }

        [UnityTest, Order(15)]
        public IEnumerator CanchangeTheFirstInputDeviceTranferFunction_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);

            m_CurrentDevice.ChangeTransferFunction(BMDTransferFunction.HLG);

            // Must be false, because we are not reinitializing the device
            // (the Shader conversion is done in the C# code).
            Assert.IsTrue(!m_CurrentDevice.UpdateSettings);
            yield return null;

            Assert.IsTrue(m_CurrentDevice.m_RequestedTransferFunction != BMDTransferFunction.UseDeviceSignal);
            Assert.IsTrue(m_CurrentDevice.m_RequestedTransferFunction == BMDTransferFunction.HLG);
        }

        [UnityTest, Order(16)]
        public IEnumerator CanchangeTheFirstInputDevicePixelFormat_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);

            m_CurrentDevice.ChangePixelFormat(BMDPixelFormat.YUV8Bit);

            // Must be true, because we are reinitializing the device.
            Assert.IsTrue(m_CurrentDevice.UpdateSettings);

            // The device is not reinitialized immediately (same constraint at the video resolution).
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsTrue(m_CurrentDevice.PixelFormat == BMDPixelFormat.YUV8Bit);
        }

        [UnityTest, Order(17)]
        public IEnumerator CanStopTheFirstInputDevice_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);

            m_CurrentDevice.UpdateInEditor = false;
            yield return null;

            Assert.IsNull(m_CurrentDevice.m_Plugin);
            Assert.IsNull(m_CurrentDevice.TargetTexture);
            Assert.IsFalse(m_CurrentDevice.UpdateInEditor);
            Assert.IsFalse(m_CurrentDevice.m_Initialized);
        }

        /// ----------------------------------------------------------------------------------------------
        /// Unit Tests for the second Input Device configured in the Blackmagic Window (Four Sub Devices).
        /// ----------------------------------------------------------------------------------------------

        [UnityTest, Order(30)]
        public IEnumerator CanStartAndUseTheSecondInputDevice_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_FourSubDevicesConfig.Devices.Count > 0);

            m_CurrentDevice = m_FourSubDevicesConfig.Devices[1];
            Assert.IsNotNull(m_CurrentDevice);

            m_CurrentDevice.UpdateInEditor = true;
            m_CurrentDevice.m_RequiresReinit = false;

            EditorApplication.QueuePlayerLoopUpdate();

            // Necessary, because the input device is not valid immediately after it's initialization.
            // It takes time to the Blackmagic API callback to be triggered, allowing to get the valid video resolution.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsNotNull(m_CurrentDevice.m_Plugin);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);
            Assert.IsTrue(m_CurrentDevice.m_Initialized);
        }

        [Test, Order(32)]
        public void IsTheSecondInputDevicePixelFormatYUV8_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);

            //The Pixel Format selected from the Blackmagic Window is YUV8 bit.
            Assert.IsTrue(m_CurrentDevice.m_DesiredInPixelFormat == BMDPixelFormat.YUV8Bit);
        }

        [Test, Order(33)]
        public void IsTheSecondInputDeviceVideoResolutionInvalid_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);

            // When the input signal is invalid, it gives us the default initial resolution value,
            // which is NTSC (525i59.94 NTSC).
            var result = m_CurrentDevice.TryGetVideoResolution(out var resolution);
            Assert.IsTrue(result);
            result = m_CurrentDevice.TryGetVideoFrameRate(out var framerate);
            Assert.IsTrue(result);

            Assert.IsTrue(resolution.HasValue && resolution.Value == VideoMode.Resolution.fNTSC);
            Assert.IsTrue(resolution.HasValue && framerate.Value == VideoMode.FrameRate.f59_94);
            Assert.IsTrue(String.Compare(m_CurrentDevice.FormatName, Contents.k_SignalNotDefined) != 0);

            Assert.IsNotNull(m_CurrentDevice.m_FrameStatus);
            Assert.IsTrue(m_CurrentDevice.m_FrameStatus.Item2 == BaseDeckLinkDevice.StatusType.Error);
        }

        [Test, Order(34)]
        public void IsTheSecondInputDeviceColorSpaceBT709_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);

            //The Pixel Format selected from the Blackmagic Window is BT709.
            Assert.IsTrue(m_CurrentDevice.m_RequestedColorSpace != BMDColorSpace.UseDeviceSignal);
            Assert.IsTrue(m_CurrentDevice.m_RequestedColorSpace == BMDColorSpace.BT709);
        }

        [UnityTest, Order(35)]
        public IEnumerator CanStopTheSecondInputDevice_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);

            m_CurrentDevice.UpdateInEditor = false;
            yield return null;

            Assert.IsNull(m_CurrentDevice.m_Plugin);
            Assert.IsNull(m_CurrentDevice.TargetTexture);
            Assert.IsFalse(m_CurrentDevice.UpdateInEditor);
            Assert.IsFalse(m_CurrentDevice.m_Initialized);
        }

        /// ----------------------------------------------------------------------------------------------
        /// Unit Tests for the third Input Device configured in the Blackmagic Window (Four Sub Devices).
        /// ----------------------------------------------------------------------------------------------

        [UnityTest, Order(40)]
        public IEnumerator CanStartButNotUseTheThirdInputDevice_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_FourSubDevicesConfig.Devices.Count > 0);

            m_CurrentDevice = m_FourSubDevicesConfig.Devices[2];
            Assert.IsNotNull(m_CurrentDevice);

            m_CurrentDevice.UpdateInEditor = true;
            m_CurrentDevice.m_RequiresReinit = false;

            EditorApplication.QueuePlayerLoopUpdate();

            // Necessary, because the input device is not valid immediately after it's initialization.
            // It takes time to the Blackmagic API callback to be triggered, allowing to get the valid video resolution.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsNull(m_CurrentDevice.m_Plugin);
            Assert.IsNull(m_CurrentDevice.TargetTexture);
            Assert.IsFalse(m_CurrentDevice.m_Initialized);

            // This one must be true (life cycle still active) because a user can change
            // the SDI port (logical device) in Runtime, even if the device is active.
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);
        }

        [Test, Order(41)]
        public void IsTheThirdInputDeviceVideoResolutionInvalid_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);

            // When the input signal is invalid, it gives us the default initial resolution value,
            // which is NTSC (525i59.94 NTSC).
            var result = m_CurrentDevice.TryGetVideoResolution(out var resolution);
            Assert.IsFalse(result && resolution.HasValue);
            result = m_CurrentDevice.TryGetVideoFrameRate(out var framerate);
            Assert.IsFalse(result && framerate.HasValue);

            Assert.IsTrue(String.Compare(m_CurrentDevice.FormatName, Contents.k_SignalNotDefined) == 0);
        }

        [UnityTest, Order(42)]
        public IEnumerator CanStopTheThirdInputDevice_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);

            m_CurrentDevice.UpdateInEditor = false;
            Assert.IsFalse(m_CurrentDevice.m_Initialized);

            yield return null;

            Assert.IsNull(m_CurrentDevice.m_Plugin);
            Assert.IsNull(m_CurrentDevice.TargetTexture);
            Assert.IsFalse(m_CurrentDevice.UpdateInEditor);
            Assert.IsFalse(m_CurrentDevice.m_Initialized);
        }

        /// -----------------------------------------------------------
        /// Change the connector mapping to One Sub Device Full Duplex.
        /// -----------------------------------------------------------

        [UnityTest, Order(51)]
        public IEnumerator CanChangeTheConnectorMappingToOneSubDevice()
        {
            ChangeConnectorMapping(OneSubDeviceFullDuplex);

            // Necessary, so all logical devices are retrieved from the C++ API callback.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsTrue(m_DeckLinkManager.connectorMapping == OneSubDeviceFullDuplex);
        }

        [Test, Order(52)]
        public void HasTheCorrectNumberOfInputDevicesConfigured_OneSubDevice()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            var inputDevicesCount = m_DeckLinkManager.m_InputDevices.Count;
            Assert.IsTrue(inputDevicesCount == m_OneSubDeviceConfig.DevicesCount);
        }

        [UnityTest, Order(53)]
        public IEnumerator HasTheCorrectNumberOfLogicalDevicesDetected_OneSubDevice()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            // Necessary, so all logical devices are retrieved from the C++ API callback.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            var logicalDevicesCount = m_DeckLinkManager.m_InputDeviceNames.Count;
            Assert.IsTrue(logicalDevicesCount == m_OneSubDeviceConfig.LogicalDevicesCount);
        }

        [Test, Order(54)]
        public void CanRetrieveInputDevicesWithoutErrors_OneSubDevice()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            for (int i = 0; i < m_OneSubDeviceConfig.DevicesCount; ++i)
            {
                var success = m_DeckLinkManager.GetExistingVideoDevice(
                    m_OneSubDeviceConfig.DevicesData[i].Item1,
                    InputContent.k_DeviceType,
                    out var videoDeviceGameObjectData);

                Assert.IsTrue(success);
                Assert.IsNotNull(videoDeviceGameObjectData);
                Assert.IsNotNull(videoDeviceGameObjectData.VideoDevice);

                var inputDevice = videoDeviceGameObjectData.VideoDevice as DeckLinkInputDevice;
                Assert.IsNotNull(m_OneSubDeviceConfig.Devices);

                m_OneSubDeviceConfig.Devices.Add(inputDevice);
                Assert.IsTrue(m_OneSubDeviceConfig.Devices.Count == i + 1);
            }
        }

        [Test, Order(55)]
        public void DoesInputDevicesHaveTheCorrectLogicalDeviceBound_OneSubDevice()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_OneSubDeviceConfig.Devices.Count == m_OneSubDeviceConfig.DevicesCount);

            for (int i = 0; i < m_OneSubDeviceConfig.DevicesCount; ++i)
            {
                var inputDevice = m_OneSubDeviceConfig.Devices[i];
                var indexLogicalDevice = m_OneSubDeviceConfig.DevicesData[i].Item2;

                Assert.IsNotNull(inputDevice);
                Assert.IsTrue(inputDevice.DeviceSelection == indexLogicalDevice);
            }
        }

        /// -------------------------------------------------------------------------------------------
        /// Unit Tests for the first Input Device configured in the Blackmagic Window (One Sub Device).
        /// -------------------------------------------------------------------------------------------

        [UnityTest, Order(60)]
        public IEnumerator CanStartAndUseTheFirstInputDevice_OneSubDevice()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_OneSubDeviceConfig.Devices.Count > 0);

            m_CurrentDevice = m_OneSubDeviceConfig.Devices.First();
            Assert.IsNotNull(m_CurrentDevice);

            m_CurrentDevice.UpdateInEditor = true;
            m_CurrentDevice.m_RequiresReinit = false;

            EditorApplication.QueuePlayerLoopUpdate();

            // Necessary, because the input device is not valid immediately after it's initialization.
            // It takes time to the Blackmagic API callback to be triggered, allowing to get the valid video resolution.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsNotNull(m_CurrentDevice.m_Plugin);
            Assert.IsNotNull(m_CurrentDevice.TargetTexture);

            Assert.IsTrue(m_CurrentDevice.TargetTexture.wrapMode == TextureWrapMode.Clamp);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);
            Assert.IsTrue(m_CurrentDevice.m_Initialized);
        }

        [UnityTest, Order(61)]
        public IEnumerator CanStopTheFirstInputDevice_OneSubDevice()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);

            m_CurrentDevice.UpdateInEditor = false;
            yield return null;

            Assert.IsNull(m_CurrentDevice.m_Plugin);
            Assert.IsNull(m_CurrentDevice.TargetTexture);
            Assert.IsFalse(m_CurrentDevice.UpdateInEditor);
        }

        /// ---------------------------------------------------------------------------------------------
        /// Unit Tests for the second Output Device configured in the Blackmagic Window (One Sub Device).
        /// ---------------------------------------------------------------------------------------------

        [UnityTest, Order(70)]
        public IEnumerator CanStartButNotUseTheSecondInputDevice_OneSubDevice()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_OneSubDeviceConfig.Devices.Count > 0);

            m_CurrentDevice = m_OneSubDeviceConfig.Devices[1];
            Assert.IsNotNull(m_CurrentDevice);

            m_CurrentDevice.UpdateInEditor = true;
            m_CurrentDevice.m_RequiresReinit = false;

            EditorApplication.QueuePlayerLoopUpdate();

            // Necessary, because the input device is not valid immediately after it's initialization.
            // It takes time to the Blackmagic API callback to be triggered, allowing to get the valid video resolution.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsNull(m_CurrentDevice.m_Plugin);
            Assert.IsNull(m_CurrentDevice.TargetTexture);
            Assert.IsFalse(m_CurrentDevice.m_Initialized);

            // This one must be true (life cycle still active) because a user can change
            // the SDI port (logical device) in Runtime, even if the device is active.
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);
        }

        [Test, Order(71)]
        public void IsTheSecondInputDeviceVideoResolutionInvalid_OneSubDevice()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);

            // When the input signal is invalid, it gives us the default initial resolution value,
            // which is NTSC (525i59.94 NTSC).
            var result = m_CurrentDevice.TryGetVideoResolution(out var resolution);
            Assert.IsFalse(result && resolution.HasValue);
            result = m_CurrentDevice.TryGetVideoFrameRate(out var framerate);
            Assert.IsFalse(result && framerate.HasValue);

            Assert.IsTrue(String.Compare(m_CurrentDevice.FormatName, Contents.k_SignalNotDefined) == 0);
        }

        [UnityTest, Order(72)]
        public IEnumerator CanStopTheSecondInputDevice_OneSubDevice()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);

            m_CurrentDevice.UpdateInEditor = false;
            Assert.IsFalse(m_CurrentDevice.m_Initialized);

            yield return null;

            Assert.IsNull(m_CurrentDevice.m_Plugin);
            Assert.IsNull(m_CurrentDevice.TargetTexture);
            Assert.IsFalse(m_CurrentDevice.UpdateInEditor);
            Assert.IsFalse(m_CurrentDevice.m_Initialized);
        }

        /// ------------------------------------------------------------
        /// Change the connector mapping to Two Sub Devices Full Duplex.
        /// ------------------------------------------------------------

        [UnityTest, Order(80)]
        public IEnumerator CanChangeTheConnectorMappingToTwoSubDevices()
        {
            ChangeConnectorMapping(TwoSubDevicesFullDuplex);

            // Necessary, so all logical devices are retrieved from the C++ API callback.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsTrue(m_DeckLinkManager.connectorMapping == TwoSubDevicesFullDuplex);
        }

        [Test, Order(81)]
        public void CantRetrieveInputDevicesWithoutErrors_TwoSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            // There's no input devices to retrieve, so we are testing with a fake device.
            var success = m_DeckLinkManager.GetExistingVideoDevice(
                "Test fake device",
                InputContent.k_DeviceType,
                out var videoDeviceGameObjectData);

            Assert.IsFalse(success);
            Assert.IsNull(videoDeviceGameObjectData);
            Assert.IsTrue(m_DeckLinkManager.m_InputDevices.Count == 0);
        }

        /// -------------------------------------------------------------
        /// Change the connector mapping to Four Sub Devices Half Duplex.
        /// -------------------------------------------------------------

        [UnityTest, Order(90)]
        public IEnumerator CanChangeTheConnectorMappingToFourSubDevice()
        {
            ChangeConnectorMapping(FourSubDevicesHalfDuplex);

            // Necessary, so all logical devices are retrieved from the C++ API callback.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsTrue(m_DeckLinkManager.connectorMapping == FourSubDevicesHalfDuplex);
        }
    }
}
