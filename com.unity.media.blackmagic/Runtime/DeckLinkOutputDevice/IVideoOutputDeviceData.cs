using static Unity.Media.Blackmagic.VideoMode;

namespace Unity.Media.Blackmagic
{
    using Resolution = VideoMode.Resolution;
    using FrameRate = VideoMode.FrameRate;

    interface IVideoOutputDeviceData : IVideoDeviceData
    {
        /// <summary>
        /// The plugin uses a Signal Generator device and all the sources are synced to the same video format.
        /// </summary>
        bool IsGenlocked { get; }

        /// <summary>
        /// The plugin uses the GPUDirect technology.
        /// </summary>
        bool IsUsingGPUDirect { get; }

        /// <summary>
        /// The cumulative number of frames displayed late since the last assembly reload.
        /// </summary>
        uint GetLateFrameCount { get; }

        /// <summary>
        /// Determines if the device is outputting audio.
        /// </summary>
        AudioOutputMode IsOutputtingAudio { get; }

        /// <summary>
        /// Determines the source of the timecode used by the output.
        /// </summary>
        OutputTimecodeMode TimecodeMode { get; set; }

        /// <summary>
        /// Changes the video resolution.
        /// </summary>
        /// <param name="resolution">The video resolution.</param>
        /// <returns>True if the configuration has successfully changed, false otherwise.</returns>
        bool ChangeVideoResolution(Resolution resolution);

        /// <summary>
        /// Changes the video frameRate.
        /// </summary>
        /// <param name="frameRate">The video framerate.</param>
        /// <returns>True if the configuration has successfully changed, false otherwise.</returns>
        bool ChangeVideoFrameRate(FrameRate frameRate);

        /// <summary>
        /// Changes the video scanning Mode.
        /// </summary>
        /// <param name="scanMode">The video scan mode.</param>
        /// <returns>True if the configuration has successfully changed, false otherwise.</returns>
        bool ChangeVideoScanMode(ScanMode scanMode);

        /// <summary>
        /// Changes the video device configuration.
        /// </summary>
        /// <param name="resolution">The video resolution.</param>
        /// <param name="frameRate">The video framerate.</param>
        /// <param name="scanMode">The video scan mode.</param>
        /// <returns>True if the configuration has successfully changed, false otherwise.</returns>
        bool ChangeVideoConfiguration(Resolution resolution, FrameRate frameRate, ScanMode scanMode);

        /// <summary>
        /// Changes the video keying mode.
        /// </summary>
        /// <param name="mode">The video keying mode.</param>
        /// <returns>True if the configuration has successfully changed, false otherwise.</returns>
        bool ChangeKeyingMode(KeyingMode mode);

        /// <summary>
        /// Configure automatic and continuous matching of video resolution,
        /// framerate, and scan mode to those of a specific input device.
        /// </summary>
        /// <param name="enabled">Enables or disables the matching.</param>
        /// <param name="inputDevice">The input device to match.</param>
        /// <returns>True if the configuration has successfully changed, false otherwise.</returns>
        bool ChangeSameVideoModeAsInputDevice(bool enabled, DeckLinkInputDevice inputDevice);

        /// <summary>
        /// Overrides the timecode used for output frames.
        /// </summary>
        /// <param name="timecode">The timecode to use in flicks. Set to <see langword="null"/> to stop overriding the output timecode.</param>
        void SetTimecodeOverride(Timecode? timecode);

#if LIVE_CAPTURE_4_0_0_OR_NEWER
        /// <summary>
        /// Overrides the timecode used for output frames.
        /// </summary>
        /// <param name="timecode">The timecode to use. Set to <see langword="null"/> to stop overriding the output timecode.</param>
        void SetTimecodeOverride(Unity.LiveCapture.Timecode? timecode);
#endif
    }
}
