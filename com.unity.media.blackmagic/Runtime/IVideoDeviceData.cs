using UnityEngine;

namespace Unity.Media.Blackmagic
{
    interface IVideoDeviceData
    {
        /// <summary>
        /// The texture containing the blit operations.
        /// </summary>
        RenderTexture TargetTexture { get; }

        /// <summary>
        /// The timecode of the oldest buffered frame.
        /// </summary>
        Timecode Timestamp { get; }

        /// <summary>
        /// The video format used by the current device.
        /// </summary>
        string FormatName { get; }

        /// <summary>
        /// The pixel format used by the current device.
        /// </summary>
        BMDPixelFormat PixelFormat { get; }

        /// <summary>
        /// The color space used by the current device.
        /// </summary>
        BMDColorSpace InColorSpace { get; }

        /// <summary>
        /// The transfer function used by the current device.
        /// </summary>
        BMDTransferFunction TransferFunction { get; }

        /// <summary>
        /// Retrieves the video resolution.
        /// </summary>
        /// <param name="resolution">The video resolution.</param>
        /// <returns>True if the resolution has been successfully retrieved, false otherwise.</returns>
        bool TryGetVideoResolution(out VideoMode.Resolution? resolution);

        /// <summary>
        /// Retrieves the video framerate.
        /// </summary>
        /// <param name="frameRate">The video framerate.</param>
        /// <returns>True if the framerate has been successfully retrieved, false otherwise.</returns>
        bool TryGetVideoFrameRate(out VideoMode.FrameRate? frameRate);

        /// <summary>
        /// Retrieves the video scanning mode.
        /// </summary>
        /// <param name="scanMode">The video scanning mode.</param>
        /// <returns>True if the scanning mode has been successfully retrieved, false otherwise.</returns>
        bool TryGetVideoScanMode(out VideoMode.ScanMode? scanMode);

        /// <summary>
        /// Changes the video pixel format.
        /// </summary>
        /// <param name="pixelFormat">The video pixel format.</param>
        void ChangePixelFormat(BMDPixelFormat pixelFormat);

        /// <summary>
        /// Changes the color space.
        /// </summary>
        /// <param name="colorSpace">The color space.</param>
        void ChangeColorSpace(BMDColorSpace colorSpace);

        /// <summary>
        /// Changes the transfer function.
        /// </summary>
        /// <param name="transferFunction">The transfer function.</param>
        void ChangeTransferFunction(BMDTransferFunction transferFunction);

        /// <summary>
        /// Enables or disables the working space conversion.
        /// </summary>
        /// <param name="apply">Boolean toggle driving the application of the conversion.</param>
        void ApplyWorkingSpaceConversion(bool apply);

        /// <summary>
        /// The frame duration of the display mode in flicks per second.
        /// </summary>
        long FrameDuration { get; }

        /// <summary>
        /// The dimensions of a frame in the display mode.
        /// </summary>
        Vector2Int FrameDimensions { get; }

        /// <summary>
        /// The frame rate of the device in use.
        /// </summary>
        /// <remarks>
        /// The frame rate is represented as the two integer components of a rational number for accuracy.
        /// The actual frame rate can be calculated by timeScale / timeValue.
        /// </remarks>
        /// <param name="numerator">The frame rate value.</param>
        /// <param name="denominator">The frame rate scale.</param>
        /// <returns>True if succeeded; false otherwise.</returns>
        bool TryGetFramerate(out int numerator, out int denominator);

        /// <summary>
        /// The number of frames dropped.
        /// </summary>
        uint DroppedFrameCount { get; }

        /// <summary>
        /// Determines if the device is in use or not.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// The device is active in edit mode.
        /// </summary>
        bool IsUsedInEditMode { get; }
    }
}
