using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine.Scripting;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Unity.Media.Blackmagic
{
    using static BaseDeckLinkDevice;

    /// <summary>
    /// A struct that represents the input format configuration.
    /// </summary>
    readonly struct InputVideoFormat
    {
        /// <summary>
        /// The input configuration.
        /// </summary>
        public readonly VideoMode? mode;

        /// <summary>
        /// The width of the video frame in pixels.
        /// </summary>
        public readonly int width;

        /// <summary>
        /// The height of the video frame in pixels.
        /// </summary>
        public readonly int height;

        /// <summary>
        /// Gets the width of the video texture in bytes.
        /// </summary>
        public readonly int byteWidth;

        /// <summary>
        /// Gets the height of the video texture in bytes.
        /// </summary>
        public readonly int byteHeight;

        /// <summary>
        /// Gets the depth of the video texture in bytes.
        /// </summary>
        public readonly int byteDepth;

        /// <summary>
        /// The numerator of the frame rate in Hz.
        /// </summary>
        public readonly int frameRateNumerator;

        /// <summary>
        /// The denominator of the frame rate in Hz.
        /// </summary>
        public readonly int frameRateDenominator;

        /// <summary>
        /// The duration of frame in flicks.
        /// </summary>
        public readonly long frameDuration;

        /// <summary>
        /// The field dominance of the video.
        /// </summary>
        public readonly BMDFieldDominance fieldDominance;

        /// <summary>
        /// The format of the video texture.
        /// </summary>
        public readonly BMDPixelFormat pixelFormat;

        /// <summary>
        /// The color space of the video texture.
        /// </summary>
        public readonly BMDColorSpace colorSpace;

        /// <summary>
        /// The transfer function of the video texture.
        /// </summary>
        public readonly BMDTransferFunction transferFunction;

        /// <summary>
        /// The name of the input format.
        /// </summary>
        public readonly string formatName;

        /// <summary>
        /// A message describing the format changes.
        /// </summary>
        public readonly string message;

        internal InputVideoFormat(in DeckLinkInputDevicePlugin.InputVideoFormatData data, string message)
        {
            mode = VideoModeRegistry.Instance.GetModeFromSDK(data.mode);

            width = data.width;
            height = data.height;

            pixelFormat = (BMDPixelFormat)data.formatCode;
            byteWidth = pixelFormat.GetByteWidth(width);
            byteHeight = pixelFormat.GetByteHeight(height);
            byteDepth = pixelFormat.GetByteDepth();

            frameRateNumerator = data.frameRateNumerator;
            frameRateDenominator = data.frameRateDenominator;
            frameDuration = data.frameRateNumerator > 0 ? (BlackmagicUtilities.k_FlicksPerSecond * data.frameRateDenominator) / data.frameRateNumerator : 0;

            fieldDominance = (BMDFieldDominance)data.fieldDominance;
            colorSpace = (BMDColorSpace)data.colorSpaceCode;
            transferFunction = (BMDTransferFunction)data.transferFunction;
            formatName = data.formatName;

            this.message = message;
        }
    }

    sealed class DeckLinkInputDevicePlugin : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public readonly struct InputVideoFormatData
        {
            public readonly int deviceIndex;
            public readonly int mode;
            public readonly int width;
            public readonly int height;
            public readonly int frameRateNumerator;
            public readonly int frameRateDenominator;
            public readonly int fieldDominance;
            public readonly int formatCode;
            public readonly int colorSpaceCode;
            public readonly int transferFunction;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public readonly string formatName;
        };

        static readonly Dictionary<int, DeckLinkInputDevicePlugin> s_IndexToPlugin = new Dictionary<int, DeckLinkInputDevicePlugin>();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void FrameErrorCallback(
            int deviceIndex,
            StatusType status,
            InputError error,
            IntPtr message
        );

        [Preserve, MonoPInvokeCallback(typeof(FrameErrorCallback))]
        static void OnFrameError(
            int deviceIndex,
            StatusType status,
            InputError error,
            IntPtr message
        )
        {
            try
            {
                Profiler.BeginSample($"{nameof(DeckLinkInputDevice)}.{nameof(OnFrameError)}()");

                if (s_IndexToPlugin.TryGetValue(deviceIndex, out var plugin))
                {
                    plugin.ErrorReceived?.Invoke(
                        status,
                        error,
                        BlackmagicUtilities.FromUTF8(message)
                    );
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void VideoFormatChangedCallback(InputVideoFormatData format, IntPtr message);

        [Preserve, MonoPInvokeCallback(typeof(VideoFormatChangedCallback))]
        static void OnVideoFormatChanged(InputVideoFormatData format, IntPtr message)
        {
            try
            {
                Profiler.BeginSample($"{nameof(DeckLinkInputDevice)}.{nameof(OnVideoFormatChanged)}()");

                if (s_IndexToPlugin.TryGetValue(format.deviceIndex, out var plugin))
                {
                    plugin.VideoFormatChanged?.Invoke(new InputVideoFormat(
                        format,
                        BlackmagicUtilities.FromUTF8(message))
                    );
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void FrameArrivedCallback(
            int deviceIndex,
            IntPtr videoData,
            long videoDataSize,
            int videoWidth,
            int videoHeight,
            int videoPixelFormat,
            int videoFieldDominace,
            long videoFrameDuration,
            long videoHardwareReferenceTimestamp,
            long videoStreamTimestamp,
            uint videoTimecode,
            IntPtr audioData,
            int audioSampleType,
            int audioChannelCount,
            int audioSampleCount,
            long audioTimestamp
        );

        [Preserve, MonoPInvokeCallback(typeof(FrameArrivedCallback))]
        static void OnFrameArrived(
            int deviceIndex,
            IntPtr videoData,
            long videoDataSize,
            int videoWidth,
            int videoHeight,
            int videoPixelFormat,
            int videoFieldDominace,
            long videoFrameDuration,
            long videoHardwareReferenceTimestamp,
            long videoStreamTimestamp,
            uint videoTimecode,
            IntPtr audioData,
            int audioSampleType,
            int audioChannelCount,
            int audioSampleCount,
            long audioTimestamp
        )
        {
            try
            {
                Profiler.BeginSample($"{nameof(DeckLinkInputDevice)}.{nameof(OnFrameArrived)}()");

                if (s_IndexToPlugin.TryGetValue(deviceIndex, out var plugin))
                {
                    var video = new InputVideoFrame(
                        videoData,
                        videoDataSize,
                        videoWidth,
                        videoHeight,
                        (BMDPixelFormat)videoPixelFormat,
                        (BMDFieldDominance)videoFieldDominace,
                        videoFrameDuration,
                        videoHardwareReferenceTimestamp,
                        videoStreamTimestamp,
                        Timecode.FromBCD(videoFrameDuration, videoTimecode)
                    );

                    var audio = default(InputAudioFrame?);

                    if (audioData != IntPtr.Zero)
                    {
                        audio = new InputAudioFrame(
                            audioData,
                            (BMDAudioSampleType)audioSampleType,
                            audioChannelCount,
                            audioSampleCount,
                            audioTimestamp
                        );
                    }

                    plugin.FrameArrived?.Invoke(video, audio);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        static IntPtr s_TextureUpdateCallback;
        static CommandBuffer s_UpdateTextureCommandBuffer;

        static DeckLinkInputDevicePlugin()
        {
            SetInputFrameErrorCallback(OnFrameError);
            SetVideoFormatChangedCallback(OnVideoFormatChanged);
            SetFrameArrivedCallback(OnFrameArrived);
            s_TextureUpdateCallback = GetTextureUpdateCallback();
        }

        IntPtr m_Device;
        int m_DeviceIndex;


        /// <summary>
        /// Determines if the device is initialized correctly.
        /// </summary>
        /// <returns>True if the device is initialized correctly, false otherwise.</returns>
        public bool IsInitialized() => IsInputDeviceInitialized(m_Device);

        /// <summary>
        /// A callback invoked when the device encounters an error.
        /// </summary>
        public Action<StatusType, InputError, string> ErrorReceived { get; set; }

        /// <summary>
        /// A callback invoked when the source format is changed.
        /// </summary>
        public Action<InputVideoFormat> VideoFormatChanged { get; set; }

        /// <summary>
        /// A callback invoked when a new frame is received.
        /// </summary>
        public Action<InputVideoFrame, InputAudioFrame?> FrameArrived { get; set; }

        // We need to add this offset when a project is used with multiple DeckLink cards.
        // The index exposed in C# is not the same as the one in C++ (BMD API), as in C#, we are only exposing
        // the usable devices.
        static int GetOffsetLogicalDevice(int deviceSelected)
        {
            if (!DeckLinkManager.TryGetInstance(out var deckLinkManager))
                return 0;

            return deckLinkManager.GetOffsetFromDeviceIndex(deviceSelected, VideoDeviceType.Input);
        }

        /// <summary>
        /// Creates an input device.
        /// </summary>
        public static DeckLinkInputDevicePlugin Create(
            int deviceIndex,
            int deviceSelected,
            int format,
            BMDPixelFormat inPixelFormat,
            bool enablePassThrough,
            out InputVideoFormat selectedFormat
        )
        {
            var plugin = new DeckLinkInputDevicePlugin
            {
                m_DeviceIndex = deviceIndex,
            };

            s_IndexToPlugin.Add(deviceIndex, plugin);

            var finalDeviceSelected = GetOffsetLogicalDevice(deviceSelected);

            plugin.m_Device = CreateInputDevice(
                deviceIndex,
                deviceSelected + finalDeviceSelected,
                format,
                (int)inPixelFormat,
                enablePassThrough,
                SystemInfo.graphicsDeviceType,
                out var outFormat);

            selectedFormat = new InputVideoFormat(outFormat, string.Empty);

            return plugin;
        }

        ~DeckLinkInputDevicePlugin()
        {
            if (m_Device != IntPtr.Zero)
            {
                Debug.LogError($"{nameof(DeckLinkInputDevicePlugin)} instance was not disposed before finalization.");
                Dispose();
            }
        }

        /// <summary>
        /// Destroys the InputDevice instance.
        /// </summary>
        public void Dispose()
        {
            if (m_Device != IntPtr.Zero)
            {
                s_IndexToPlugin.Remove(m_DeviceIndex);

                DestroyInputDevice(m_Device);

                m_Device = IntPtr.Zero;
                m_DeviceIndex = -1;
            }
        }

        /// <summary>
        /// Copies raw texture data into a texture using the graphics thread.
        /// </summary>
        /// <param name="texture">The texture to update.</param>
        /// <param name="data">The data to fill the texture with.</param>
        public void UpdateTexture(Texture texture, IntPtr data)
        {
            SetTextureUpdateSource(m_Device, data);

            if (s_UpdateTextureCommandBuffer == null)
            {
                s_UpdateTextureCommandBuffer = new CommandBuffer
                {
                    name = "Update Video Texture",
                };
            }

            var userData = GetInputDeviceID(m_Device);

            s_UpdateTextureCommandBuffer.IssuePluginCustomTextureUpdateV2(s_TextureUpdateCallback, texture, userData);
            Graphics.ExecuteCommandBuffer(s_UpdateTextureCommandBuffer);
            s_UpdateTextureCommandBuffer.Clear();

            texture.IncrementUpdateCount();
        }

        /// <summary>
        /// Determines if the device has an active video signal or not.
        /// </summary>
        /// <returns>True if it has an active video signal; false otherwise.</returns>
        public bool HasInputSource()
        {
            return GetHasInputSource(m_Device);
        }

        public readonly struct QueueLockScope : IDisposable
        {
            readonly DeckLinkInputDevicePlugin m_Plugin;

            public QueueLockScope(DeckLinkInputDevicePlugin plugin)
            {
                m_Plugin = plugin;
                LockInputDeviceQueue(m_Plugin.m_Device);
            }

            public void Dispose()
            {
                UnlockInputDeviceQueue(m_Plugin.m_Device);
            }
        }

        /// <summary>
        /// Waits for and aquires the lock to the device frame queue.
        /// </summary>
        /// <returns>Dispose the lock scope to release the lock. The locking is
        /// done in the native plugin so that plugin events executed from from Unity's
        /// graphics thread may be synchronized.</returns>
        public QueueLockScope LockQueue()
        {
            return new QueueLockScope(this);
        }

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern void SetInputFrameErrorCallback(FrameErrorCallback callback);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern void SetVideoFormatChangedCallback(VideoFormatChangedCallback callback);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern void SetFrameArrivedCallback(FrameArrivedCallback callback);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern IntPtr GetTextureUpdateCallback();

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern IntPtr CreateInputDevice(
            int deviceIndex,
            int deviceSelected,
            int format,
            int pixelFormat,
            bool enablePassThrough,
            GraphicsDeviceType graphicsAPI,
            out InputVideoFormatData selectedFormat);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern void DestroyInputDevice(IntPtr inputDevice);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern bool IsInputDeviceInitialized(IntPtr inputDevice);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern void SetTextureUpdateSource(IntPtr inputDevice, IntPtr data);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern void LockInputDeviceQueue(IntPtr inputDevice);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern void UnlockInputDeviceQueue(IntPtr inputDevice);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern uint GetInputDeviceID(IntPtr inputDevice);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern bool GetHasInputSource(IntPtr inputDevice);
    }
}
