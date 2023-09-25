using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;

#if LIVE_CAPTURE_4_0_0_OR_NEWER
using Unity.LiveCapture;
#endif

namespace Unity.Media.Blackmagic
{
    using Resolution = VideoMode.Resolution;
    using ScanMode = VideoMode.ScanMode;

    public enum InputError
    {
        NoError,
        IncompatiblePixelFormatAndVideoMode,
        AudioPacketInvalid,
        DeviceAlreadyUsed,
        NoInputSource
    }

    /// <summary>
    /// The class used to retrieve and draw a video stream in a RenderTexture.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    sealed partial class DeckLinkInputDevice : BaseDeckLinkDevice, IVideoInputDeviceData
    {
#pragma warning disable 649
        [SerializeField]
        FilterMode m_FilterMode;

        [SerializeField]
        internal BMDPixelFormat m_RequestedInPixelFormat;
        [SerializeField]
        internal BMDColorSpace m_RequestedColorSpace;
        [SerializeField]
        internal BMDTransferFunction m_RequestedTransferFunction;

        [SerializeField]
        internal bool m_EnablePassThrough = false;

        [SerializeField]
        internal int m_SignalOverride = 0;

#if UNITY_EDITOR
#pragma warning disable 414
        [SerializeField]
        bool m_VideoModeFoldout = true;
        [SerializeField]
        bool m_DeviceSettingsFoldout = true;
#pragma warning restore 414
#endif

#pragma warning restore 649

        internal BMDPixelFormat m_DesiredInPixelFormat;

        internal DeckLinkInputDevicePlugin m_Plugin;
        internal Texture2D m_SourceTexture;
        NativeArray<float> m_SynchronizedAudioBuffer;

        InputVideoFormat? m_Format;
        FrameQueue m_Queue;
        Material m_UnpackMaterial;
        int m_UnpackPass;
        ThreadedMemcpy m_Memcpy;
        bool m_IsDropFrame;
        long m_FrameElapsedDuration;

        /// <inheritdoc/>
        public RenderTexture TargetTexture => CaptureRenderTexture;

        /// <inheritdoc/>
        public Timecode Timestamp { get; private set; }

        /// <inheritdoc/>
        public string FormatName => m_Format?.formatName ?? BlackmagicUtilities.k_SignalVideoNotDefined;

        /// <inheritdoc/>
        public bool TryGetVideoResolution(out Resolution? resolution)
        {
            resolution = m_Format?.mode?.resolution;
            return m_Format.HasValue;
        }

        /// <inheritdoc/>
        public bool TryGetVideoFrameRate(out VideoMode.FrameRate? frameRate)
        {
            frameRate = m_Format?.mode?.frameRate;
            return m_Format.HasValue;
        }

        /// <inheritdoc/>
        public bool TryGetVideoScanMode(out ScanMode? scanMode)
        {
            scanMode = m_Format?.mode?.scanMode;
            return m_Format.HasValue;
        }

        /// <summary>
        /// The VideoMode of the incoming signal.
        /// </summary>
        /// <remarks>
        /// Value is unset if the mode is unknown or if the device isn't active.
        /// </remarks>
        public VideoMode? VideoMode => m_Format?.mode;

        /// <inheritdoc/>
        public long FrameDuration => m_Format?.frameDuration ?? 0;

        /// <summary>
        /// The pixel format to be requested and handled by the input device.
        /// </summary>
        public BMDPixelFormat requestedInPixelFormat
        {
            get => m_RequestedInPixelFormat;
            set => m_RequestedInPixelFormat = value;
        }

        /// <summary>
        /// The pixel format to be requested and handled by the input device.
        /// </summary>
        public BMDColorSpace requestedColorSpace
        {
            get => m_RequestedColorSpace;
            set => m_RequestedColorSpace = value;
        }

        /// <summary>
        /// The pixel format to be requested and handled by the input device.
        /// </summary>
        public BMDTransferFunction requestedTransferFormat
        {
            get => m_RequestedTransferFunction;
            set => m_RequestedTransferFunction = value;
        }

        void HandleOverride()
        {
            var needsOverride =
                m_RequestedInPixelFormat != BMDPixelFormat.UseBestQuality ||
                m_RequestedColorSpace != BMDColorSpace.UseDeviceSignal ||
                m_RequestedTransferFunction != BMDTransferFunction.UseDeviceSignal;
            m_SignalOverride = needsOverride ? 1 : 0;
        }

        /// <inheritdoc/>
        public void ChangePixelFormat(BMDPixelFormat pixelFormat)
        {
            m_RequestedInPixelFormat = pixelFormat;
            HandleOverride();
        }

        /// <inheritdoc/>
        public void ChangeColorSpace(BMDColorSpace colorSpace)
        {
            m_RequestedColorSpace = colorSpace;
            HandleOverride();
        }

        /// <inheritdoc/>
        public void ChangeTransferFunction(BMDTransferFunction transferFunction)
        {
            m_RequestedTransferFunction = transferFunction;
            HandleOverride();
        }

        /// <inheritdoc/>
        public BMDPixelFormat PixelFormat => m_Format?.pixelFormat ?? default;

        /// <inheritdoc/>
        public BMDColorSpace InColorSpace => m_RequestedColorSpace != BMDColorSpace.UseDeviceSignal ? m_RequestedColorSpace : (m_Format?.colorSpace ?? default);

        /// <inheritdoc/>
        public BMDTransferFunction TransferFunction => m_Format?.transferFunction ?? default;

        /// <inheritdoc/>
        public Vector2Int FrameDimensions => m_Format != null ? new Vector2Int(m_Format.Value.width, m_Format.Value.height) : default;

        /// <inheritdoc/>
        public void ApplyWorkingSpaceConversion(bool apply) => m_WorkingSpaceConversion = apply;

        /// <inheritdoc/>
        public bool TryGetFramerate(out int numerator, out int denominator)
        {
            if (m_Format != null)
            {
                numerator = m_Format.Value.frameRateNumerator;
                denominator = m_Format.Value.frameRateDenominator;
                return true;
            }

            numerator = 0;
            denominator = 0;
            return false;
        }

        /// <inheritdoc/>
        public uint DroppedFrameCount { get; private set; }

        /// <inheritdoc/>
        public bool IsUsedInEditMode => UpdateInEditor;

        /// <inheritdoc/>
        public InputError LastFrameError { get; private set; }

        internal override VideoDeviceType DeviceType => VideoDeviceType.Input;

        event Action<InputVideoFrame> VideoFrameArrived;
        event Action<InputAudioFrame> AudioFrameArrived;
        event Action<SynchronizedAudioFrame> SynchronizedAudioFrameCallback;

        internal override bool UpdateSettings
        {
            get
            {
                var requestedInPixelFormat = (m_SignalOverride == 0)
                    ? BMDPixelFormat.UseBestQuality
                    : m_RequestedInPixelFormat;

                return m_RequiresReinit || requestedInPixelFormat != m_DesiredInPixelFormat;
            }
        }

#if UNITY_EDITOR
        void Awake()
        {
            ColorTransform.AddShadersToSettings();
        }

#endif

        /// <inheritdoc />
        protected override void OnEnable()
        {
            base.OnEnable();

#if LIVE_CAPTURE_4_0_0_OR_NEWER
            TimecodeSourceManager.Instance.Register(this);
            TimedDataSourceManager.Instance.Register(this);
#endif
        }

        /// <inheritdoc />
        protected override void OnDisable()
        {
            base.OnDisable();

#if LIVE_CAPTURE_4_0_0_OR_NEWER
            TimecodeSourceManager.Instance.Unregister(this);
            TimedDataSourceManager.Instance.Unregister(this);
#endif
        }

        protected override bool Initialize()
        {
            m_DeviceIndex = VideoIOFrameManager.Register(this);

            if (DeviceSelection < 0)
            {
                Debug.LogWarning("Input device not used, the index is set to None.");
                return false;
            }

            if (!InitializeResources())
            {
                VideoIOFrameManager.Unregister(this);
                m_DeviceIndex = -1;
                return false;
            }

#if LIVE_CAPTURE_4_0_0_OR_NEWER
            m_TimecodeSourceState = new TimecodeSourceState(PollTimecode);
#endif
#if UNITY_EDITOR
            runInEditMode = true;
#endif
            return true;
        }

        protected override void UpdateResources()
        {
            if (!UpdateSettings)
                return;

            m_DesiredInPixelFormat = requestedInPixelFormat;

            DestroyResources();

            if (DeviceSelection < 0)
            {
                Debug.LogWarning("Input device not used, the index is set to None.");
                UpdateInEditor = false;
                return;
            }

            InitializeResources();
            m_RequiresReinit = false;
        }

        bool InitializeResources()
        {
            m_IsDropFrame = default;
            m_FrameElapsedDuration = default;
            LastFrameError = InputError.NoError;
            Timestamp = default;

            m_DesiredInPixelFormat = m_SignalOverride == 0 ? BMDPixelFormat.UseBestQuality : m_RequestedInPixelFormat;

            m_SynchronizedAudioBuffer = new NativeArray<float>(
                128 * 1024,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory
            );

            m_Plugin = DeckLinkInputDevicePlugin.Create(m_DeviceIndex, DeviceSelection, 0, m_DesiredInPixelFormat, m_EnablePassThrough, out var selectedFormat);

            if (m_Plugin == null)
            {
                m_Format = default;
                return false;
            }

            // TODO: Improve the 'Error Callback' system, we cannot use the callback during the plugin creation.
            if (!m_Plugin.IsInitialized())
            {
                OnErrorTriggered(StatusType.Error, InputError.DeviceAlreadyUsed, "Can't start input device (possibly already used)");
            }

            using (_ = m_Plugin.LockQueue())
            {
                m_Format = selectedFormat;

                m_Queue?.Dispose();
                m_Queue = new FrameQueue(QueueLength, () => new BufferedFrame(m_Format.Value));
            }

            m_Plugin.ErrorReceived = OnErrorTriggered;
            m_Plugin.VideoFormatChanged = OnVideoFormatChanged;
            m_Plugin.FrameArrived = OnFrameArrived;

            m_Initialized = true;

            return true;
        }

        void DestroyResources()
        {
            if (m_Plugin != null)
            {
                m_Plugin.Dispose();
                m_Plugin = null;
            }

            if (m_SynchronizedAudioBuffer.IsCreated)
            {
                m_SynchronizedAudioBuffer.Dispose();
                m_SynchronizedAudioBuffer = default;
            }

            if (m_SourceTexture != null)
            {
                BlackmagicUtilities.Destroy(m_SourceTexture);
                m_SourceTexture = null;
            }

            DisposeBufferTexture();

            if (m_Queue != null)
            {
                m_Queue.Dispose();
                m_Queue = null;
            }
            if (m_Memcpy != null)
            {
                m_Memcpy.Dispose();
                m_Memcpy = null;
            }

            m_Format = default;
            m_IsDropFrame = default;
            m_FrameElapsedDuration = default;
            LastFrameError = InputError.NoError;
            Timestamp = default;

#if LIVE_CAPTURE_4_0_0_OR_NEWER
            if (m_TimecodeSourceState != null)
            {
                m_TimecodeSourceState.Dispose();
                m_TimecodeSourceState = null;
            }
#endif
        }

        protected override void Cleanup()
        {
            VideoIOFrameManager.Unregister(this);
            m_DeviceIndex = -1;

            DestroyResources();

            InputColorTransform.Reset();
        }

        /// <summary>
        /// Updates the video device and blits the video into the target texture.
        /// </summary>
        public override void PerformUpdate()
        {
            base.PerformUpdate();

            if (!IsActive)
                return;

            using (_ = m_Plugin.LockQueue())
            {
                if (m_Format == null || m_Queue == null)
                {
                    return;
                }

                m_Queue.SetCapacity(QueueLength);

                if (m_Queue.Count == 0)
                {
                    Timestamp = default;
                    return;
                }

                // Generally, we should avoid presenting the oldest video frame in the queue. This allows us to
                // present a frame without forcing the BMD receiving thread to wait until the graphics thread
                // texture update is complete.
                //
                // Otherwise, if we present the oldest queued frame (the next in the queue to be overwritten) we can get the following:
                //
                // Present frame 100 -> GFX thread copies frame -> Receive 104, overwriting 100 -> Frame 100 presented
                // Present frame 101 -> Receive 105, overwriting 101 -> GFX thread copies frame -> Frame 105 presented!!!
                // Present frame 102 -> GFX thread copies frame -> Receive 106, overwriting 102 -> Frame 102 presented
                //
                // Thus, users should be using a queue size of 2 or greater when possible.
                var frame = m_Queue.Count > 1 ? m_Queue[1] : m_Queue.Front();

                Timestamp = frame.timecode;

                // We show the oldest frame in the queue, unless synchronized via live capture.
#if LIVE_CAPTURE_4_0_0_OR_NEWER
                if (m_IsSynchronized)
                    return;
#endif

                PresentFrame(frame, null);
            }

            UnpackTexture();
        }

        void PresentFrame(BufferedFrame frame, long? timeInFrame)
        {
            if (frame.CurrentStatus == BufferedFrame.Status.Queued)
            {
                m_FrameElapsedDuration = 0;

                // Only process the audio if it has not yet been presented to prevent overplaying audio.
                PresentAudio(frame);
            }

            PresentTexture(frame, timeInFrame ?? m_FrameElapsedDuration);

            frame.CurrentStatus = BufferedFrame.Status.Presented;

            // Keep track of how long this latest frame has been shown.
            m_FrameElapsedDuration += (long)(Time.unscaledDeltaTime * BlackmagicUtilities.k_FlicksPerSecond);

            InvokeOnFrameProcessedEvent();
        }

        unsafe void PresentTexture(BufferedFrame frame, long timeInFrame)
        {
            // read the video texture
            UpdateSourceTexture();

            m_Plugin.UpdateTexture(m_SourceTexture, (IntPtr)frame.texture.GetUnsafeReadOnlyPtr());

            // Unpack the video texture (format conversion, chroma upsampling and color space conversion)
            var format = m_Format.Value;

            UpdateBufferTexture(format.width, format.height);

            switch (frame.videoFieldDominance)
            {
                default:
                    m_UnpackPass = 0;
                    break;
                case BMDFieldDominance.LowerFieldFirst:
                    m_UnpackPass = timeInFrame < (frame.frameDuration / 2) ? 1 : 2;
                    break;
                case BMDFieldDominance.UpperFieldFirst:
                    m_UnpackPass = timeInFrame < (frame.frameDuration / 2) ? 2 : 1;
                    break;
            }

            var cs = InColorSpace;
            var tf = m_RequestedTransferFunction != BMDTransferFunction.UseDeviceSignal ? m_RequestedTransferFunction : format.transferFunction;

            m_UnpackMaterial = InputColorTransform.Get(format.pixelFormat, cs, tf, m_WorkingSpaceConversion);
        }

        void UnpackTexture()
        {
            // Blit overrides the active textures, and must be restored.
            var tmp = RenderTexture.active;

            // The blit causes a deadlock when surrounded by the queue lock and the source texture is updated from
            // the native plugin, since the texture update waits to aquire the lock, but the blit does not return
            // until the graphics thread progresses. Thus, this method must not be called while the lock is held
            // by the main thread.
            Graphics.Blit(m_SourceTexture, CaptureRenderTexture, m_UnpackMaterial, m_UnpackPass);

            RenderTexture.active = tmp;

            CaptureRenderTexture.IncrementUpdateCount();
        }

        void PresentAudio(BufferedFrame frame)
        {
            if (frame.audioLength <= 0)
                return;

            // only process audio if there is a callback using it
            if (SynchronizedAudioFrameCallback == null)
                return;

            // convert the audio to floats using burst, this is vectorized
            var sampleCount = 0;

            switch (frame.audioSampleType)
            {
                case BMDAudioSampleType.Int16:
                {
                    sampleCount = frame.audioLength / sizeof(short);
                    AudioUtilities.ConvertToFloats(
                        m_SynchronizedAudioBuffer,
                        frame.audio.Reinterpret<short>(sizeof(byte)),
                        sampleCount
                    );
                    break;
                }
                case BMDAudioSampleType.Int32:
                {
                    sampleCount = frame.audioLength / sizeof(int);
                    AudioUtilities.ConvertToFloats(
                        m_SynchronizedAudioBuffer,
                        frame.audio.Reinterpret<int>(sizeof(byte)),
                        sampleCount
                    );
                    break;
                }
            }

            var audioFrame = new SynchronizedAudioFrame(
                m_SynchronizedAudioBuffer.Slice(0, sampleCount),
                frame.audioChannelCount
            );

            try
            {
                SynchronizedAudioFrameCallback(audioFrame);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        internal void UpdateSourceTexture()
        {
            if (m_Format == null)
                return;

            var format = m_Format.Value;

            // Renew texture objects when the frame dimensions were changed.
            var formatError = true;
            var w = format.byteWidth;
            var h = format.byteHeight;

            const int depthToUse = 4;
            if (format.byteDepth == depthToUse)
            {
                formatError = false;
                Assert.IsTrue(w % depthToUse == 0);
                w /= depthToUse; // 4 bytes per pixel
            }

            if (m_SourceTexture != null && (m_SourceTexture.width != w || m_SourceTexture.height != h))
            {
                BlackmagicUtilities.Destroy(m_SourceTexture);
                m_SourceTexture = null;
            }

            if (!formatError && m_SourceTexture == null)
            {
                m_SourceTexture = new Texture2D(w, h, TextureFormat.RGBA32, false, true)
                {
                    filterMode = FilterMode.Point,
                };
            }
        }

        void UpdateBufferTexture(int width, int height)
        {
            if (CaptureRenderTexture == null || CaptureRenderTexture.width != width || CaptureRenderTexture.height != height)
            {
                DisposeBufferTexture();

                CaptureRenderTexture = new RenderTexture(width, height, 0, GraphicsFormat.R16G16B16A16_SFloat)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = m_FilterMode,
                    hideFlags = HideFlags.DontSave,
                };
            }

            if (CaptureRenderTexture.filterMode != m_FilterMode)
            {
                CaptureRenderTexture.filterMode = m_FilterMode;
            }
        }

        void DisposeBufferTexture()
        {
            if (CaptureRenderTexture != null)
            {
                CaptureRenderTexture.Release();
                BlackmagicUtilities.Destroy(CaptureRenderTexture);
                CaptureRenderTexture = null;
            }
        }

        void OnErrorTriggered(StatusType status, InputError inputError, string message)
        {
            LastFrameError = inputError;
            m_FrameStatus = (message, status);
        }

        void OnVideoFormatChanged(InputVideoFormat format)
        {
            try
            {
                using (_ = m_Plugin.LockQueue())
                {
                    m_Format = format;

                    // clear the frame queue since the allocated frames might not match the new configuration
                    m_Queue?.Dispose();
                    m_Queue = new FrameQueue(QueueLength, () => new BufferedFrame(m_Format.Value));

                    DroppedFrameCount = 0;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void OnFrameArrived(InputVideoFrame videoFrame, InputAudioFrame? audioFrame)
        {
            try
            {
                // add the frame to the queue
                using (_ = m_Plugin.LockQueue())
                {
                    // only receive frames once the queue has been initialized
                    if (m_Queue == null)
                    {
                        return;
                    }

                    if (m_Memcpy == null)
                    {
                        m_Memcpy = new ThreadedMemcpy($"InputDevice {m_DeviceIndex}");
                    }

                    // when overwriting a frame that was never presented, it is counted as dropped
                    if (m_Queue.Enqueue(out var frame) && frame.CurrentStatus == BufferedFrame.Status.Queued)
                    {
                        DroppedFrameCount++;
                    }

                    frame.CopyFrom(videoFrame, audioFrame, m_Memcpy);

                    // the only way to determine if drop frame is used is from this callback, so we must cache this value
                    m_IsDropFrame = frame.timecode.IsDropFrame;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            try
            {
                VideoFrameArrived?.Invoke(videoFrame);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (audioFrame != null)
            {
                try
                {
                    AudioFrameArrived?.Invoke(audioFrame.Value);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <inheritdoc/>
        public void AddVideoFrameCallback(Action<InputVideoFrame> callback)
        {
            VideoFrameArrived += callback;
        }

        /// <inheritdoc/>
        public void RemoveVideoFrameCallback(Action<InputVideoFrame> callback)
        {
            VideoFrameArrived -= callback;
        }

        /// <inheritdoc/>
        public void AddAudioFrameCallback(Action<InputAudioFrame> callback)
        {
            AudioFrameArrived += callback;
        }

        /// <inheritdoc/>
        public void RemoveAudioFrameCallback(Action<InputAudioFrame> callback)
        {
            AudioFrameArrived -= callback;
        }

        /// <inheritdoc/>
        public void AddSynchronizedAudioFrameCallback(Action<SynchronizedAudioFrame> callback)
        {
            SynchronizedAudioFrameCallback += callback;
        }

        /// <inheritdoc/>
        public void RemoveSynchronizedAudioFrameCallback(Action<SynchronizedAudioFrame> callback)
        {
            SynchronizedAudioFrameCallback -= callback;
        }

        /// <inheritdoc/>
        public bool HasInputSource()
        {
            return m_Plugin.HasInputSource();
        }
    }
}
