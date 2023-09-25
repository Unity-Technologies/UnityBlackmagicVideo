using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using System;
using System.Runtime.InteropServices;

using static Unity.Media.Blackmagic.BaseDeckLinkDevice;

namespace Unity.Media.Blackmagic
{
    sealed class DeckLinkOutputDevicePlugin : IDisposable
    {
        const int k_DefaultCapacity = 4;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void FrameErrorCallback(int index, IntPtr message, StatusType status);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void FrameCompletedCallback(int index, long frameNumber);

        /// <summary>
        /// Represents a callback that can be used to retrieve the plugin errors.
        /// </summary>
        internal event FrameErrorCallback OnFrameError;

        internal event FrameCompletedCallback OnFrameCompleted;

        IntPtr m_CurrentDevice;

        internal IntPtr CurrentDevice => m_CurrentDevice;

        // We need to add this offset when a project is used with multiple DeckLink cards.
        // The index exposed in C# is not the same as the one in C++ (BMD API), as in C#, we are only exposing
        // the usable devices.
        static int GetOffsetLogicalDevice(int deviceSelected)
        {
            if (!DeckLinkManager.TryGetInstance(out var deckLinkManager))
                return 0;

            return deckLinkManager.GetOffsetFromDeviceIndex(deviceSelected, VideoDeviceType.Output);
        }

        /// <summary>
        /// Creates a new async static instance of the class.
        /// </summary>
        /// <param name="device">Index of the device selected.</param>
        /// <param name="displayMode">A BMDDisplayMode enum value.</param>
        /// <param name="preroll">Queue length wait for Output Device completion.</param>
        /// <returns> Static instance of the class. </returns>
        public static DeckLinkOutputDevicePlugin CreateAsyncOutputDevice(
            int deviceIndex,
            int deviceSelected,
            int displayMode,
            BMDPixelFormat pixelFormat,
            BMDColorSpace colorSpace,
            BMDTransferFunction transferFunction,
            int preroll,
            bool enableAudio,
            int audioChannelCount,
            int audioSampleRate,
            bool useGPUDirect
        )
        {
            var finalDeviceSelected = GetOffsetLogicalDevice(deviceSelected);

            var intPtr = CreateAsyncOutputDeviceNative(
                deviceIndex,
                deviceSelected + finalDeviceSelected,
                displayMode,
                (int)pixelFormat,
                (int)colorSpace,
                (int)transferFunction,
                preroll,
                enableAudio,
                audioChannelCount,
                audioSampleRate,
                useGPUDirect
            );

            if (intPtr == IntPtr.Zero)
                return null;

            var outputDevice = new DeckLinkOutputDevicePlugin(intPtr, deviceSelected);
            return outputDevice;
        }

        /// <summary>
        /// Creates a new manual static instance of the class.
        /// </summary>
        /// <param name="device">Index of the device selected.</param>
        /// <param name="displayMode">A BMDDisplayMode enum value.</param>
        /// <returns>Static instance of the class.</returns>
        public static DeckLinkOutputDevicePlugin CreateManualOutputDevice(
            int deviceIndex,
            int deviceSelected,
            int displayMode,
            BMDPixelFormat pixelFormat,
            BMDColorSpace colorSpace,
            BMDTransferFunction transferFunction,
            int preroll,
            bool enableAudio,
            int audioChannelCount,
            int audioSampleRate,
            bool useGPUDirect
        )
        {
            var finalDeviceSelected = GetOffsetLogicalDevice(deviceSelected);

            var intPtr = CreateManualOutputDeviceNative(
                deviceIndex,
                deviceSelected + finalDeviceSelected,
                displayMode,
                (int)pixelFormat,
                (int)colorSpace,
                (int)transferFunction,
                preroll,
                enableAudio,
                audioChannelCount,
                audioSampleRate,
                useGPUDirect);

            if (intPtr == IntPtr.Zero)
                return null;

            var outputDevice = new DeckLinkOutputDevicePlugin(intPtr, deviceSelected);
            return outputDevice;
        }

        DeckLinkOutputDevicePlugin(IntPtr plugin, int device)
        {
            m_CurrentDevice = plugin;
        }

        ~DeckLinkOutputDevicePlugin()
        {
            if (m_CurrentDevice != IntPtr.Zero)
                Debug.LogError("OutputDevice instance should be disposed before finalization.");
        }

        /// <summary>
        /// Initialize the plugin low-level callbacks with the managed callbacks.
        /// </summary>
        public void InitializeCallbacks()
        {
            if (OnFrameError != null)
            {
                SetFrameErrorCallback(m_CurrentDevice, Marshal.GetFunctionPointerForDelegate(OnFrameError));
            }
            if (OnFrameCompleted != null)
            {
                SetFrameCompletedCallback(m_CurrentDevice, Marshal.GetFunctionPointerForDelegate(OnFrameCompleted));
            }
        }

        /// <summary>
        /// Determines if the device is initialized correctly.
        /// </summary>
        /// <returns>True if the device is initialized correctly, false otherwise.</returns>
        public bool IsInitialized() => IsOutputDeviceInitialized(m_CurrentDevice);

        /// <summary>
        /// Defines a recovery time for the current output device in use.
        /// </summary>
        /// <param name="defaultTime">The time to recover.</param>
        public void SetDefaultScheduleTime(float defaultTime)
        {
            SetDefaultScheduleTime(m_CurrentDevice, defaultTime);
        }

        /// <summary>
        /// Retrieves the framerate duration and scale.
        /// </summary>
        /// <param name="numerator"> Represents the framerate duration. </param>
        /// <param name="denominator"> Represents the framerate scale. </param>
        public void GetFrameRate(out int numerator, out int denominator)
        {
            GetOutputDeviceFrameRate(m_CurrentDevice, out numerator, out denominator);
        }

        /// <summary>
        /// Removes the static instance resources.
        /// </summary>
        public void Dispose()
        {
            DestroyOutputDevice(m_CurrentDevice);
            m_CurrentDevice = IntPtr.Zero;
        }

        /// <summary>
        /// Dimensions of the Frame (width and height).
        /// </summary>
        public Vector2Int FrameDimensions => new Vector2Int(
            GetOutputDeviceFrameWidth(m_CurrentDevice),
            GetOutputDeviceFrameHeight(m_CurrentDevice)
        );

        /// <summary>
        /// Retrieves the actual pixel format used for the current device.
        /// </summary>
        public string PixelFormat
        {
            get
            {
                var bstr = GetOutputDevicePixelFormat(m_CurrentDevice);
                if (bstr == IntPtr.Zero)
                    return null;
                return BlackmagicUtilities.FromUTF8(bstr);
            }
        }

        /// <summary>
        /// Duration of the frame (flicksPerSecond * frameDuration_ / timeScale_).
        /// </summary>
        public long FrameDuration => GetOutputDeviceFrameDuration(m_CurrentDevice);

        /// <summary>
        /// Determines if the content is in progressive mode. It displays both the even and odd scan lines
        /// (the entire video frame) at the same time, compared to the interlaced mode.
        /// </summary>
        public bool IsProgressive => IsOutputDeviceProgressive(m_CurrentDevice) != 0;

        /// <summary>
        /// Determines if the Output Device is locked or not (GenLock input status)
        /// </summary>
        public bool IsReferenceLocked => IsOutputDeviceReferenceLocked(m_CurrentDevice) != 0;

        /// <summary>
        /// The number of frames dropped.
        /// </summary>
        public uint DroppedFrameCount => CountDroppedOutputDeviceFrames(m_CurrentDevice);

        /// <summary>
        /// The number of frames displayed late.
        /// </summary>
        public uint LateFrameCount => CountLateOutputDeviceFrames(m_CurrentDevice);

        /// <summary>
        /// Queries the initialized plugin and validates its configuration.
        /// </summary>
        /// <returns>A positive value is returned if the card supports the requested configuration, false otherwise.</returns>
        public bool IsValidConfiguration() => IsValidConfiguration(m_CurrentDevice);

        /// <summary>
        /// Determines if the selected keying mode is compatible with the device.
        /// </summary>
        /// <returns>True if compatible, false otherwise.</returns>
        public bool IsKeyingModeCompatible(int keyingMode) => IsOutputKeyingCompatible(m_CurrentDevice, keyingMode);

        /// <summary>
        /// Initializes the selected keying mode.
        /// </summary>
        /// <returns>True if succeeded, false otherwise.</returns>
        public bool InitializeKeyingMode(int keyingMode) => InitializeOutputKeyerParameters(m_CurrentDevice, keyingMode);

        /// <summary>
        /// Changes the selected keying mode.
        /// </summary>
        /// <returns>True if succeeded, false otherwise.</returns>
        public bool ChangeKeyingMode(int keyingMode) => ChangeKeyingMode(m_CurrentDevice, keyingMode);

        /// <summary>
        /// Disables the use of keying.
        /// </summary>
        /// <returns>True if succeeded, false otherwise.</returns>
        public bool DisableKeying() => DisableKeying(m_CurrentDevice);

        /// <summary>
        /// Determines if the selected Link mode is compatible with the device.
        /// </summary>
        /// <param name="mode">The Link mode selected.</param>
        /// <returns>True if compatible, false otherwise.</returns>
        public bool IsLinkCompatible(int mode) => IsOutputLinkCompatible(m_CurrentDevice, mode);

        /// <summary>
        /// Changes the selected Link mode.
        /// </summary>
        /// <param name="mode">The Link mode selected.</param>
        /// <returns>True if succeeded, false otherwise.</returns>
        public bool SetLinkMode(int mode) => SetOutputLinkMode(m_CurrentDevice, mode);

        /// <summary>
        /// Feeds a frame to the output device plugin.
        /// </summary>
        /// <typeparam name="T">The type of the frame.</typeparam>
        /// <param name="data">The actual frame to feed.</param>
        /// <param name="timecode">The timecode value in flicks.</param>
        public unsafe void FeedFrame<T>(NativeArray<T> data, Timecode timecode) where T : struct
        {
            FeedFrameToOutputDevice(m_CurrentDevice, (IntPtr)data.GetUnsafeReadOnlyPtr(), timecode.ToBCD());
        }

        /// <summary>
        /// Blocks the thread to wait for Output Device completion every end-of-frame.
        /// </summary>
        /// <param name="frameNumber"> The frame number to wait for completion. </param>
        public void WaitCompletion(long frameNumber)
        {
            WaitOutputDeviceCompletion(m_CurrentDevice, frameNumber);

            string message;
            if (CheckError(out message))
            {
                Debug.LogWarning(message);
            }
        }

        /// <summary>
        /// Feeds audio samples to the plugin.
        /// </summary>
        /// <param name="samples">The audio samples to feed to the current device.</param>
        /// <param name="sampleCount">The number of samples to feed to the current device.</param>
        public unsafe void FeedAudioSampleFrames(float* samples, int sampleCount)
        {
            if (samples != null && sampleCount > 0)
                FeedAudioSampleFramesToOutputDevice(m_CurrentDevice, samples, sampleCount);
        }

        bool CheckError(out string message)
        {
            message = null;
            if (m_CurrentDevice == IntPtr.Zero)
                return false;

            var error = GetOutputDeviceError(m_CurrentDevice);
            if (error == IntPtr.Zero)
                return false;

            message = Marshal.PtrToStringAnsi(error);
            return !String.IsNullOrEmpty(message);
        }

        /// <summary>
        /// Get the backing frame dimensions for download operation.
        /// </summary>
        /// <remarks>
        /// Queries the plugin the allocation information for the source texture. We need to use the
        /// adjusted dimensions (width and height) for allocation, which varies according to pixel format
        /// component mapping. The depth is also important to be respected, as this will permit us to
        /// unpack using precision float arithmetic.
        /// </remarks>
        /// <param name="w">The width of the texture to instantiate.</param>
        /// <param name="h">The height of the texture to instantiate.</param>
        /// <param name="d">The depth of the texture to instantiate.</param>
        public void GetBackingFrameByteDimensions(out uint w, out uint h, out uint d)
        {
            GetOutputDeviceBackingFrameByteDimensions(m_CurrentDevice, out w, out h, out d);
        }

        [DllImport(BlackmagicUtilities.k_PluginName, EntryPoint = "CreateAsyncOutputDevice")]
        static extern IntPtr CreateAsyncOutputDeviceNative(
            int deviceIndex,
            int deviceSelected,
            int displayMode,
            int pixelFormat,
            int colorSpace,
            int transferFunction,
            int preroll,
            bool enableAudio,
            int audioChannelCount,
            int audioSampleRate,
            bool useGPUDirect
        );

        [DllImport(BlackmagicUtilities.k_PluginName, EntryPoint = "CreateManualOutputDevice")]
        static extern IntPtr CreateManualOutputDeviceNative(
            int deviceIndex,
            int deviceSelected,
            int displayMode,
            int pixelFormat,
            int colorSpace,
            int transferFunction,
            int preroll,
            bool enableAudio,
            int audioChannelCount,
            int audioSampleRate,
            bool useGPUDirect
        );

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern void DestroyOutputDevice(IntPtr outputDevice);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern bool IsOutputDeviceInitialized(IntPtr outputDevice);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern int GetOutputDeviceFrameWidth(IntPtr outputDevice);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern int GetOutputDeviceFrameHeight(IntPtr outputDevice);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern long GetOutputDeviceFrameRate(IntPtr outputDevice, out int numerator, out int denominator);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern IntPtr GetOutputDevicePixelFormat(IntPtr outputDevice);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern long GetOutputDeviceFrameDuration(IntPtr outputDevice);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern int IsOutputDeviceProgressive(IntPtr outputDevice);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern void GetOutputDeviceBackingFrameByteDimensions(IntPtr inputDevice, out uint w, out uint h, out uint d);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern int IsOutputDeviceReferenceLocked(IntPtr outputDevice);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern void FeedFrameToOutputDevice(IntPtr outputDevice, IntPtr frameData, uint timecode);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern void WaitOutputDeviceCompletion(IntPtr outputDevice, long frameNumber);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern uint CountDroppedOutputDeviceFrames(IntPtr outputDevice);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern uint CountLateOutputDeviceFrames(IntPtr outputDevice);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern unsafe void FeedAudioSampleFramesToOutputDevice(IntPtr outputDevice, float* sampleFrames, int sampleCount);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern IntPtr GetOutputDeviceError(IntPtr outputDevice);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern void SetFrameErrorCallback(IntPtr outputDevice, IntPtr handler);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern void SetFrameCompletedCallback(IntPtr outputDevice, IntPtr handler);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern bool IsValidConfiguration(IntPtr outputDevice);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern bool IsOutputKeyingCompatible(IntPtr outputDevice, int keying);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern bool InitializeOutputKeyerParameters(IntPtr outputDevice, int keying);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern bool ChangeKeyingMode(IntPtr outputDevice, int keying);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern bool DisableKeying(IntPtr outputDevice);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern void SetDefaultScheduleTime(IntPtr outputDevice, float defaultTime);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern bool IsOutputLinkCompatible(IntPtr outputDevice, int mode);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern bool SetOutputLinkMode(IntPtr outputDevice, int mode);
    }
}
