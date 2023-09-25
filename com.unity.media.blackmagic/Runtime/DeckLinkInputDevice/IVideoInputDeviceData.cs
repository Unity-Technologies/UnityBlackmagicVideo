using System;
using Unity.Collections;

namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// A struct containing the data for a frame of video.
    /// </summary>
    public readonly struct InputVideoFrame
    {
        /// <summary>
        /// The contents of the video frame.
        /// </summary>
        public readonly IntPtr data;

        /// <summary>
        /// The length of the video data in bytes.
        /// </summary>
        public readonly long size;

        /// <summary>
        /// The width of the video frame in pixels.
        /// </summary>
        public readonly int width;

        /// <summary>
        /// The height of the video frame in pixels.
        /// </summary>
        public readonly int height;

        /// <summary>
        /// The pixel format of the video frame.
        /// </summary>
        public readonly BMDPixelFormat pixelFormat;

        /// <summary>
        /// The field dominance of this video frame.
        /// </summary>
        public readonly BMDFieldDominance fieldDominance;

        /// <summary>
        /// The duration of the frame in flicks.
        /// </summary>
        public readonly long frameDuration;

        /// <summary>
        /// The time in flicks when the frame was received by the hardware device.
        /// </summary>
        /// <remarks>
        /// Since this is based on the hardware clock time, it is not expected to increase a consistent amount between frames.
        /// </remarks>
        public readonly long hardwareReferenceTimestamp;

        /// <summary>
        /// The time in flicks of the frame relative to the video stream.
        /// </summary>
        /// <remarks>
        /// This increases a consistent amount every frame, based on the frame rate. If a frame is dropped by the hardware,
        /// it will skip over the dropped frame, so it may be used to detect frame drops.
        /// </remarks>
        public readonly long streamTimestamp;

        /// <summary>
        /// The timecode for the frame.
        /// </summary>
        /// <remarks>
        /// If timecode is not available for this frame, the value will be <see langword="null"/>.
        /// </remarks>
        public readonly Timecode? timecode;

        internal InputVideoFrame(
            IntPtr data,
            long size,
            int width,
            int height,
            BMDPixelFormat pixelFormat,
            BMDFieldDominance fieldDominance,
            long frameDuration,
            long hardwareReferenceTimestamp,
            long streamTimestamp,
            Timecode? timecode
        )
        {
            this.data = data;
            this.size = size;
            this.width = width;
            this.height = height;
            this.pixelFormat = pixelFormat;
            this.fieldDominance = fieldDominance;
            this.frameDuration = frameDuration;
            this.hardwareReferenceTimestamp = hardwareReferenceTimestamp;
            this.streamTimestamp = streamTimestamp;
            this.timecode = timecode;
        }
    }

    /// <summary>
    /// A struct containing the data for a frame of audio.
    /// </summary>
    public readonly struct InputAudioFrame
    {
        /// <summary>
        /// The contents of the audio data.
        /// </summary>
        /// <remarks>
        /// The samples for each channel are interleaved together.
        /// </remarks>
        public readonly IntPtr data;

        /// <summary>
        /// The length of the audio data in bytes.
        /// </summary>
        public readonly long size;

        /// <summary>
        /// The audio samples type.
        /// </summary>
        public readonly BMDAudioSampleType sampleType;

        /// <summary>
        /// The number of audio channels.
        /// </summary>
        public readonly int channelCount;

        /// <summary>
        /// The number of audio samples per channel in the frame.
        /// </summary>
        public readonly int sampleCount;

        /// <summary>
        /// The time in flicks of the video frame corresponding to this audio packet.
        /// </summary>
        public readonly long timestamp;

        internal InputAudioFrame(
            IntPtr data,
            BMDAudioSampleType sampleType,
            int channelCount,
            int sampleCount,
            long timestamp
        )
        {
            this.data = data;
            this.size = sampleCount * channelCount * sampleType.GetBytesPerSample();
            this.sampleType = sampleType;
            this.channelCount = channelCount;
            this.sampleCount = sampleCount;
            this.timestamp = timestamp;
        }
    }

    /// <summary>
    /// A struct containing the data for a frame of audio for playback in the engine.
    /// </summary>
    public readonly struct SynchronizedAudioFrame
    {
        /// <summary>
        /// The audio samples for the frame.
        /// </summary>
        /// <remarks>
        /// The samples for each channel are interleaved together.
        /// </remarks>
        public readonly NativeSlice<float> data;

        /// <summary>
        /// The number of audio channels.
        /// </summary>
        public readonly int channelCount;

        internal SynchronizedAudioFrame(NativeSlice<float> data, int channelCount)
        {
            this.data = data;
            this.channelCount = channelCount;
        }
    }

    interface IVideoInputDeviceData : IVideoDeviceData
    {
        /// <summary>
        /// Adds a callback that receives video frames directly from the device.
        /// </summary>
        /// <param name="callback">The callback function</param>
        void AddVideoFrameCallback(Action<InputVideoFrame> callback);

        /// <summary>
        /// Remove a callback for receiving video frames from the device.
        /// </summary>
        /// <param name="callback">The callback function</param>
        void RemoveVideoFrameCallback(Action<InputVideoFrame> callback);

        /// <summary>
        /// Adds a callback that receives audio frames directly from the device.
        /// </summary>
        /// <param name="callback">The callback to add.</param>
        void AddAudioFrameCallback(Action<InputAudioFrame> callback);

        /// <summary>
        /// Removes a callback that receives audio packets from the device.
        /// </summary>
        /// <param name="callback">The callback to remove.</param>
        void RemoveAudioFrameCallback(Action<InputAudioFrame> callback);

        /// <summary>
        /// Registers a callback which will be triggered each time an audio packet should be played to match the video.
        /// </summary>
        /// <param name="callback">The callback to add.</param>
        void AddSynchronizedAudioFrameCallback(Action<SynchronizedAudioFrame> callback);

        /// <summary>
        /// Removes a callback that receives audio packets from the device.
        /// </summary>
        /// <param name="callback">The callback to remove.</param>
        void RemoveSynchronizedAudioFrameCallback(Action<SynchronizedAudioFrame> callback);

        /// <summary>
        /// Determines if the device has an active video signal or not.
        /// </summary>
        /// <returns>True if it has an active video signal; false otherwise.</returns>
        bool HasInputSource();

        /// <summary>
        /// Retrieves the status of the last input frame.
        /// </summary>
        InputError LastFrameError { get; }
    }
}
