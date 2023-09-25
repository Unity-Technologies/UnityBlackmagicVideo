using NUnit.Framework;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.TestTools;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

using static Unity.Media.Blackmagic.DeckLinkConnectorMapping;
using DeviceData = System.Tuple<string, int>;
using UnityEngine.SceneManagement;

namespace Unity.Media.Blackmagic.Tests
{
    [Category(Contents.k_DefaultCategory)]
    class DeckLinkOutputTestsHardware
    {
        class OutputFourSubDevicesConfiguration
        {
            public readonly DeviceData[] DevicesData = new DeviceData[]
            {
                new Tuple<string, int>("Device 1", 1),
                new Tuple<string, int>("Device 2", 3),
                new Tuple<string, int>("Device 3", -1)
            };

            public int DevicesCount = 3;
            public int LogicalDevicesCount = 4;
            public List<DeckLinkOutputDevice> Devices = new List<DeckLinkOutputDevice>();
            public DeviceData InputDeviceData = new Tuple<string, int>("Device 1", 2);
        }

        class OutputOneSubDeviceConfiguration
        {
            public readonly DeviceData[] DevicesData = new DeviceData[]
            {
                new Tuple<string, int>("Device 1", 0),
                new Tuple<string, int>("Device 2", -1)
            };

            public int DevicesCount = 2;
            public int LogicalDevicesCount = 1;
            public List<DeckLinkOutputDevice> Devices = new List<DeckLinkOutputDevice>();
        }

        class OutputContent
        {
            public const VideoDeviceType k_DeviceType = VideoDeviceType.Output;
        }

        DeckLinkManager m_DeckLinkManager;
        OutputFourSubDevicesConfiguration m_FourSubDevicesConfig;
        OutputOneSubDeviceConfiguration m_OneSubDeviceConfig;
        DeckLinkOutputDevice m_CurrentDevice;
        int m_AudioPacketsCount = 0;

        void ChangeConnectorMapping(DeckLinkConnectorMapping mapping)
        {
            var cardIndex = m_DeckLinkManager.deckLinkCardIndex;
            m_DeckLinkManager.m_DevicesConnectorMapping[cardIndex] = mapping;
            m_DeckLinkManager.MappingConnectorProfileChanged(mapping, cardIndex);
        }

        [OneTimeSetUp]
        public void SetUp()
        {
            m_FourSubDevicesConfig = new OutputFourSubDevicesConfiguration();
            m_OneSubDeviceConfig = new OutputOneSubDeviceConfiguration();

            var scene = EditorSceneManager.OpenScene(AssetDatabase.GetAllAssetPaths().FirstOrDefault(x => x.EndsWith(Contents.k_DefaultScene)));
            Assert.IsNotNull(scene);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            m_FourSubDevicesConfig = null;
            m_OneSubDeviceConfig = null;
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
        public void HasTheCorrectNumberOfOutputDevicesConfigured_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            var devicesCount = m_DeckLinkManager.m_OutputDevices.Count;
            Assert.IsTrue(devicesCount == m_FourSubDevicesConfig.DevicesCount);
        }

        [Test, Order(4)]
        public void HasTheCorrectNumberOfLogicalDevicesDetected_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            var logicalDevicesCount = m_DeckLinkManager.m_OutputDeviceNames.Count;
            Assert.IsTrue(logicalDevicesCount == m_FourSubDevicesConfig.LogicalDevicesCount);
        }

        [Test, Order(5)]
        public void CanRetrieveOutputDevicesWithoutErrors_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            // Retrieve the 3 output devices configured in the Blackmagic window.
            for (int i = 0; i < m_FourSubDevicesConfig.DevicesCount; ++i)
            {
                var success = m_DeckLinkManager.GetExistingVideoDevice(
                    m_FourSubDevicesConfig.DevicesData[i].Item1,
                    OutputContent.k_DeviceType,
                    out var videoDeviceGameObjectData);

                Assert.IsTrue(success);
                Assert.IsNotNull(videoDeviceGameObjectData);
                Assert.IsNotNull(videoDeviceGameObjectData.VideoDevice);

                var device = videoDeviceGameObjectData.VideoDevice as DeckLinkOutputDevice;
                Assert.IsNotNull(m_FourSubDevicesConfig.Devices);

                m_FourSubDevicesConfig.Devices.Add(device);
                Assert.IsTrue(m_FourSubDevicesConfig.Devices.Count == i + 1);
            }
        }

        [Test, Order(6)]
        public void DoesOutputDevicesHaveTheCorrectLogicalDeviceBound_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_FourSubDevicesConfig.Devices.Count == m_FourSubDevicesConfig.DevicesCount);

            // Retrieve the 3 output devices configured in the Blackmagic window.
            for (int i = 0; i < m_FourSubDevicesConfig.DevicesCount; ++i)
            {
                var device = m_FourSubDevicesConfig.Devices[i];
                var indexLogicalDevice = m_FourSubDevicesConfig.DevicesData[i].Item2;

                Assert.IsNotNull(device);
                Assert.IsTrue(device.DeviceSelection == indexLogicalDevice);
            }
        }

        /// ----------------------------------------------------------------------------------------------
        /// Unit Tests for the first Output Device configured in the Blackmagic Window (Four Sub Devices).
        /// ----------------------------------------------------------------------------------------------

        [UnityTest, Order(7)]
        public IEnumerator CanStartAndUseTheFirstOutputDevice_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_FourSubDevicesConfig.Devices.Count > 0);

            m_CurrentDevice = m_FourSubDevicesConfig.Devices.First();
            Assert.IsNotNull(m_CurrentDevice);

            m_CurrentDevice.UpdateInEditor = true;
            m_CurrentDevice.m_RequiresReinit = false;

            EditorApplication.QueuePlayerLoopUpdate();
            yield return null;

            Assert.IsNotNull(m_CurrentDevice.m_Plugin);
            Assert.IsNotNull(m_CurrentDevice.TargetTexture);
            Assert.IsTrue(m_CurrentDevice.TargetTexture.wrapMode == TextureWrapMode.Clamp);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);
            Assert.IsTrue(m_CurrentDevice.m_Initialized);

            // This device is configured to receive audio packets.
            // We are testing audio reception later in the tests.
            Assert.IsNotNull(m_CurrentDevice.m_AudioSameAsInputDevice);
        }

        [Test, Order(8)]
        public void CanPromoteTheFirstOutputDeviceToManualMode_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            m_CurrentDevice.PromoteToManualMode();

            Assert.IsTrue(DeckLinkOutputDevice.ManualModeInstance == m_CurrentDevice);
        }

        [UnityTest, Order(9)]
        public IEnumerator IsTheFirstOutputDeviceFramesCorrectlyEnqueued_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            yield return null;

            var renderTexture = m_CurrentDevice.TargetTexture;
            Assert.IsNotNull(renderTexture);

            var initialCount = m_CurrentDevice.m_FrameQueue.Count;
            const int CountTest = 4;
            for (int i = 0; i < CountTest; ++i)
            {
                m_CurrentDevice.EncodeFrameAndAddToQueue(renderTexture);
            }

            var count = (m_CurrentDevice.m_Plugin.IsProgressive) ? CountTest : (CountTest / 2);
            Assert.IsTrue(m_CurrentDevice.m_FrameQueue.Count == initialCount + count);
        }

        [Test, Order(10)]
        public void IsTheFirstOutputDevicePixelFormatYUV8_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            // By Default, the Pixel Format selected (without any changes) is YUV8 bit (best quality).
            Assert.IsTrue(m_CurrentDevice.PixelFormat == BMDPixelFormat.YUV8Bit);
        }

        [Test, Order(11)]
        public void IsTheFirstOutputDeviceColorSpaceBT709_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            //The Pixel Format selected from the Blackmagic Window is BT709.
            Assert.IsTrue(m_CurrentDevice.RequestedColorSpace == BMDColorSpace.BT709);
            Assert.IsTrue(m_CurrentDevice.InColorSpace == BMDColorSpace.BT709);
        }

        [UnityTest, Order(12)]
        public IEnumerator IsTheFirstOutputDeviceVideoResolutionValid_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            // When the input signal is invalid, it gives us the default initial resolution value,
            // which is NTSC (525i59.94 NTSC).
            var result = m_CurrentDevice.TryGetVideoResolution(out var resolution);
            Assert.IsTrue(result);
            result = m_CurrentDevice.TryGetVideoFrameRate(out var framerate);
            Assert.IsTrue(result);

            Assert.IsTrue(resolution.HasValue && resolution.Value != VideoMode.Resolution.fNTSC);
            Assert.IsTrue(resolution.HasValue && framerate.Value != VideoMode.FrameRate.f59_94);
            Assert.IsTrue(String.Compare(m_CurrentDevice.FormatName, Contents.k_SignalNotDefined) != 0);

            // Necessary for m_FrameStatus, so at least 1 frame has been completed.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsNotNull(m_CurrentDevice.m_FrameStatus);
            Assert.IsTrue(m_CurrentDevice.m_FrameStatus.Item2 == BaseDeckLinkDevice.StatusType.Ok);
        }

        [Test, Order(13)]
        public void IsTheFirstOutputDeviceRenderTextureValid_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            var frameDimensions = m_CurrentDevice.FrameDimensions;
            var textureWidth = m_CurrentDevice.TargetTexture.width;
            var textureHeight = m_CurrentDevice.TargetTexture.height;

            Assert.IsTrue(frameDimensions.x > 1 && frameDimensions.y > 1);
            Assert.IsTrue(textureWidth == frameDimensions.x);
            Assert.IsTrue(textureHeight == frameDimensions.y);
        }

        [UnityTest, Order(14)]
        public IEnumerator CanChangeTheFirstOutputDeviceColorSpace_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            m_CurrentDevice.ChangeColorSpace(BMDColorSpace.BT2020);

            // Must be true, we are reinitializing the device.
            Assert.IsTrue(m_CurrentDevice.UpdateSettings);
            yield return null;

            Assert.IsTrue(m_CurrentDevice.RequestedColorSpace == BMDColorSpace.BT2020);
            Assert.IsTrue(m_CurrentDevice.InColorSpace == BMDColorSpace.BT2020);
        }

        [UnityTest, Order(15)]
        public IEnumerator CanChangeTheFirstOutputDeviceTranferFunction_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            m_CurrentDevice.ChangeTransferFunction(BMDTransferFunction.HDR);

            // Must be true, we are reinitializing the device.
            Assert.IsTrue(m_CurrentDevice.UpdateSettings);
            yield return null;

            Assert.IsTrue(m_CurrentDevice.TransferFunction == BMDTransferFunction.HDR);
        }

        [UnityTest, Order(16)]
        public IEnumerator CanChangeTheFirstOutputDevicePixelFormat_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            m_CurrentDevice.ChangePixelFormat(BMDPixelFormat.ARGB8Bit);

            // Must be true, because we are reinitializing the device.
            Assert.IsTrue(m_CurrentDevice.UpdateSettings);

            // The device is not reinitialized immediately (same constraint at the video resolution).
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsTrue(m_CurrentDevice.PixelFormat == BMDPixelFormat.ARGB8Bit);
        }

        [UnityTest, Order(17)]
        public IEnumerator CanChangeTheFirstOutputDeviceVideoMode_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            var result = m_CurrentDevice.ChangeVideoConfiguration(VideoMode.Resolution.fHD1080,
                VideoMode.FrameRate.f24,
                VideoMode.ScanMode.Progressive);

            Assert.IsTrue(m_CurrentDevice.UpdateSettings);
            Assert.IsTrue(result);

            yield return null;

            result = m_CurrentDevice.TryGetVideoResolution(out var resolution);
            Assert.IsTrue(result);
            result = m_CurrentDevice.TryGetVideoFrameRate(out var framerate);
            Assert.IsTrue(result);
            result = m_CurrentDevice.TryGetVideoScanMode(out var scanMode);
            Assert.IsTrue(result);

            Assert.IsTrue(resolution.HasValue && resolution.Value == VideoMode.Resolution.fHD1080);
            Assert.IsTrue(framerate.HasValue && framerate.Value == VideoMode.FrameRate.f24);
            Assert.IsTrue(scanMode.HasValue && scanMode.Value == VideoMode.ScanMode.Progressive);
            Assert.IsTrue(String.Compare(m_CurrentDevice.FormatName, Contents.k_SignalNotDefined) != 0);
        }

        [Test, Order(18)]
        public void CantChangeTheFirstOutputDeviceScanMode_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            var result = m_CurrentDevice.ChangeVideoScanMode(VideoMode.ScanMode.Interlaced);

            Assert.IsFalse(m_CurrentDevice.UpdateSettings);
            Assert.IsFalse(result);

            result = m_CurrentDevice.TryGetVideoResolution(out var resolution);
            Assert.IsTrue(result);
            result = m_CurrentDevice.TryGetVideoFrameRate(out var framerate);
            Assert.IsTrue(result);
            result = m_CurrentDevice.TryGetVideoScanMode(out var scanMode);
            Assert.IsTrue(result);

            Assert.IsTrue(resolution.HasValue && resolution.Value == VideoMode.Resolution.fHD1080);
            Assert.IsTrue(framerate.HasValue && framerate.Value == VideoMode.FrameRate.f24);
            Assert.IsTrue(scanMode.HasValue && scanMode.Value == VideoMode.ScanMode.Progressive);
            Assert.IsTrue(String.Compare(m_CurrentDevice.FormatName, Contents.k_SignalNotDefined) != 0);
        }

        [UnityTest, Order(19)]
        public IEnumerator CanPromoteTheFirstOutputDeviceToAsyncMode_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            m_CurrentDevice.m_RequestedSyncMode = DeckLinkOutputDevice.SyncMode.AsyncMode;
            Assert.IsTrue(m_CurrentDevice.UpdateSettings);

            yield return null;

            Assert.IsTrue(m_CurrentDevice.CurrentSyncMode == DeckLinkOutputDevice.SyncMode.AsyncMode);
        }

        [UnityTest, Order(20)]
        public IEnumerator CanChangeTheFirstOutputDeviceFilterMode_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            m_CurrentDevice.m_FilterMode = FilterMode.Trilinear;
            yield return null;

            Assert.IsTrue(m_CurrentDevice.CaptureRenderTexture.filterMode == FilterMode.Trilinear);
        }

        [UnityTest, Order(21)]
        public IEnumerator CantChangeTheFirstOutputDeviceKeyingMode_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            // This one is only detecting if the Keying is available based on the Pixel Format
            // (which is RGBA8, we changed it in the previous tests).
            Assert.IsTrue(m_CurrentDevice.IsKeyingAvailable());

            // It should fails, because Keying is not available on Four Sub Devices.
            var result = m_CurrentDevice.ChangeKeyingMode(KeyingMode.External);
            Assert.IsFalse(result);
            Assert.IsFalse(m_CurrentDevice.UpdateSettings);

            yield return null;

            // Must be true because keying can be available on a DeckLink card and unavaible on another one.
            // Can be improved after merging the multi-cards support.
            Assert.IsTrue(m_CurrentDevice.OutputKeyingMode == KeyingMode.External);
        }

        [UnityTest, Order(22)]
        public IEnumerator CantChangeTheFirstOutputDeviceLinkMode_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            // It should fails, because Keying is not available on Four Sub Devices.
            var result = m_CurrentDevice.ChangeLinkMode(LinkMode.Dual);
            Assert.IsFalse(result);
            Assert.IsFalse(m_CurrentDevice.UpdateSettings);

            yield return null;

            // Must be true because a link mode can be available on a DeckLink card and unavaible on another one.
            // Can be improved after merging the multi-cards support.
            Assert.IsTrue(m_CurrentDevice.OutputLinkMode == LinkMode.Dual);
        }

        [UnityTest, Order(23)]
        public IEnumerator CanUseTheFirstOutputDeviceSameAsInputOption_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            var result = m_DeckLinkManager.GetExistingVideoDevice(
                m_FourSubDevicesConfig.InputDeviceData.Item1,
                VideoDeviceType.Input,
                out var videoDeviceGameObjectData);

            Assert.IsTrue(result);
            Assert.IsNotNull(videoDeviceGameObjectData);
            Assert.IsNotNull(videoDeviceGameObjectData.VideoDevice);

            var inputDevice = videoDeviceGameObjectData.VideoDevice as DeckLinkInputDevice;
            Assert.IsNotNull(m_FourSubDevicesConfig.Devices);
            inputDevice.UpdateInEditor = true;
            inputDevice.m_RequiresReinit = false;

            // The input device is not reinitialized immediately (same constraint at the video resolution).
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsNotNull(inputDevice.m_Plugin);
            Assert.IsNotNull(inputDevice.TargetTexture);
            Assert.IsTrue(inputDevice.TargetTexture.wrapMode == TextureWrapMode.Clamp);
            Assert.IsTrue(inputDevice.UpdateInEditor);
            Assert.IsTrue(inputDevice.m_Initialized);

            result = m_CurrentDevice.ChangeSameVideoModeAsInputDevice(true, inputDevice);
            Assert.IsTrue(result);
            Assert.IsTrue(m_CurrentDevice.UpdateSettings);

            yield return null;

            Assert.IsTrue(m_CurrentDevice.TryGetVideoResolution(out var outputResolution));
            Assert.IsTrue(m_CurrentDevice.TryGetVideoFrameRate(out var outputFramerate));
            Assert.IsTrue(m_CurrentDevice.TryGetVideoScanMode(out var outputScanMode));

            Assert.IsTrue(inputDevice.TryGetVideoResolution(out var inputResolution));
            Assert.IsTrue(inputDevice.TryGetVideoFrameRate(out var inputFramerate));
            Assert.IsTrue(inputDevice.TryGetVideoScanMode(out var inputScanMode));

            Assert.IsTrue(outputResolution.HasValue && inputResolution.HasValue);
            Assert.IsTrue(outputFramerate.HasValue && inputFramerate.HasValue);
            Assert.IsTrue(outputScanMode.HasValue && inputScanMode.HasValue);

            Assert.IsTrue(outputResolution.Value == inputResolution.Value);
            Assert.IsTrue(outputFramerate.Value == inputFramerate.Value);
            Assert.IsTrue(outputScanMode.Value == inputScanMode.Value);

            Assert.IsTrue(String.Compare(m_CurrentDevice.FormatName, Contents.k_SignalNotDefined) != 0);

            // Test audio packets reception.
            {
                inputDevice.AddAudioFrameCallback(FakeOnAudioFrameArrived);

                // Fake delay to queue audio packets.
                for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                    yield return null;

                Assert.IsTrue(m_AudioPacketsCount > 0);
                inputDevice.RemoveAudioFrameCallback(FakeOnAudioFrameArrived);
            }

            // Stop input device.
            {
                inputDevice.UpdateInEditor = false;
                yield return null;

                Assert.IsNull(inputDevice.m_Plugin);
                Assert.IsNull(inputDevice.TargetTexture);
                Assert.IsFalse(inputDevice.UpdateInEditor);
                Assert.IsFalse(inputDevice.m_Initialized);

                // No signal found, we stopped the input device (and so the output device).
                Assert.IsFalse(m_CurrentDevice.IsActive);
                Assert.IsNull(m_CurrentDevice.m_Plugin);
                Assert.IsNull(m_CurrentDevice.TargetTexture);
                Assert.IsFalse(m_CurrentDevice.UpdateInEditor);
                Assert.IsFalse(m_CurrentDevice.m_Initialized);
            }
        }

        void FakeOnAudioFrameArrived(InputAudioFrame frame)
        {
            m_AudioPacketsCount++;
        }

        [UnityTest, Order(24)]
        public IEnumerator CantStartTheFirstOutputDevice_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);

            // We are still in Same As Input mode, but the input is inactive.
            // It should not start the Output device.
            m_CurrentDevice.UpdateInEditor = true;
            m_CurrentDevice.m_RequiresReinit = false;

            EditorApplication.QueuePlayerLoopUpdate();
            yield return null;

            Assert.IsNull(m_CurrentDevice.m_Plugin);
            Assert.IsNull(m_CurrentDevice.TargetTexture);
            Assert.IsFalse(m_CurrentDevice.m_Initialized);
            Assert.IsFalse(m_CurrentDevice.UpdateInEditor);
        }

        [UnityTest, Order(25)]
        public IEnumerator CanChangeTheFirstOutputDeviceVideoModeAfterUsingSameAsInput_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);

            m_CurrentDevice.UpdateInEditor = false;

            Assert.IsFalse(m_CurrentDevice.UpdateInEditor);

            var result = m_CurrentDevice.ChangeVideoConfiguration(VideoMode.Resolution.fHD1080,
                VideoMode.FrameRate.f24,
                VideoMode.ScanMode.Progressive);
            Assert.IsTrue(result);

            // We must re-enable the device, that's a limitation of our current design.
            m_CurrentDevice.UpdateInEditor = true;
            Assert.IsTrue(m_CurrentDevice.UpdateSettings);

            EditorApplication.QueuePlayerLoopUpdate();
            yield return null;

            result = m_CurrentDevice.TryGetVideoResolution(out var resolution);
            Assert.IsTrue(result);
            result = m_CurrentDevice.TryGetVideoFrameRate(out var framerate);
            Assert.IsTrue(result);
            result = m_CurrentDevice.TryGetVideoScanMode(out var scanMode);
            Assert.IsTrue(result);

            Assert.IsTrue(resolution.HasValue && resolution.Value == VideoMode.Resolution.fHD1080);
            Assert.IsTrue(framerate.HasValue && framerate.Value == VideoMode.FrameRate.f24);
            Assert.IsTrue(scanMode.HasValue && scanMode.Value == VideoMode.ScanMode.Progressive);
            Assert.IsTrue(String.Compare(m_CurrentDevice.FormatName, Contents.k_SignalNotDefined) != 0);

            Assert.IsFalse(m_CurrentDevice.m_SameVideoModeAsInput);
            Assert.IsNotNull(m_CurrentDevice.m_Plugin);
            Assert.IsNotNull(m_CurrentDevice.TargetTexture);
            Assert.IsTrue(m_CurrentDevice.m_Initialized);
        }

        [UnityTest, Order(26)]
        public IEnumerator CantReceiveAudioPacketFirstOutputDevice_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            var inputDeviceReferenced = m_CurrentDevice.m_AudioSameAsInputDevice;
            Assert.IsNotNull(inputDeviceReferenced);

            // Test audio packets reception.
            // The input device is inactive, we shouldn't receive any audio packet.
            {
                m_AudioPacketsCount = 0;
                inputDeviceReferenced.AddAudioFrameCallback(FakeOnAudioFrameArrived);

                // Fake delay to queue audio packets.
                for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                    yield return null;

                Assert.IsTrue(m_AudioPacketsCount == 0);
                inputDeviceReferenced.RemoveAudioFrameCallback(FakeOnAudioFrameArrived);
            }
        }

        [UnityTest, Order(27)]
        public IEnumerator CanStopTheFirstOutputDevice_FourSubDevices()
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

        /// -----------------------------------------------------------------------------------------------
        /// Unit Tests for the second Output Device configured in the Blackmagic Window (Four Sub Devices).
        /// -----------------------------------------------------------------------------------------------

        [UnityTest, Order(41)]
        public IEnumerator CanStartAndUseTheSecondOutputDevice_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_FourSubDevicesConfig.Devices.Count > 0);

            m_CurrentDevice = m_FourSubDevicesConfig.Devices[1];
            Assert.IsNotNull(m_CurrentDevice);

            m_CurrentDevice.UpdateInEditor = true;
            m_CurrentDevice.m_RequiresReinit = false;

            EditorApplication.QueuePlayerLoopUpdate();
            yield return null;

            Assert.IsNotNull(m_CurrentDevice.m_Plugin);
            Assert.IsNotNull(m_CurrentDevice.TargetTexture);
            Assert.IsTrue(m_CurrentDevice.TargetTexture.wrapMode == TextureWrapMode.Clamp);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);
            Assert.IsTrue(m_CurrentDevice.m_Initialized);
        }

        [UnityTest, Order(42)]
        public IEnumerator IsTheSecondOutputDeviceFramesCorrectlyEnqueued_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);

            EditorApplication.QueuePlayerLoopUpdate();
            yield return null;

            var renderTexture = m_CurrentDevice.TargetTexture;
            Assert.IsNotNull(renderTexture);

            var initialCount = m_CurrentDevice.m_FrameQueue.Count;
            const int CountTest = 4;
            for (int i = 0; i < CountTest; ++i)
            {
                m_CurrentDevice.EncodeFrameAndAddToQueue(renderTexture);
            }

            var count = (m_CurrentDevice.m_Plugin.IsProgressive) ? CountTest : (CountTest / 2);
            Assert.IsTrue(m_CurrentDevice.m_FrameQueue.Count == initialCount + count);
        }

        [Test, Order(43)]
        public void IsTheSecondOutputDevicePixelFormatYUV10_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);

            // By Default, the Pixel Format selected (without any changes) is YUV10 bit (best quality).
            Assert.IsTrue(m_CurrentDevice.PixelFormat == BMDPixelFormat.YUV10Bit);
        }

        [Test, Order(44)]
        public void IsTheSecondOutputDeviceColorSpaceBT2020_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);

            //The Pixel Format selected from the Blackmagic Window is BT2020.
            Assert.IsTrue(m_CurrentDevice.RequestedColorSpace == BMDColorSpace.BT2020);
            Assert.IsTrue(m_CurrentDevice.InColorSpace == BMDColorSpace.BT2020);
        }

        [UnityTest, Order(45)]
        public IEnumerator IsTheSecondOutputDeviceVideoResolutionValid_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);

            // When the input signal is invalid, it gives us the default initial resolution value,
            // which is NTSC (525i59.94 NTSC).
            var result = m_CurrentDevice.TryGetVideoResolution(out var resolution);
            Assert.IsTrue(result);
            result = m_CurrentDevice.TryGetVideoFrameRate(out var framerate);
            Assert.IsTrue(result);
            result = m_CurrentDevice.TryGetVideoScanMode(out var scanMode);
            Assert.IsTrue(result);

            Assert.IsTrue(resolution.HasValue && resolution.Value != VideoMode.Resolution.fNTSC);
            Assert.IsTrue(resolution.HasValue && framerate.Value != VideoMode.FrameRate.f59_94);
            Assert.IsTrue(scanMode.HasValue && scanMode.Value == VideoMode.ScanMode.Interlaced);
            Assert.IsTrue(String.Compare(m_CurrentDevice.FormatName, Contents.k_SignalNotDefined) != 0);

            // Necessary for m_FrameStatus, so at least 1 frame has been completed.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsNotNull(m_CurrentDevice.m_FrameStatus);
        }

        [Test, Order(46)]
        public void IsTheSecondOutputDeviceRenderTextureValid_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);

            var frameDimensions = m_CurrentDevice.FrameDimensions;
            var textureWidth = m_CurrentDevice.TargetTexture.width;
            var textureHeight = m_CurrentDevice.TargetTexture.height;

            Assert.IsTrue(frameDimensions.x > 1 && frameDimensions.y > 1);
            Assert.IsTrue(textureWidth == frameDimensions.x);
            Assert.IsTrue(textureHeight == frameDimensions.y);
        }

        [UnityTest, Order(47)]
        public IEnumerator CanchangeTheSecondOutputDeviceColorSpace_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);

            m_CurrentDevice.ChangeColorSpace(BMDColorSpace.BT709);

            // Must be true, we are reinitializing the device.
            Assert.IsTrue(m_CurrentDevice.UpdateSettings);
            yield return null;

            Assert.IsTrue(m_CurrentDevice.RequestedColorSpace == BMDColorSpace.BT709);
            Assert.IsTrue(m_CurrentDevice.InColorSpace == BMDColorSpace.BT709);
        }

        [UnityTest, Order(48)]
        public IEnumerator CanchangeTheSecondInputDeviceTranferFunction_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);

            m_CurrentDevice.ChangeTransferFunction(BMDTransferFunction.PQ);

            // TO DO:
            //Should be false, the PixelFormat used is not BT2020.
            Assert.IsTrue(m_CurrentDevice.UpdateSettings);
            yield return null;

            Assert.IsTrue(m_CurrentDevice.TransferFunction == BMDTransferFunction.PQ);
        }

        [UnityTest, Order(49)]
        public IEnumerator CanchangeTheSecondOutputDevicePixelFormat_FourSubDevices()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);

            m_CurrentDevice.ChangePixelFormat(BMDPixelFormat.RGB12Bit);

            // Must be true, because we are reinitializing the device.
            Assert.IsTrue(m_CurrentDevice.UpdateSettings);

            yield return null;

            Assert.IsTrue(m_CurrentDevice.PixelFormat == BMDPixelFormat.RGB12Bit);
        }

        [UnityTest, Order(50)]
        public IEnumerator CanStopTheSecondOutputDevice_FourSubDevices()
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
        /// Unit Tests for the third Output Device configured in the Blackmagic Window (Four Sub Devices).
        /// ----------------------------------------------------------------------------------------------

        [UnityTest, Order(60)]
        public IEnumerator CanStartButNotUseTheThirdDevice_FourSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_FourSubDevicesConfig.Devices.Count > 0);

            m_CurrentDevice = m_FourSubDevicesConfig.Devices[2];
            Assert.IsNotNull(m_CurrentDevice);

            m_CurrentDevice.UpdateInEditor = true;
            m_CurrentDevice.m_RequiresReinit = false;

            EditorApplication.QueuePlayerLoopUpdate();
            yield return null;

            Assert.IsNull(m_CurrentDevice.m_Plugin);
            Assert.IsNull(m_CurrentDevice.TargetTexture);
            Assert.IsFalse(m_CurrentDevice.m_Initialized);
            Assert.IsFalse(m_CurrentDevice.UpdateInEditor);
        }

        [UnityTest, Order(61)]
        public IEnumerator CanStopTheThirdOutputDevice_FourSubDevices()
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

        [UnityTest, Order(70)]
        public IEnumerator CanChangeTheConnectorMappingToOneSubDevice()
        {
            ChangeConnectorMapping(OneSubDeviceFullDuplex);

            // Necessary, so all logical devices are retrieved from the C++ API callback.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsTrue(m_DeckLinkManager.connectorMapping == OneSubDeviceFullDuplex);
        }

        [Test, Order(71)]
        public void HasTheCorrectNumberOfOutputDevicesConfigured_OneSubDevice()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            var devicesCount = m_DeckLinkManager.m_OutputDevices.Count;
            Assert.IsTrue(devicesCount == m_OneSubDeviceConfig.DevicesCount);
        }

        [Test, Order(72)]
        public void HasTheCorrectNumberOfLogicalDevicesDetected_OneSubDevice()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            var logicalDevicesCount = m_DeckLinkManager.m_OutputDeviceNames.Count;
            Assert.IsTrue(logicalDevicesCount == m_OneSubDeviceConfig.LogicalDevicesCount);
        }

        [Test, Order(73)]
        public void CanRetrieveOutputDevicesWithoutErrors_OneSubDevice()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            for (int i = 0; i < m_OneSubDeviceConfig.DevicesCount; ++i)
            {
                var success = m_DeckLinkManager.GetExistingVideoDevice(
                    m_OneSubDeviceConfig.DevicesData[i].Item1,
                    OutputContent.k_DeviceType,
                    out var videoDeviceGameObjectData);

                Assert.IsTrue(success);
                Assert.IsNotNull(videoDeviceGameObjectData);
                Assert.IsNotNull(videoDeviceGameObjectData.VideoDevice);

                var dDevice = videoDeviceGameObjectData.VideoDevice as DeckLinkOutputDevice;
                Assert.IsNotNull(m_OneSubDeviceConfig.Devices);

                m_OneSubDeviceConfig.Devices.Add(dDevice);
                Assert.IsTrue(m_OneSubDeviceConfig.Devices.Count == i + 1);
            }
        }

        [Test, Order(74)]
        public void DoesOutputDevicesHaveTheCorrectLogicalDeviceBound_OneSubDevice()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_OneSubDeviceConfig.Devices.Count == m_OneSubDeviceConfig.DevicesCount);

            for (int i = 0; i < m_OneSubDeviceConfig.DevicesCount; ++i)
            {
                var device = m_OneSubDeviceConfig.Devices[i];
                var indexLogicalDevice = m_OneSubDeviceConfig.DevicesData[i].Item2;

                Assert.IsNotNull(device);
                Assert.IsTrue(device.DeviceSelection == indexLogicalDevice);
            }
        }

        /// --------------------------------------------------------------------------------------------
        /// Unit Tests for the first Output Device configured in the Blackmagic Window (One Sub Device).
        /// --------------------------------------------------------------------------------------------

        [UnityTest, Order(80)]
        public IEnumerator CanStartAndUseTheFirstOutputDevice_OneSubDevice()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_OneSubDeviceConfig.Devices.Count > 0);

            m_CurrentDevice = m_OneSubDeviceConfig.Devices.First();
            Assert.IsNotNull(m_CurrentDevice);

            m_CurrentDevice.UpdateInEditor = true;
            m_CurrentDevice.m_RequiresReinit = false;

            EditorApplication.QueuePlayerLoopUpdate();
            yield return null;

            Assert.IsNotNull(m_CurrentDevice.m_Plugin);
            Assert.IsNotNull(m_CurrentDevice.TargetTexture);

            Assert.IsTrue(m_CurrentDevice.TargetTexture.wrapMode == TextureWrapMode.Clamp);
            Assert.IsTrue(m_CurrentDevice.UpdateInEditor);
            Assert.IsTrue(m_CurrentDevice.m_Initialized);
        }

        [UnityTest, Order(81)]
        public IEnumerator IsTheFirstOutputDeviceFramesCorrectlyEnqueued_OneSubDevice()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            yield return null;

            var renderTexture = m_CurrentDevice.TargetTexture;
            Assert.IsNotNull(renderTexture);

            var initialCount = m_CurrentDevice.m_FrameQueue.Count;
            const int CountTest = 4;
            for (int i = 0; i < CountTest; ++i)
            {
                m_CurrentDevice.EncodeFrameAndAddToQueue(renderTexture);
            }

            var count = (m_CurrentDevice.m_Plugin.IsProgressive) ? CountTest : (CountTest / 2);
            Assert.IsTrue(m_CurrentDevice.m_FrameQueue.Count == initialCount + count);
        }

        [Test, Order(82)]
        public void IsTheFirstOutputDevicePixelFormatYUV8_OneSubDevice()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            // By Default, the Pixel Format selected (without any changes) is YUV8 bit (best quality).
            Assert.IsTrue(m_CurrentDevice.PixelFormat == BMDPixelFormat.YUV8Bit);
        }

        [Test, Order(83)]
        public void IsTheFirstOutputDeviceColorSpaceBT709_OneSubDevice()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            //The Pixel Format selected from the Blackmagic Window is BT709.
            Assert.IsTrue(m_CurrentDevice.RequestedColorSpace == BMDColorSpace.BT709);
            Assert.IsTrue(m_CurrentDevice.InColorSpace == BMDColorSpace.BT709);
        }

        [UnityTest, Order(84)]
        public IEnumerator IsTheFirstOutputDeviceVideoResolutionValid_OneSubDevice()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            // When the input signal is invalid, it gives us the default initial resolution value,
            // which is NTSC (525i59.94 NTSC).
            var result = m_CurrentDevice.TryGetVideoResolution(out var resolution);
            Assert.IsTrue(result);
            result = m_CurrentDevice.TryGetVideoFrameRate(out var framerate);
            Assert.IsTrue(result);

            Assert.IsTrue(resolution.HasValue && resolution.Value != VideoMode.Resolution.fNTSC);
            Assert.IsTrue(resolution.HasValue && framerate.Value != VideoMode.FrameRate.f59_94);
            Assert.IsTrue(String.Compare(m_CurrentDevice.FormatName, Contents.k_SignalNotDefined) != 0);

            // Necessary for m_FrameStatus, so at least 1 frame has been completed.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsNotNull(m_CurrentDevice.m_FrameStatus);
            Assert.IsTrue(m_CurrentDevice.m_FrameStatus.Item2 == BaseDeckLinkDevice.StatusType.Ok);
        }

        [Test, Order(85)]
        public void IsTheFirstOutputDeviceRenderTextureValid_OneSubDevice()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            var frameDimensions = m_CurrentDevice.FrameDimensions;
            var textureWidth = m_CurrentDevice.TargetTexture.width;
            var textureHeight = m_CurrentDevice.TargetTexture.height;

            Assert.IsTrue(frameDimensions.x > 1 && frameDimensions.y > 1);
            Assert.IsTrue(textureWidth == frameDimensions.x);
            Assert.IsTrue(textureHeight == frameDimensions.y);
        }

        [Test, Order(86)]
        public void CantUseKeyingOnTheFirstOutputDevice_OneSubDevice()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);
            Assert.IsFalse(m_CurrentDevice.IsKeyingAvailable());
        }

        [UnityTest, Order(87)]
        public IEnumerator CanChangeTheFirstOutputDevicePixelFormat_OneSubDevice()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            m_CurrentDevice.ChangePixelFormat(BMDPixelFormat.ARGB8Bit);

            // Must be true, because we are reinitializing the device.
            Assert.IsTrue(m_CurrentDevice.UpdateSettings);

            yield return null;

            Assert.IsTrue(m_CurrentDevice.PixelFormat == BMDPixelFormat.ARGB8Bit);
        }

        [UnityTest, Order(88)]
        public IEnumerator CantChangeTheFirstOutputDeviceKeyingMode_OneSubDevice()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            // This one is only detecting if the Keying is available based on the Pixel Format
            // (which is RGBA8, we changed it in the previous tests).
            Assert.IsTrue(m_CurrentDevice.IsKeyingAvailable());

            // It should fails, because Keying is not available on Four Sub Devices.
            var result = m_CurrentDevice.ChangeKeyingMode(KeyingMode.External);
            Assert.IsTrue(result);

            // Changing the current Keying Mode doesn't require to restart the device (dynamic solution).
            Assert.IsFalse(m_CurrentDevice.UpdateSettings);

            yield return null;

            Assert.IsTrue(m_CurrentDevice.OutputKeyingMode == KeyingMode.External);
        }

        [UnityTest, Order(89)]
        public IEnumerator CantChangeTheFirstOutputDeviceLinkMode_OneSubDevice()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            // It should fails, because Keying is not available on Four Sub Devices.
            var result = m_CurrentDevice.ChangeLinkMode(LinkMode.Dual);
            Assert.IsTrue(result);

            // Changing the current Link Mode doesn't require to restart the device (dynamic solution).
            Assert.IsFalse(m_CurrentDevice.UpdateSettings);

            yield return null;

            // Must be true because a link mode can be available on a DeckLink card and unavaible on another one.
            // Can be improved after merging the multi-cards support.
            Assert.IsTrue(m_CurrentDevice.OutputLinkMode == LinkMode.Dual);
        }

        [Test, Order(90)]
        public void IsTheFirstOutputDeviceAudioListenerValid()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            Assert.IsTrue(m_CurrentDevice.m_AudioOutputMode == AudioOutputMode.AudioListener);
            var audioComponent = Camera.main.transform.GetComponent<AudioOutputDevice>();
            Assert.IsNotNull(audioComponent);

            var audioSources = GameObject.FindObjectsOfType<AudioSource>();
            Assert.IsTrue(audioSources != null && audioSources.Length > 0);
        }

        [UnityTest, Order(91)]
        public IEnumerator CantUseAnInvalidLogicalDeviceOnTheFirstOutputDevice_OneSubDevice()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsTrue(m_CurrentDevice.IsActive);

            m_CurrentDevice.DeviceSelection = 10;

            // Changing the current Link Mode doesn't require to restart the device (dynamic solution).
            Assert.IsTrue(m_CurrentDevice.UpdateSettings);

            yield return null;

            Assert.IsNull(m_CurrentDevice.m_Plugin);
            Assert.IsNull(m_CurrentDevice.TargetTexture);
            Assert.IsFalse(m_CurrentDevice.m_Initialized);
            Assert.IsFalse(m_CurrentDevice.UpdateInEditor);
        }

        [Test, Order(92)]
        public void CanStopTheFirstOutputDevice_OneSubDevice()
        {
            // The previous test should already have liberated the allocated resources.

            Assert.IsNull(m_CurrentDevice.m_Plugin);
            Assert.IsNull(m_CurrentDevice.TargetTexture);
            Assert.IsFalse(m_CurrentDevice.UpdateInEditor);
            Assert.IsFalse(m_CurrentDevice.m_Initialized);
        }

        /// ---------------------------------------------------------------------------------------------
        /// Unit Tests for the second Output Device configured in the Blackmagic Window (One Sub Device).
        /// ---------------------------------------------------------------------------------------------

        [UnityTest, Order(100)]
        public IEnumerator CanStartButNotUseTheSecondOutputDevice_OneSubDevice()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_OneSubDeviceConfig.Devices.Count > 0);

            m_CurrentDevice = m_OneSubDeviceConfig.Devices[1];
            Assert.IsNotNull(m_CurrentDevice);

            m_CurrentDevice.UpdateInEditor = true;
            m_CurrentDevice.m_RequiresReinit = false;

            EditorApplication.QueuePlayerLoopUpdate();
            yield return null;

            Assert.IsNull(m_CurrentDevice.m_Plugin);
            Assert.IsNull(m_CurrentDevice.TargetTexture);
            Assert.IsFalse(m_CurrentDevice.m_Initialized);
            Assert.IsFalse(m_CurrentDevice.UpdateInEditor);
        }

        [Test, Order(101)]
        public void IsTheSecondOutputDeviceVideoResolutionValid_OneSubDevice()
        {
            Assert.IsNotNull(m_CurrentDevice);
            Assert.IsFalse(m_CurrentDevice.UpdateInEditor);

            var result = m_CurrentDevice.TryGetVideoResolution(out var resolution);
            Assert.IsTrue(result && resolution.HasValue);
            result = m_CurrentDevice.TryGetVideoFrameRate(out var framerate);
            Assert.IsTrue(result && framerate.HasValue);

            // The Video Resolution must be invalid in the C++ side of the plugin
            // (the device can't be used, so the resolution can't be valid).
            Assert.IsTrue(String.Compare(m_CurrentDevice.FormatName, Contents.k_SignalNotDefined) == 0);

            // The Video Resolution must be valid in the C# side of the plugin
            // (the device has been configured from the Blackmagic window, so the resolution should always be valid).
            Assert.IsTrue(resolution.HasValue && resolution.Value == VideoMode.Resolution.fNTSC);
            Assert.IsTrue(resolution.HasValue && framerate.Value == VideoMode.FrameRate.f59_94);
        }

        [UnityTest, Order(102)]
        public IEnumerator CanStopTheSecondOutputDevice_OneSubDevice()
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

        [UnityTest, Order(110)]
        public IEnumerator CanChangeTheConnectorMappingToTwoSubDevices()
        {
            ChangeConnectorMapping(TwoSubDevicesFullDuplex);

            // Necessary, so all logical devices are retrieved from the C++ API callback.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsTrue(m_DeckLinkManager.connectorMapping == TwoSubDevicesFullDuplex);
        }

        [Test, Order(111)]
        public void CanRetrieveOutputDevicesWithoutErrors_TwoSubDevices()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            // There's no output devices to retrieve, so we are testing with a fake device.
            var success = m_DeckLinkManager.GetExistingVideoDevice(
                "Test fake device",
                OutputContent.k_DeviceType,
                out var videoDeviceGameObjectData);

            Assert.IsFalse(success);
            Assert.IsNull(videoDeviceGameObjectData);
            Assert.IsTrue(m_DeckLinkManager.m_OutputDevices.Count == 0);
        }

        /// -------------------------------------------------------------
        /// Change the connector mapping to Four Sub Devices Half Duplex.
        /// -------------------------------------------------------------

        [UnityTest, Order(120)]
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
