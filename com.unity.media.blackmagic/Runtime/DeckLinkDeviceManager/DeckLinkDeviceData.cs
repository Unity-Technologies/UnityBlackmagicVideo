using System;
using UnityEngine;
using static Unity.Media.Blackmagic.VideoMode;

namespace Unity.Media.Blackmagic
{
    using Resolution = VideoMode.Resolution;
    using FrameRate = VideoMode.FrameRate;

    public class LabelOverride : PropertyAttribute
    {
        public string Label;
        public LabelOverride(string label)
        {
            this.Label = label;
        }
    }

    /// <summary>
    /// The class used to access the data of a device.
    /// </summary>
    [Serializable]
    public abstract class VideoDeviceHandle
    {
        /// <summary>
        /// The name of the device.
        /// </summary>
        public string Name;

        /// <summary>
        /// The type of the device.
        /// </summary>
        public abstract VideoDeviceType DeviceType { get; }

        /// <summary>
        /// The name of the device used before it has been disabled.
        /// </summary>
        [SerializeField]
        string m_OldDeviceName = null;

        /// <summary>
        /// The device in use needs to be updated.
        /// </summary>
        [SerializeField]
        bool m_UpdateDevice = false;

        internal string OldDeviceName => m_OldDeviceName;

        internal bool UpdateDevice => m_UpdateDevice;

        /// <summary>
        /// Retrieves the RenderTexture which contains the blit operations.
        /// </summary>
        /// <param name="renderTexture">The RenderTexture to retrieve.</param>
        /// <returns>True if succeeded; false otherwise.</returns>
        public bool TryGetRenderTexture(out RenderTexture renderTexture)
        {
            renderTexture = TryGetVideoDevice<IVideoDeviceData>(Name, out var device)
                ? device.TargetTexture
                : null;
            return renderTexture != null;
        }

        /// <summary>
        /// Retrieves the video frame rate.
        /// </summary>
        /// <param name="numerator">The numerator to retrieve.</param>
        /// <param name="denominator">The denominator to retrieve.</param>
        /// <returns>True if succeeded; false otherwise.</returns>
        public bool TryGetFramerate(out int numerator, out int denominator)
        {
            if (TryGetVideoDevice<IVideoDeviceData>(Name, out var device))
            {
                return device.TryGetFramerate(out numerator, out denominator);
            }

            numerator = 0;
            denominator = 0;
            return false;
        }

        /// <summary>
        /// Determines if the device is in use or not.
        /// </summary>
        /// <returns>True if used; false otherwise.</returns>
        public bool IsActive()
        {
            return TryGetVideoDevice<IVideoDeviceData>(Name, out var device)
                ? device.IsActive
                : false;
        }

        /// <summary>
        /// Retrieves the timestamp of the oldest frame.
        /// </summary>
        /// <returns>The timestamp.</returns>
        public Timecode GetTimestamp() => GetVideoDeviceOrThrowInvalidOperation(Name).Timestamp;

        /// <summary>
        /// Retrieves the video format used by the current device.
        /// </summary>
        /// <returns>The video format name.</returns>
        public string GetFormatName() => GetVideoDeviceOrThrowInvalidOperation(Name).FormatName;

        /// <summary>
        /// Retrieves the pixel format used by the current device.
        /// </summary>
        /// <returns>The pixel format value.</returns>
        public BMDPixelFormat GetPixelFormat() => GetVideoDeviceOrThrowInvalidOperation(Name).PixelFormat;

        /// <summary>
        /// Retrieves the color space used by the current device.
        /// </summary>
        /// <returns>The color space value.</returns>
        public BMDColorSpace GetColorSpace() => GetVideoDeviceOrThrowInvalidOperation(Name).InColorSpace;

        /// <summary>
        /// Retrieves the transfer function used by the current device.
        /// </summary>
        /// <returns>The transfer function value.</returns>
        public BMDTransferFunction GetTransferFunction() => GetVideoDeviceOrThrowInvalidOperation(Name).TransferFunction;

        /// <summary>
        /// Changes the video pixel format.
        /// </summary>
        /// <param name="pixelFormat">The video pixel format.</param>
        public void ChangePixelFormat(BMDPixelFormat pixelFormat) => GetVideoDeviceOrThrowInvalidOperation(Name).ChangePixelFormat(pixelFormat);

        /// <summary>
        /// Changes the color space.
        /// </summary>
        /// <param name="colorSpace">The color space.</param>
        public void ChangeColorSpace(BMDColorSpace colorSpace) => GetVideoDeviceOrThrowInvalidOperation(Name).ChangeColorSpace(colorSpace);

        /// <summary>
        /// Changes the transfer function.
        /// </summary>
        /// <param name="transferFunction">The transfer function.</param>
        public void ChangeTransferFunction(BMDTransferFunction transferFunction) => GetVideoDeviceOrThrowInvalidOperation(Name).ChangeTransferFunction(transferFunction);

        /// <summary>
        /// Enables or disables the working space conversion.
        /// </summary>
        /// <param name="apply">Boolean toggle driving the application of the conversion.</param>
        public void ApplyWorkingSpaceConversion(bool apply) => GetVideoDeviceOrThrowInvalidOperation(Name).ApplyWorkingSpaceConversion(apply);

        /// <summary>
        /// Determines if the device is active in edit mode.
        /// </summary>
        /// <returns>True if used in edit mode; false otherwise.</returns>
        public bool IsUsedInEditMode() => GetVideoDeviceOrThrowInvalidOperation(Name).IsUsedInEditMode;

        /// <summary>
        /// Retrieves the number of dropped frames.
        /// </summary>
        /// <returns>The number of dropped frames.</returns>
        public uint GetDroppedFrameCount() => GetVideoDeviceOrThrowInvalidOperation(Name).DroppedFrameCount;

        /// <summary>
        /// Retrieves the video resolution.
        /// </summary>
        /// <param name="resolution">The resolution to retrieve.</param>
        /// <returns>True if succeeded; false otherwise.</returns>
        public bool TryGetVideoResolution(out Resolution? resolution)
        {
            return GetVideoDeviceOrThrowInvalidOperation(Name).TryGetVideoResolution(out resolution);
        }

        /// <summary>
        /// Retrieves the video frame rate.
        /// </summary>
        /// <param name="framerate">The framerate to retrieve.</param>
        /// <returns>True if succeeded; false otherwise.</returns>
        public bool TryGetVideoFramerate(out FrameRate? framerate)
        {
            return GetVideoDeviceOrThrowInvalidOperation(Name).TryGetVideoFrameRate(out framerate);
        }

        /// <summary>
        /// Retrieves the video scanning mode.
        /// </summary>
        /// <param name="scanMode">The scanMode to retrieve.</param>
        /// <returns>True if succeeded; false otherwise.</returns>
        public bool TryGetVideoScanMode(out ScanMode? scanMode)
        {
            return GetVideoDeviceOrThrowInvalidOperation(Name).TryGetVideoScanMode(out scanMode);
        }

        IVideoDeviceData GetVideoDeviceOrThrowInvalidOperation(string name) => GetVideoDeviceOrThrowInvalidOperation<IVideoDeviceData>(name);

        internal T GetVideoDeviceOrThrowInvalidOperation<T>(string name, VideoDeviceType? deviceType = null) where T : IVideoDeviceData
        {
            if (TryGetVideoDevice(name, out T device, deviceType))
            {
                return device;
            }

            throw new InvalidOperationException("Device is invalid or not active, check the 'IsActive' property before invocation.");
        }

        bool TryGetVideoDevice<T>(string name, out T device, VideoDeviceType? deviceType = null) where T : IVideoDeviceData
        {
            if (!string.IsNullOrEmpty(name) && DeckLinkManager.TryGetInstance(out var videoIOManager))
            {
                if (videoIOManager.GetDeviceDataByName(name, deviceType ?? DeviceType, out var deviceComponent))
                {
                    if (deviceComponent is T castDevice)
                    {
                        device = castDevice;
                        return true;
                    }
                }
            }
            device = default;
            return false;
        }
    }

    /// <summary>
    /// The class used to access the data of an input device.
    /// </summary>
    [Serializable]
    public class InputVideoDeviceHandle : VideoDeviceHandle
    {
        [SerializeField]
        VideoDeviceType m_DeviceType = VideoDeviceType.Input;

        /// <inheritdoc/>
        public override VideoDeviceType DeviceType => m_DeviceType;

        /// <summary>
        /// Registers a callback which will be triggered each time a video frame arrived.
        /// </summary>
        /// <param name="callback">The video callback.</param>
        public void RegisterVideoFrameCallback(Action<InputVideoFrame> callback)
        {
            GetVideoDeviceOrThrowInvalidOperation<IVideoInputDeviceData>(Name).AddVideoFrameCallback(callback);
        }

        /// <summary>
        /// Unregisters the video callback.
        /// </summary>
        /// <param name="callback">The video callback.</param>
        public void UnregisterVideoFrameCallback(Action<InputVideoFrame> callback)
        {
            GetVideoDeviceOrThrowInvalidOperation<IVideoInputDeviceData>(Name).RemoveVideoFrameCallback(callback);
        }

        /// <summary>
        /// Registers a callback which will be triggered each time an audio packet arrived.
        /// </summary>
        /// <param name="callback">The audio callback.</param>
        public void RegisterAudioFrameCallback(Action<InputAudioFrame> callback)
        {
            GetVideoDeviceOrThrowInvalidOperation<IVideoInputDeviceData>(Name).AddAudioFrameCallback(callback);
        }

        /// <summary>
        /// Unregisters the audio callback.
        /// </summary>
        /// <param name="callback">The audio callback.</param>
        public void UnregisterAudioFrameCallback(Action<InputAudioFrame> callback)
        {
            GetVideoDeviceOrThrowInvalidOperation<IVideoInputDeviceData>(Name).RemoveAudioFrameCallback(callback);
        }

        /// <summary>
        /// Registers a callback which will be triggered each time an audio packet should be played to match the video.
        /// </summary>
        /// <param name="callback">The audio callback.</param>
        public void RegisterSynchronizedAudioFrameCallback(Action<SynchronizedAudioFrame> callback)
        {
            GetVideoDeviceOrThrowInvalidOperation<IVideoInputDeviceData>(Name).AddSynchronizedAudioFrameCallback(callback);
        }

        /// <summary>
        /// Unregisters the audio callback.
        /// </summary>
        /// <param name="callback">The audio callback.</param>
        public void UnregisterSynchronizedAudioFrameCallback(Action<SynchronizedAudioFrame> callback)
        {
            GetVideoDeviceOrThrowInvalidOperation<IVideoInputDeviceData>(Name).RemoveSynchronizedAudioFrameCallback(callback);
        }

        /// <summary>
        /// Determines if the device has an active video signal or not.
        /// </summary>
        /// <returns>True if it has an active video signal; false otherwise.</returns>
        public bool HasInputSource()
        {
            return GetVideoDeviceOrThrowInvalidOperation<IVideoInputDeviceData>(Name).HasInputSource();
        }

        /// <summary>
        /// Retrieves the status of the device during the last frame.
        /// </summary>
        /// <param name="status">The status of the device during the last frame.</param>
        /// <param name="message">The error description if an error is detected.</param>
        public InputError GetLastFrameStatus()
        {
            return GetVideoDeviceOrThrowInvalidOperation<IVideoInputDeviceData>(Name).LastFrameError;
        }
    }

    /// <summary>
    /// The class used to access the data of an output device.
    /// </summary>
    [Serializable]
    public class OutputVideoDeviceHandle : VideoDeviceHandle
    {
        [SerializeField]
        VideoDeviceType m_DeviceType = VideoDeviceType.Output;

        /// <inheritdoc/>
        public override VideoDeviceType DeviceType => m_DeviceType;

        /// <summary>
        /// The plugin uses a Signal Generator device and all the sources are synced to the same video format.
        /// </summary>
        /// <returns>True if the plugin device is genlocked, false otherwise.</returns>
        public bool IsGenlocked() => GetVideoDeviceOrThrowInvalidOperation<IVideoOutputDeviceData>(Name).IsGenlocked;

        /// <summary>
        /// The plugin uses the GPUDirect technology.
        /// </summary>
        /// <returns>True if the plugin device is using GPUDirect, false otherwise.</returns>
        public bool IsUsingGPUDirect() => GetVideoDeviceOrThrowInvalidOperation<IVideoOutputDeviceData>(Name).IsUsingGPUDirect;

        /// <summary>
        /// Retrieves the number of frames displayed late.
        /// </summary>
        /// <returns>The number of frames displayed late.</returns>
        public uint GetLateFrameCount() => GetVideoDeviceOrThrowInvalidOperation<IVideoOutputDeviceData>(Name).GetLateFrameCount;

        /// <summary>
        /// Determines if the device is outputting audio.
        /// </summary>
        /// <returns>True if the device is outputting audio, false otherwise.</returns>
        public AudioOutputMode IsOutputtingAudio() => GetVideoDeviceOrThrowInvalidOperation<IVideoOutputDeviceData>(Name).IsOutputtingAudio;

        /// <summary>
        /// Determines the source of the timecode used by the output.
        /// </summary>
        public OutputTimecodeMode TimecodeMode
        {
            get => GetVideoDeviceOrThrowInvalidOperation<IVideoOutputDeviceData>(Name).TimecodeMode;
            set => GetVideoDeviceOrThrowInvalidOperation<IVideoOutputDeviceData>(Name).TimecodeMode = value;
        }

        /// <summary>
        /// Changes the video pixel format.
        /// </summary>
        /// <param name="pixelFormat">The video pixel format.</param>
        public new void ChangePixelFormat(BMDPixelFormat pixelFormat)
        {
            GetVideoDeviceOrThrowInvalidOperation<IVideoOutputDeviceData>(Name).ChangePixelFormat(pixelFormat);
        }

        /// <summary>
        /// Changes the color space.
        /// </summary>
        /// <param name="colorSpace">The color space.</param>
        public new void ChangeColorSpace(BMDColorSpace colorSpace)
        {
            GetVideoDeviceOrThrowInvalidOperation<IVideoOutputDeviceData>(Name).ChangeColorSpace(colorSpace);
        }

        /// <summary>
        /// Changes the transfer function.
        /// </summary>
        /// <param name="transferFunction">The transfer function.</param>
        public new void ChangeTransferFunction(BMDTransferFunction transferFunction)
        {
            GetVideoDeviceOrThrowInvalidOperation<IVideoOutputDeviceData>(Name).ChangeTransferFunction(transferFunction);
        }

        /// <summary>
        /// Enables or disables the working space conversion.
        /// </summary>
        /// <param name="apply">Boolean toggle driving the application of the conversion.</param>
        public new void ApplyWorkingSpaceConversion(bool apply)
        {
            GetVideoDeviceOrThrowInvalidOperation<IVideoOutputDeviceData>(Name).ApplyWorkingSpaceConversion(apply);
        }

        /// <summary>
        /// Changes the video device mode configuration.
        /// </summary>
        /// <param name="resolution">The video resolution.</param>
        /// <param name="frameRate">The video framerate.</param>
        /// <param name="scanMode">The video scan mode.</param>
        /// <returns>True if the configuration has successfully changed, false otherwise.</returns>
        public bool ChangeVideoMode(Resolution resolution, FrameRate frameRate, ScanMode scanMode)
        {
            return GetVideoDeviceOrThrowInvalidOperation<IVideoOutputDeviceData>(Name)
                .ChangeVideoConfiguration(resolution, frameRate, scanMode);
        }

        /// <summary>
        /// Changes the video resolution.
        /// </summary>
        /// <param name="resolution">The video resolution.</param>
        /// <returns>True if the configuration has successfully changed, false otherwise.</returns>
        public bool ChangeVideoResolution(Resolution resolution)
        {
            return GetVideoDeviceOrThrowInvalidOperation<IVideoOutputDeviceData>(Name).ChangeVideoResolution(resolution);
        }

        /// <summary>
        /// Changes the video frameRate.
        /// </summary>
        /// <remarks>
        /// Available combinations are dictated by the physical device, and detected at runtime.
        /// </remarks>
        /// <param name="frameRate">The video framerate.</param>
        /// <returns>True if the configuration has successfully changed, false otherwise.</returns>
        public bool ChangeVideoFrameRate(FrameRate frameRate)
        {
            return GetVideoDeviceOrThrowInvalidOperation<IVideoOutputDeviceData>(Name).ChangeVideoFrameRate(frameRate);
        }

        /// <summary>
        /// Changes the video scanning Mode.
        /// </summary>
        /// <param name="scanMode">The video scan mode.</param>
        /// <returns>True if the configuration has successfully changed, false otherwise.</returns>
        public bool ChangeVideoScanMode(ScanMode scanMode)
        {
            return GetVideoDeviceOrThrowInvalidOperation<IVideoOutputDeviceData>(Name).ChangeVideoScanMode(scanMode);
        }

        /// <summary>
        /// Changes the video keying mode.
        /// </summary>
        /// <param name="keyingMode">The video keying mode.</param>
        /// <returns>True if the configuration has successfully changed, false otherwise.</returns>
        public bool ChangeKeyingMode(KeyingMode keyingMode)
        {
            return GetVideoDeviceOrThrowInvalidOperation<IVideoOutputDeviceData>(Name).ChangeKeyingMode(keyingMode);
        }

        /// <summary>
        /// Configure automatic and continuous matching of video resolution,
        /// framerate, and scan mode to those of a specific input device.
        /// </summary>
        /// <param name="enabled">Enables or disables the matching.</param>
        /// <param name="inputDeviceName">The name of the input device to match.</param>
        /// <returns>True if the configuration has successfully changed, false otherwise.</returns>
        public bool ChangeSameVideoModeAsInputDevice(bool enabled, string inputDeviceName)
        {
            var inputDevice = GetVideoDeviceOrThrowInvalidOperation<DeckLinkInputDevice>(inputDeviceName, VideoDeviceType.Input);
            return GetVideoDeviceOrThrowInvalidOperation<IVideoOutputDeviceData>(Name).ChangeSameVideoModeAsInputDevice(enabled, inputDevice);
        }

        /// <summary>
        /// Overrides the timecode used for output frames.
        /// </summary>
        /// <param name="timecode">The timecode to use in flicks. Set to <see langword="null"/> to stop overriding the output timecode.</param>
        public void SetTimecodeOverride(Timecode? timecode)
        {
            GetVideoDeviceOrThrowInvalidOperation<IVideoOutputDeviceData>(Name).SetTimecodeOverride(timecode);
        }

#if LIVE_CAPTURE_4_0_0_OR_NEWER
        /// <summary>
        /// Overrides the timecode used for output frames.
        /// </summary>
        /// <param name="timecode">The timecode to use. Set to <see langword="null"/> to stop overriding the output timecode.</param>
        public void SetTimecodeOverride(Unity.LiveCapture.Timecode? timecode)
        {
            GetVideoDeviceOrThrowInvalidOperation<IVideoOutputDeviceData>(Name).SetTimecodeOverride(timecode);
        }

#endif
    }
}
