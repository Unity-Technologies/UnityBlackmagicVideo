#if !HDRP_10_2_OR_NEWER && !URP_10_2_OR_NEWER
#define LEGACY_RENDER_PIPELINE
#endif

#if HDRP_10_2_OR_NEWER && !HDRP_12_0_0_OR_NEWER
#define USE_CAMERA_TARGET_TEXTURE
#endif

#if !LEGACY_RENDER_PIPELINE && !USE_CAMERA_TARGET_TEXTURE
#define USE_CAMERA_BRIDGE
#endif

#if UNITY_EDITOR
using UnityEditor.Media;
#endif

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static Unity.Media.Blackmagic.VideoMode;
using System.Collections.Concurrent;
using Unity.Collections.LowLevel.Unsafe;

#if LIVE_CAPTURE_4_0_0_OR_NEWER
using Unity.LiveCapture;
#endif

namespace Unity.Media.Blackmagic
{
    using Resolution = VideoMode.Resolution;
    using FrameRate = VideoMode.FrameRate;

    /// <summary>
    /// Determines what timecode is used by output video frames.
    /// </summary>
    public enum OutputTimecodeMode
    {
        /// <summary>
        /// The timecode is calculated from the frame number.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// The timecode is given by the input video associated with this output device.
        /// </summary>
        SameAsInput = 4,

#if LIVE_CAPTURE_4_0_0_OR_NEWER
        /// <summary>
        /// The timecode is given by a timecode source.
        /// </summary>
        TimecodeSource = 6,

        /// <summary>
        /// The timecode is given by the last frame presented by a timecode synchronizer.
        /// </summary>
        TimecodeSynchronizer = 7,
#endif
    }

    /// <summary>
    /// The class used to retrieve the RenderTexture of an attached camera, to sent it to an external output device.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    partial class DeckLinkOutputDevice : BaseDeckLinkDevice, IVideoOutputDeviceData
    {
        static class Contents
        {
            public static readonly string InvalidPlugin = "The output device failed to initialize.";
            public static readonly string InvalidCameraReference = "The 'Camera' field cannot be null.";
            public static readonly string NoInputDevice = "Same as Input Video Mode requested but no target input device set.";
            public static readonly int SubSamplerFieldName = Shader.PropertyToID("_FieldTex");

            public static void PrintKeyingErrorLogWarning(KeyingMode mode)
            {
                Debug.LogWarning("The selected Fill and Key mode is not compatible with your card or your"
                    + $" current connector mapping. Current mode is {mode}.");
            }

            public static void PrintLinkModeErrorLogWarning(LinkMode mode)
            {
                Debug.LogWarning("This selected Link Mode is not compatible with this device.");
            }
        }

        internal enum SyncMode
        {
            // Output frames are directly scheduled by Unity. It can guarantee that
            // all frames are to be scheduled on time but needs to be explicitly
            // synchronized to output refreshing. The WaitFrameCompletion method is
            // provided for this purpose.
            ManualMode = 0,

            // Output frames are asynchronously scheduled by the completion callback.
            // Unity can update the frame at any time, but it's not guaranteed to be
            // scheduled, as the completion callback only takes the latest state.
            AsyncMode = 1
        }

        internal struct BufferedFrameData
        {
            public long frameCount;
            public AsyncGPUReadbackRequest request;
            public Timecode? timecode;
        }

        internal static DeckLinkOutputDevice ManualModeInstance { get; private set; }

        [SerializeField]
        internal bool m_SameVideoModeAsInput;

        [SerializeField]
        DeckLinkInputDevice m_SameVideoModeAsInputDevice;

#pragma warning disable 649
        [SerializeField]
        int m_SDKDisplayMode = DefaultVideoMode.sdkValue;

        [SerializeField, Range(0, 6)]
        int m_PrerollLength = 3;

        [SerializeField]
        BMDPixelFormat m_RequestedPixelFormat = BMDPixelFormat.YUV8Bit;

        [SerializeField]
        BMDColorSpace m_RequestedColorSpace = BMDColorSpace.BT709;

        [SerializeField]
        BMDTransferFunction m_RequestedTransferFunction = BMDTransferFunction.HLG;

        [SerializeField]
        bool m_LowLatencyMode;

        [SerializeField]
        internal SyncMode m_RequestedSyncMode;

        [SerializeField]
        internal Camera m_TargetCamera;

        [SerializeField]
        KeyingMode m_KeyingMode = KeyingMode.None;

        [SerializeField]
        OutputTimecodeMode m_TimecodeMode;

        [SerializeField]
        bool m_RequestedUseGPUDirect;

#if LIVE_CAPTURE_4_0_0_OR_NEWER
        [SerializeField, HideInInspector]
        TimecodeSourceRef m_TimecodeSource;

        [SerializeField, HideInInspector]
        SynchronizerComponent m_Synchronizer;
#endif

        [SerializeField]
        LinkMode m_LinkMode = LinkMode.Single;

        [SerializeField]
        internal FilterMode m_FilterMode;

#if UNITY_EDITOR
#pragma warning disable 414
        [SerializeField]
        bool m_VideoModeFoldout = true;

        [SerializeField]
        bool m_DeviceSettingsFoldout = true;

        [SerializeField]
        bool m_AudioConfigFoldout = true;
#pragma warning restore 414
#endif

#pragma warning restore 649

        internal DeckLinkOutputDevicePlugin m_Plugin;
        internal Queue<BufferedFrameData> m_FrameQueue;
        internal BlockingCollection<BufferedFrameTexture> m_FrameQueueTexture;

        string m_FormatName;
        RenderTexture m_OddField;
        long m_FrameCount;
        Timecode? m_TimecodeOverride;
        float m_PreviousTime;
        SyncMode m_CurrentSyncMode;
        BMDPixelFormat m_CurrentPixelFormat = BMDPixelFormat.YUV8Bit;
        BMDColorSpace m_CurrentColorSpace = BMDColorSpace.BT709;
        BMDTransferFunction m_CurrentTransferFunction = BMDTransferFunction.HLG;

        bool m_ResourcesUpdated;
        bool m_KeyingIsInitialized;
        int? m_OldSDKDisplayMode = DefaultVideoMode.sdkValue;
        OutputGPUDirect m_GPUDirect;
        bool m_CurrentUseGPUDirect;

#if LEGACY_RENDER_PIPELINE
        bool m_UsingLegacyRenderPipeline;
        CommandBuffer m_LegacyCaptureCommandBuffer;
        bool m_AddedLegacyCommandBuffer;
        Vector2Int m_ResolutionCached;
#endif

        PooledBufferAsyncGPUReadback m_PooledRequests = new PooledBufferAsyncGPUReadback();

        /// <summary>
        /// <see cref="m_SDKDisplayMode"/> is set to DefaultVideoMode.m_SDKValue
        /// in newly constructed instances.
        /// </summary>
        internal static VideoMode DefaultVideoMode
        {
            get
            {
                return VideoModeRegistry.Instance.GetMode(
                    VideoMode.Resolution.fHD1080,
                    VideoMode.FrameRate.f24,
                    VideoMode.ScanMode.Progressive).Value;
            }
        }

        /// <inheritdoc/>
        public RenderTexture TargetTexture => CaptureRenderTexture;

        /// <inheritdoc/>
        public Timecode Timestamp
        {
            get
            {
                var timecode = GetTimecode();

                if (timecode != null)
                {
                    return timecode.Value;
                }

                var frameDuration = m_Plugin?.FrameDuration ?? 0L;
                return frameDuration == 0 ? default : new Timecode(frameDuration, m_FrameCount * frameDuration);
            }
        }

        /// <inheritdoc/>
        public string FormatName
        {
            get
            {
                return !String.IsNullOrEmpty(m_FormatName) ? m_FormatName : BlackmagicUtilities.k_SignalVideoNotDefined;
            }
            set
            {
                if (value != m_FormatName)
                {
                    m_FormatName = value;

                    if (!m_SameVideoModeAsInput)
                    {
                        m_RequiresReinit = true;
                    }
                }
            }
        }

        /// <inheritdoc/>
        public BMDPixelFormat PixelFormat => m_CurrentPixelFormat;

        /// <inheritdoc/>
        public BMDColorSpace InColorSpace => m_CurrentColorSpace;

        /// <inheritdoc/>
        public BMDTransferFunction TransferFunction => m_CurrentTransferFunction;

        /// <inheritdoc/>
        public long FrameDuration => (m_Plugin == null) ? 0
        : (m_Plugin.IsProgressive) ? m_Plugin.FrameDuration
        : m_Plugin.FrameDuration / 2;

        /// <inheritdoc/>
        public Vector2Int FrameDimensions => m_Plugin.FrameDimensions;

        /// <inheritdoc/>
        public bool TryGetFramerate(out int numerator, out int denominator)
        {
            if (m_Plugin != null)
            {
                m_Plugin.GetFrameRate(out numerator, out denominator);
                return true;
            }
            numerator = 0;
            denominator = 0;

            return false;
        }

        /// <inheritdoc/>
        public uint DroppedFrameCount => m_Plugin?.DroppedFrameCount ?? 0;

        /// <inheritdoc/>
        public override bool IsActive => base.IsActive && m_TargetCamera != null;

        /// <inheritdoc/>
        public bool IsUsedInEditMode => UpdateInEditor;

        /// <inheritdoc/>
        public bool IsGenlocked => m_Plugin?.IsReferenceLocked ?? false;

        /// <inheritdoc/>
        public bool IsUsingGPUDirect => m_CurrentUseGPUDirect;

        /// <inheritdoc/>
        public uint GetLateFrameCount => m_Plugin?.LateFrameCount ?? 0;

        /// <inheritdoc/>
        public AudioOutputMode IsOutputtingAudio => m_AudioOutputMode;

        /// <inheritdoc/>
        public OutputTimecodeMode TimecodeMode
        {
            get => m_TimecodeMode;
            set => m_TimecodeMode = value;
        }

        /// <inheritdoc/>
        public bool TryGetVideoResolution(out Resolution? resolution)
        {
            var SDKDisplayMode = PollSameVideoModeAsInputDevice(out var changed);
            if (!SDKDisplayMode.HasValue)
            {
                resolution = null;
                return false;
            }

            resolution = (VideoModeRegistry.Instance.GetModeFromSDK(SDKDisplayMode.Value) is var videoMode && videoMode == null)
                ? resolution = null
                : resolution = videoMode.Value.resolution;

            return resolution != null;
        }

        /// <inheritdoc/>
        public bool TryGetVideoFrameRate(out FrameRate? frameRate)
        {
            var SDKDisplayMode = PollSameVideoModeAsInputDevice(out var changed);
            if (!SDKDisplayMode.HasValue)
            {
                frameRate = null;
                return false;
            }

            frameRate = (VideoModeRegistry.Instance.GetModeFromSDK(SDKDisplayMode.Value) is var videoMode && videoMode == null)
                ? frameRate = null
                : frameRate = videoMode.Value.frameRate;

            return frameRate != null;
        }

        /// <inheritdoc/>
        public bool TryGetVideoScanMode(out ScanMode? scanMode)
        {
            var SDKDisplayMode = PollSameVideoModeAsInputDevice(out var changed);
            if (!SDKDisplayMode.HasValue)
            {
                scanMode = null;
                return false;
            }

            scanMode = (VideoModeRegistry.Instance.GetModeFromSDK(SDKDisplayMode.Value) is var videoMode && videoMode == null)
                ? scanMode = null
                : scanMode = videoMode.Value.scanMode;

            return scanMode != null;
        }

        /// <inheritdoc/>
        public void ChangePixelFormat(BMDPixelFormat pixelFormat) => m_RequestedPixelFormat = pixelFormat;

        /// <inheritdoc/>
        public void ChangeColorSpace(BMDColorSpace colorSpace) => m_RequestedColorSpace = colorSpace;

        /// <inheritdoc/>
        public void ChangeTransferFunction(BMDTransferFunction transferFunction) => m_RequestedTransferFunction = transferFunction;

        /// <inheritdoc/>
        public void ApplyWorkingSpaceConversion(bool apply) => m_WorkingSpaceConversion = apply;

        /// <inheritdoc/>
        public bool ChangeVideoResolution(Resolution resolution)
        {
            if (VideoModeRegistry.Instance.GetModeFromSDK(m_SDKDisplayMode) is var videoMode && videoMode == null)
                return false;

            var frameRate = videoMode.Value.frameRate;
            var scanMode = videoMode.Value.scanMode;

            return ChangeVideoConfiguration(resolution, frameRate, scanMode);
        }

        /// <inheritdoc/>
        public bool ChangeVideoFrameRate(FrameRate frameRate)
        {
            if (VideoModeRegistry.Instance.GetModeFromSDK(m_SDKDisplayMode) is var videoMode && videoMode == null)
                return false;

            var resolution = videoMode.Value.resolution;
            var scanMode = videoMode.Value.scanMode;

            return ChangeVideoConfiguration(resolution, frameRate, scanMode);
        }

        /// <inheritdoc/>
        public bool ChangeVideoScanMode(ScanMode scanMode)
        {
            if (VideoModeRegistry.Instance.GetModeFromSDK(m_SDKDisplayMode) is var videoMode && videoMode == null)
                return false;

            var frameRate = videoMode.Value.frameRate;
            var resolution = videoMode.Value.resolution;

            return ChangeVideoConfiguration(resolution, frameRate, scanMode);
        }

        /// <inheritdoc/>
        public bool ChangeVideoConfiguration(Resolution resolution, FrameRate frameRate, ScanMode scanMode)
        {
            var mode = VideoModeRegistry.Instance.GetMode(resolution, frameRate, scanMode);
            var modeIsValid = mode.HasValue;

            if (modeIsValid && m_SDKDisplayMode != mode.Value.sdkValue
                || m_SameVideoModeAsInput == true)
            {
                m_SameVideoModeAsInput = false;
                m_SDKDisplayMode = mode.Value.sdkValue;
                FormatName = mode.Value.VideoModeName();
                m_RequiresReinit = true;
            }

            return modeIsValid;
        }

        /// <inheritdoc/>
        public bool ChangeKeyingMode(KeyingMode mode)
        {
            if (m_KeyingMode == mode)
                return false;

            m_KeyingMode = mode;

            if (!IsActive)
                return true;

            if (mode == KeyingMode.None)
                return (m_KeyingIsInitialized) ? m_Plugin.DisableKeying() : true;

            if (m_Plugin.IsKeyingModeCompatible((int)m_KeyingMode) is var isCompatible && !isCompatible)
            {
                Contents.PrintKeyingErrorLogWarning(m_KeyingMode);
                return isCompatible;
            }

            if (m_CurrentColorSpace == BMDColorSpace.BT2020)
                return false;

            return (m_KeyingIsInitialized)
                ? m_Plugin.ChangeKeyingMode((int)mode)
                : InitializeKeying();
        }

        public bool ChangeLinkMode(LinkMode mode)
        {
            if (m_LinkMode == mode)
                return false;

            m_LinkMode = mode;

            // The new Link Mode will be set as soon as the device is in use.
            if (!IsActive)
                return true;

            var linkMode = (int)mode;
            if (!m_Plugin.IsLinkCompatible(linkMode))
            {
                Contents.PrintLinkModeErrorLogWarning(mode);
                return true;
            }

            return m_Plugin.SetLinkMode(linkMode);
        }

        /// <inheritdoc/>
        public bool ChangeSameVideoModeAsInputDevice(bool enabled, DeckLinkInputDevice inputDevice)
        {
            var changedEnabled = m_SameVideoModeAsInput != enabled;
            var changedDevice = m_SameVideoModeAsInputDevice != inputDevice;

            m_SameVideoModeAsInput = enabled;
            m_SameVideoModeAsInputDevice = inputDevice;

            PollSameVideoModeAsInputDevice(out var changed);
            if (changed)
                m_RequiresReinit = true;

            return changedEnabled || changedDevice;
        }

        /// <inheritdoc/>
        public void SetTimecodeOverride(Timecode? timecode)
        {
            m_TimecodeOverride = timecode;
        }

#if LIVE_CAPTURE_4_0_0_OR_NEWER
        /// <inheritdoc/>
        public void SetTimecodeOverride(Unity.LiveCapture.Timecode? timecode)
        {
            if (!timecode.HasValue)
            {
                m_TimecodeOverride = null;
                return;
            }

            if (!TryGetFramerate(out var num, out var den))
            {
                return;
            }

            if (!m_Plugin.IsProgressive)
            {
                den /= 2;
            }

            var isDropFrame = BlackmagicUtilities.k_FlicksPerSecond % m_Plugin.FrameDuration != 0;
            var frameRate = new LiveCapture.FrameRate(num, den, isDropFrame);
            var flicks = BlackmagicUtilities.TimecodeToFlicks(timecode.Value, frameRate);

            m_TimecodeOverride = flicks;
        }

#endif

        internal override VideoDeviceType DeviceType => VideoDeviceType.Output;

        internal override bool UpdateSettings
        {
            get
            {
                if (m_RequestedPixelFormat == m_CurrentPixelFormat &&
                    m_RequestedColorSpace == m_CurrentColorSpace &&
                    m_RequestedTransferFunction == m_CurrentTransferFunction &&
                    m_CurrentSyncMode == m_RequestedSyncMode &&
                    !m_RequiresReinit)
                {
                    m_ResourcesUpdated = false;
                    return false;
                }
                return true;
            }
        }

        internal KeyingMode OutputKeyingMode => m_KeyingMode;

        internal LinkMode OutputLinkMode => m_LinkMode;

        internal BMDColorSpace RequestedColorSpace => m_RequestedColorSpace;

        internal bool IsWindowsPlatform()
        {
            return Application.platform == RuntimePlatform.WindowsEditor ||
                Application.platform == RuntimePlatform.WindowsPlayer;
        }

#if UNITY_EDITOR
        /// <summary>
        /// The framerate of the current device.
        /// </summary>
        internal MediaRational FrameRate => new MediaRational(
            (int)BlackmagicUtilities.k_FlicksPerSecond,
            (m_Plugin.IsProgressive) ? (int)m_Plugin.FrameDuration : (int)m_Plugin.FrameDuration / 2
        );
#endif
        internal SyncMode CurrentSyncMode => m_CurrentSyncMode;
        internal SyncMode RequestedSyncMode => m_RequestedSyncMode;

        internal bool IsGPUDirectNotAvailable()
        {
            m_CurrentUseGPUDirect = IsWindowsPlatform()
                ? m_RequestedUseGPUDirect && OutputGPUDirectPlugin.IsGPUDirectAvailable()
                : false;

            return m_RequestedUseGPUDirect && !m_CurrentUseGPUDirect;
        }

#if UNITY_EDITOR
        protected void Awake()
        {
            OutputColorTransform.AddShadersToSettings();

            if (IsWindowsPlatform())
            {
                OutputGPUDirect.CacheIfGPUDirectIsAvailable("Is GPUDirect Compatible");
            }
        }

#endif

        /// <inheritdoc/>
        protected override bool Initialize()
        {
            m_DeviceIndex = VideoIOFrameManager.Register(this);

            // The Register must be done even if the initialization has failed,
            // so the 'UpdateResources' can be called, trying to re-initialize the device.
            if (!InitializeResources())
            {
                VideoIOFrameManager.Unregister(this);
                m_DeviceIndex = -1;
                UpdateInEditor = false;
                return false;
            }

#if UNITY_EDITOR
            runInEditMode = true;
#endif
            return true;
        }

        bool InitializeKeying()
        {
            m_KeyingIsInitialized = m_Plugin.InitializeKeyingMode((int)m_KeyingMode);
            if (!m_KeyingIsInitialized)
            {
                Debug.LogWarning("The current Fill and Key mode failed during initialization.");
            }
            return m_KeyingIsInitialized;
        }

        void SetupRenderTexture()
        {
#if USE_CAMERA_BRIDGE
            CameraCaptureBridge.enabled = true;
            CameraCaptureBridge.AddCaptureAction(m_TargetCamera, Capture);
#endif
            var dimensions = m_Plugin.FrameDimensions;
            if (dimensions.x <= 1 || dimensions.y <= 1)
            {
                Debug.LogWarning("Invalid output dimension, initialization has failed.");
                return;
            }

            Assert.IsNull(CaptureRenderTexture);

            CaptureRenderTexture = new RenderTexture(dimensions.x, dimensions.y, 0, GraphicsFormat.R16G16B16A16_SFloat);
            CaptureRenderTexture.hideFlags = HideFlags.DontSave;
            CaptureRenderTexture.antiAliasing = BlackmagicUtilities.GetAntiAliasingValueFromCamera(m_TargetCamera);
            CaptureRenderTexture.filterMode = m_FilterMode;

            // TODO, depends on upcoming HDRP changes for the Graphics Compositor Custom Render.
#if USE_CAMERA_TARGET_TEXTURE
            m_TargetCamera.targetTexture = CaptureRenderTexture;
#endif

#if LEGACY_RENDER_PIPELINE
            if (m_UsingLegacyRenderPipeline)
            {
                m_LegacyCaptureCommandBuffer = new CommandBuffer();
                m_TargetCamera.AddCommandBuffer(CameraEvent.BeforeImageEffects, m_LegacyCaptureCommandBuffer);
                m_AddedLegacyCommandBuffer = true;
            }
#endif
        }

        bool InitializeResources()
        {
            if (DeviceSelection < 0)
            {
                Debug.LogWarning("Output device not used, the index is set to None.");
                return false;
            }

            if (m_TargetCamera == null)
            {
                Debug.LogError(Contents.InvalidCameraReference);
                return false;
            }

            var SDKDisplayMode = PollSameVideoModeAsInputDevice(out var changed);
            if (SDKDisplayMode.HasValue)
            {
                var videoMode = SDKDisplayMode.Value.SDKValueToVideoMode();
                m_FormatName = videoMode.HasValue ?
                    videoMode.Value.VideoModeName() : BlackmagicUtilities.k_SignalVideoNotDefined;
            }
            else
            {
                m_FormatName = BlackmagicUtilities.k_SignalVideoNotDefined;
                m_FrameStatus = (Contents.NoInputDevice, StatusType.Error);

                if (m_SameVideoModeAsInputDevice == null)
                    Debug.LogWarning(Contents.NoInputDevice);
                else
                    Debug.LogWarning($"Failed to detect and match Video Mode of input device {m_SameVideoModeAsInputDevice.name}.");

                return false;
            }

#if LEGACY_RENDER_PIPELINE
            m_ResolutionCached = Vector2Int.zero;
            m_UsingLegacyRenderPipeline = true;
#endif

            m_CurrentUseGPUDirect = IsWindowsPlatform()
                ? m_RequestedUseGPUDirect && OutputGPUDirectPlugin.IsGPUDirectAvailable()
                : false;

            if (!CreatePlugin())
                return false;

            Assert.IsNotNull(m_Plugin, $"Failed to initialize the output device, index is {DeviceSelection}.");

            SetupRenderTexture();

            m_FrameQueue = new Queue<BufferedFrameData>();
            m_FrameQueueTexture = new BlockingCollection<BufferedFrameTexture>();

            if (!m_Plugin.IsValidConfiguration())
            {
                m_FrameStatus = ("Invalid configuration (incompatible settings used)", StatusType.Error);
            }
            else
            {
                InitializeKeyingPlugin();
                InitializeLinkModePlugin();
                SetupAudioOutput();
            }

            if (m_CurrentUseGPUDirect)
            {
                m_GPUDirect = new OutputGPUDirect();
                m_GPUDirect.Setup(m_Plugin.CurrentDevice);
            }

            return true;
        }

        void InitializeKeyingPlugin()
        {
            // Keying is not compatible with BT2020.
            if (m_CurrentColorSpace == BMDColorSpace.BT2020)
                return;

            m_KeyingIsInitialized = false;
            var keyingActivated = m_KeyingMode != KeyingMode.None;

            if (keyingActivated && m_Plugin.IsKeyingModeCompatible((int)m_KeyingMode))
            {
                InitializeKeying();
            }
            else if (keyingActivated)
            {
                Contents.PrintKeyingErrorLogWarning(m_KeyingMode);
            }
        }

        void InitializeLinkModePlugin()
        {
            var linkMode = (int)m_LinkMode;
            var succeededLinkMode = false;

            if (m_Plugin.IsLinkCompatible(linkMode))
            {
                succeededLinkMode = m_Plugin.SetLinkMode(linkMode);
            }
            else
            {
                Contents.PrintLinkModeErrorLogWarning(m_LinkMode);
            }

            if (!succeededLinkMode)
            {
                m_Plugin.SetLinkMode((int)LinkMode.Single);
            }
        }

        void DestroyResources()
        {
            if (m_FrameQueueTexture != null)
            {
                m_FrameQueueTexture.Dispose();
                m_FrameQueueTexture = null;
            }

            if (m_GPUDirect != null)
            {
                m_GPUDirect.Dispose();
                m_GPUDirect = null;
            }

            if (m_OddField != null)
            {
                RenderTexture.ReleaseTemporary(m_OddField);
                m_OddField = null;
            }

            if (m_CurrentSyncMode == SyncMode.ManualMode)
                ManualModeInstance = null;

            CleanupAudioOutput();

            if (m_Plugin != null)
            {
#if UNITY_EDITOR
                m_Plugin.OnFrameError -= OnFrameErrorTriggered;
#endif
                m_Plugin.Dispose();
                m_Plugin = null;
            }

#if USE_CAMERA_TARGET_TEXTURE
            if (m_TargetCamera)
            {
                m_TargetCamera.targetTexture = null;
            }
#endif
            m_FrameQueue?.Clear();

            CleanupRenderTexture();

            m_PooledRequests.Dispose();
        }

        protected override void UpdateResources()
        {
            if (!UpdateSettings)
            {
                m_ResourcesUpdated = false;
                return;
            }

            DestroyResources();

            m_Initialized = InitializeResources();
            if (!m_Initialized)
            {
                UpdateInEditor = false;
                m_Initialized = true;
                return;
            }

            m_RequiresReinit = false;
            m_ResourcesUpdated = true;
            m_PreviousTime = -1.0f;
        }

        bool CreatePlugin()
        {
            var audioData = GetEnableAudioAndSampleRate();
            var resolvedSDKDisplayMode = ResolveSDKDisplayMode().Value;

            m_CurrentSyncMode = m_RequestedSyncMode;

            m_Plugin = (m_CurrentSyncMode == SyncMode.ManualMode)
                ? DeckLinkOutputDevicePlugin.CreateManualOutputDevice(
                m_DeviceIndex,
                DeviceSelection,
                resolvedSDKDisplayMode,
                m_RequestedPixelFormat,
                m_RequestedColorSpace,
                m_RequestedTransferFunction,
                m_PrerollLength,
                audioData.EnableAudio,
                audioData.AudioChannelCount,
                audioData.AudioSampleRate,
                m_CurrentUseGPUDirect)
                : DeckLinkOutputDevicePlugin.CreateAsyncOutputDevice(
                m_DeviceIndex,
                DeviceSelection,
                resolvedSDKDisplayMode,
                m_RequestedPixelFormat,
                m_RequestedColorSpace,
                m_RequestedTransferFunction,
                m_PrerollLength,
                audioData.EnableAudio,
                audioData.AudioChannelCount,
                audioData.AudioSampleRate,
                m_CurrentUseGPUDirect);

            if (m_Plugin == null)
                return false;

            // TODO: Improve the 'Error Callback' system, we cannot use the callback during the plugin creation.
            if (!m_Plugin.IsInitialized())
            {
                m_FrameStatus = ("Can't start output device (possibly already used)", StatusType.Error);
            }

            if (m_CurrentSyncMode == SyncMode.ManualMode)
                PromoteToManualMode();

            // This callback is not useful in a standalone build (for now).
            // It's also causing issues in a IL2CPP build.
#if UNITY_EDITOR
            m_Plugin.OnFrameError += OnFrameErrorTriggered;
#endif

            m_Plugin.InitializeCallbacks();
            m_Plugin.SetDefaultScheduleTime(0.0f);
            m_FrameCount = 0;
            m_PreviousTime = Time.unscaledTime;
            m_CurrentPixelFormat = m_RequestedPixelFormat;
            m_CurrentColorSpace = m_RequestedColorSpace;
            m_CurrentTransferFunction = m_RequestedTransferFunction;

            return true;
        }

#if USE_CAMERA_BRIDGE
        void Capture(RenderTargetIdentifier source, CommandBuffer cmd)
        {
            cmd.Blit(source, CaptureRenderTexture);
        }

#endif

        /// <inheritdoc/>
        protected override void Cleanup()
        {
            VideoIOFrameManager.Unregister(this);
            m_DeviceIndex = -1;

            OutputColorTransform.Reset();

            DestroyResources();
        }

        void CleanupRenderTexture()
        {
#if USE_CAMERA_BRIDGE
            CameraCaptureBridge.RemoveCaptureAction(m_TargetCamera, Capture);
#endif
            if (CaptureRenderTexture != null)
            {
                CaptureRenderTexture.Release();
                BlackmagicUtilities.Destroy(CaptureRenderTexture);
                CaptureRenderTexture = null;
            }

#if LEGACY_RENDER_PIPELINE
            if (m_AddedLegacyCommandBuffer && m_TargetCamera)
            {
                m_TargetCamera.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, m_LegacyCaptureCommandBuffer);
                m_LegacyCaptureCommandBuffer.Release();
                m_AddedLegacyCommandBuffer = false;
            }
#endif
        }

        /// <summary>
        /// This function allows to not stay in a Dropped Frames status by recovering the lost time.
        /// </summary>
        /// <remarks>
        /// In most cases, it means there's an unrecoverable pipeline or hardware error.
        /// But this feature enables recovery in edge cases, such as an abnormally long initialization.
        /// </remarks>
        void CalculateTimeBetweenFrame()
        {
            if (m_Plugin != null && m_CurrentSyncMode == SyncMode.ManualMode)
            {
                var currentTime = Time.unscaledTime;

                if (m_PreviousTime < 0.0f)
                    m_PreviousTime = currentTime;

                var timeElapsed = currentTime - m_PreviousTime;
                m_PreviousTime = currentTime;

                if (!TryGetFramerate(out var numerator, out var denominator))
                    return;

                var delta = (float)(denominator) / (float)(numerator);
                const float k_DelayToRecoverInMs = 50.0f;

                if (timeElapsed > (delta * k_DelayToRecoverInMs))
                {
                    var frameToAdvance = Math.Round(timeElapsed / delta);
                    m_Plugin.SetDefaultScheduleTime((float)frameToAdvance);
                }
                else
                {
                    m_Plugin.SetDefaultScheduleTime(0.0f);
                }
            }
        }

        /// <summary>
        /// Updates the video device and blits the target camera into the target texture.
        /// </summary>
        public override void PerformUpdate()
        {
            PollSameVideoModeAsInputDevice(out var changed);
            if (changed)
                m_RequiresReinit = true;

            base.PerformUpdate();

            var captureRenderTexture = CaptureRenderTexture;
            if (!IsActive || captureRenderTexture == null)
                return;

            if (!m_ResourcesUpdated)
            {
                CalculateTimeBetweenFrame();
            }

            if (m_FilterMode != captureRenderTexture.filterMode)
            {
                captureRenderTexture.filterMode = m_FilterMode;
            }


#if USE_CAMERA_TARGET_TEXTURE
            m_TargetCamera.targetTexture.filterMode = m_FilterMode;
            EncodeFrameAndAddToQueue(m_TargetCamera.targetTexture);
#else
            EncodeFrameAndAddToQueue(captureRenderTexture);
#endif

            if (m_GPUDirect != null)
            {
                ProcessFrameQueueGPUDirect(m_LowLatencyMode);
            }
            else
            {
                ProcessFrameQueue(m_LowLatencyMode);
            }

            UpdateAudio();

#if LIVE_CAPTURE_4_0_0_OR_NEWER
            var syncProvider = SyncManager.Instance.ActiveSyncProvider;
            if (syncProvider == null || syncProvider.Status == SyncStatus.Synchronized)
                return;
#endif

            if (Application.isPlaying && m_CurrentSyncMode == SyncMode.ManualMode && m_FrameCount > QueueLength)
                m_Plugin.WaitCompletion(m_FrameCount - QueueLength);
        }

        internal void PromoteToManualMode()
        {
            // Promote this instance to Manual mode.
            // Check the frame duration if there is another Manual OutputDevice.
            var duration = FrameDuration;
            if (ManualModeInstance != null && ManualModeInstance.FrameDuration != duration)
            {
                Debug.LogError("Master frame rate mismatch. When using multiple master OutputDevices, they should have " +
                    "the exact same frame rate.");
            }

            ManualModeInstance = this;
        }

        internal void EncodeFrameAndAddToQueue(RenderTexture source)
        {
            RenderTexture temporaryTextureFrame = null;
            var dimensions = m_Plugin.FrameDimensions;
            if (dimensions.x <= 1 || dimensions.y <= 1)
                return;

#if LEGACY_RENDER_PIPELINE
            if (m_UsingLegacyRenderPipeline && m_ResolutionCached != dimensions)
            {
                m_LegacyCaptureCommandBuffer.Clear();
                m_LegacyCaptureCommandBuffer.Blit(BuiltinRenderTextureType.CameraTarget, CaptureRenderTexture);
            }
            m_ResolutionCached = dimensions;
#endif
            uint w, h, d;
            m_Plugin.GetBackingFrameByteDimensions(out w, out h, out d);

            Assert.IsTrue(w % 4 == 0); // validate byte length is word aligned

            // Allocate GPU memory for packed texture transfer. Sampling must be exact to preserve packed values.
            temporaryTextureFrame = RenderTexture.GetTemporary((int)w / 4, (int)h, 0, GraphicsFormat.R8G8B8A8_UNorm);
            temporaryTextureFrame.filterMode = FilterMode.Point;

            // Pack the image using user defined preference
            if (m_Plugin.IsProgressive)
            {
                var mat = OutputColorTransform.Get(m_CurrentPixelFormat, m_CurrentColorSpace, m_CurrentTransferFunction, m_WorkingSpaceConversion);
                var tmp = RenderTexture.active;
                Graphics.Blit(source, temporaryTextureFrame, mat, 0);
                RenderTexture.active = tmp;
            }
            // Interlace mode: displaying alternating sets of lines.
            // First even numbered lines are displayed and then odd numbered lines are displayed.
            // Note: can lead to motion artifacts.
            else if (m_OddField == null)
            {
                // Odd field: Make a copy of this frame to _oddField.
                m_OddField = RenderTexture.GetTemporary(dimensions.x, dimensions.y, 0, GraphicsFormat.R16G16B16A16_SFloat);
                m_OddField.filterMode = m_FilterMode;
                RenderTexture.ReleaseTemporary(temporaryTextureFrame);
                var tmp = RenderTexture.active;
                Graphics.Blit(source, m_OddField);
                RenderTexture.active = tmp;
                return;
            }
            else
            {
                var mat = OutputColorTransform.Get(m_CurrentPixelFormat, m_CurrentColorSpace, m_CurrentTransferFunction, m_WorkingSpaceConversion);
                Assert.IsNotNull(mat);
                mat.SetTexture(Contents.SubSamplerFieldName, m_OddField);
                var tmp = RenderTexture.active;
                Graphics.Blit(source, temporaryTextureFrame, mat, 1);
                RenderTexture.active = tmp;

                RenderTexture.ReleaseTemporary(m_OddField);
                m_OddField = null;
            }

            // Push actual frame in the queue and release the RenderTexture.
            if (temporaryTextureFrame != null)
            {
                if (m_GPUDirect != null)
                {
                    var timecode = GetTimecode();

                    m_FrameQueueTexture.Add(new BufferedFrameTexture
                    {
                        texture = temporaryTextureFrame,
                        timecode = timecode
                    });
                }
                else
                {
                    var request = m_PooledRequests.RequestGPUReadBack(m_FrameCount, temporaryTextureFrame);
                    var timecode = GetTimecode();

                    m_FrameQueue.Enqueue(new BufferedFrameData
                    {
                        frameCount = m_FrameCount,
                        request = request,
                        timecode = timecode
                    });

                    RenderTexture.ReleaseTemporary(temporaryTextureFrame);
                }
            }
        }

        void ProcessFrameQueue(bool sync)
        {
            while (m_FrameQueue.Count > 0)
            {
                var frame = m_FrameQueue.Peek();

                if (frame.request.hasError)
                {
                    Debug.LogWarning("GPU readback error was detected.");
                    m_FrameQueue.Dequeue();
                    continue;
                }

                if (sync)
                {
                    frame.request.WaitForCompletion();
                }
                else
                {
                    if (!frame.request.done)
                        break;
                }

                if (frame.request.hasError)
                {
                    Debug.LogWarning("GPU readback error was detected.");
                    m_FrameQueue.Dequeue();
                    continue;
                }

                var timecode = frame.timecode ?? new Timecode(m_Plugin.FrameDuration, m_FrameCount * m_Plugin.FrameDuration);

                m_Plugin.FeedFrame(frame.request.GetData<byte>(), timecode);

                m_PooledRequests.BMDRelease(frame.frameCount);

                m_FrameCount++;
                m_FrameQueue.Dequeue();
            }

            InvokeOnFrameProcessedEvent();
        }

        void ProcessFrameQueueGPUDirect(bool sync)
        {
            while (m_FrameQueueTexture.Count > 0)
            {
                if (!m_FrameQueueTexture.TryTake(out BufferedFrameTexture frame))
                    continue;

                if (frame.texture == null)
                {
                    Debug.LogWarning("Invalid RenderTexture detected.");
                    continue;
                }

                var timecode = frame.timecode ?? new Timecode(m_Plugin.FrameDuration, m_FrameCount * m_Plugin.FrameDuration);

                m_GPUDirect.FeedFrameTexture(m_Plugin.CurrentDevice, frame.texture, timecode.ToBCD());

                RenderTexture.ReleaseTemporary(frame.texture);

                m_FrameCount++;
            }

            InvokeOnFrameProcessedEvent();
        }

        internal bool IsKeyingAvailable()
        {
            switch (m_RequestedPixelFormat)
            {
                case BMDPixelFormat.ARGB8Bit:
                case BMDPixelFormat.BGRA8Bit:
                    return true;
                default:
                    return false;
            }
        }

        Timecode? GetTimecode()
        {
            if (m_TimecodeOverride.HasValue)
            {
                return m_TimecodeOverride.Value;
            }

            switch (m_TimecodeMode)
            {
                case OutputTimecodeMode.SameAsInput:
                {
                    if (m_SameVideoModeAsInput && m_SameVideoModeAsInputDevice != null)
                    {
                        return m_SameVideoModeAsInputDevice.Timestamp;
                    }
                    break;
                }
#if LIVE_CAPTURE_4_0_0_OR_NEWER
                case OutputTimecodeMode.TimecodeSource:
                {
                    var source = m_TimecodeSource.Resolve();

                    if (source != null)
                    {
                        var currentTime = source?.CurrentTime;

                        if (currentTime != null)
                        {
                            return BlackmagicUtilities.FrameTimeToFlicks(currentTime.Value);
                        }
                    }
                    break;
                }
                case OutputTimecodeMode.TimecodeSynchronizer:
                {
                    if (m_Synchronizer != null)
                    {
                        var synchronizer = m_Synchronizer.Synchronizer;
                        var presentTime = synchronizer.PresentTime;

                        if (presentTime != null)
                        {
                            return BlackmagicUtilities.FrameTimeToFlicks(presentTime.Value);
                        }
                    }
                    break;
                }
#endif
            }

            return null;
        }

        int? ResolveSDKDisplayMode()
        {
            if (m_SameVideoModeAsInput)
            {
                if (m_SameVideoModeAsInputDevice == null)
                    return null;
                var mode = m_SameVideoModeAsInputDevice.VideoMode;
                return (mode.HasValue) ? mode.Value.sdkValue : null as int?;
            }
            return m_SDKDisplayMode;
        }

        int? PollSameVideoModeAsInputDevice(out bool changed)
        {
            var newSDKDisplayMode = ResolveSDKDisplayMode();
            changed = m_OldSDKDisplayMode != newSDKDisplayMode;
            m_OldSDKDisplayMode = newSDKDisplayMode;
            return newSDKDisplayMode;
        }

        [AOT.MonoPInvokeCallback(typeof(DeckLinkOutputDevicePlugin.FrameErrorCallback))]
        static void OnFrameErrorTriggered(int index, IntPtr message, StatusType status)
        {
            if (message == IntPtr.Zero)
                return;

            if (index == -1 || !VideoIOFrameManager.GetOutputDevice(index, out var deckLinkOutputDevice))
                return;

            var error = Marshal.PtrToStringAnsi(message);
            deckLinkOutputDevice.m_FrameStatus = (error, status);
        }
    }
}
