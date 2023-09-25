using System;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
#if HDRP_10_2_OR_NEWER
using UnityEngine.Rendering.HighDefinition;
#endif

namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// The class that contains macros and helper methods used in the Blackmagic package.
    /// </summary>
    static class BlackmagicUtilities
    {
        /// <summary>
        /// Represents the flicks per second value.
        /// </summary>
        public static readonly long k_FlicksPerSecond = 705600000L;

        /// <summary>
        /// The keyword used to enable or disable the Linear color conversion.
        /// </summary>
        public static readonly string k_KeywordLinearColorConversion = "LINEAR_COLOR_CONVERSION";

        /// <summary>
        /// The message used when a video signal cannot be retrieved.
        /// </summary>
        public static readonly string k_SignalVideoNotDefined = "Not defined";

        /// <summary>
        /// The plugin name used for extern functions.
        /// </summary>
        public const string k_PluginName =
#if UNITY_STANDALONE_OSX
            "Blackmagic.dylib";
#else
            "Blackmagic";
#endif

        /// <summary>
        /// Calculates the delta time value in flicks.
        /// </summary>
        public static long DeltaTimeInFlicks => (long)((double)Time.deltaTime * k_FlicksPerSecond);

        /// <summary>
        /// Gets the components of a BCD timecode value.
        /// </summary>
        /// <remarks>
        /// BCD (binary-coded decimal) is a class of binary encodings of decimal numbers where
        /// each digit is represented by a fixed number of bits (usually 4 or 8).
        /// </remarks>
        /// <param name="timecode">The BCD timecode value to decode.</param>
        /// <param name="frameDuration">The actual frame duration in flicks.</param>
        /// <param name="hour">The hour value of the timecode.</param>
        /// <param name="minute">The minute value of the timecode.</param>
        /// <param name="second">The second value of the timecode.</param>
        /// <param name="frame">The frame value of the timecode.</param>
        /// <param name="isDropFrame">Is the timecode specified in drop frame.</param>
        /// <returns><see langword="true"/> if <paramref name="timecode"/> is valid; otherwise, <see langword="false"/>.</returns>
        internal static bool UnpackBcdTimecode(uint timecode, long frameDuration, out int hour, out int minute, out int second, out int frame, out bool isDropFrame)
        {
            if (timecode == 0xffffffffU)
            {
                hour = 0;
                minute = 0;
                second = 0;
                frame = 0;
                isDropFrame = false;
                return false;
            }

            var t = (int)timecode;

            hour = ((t >> 28) & 0x3) * 10 + ((t >> 24) & 0xf);
            minute = ((t >> 20) & 0x7) * 10 + ((t >> 16) & 0xf);
            second = ((t >> 12) & 0x7) * 10 + ((t >> 8) & 0xf);
            frame = ((t >> 4) & 0x3) * 10 + ((t) & 0xf);

            if (frameDuration <= k_FlicksPerSecond / 50)
            {
                var field = ((t >> 7) & 0x1);
                frame = (2 * frame) + field;
            }

            isDropFrame = ((t >> 6) & 0x1) != 0;
            return true;
        }

        /// <summary>
        /// Packs a timecode into a BCD timecode.
        /// </summary>
        /// <param name="frameDuration">The actual frame duration in flicks.</param>
        /// <param name="hour">The hour value of the timecode.</param>
        /// <param name="minute">The minute value of the timecode.</param>
        /// <param name="second">The second value of the timecode.</param>
        /// <param name="frame">The frame value of the timecode.</param>
        /// <param name="isDropFrame">Is the timecode specified in drop frame.</param>
        /// <returns>The timecode represented in BCD.</returns>
        internal static uint PackBcdTimecode(long frameDuration, int hour, int minute, int second, int frame, bool isDropFrame)
        {
            // Divide into fields when using a frame rate over 50Hz.
            var field = 0L;
            if (frameDuration <= k_FlicksPerSecond / 50)
            {
                field = frame & 1;
                frame /= 2;
            }

            // Integer value -> BCD
            var bcd = 0L;
            bcd += (hour / 10) * 0x10000000 + (hour % 10) * 0x01000000;
            bcd += (minute / 10) * 0x00100000 + (minute % 10) * 0x00010000;
            bcd += (second / 10) * 0x00001000 + (second % 10) * 0x00000100;
            bcd += field * 0x00000080;
            bcd += isDropFrame ? 0x00000040 : 0;
            bcd += (frame / 10) * 0x00000010 + (frame % 10) * 0x00000001;

            return (uint)bcd;
        }

#if LIVE_CAPTURE_4_0_0_OR_NEWER
        /// <summary>
        /// Converts a frame time to a time in flicks.
        /// </summary>
        /// <param name="frameTime">The frame time to convert.</param>
        /// <returns>The time in flicks, or zero if the frame rate was invalid.</returns>
        public static Timecode FrameTimeToFlicks(Unity.LiveCapture.FrameTimeWithRate frameTime)
        {
            var frameDuration = (long)(frameTime.Rate.FrameInterval * k_FlicksPerSecond);
            var flicks = (long)(frameTime.ToSeconds() * k_FlicksPerSecond);

            return new Timecode(frameDuration, flicks, frameTime.Rate.IsDropFrame);
        }

        /// <summary>
        /// Converts a timecode to a time in flicks.
        /// </summary>
        /// <param name="timecode">The timecode to convert.</param>
        /// <param name="frameRate">The frame rate of the timecode.</param>
        /// <returns>The time in flicks, or zero if the frame rate was invalid.</returns>
        public static Timecode TimecodeToFlicks(Unity.LiveCapture.Timecode timecode, Unity.LiveCapture.FrameRate frameRate)
        {
            var frameDuration = (long)(frameRate.FrameInterval * k_FlicksPerSecond);
            var flicks = (long)(timecode.ToSeconds(frameRate) * k_FlicksPerSecond);

            return new Timecode(frameDuration, flicks, timecode.IsDropFrame);
        }

        /// <summary>
        /// Converts a time in flicks to a frame time.
        /// </summary>
        /// <param name="flicks">The time to convert.</param>
        /// <param name="frameRate">The frame rate of the frame sequence.</param>
        /// <returns>The frame time.</returns>
        public static Unity.LiveCapture.FrameTimeWithRate FlicksToFrameTime(Timecode flicks, Unity.LiveCapture.FrameRate frameRate)
        {
            var time  = (double)flicks.Flicks * frameRate.Numerator / (frameRate.Denominator * k_FlicksPerSecond);
            var frameTime = Unity.LiveCapture.FrameTime.FromFrameTime(time);
            return new Unity.LiveCapture.FrameTimeWithRate(frameRate, frameTime);
        }

#endif

        /// <summary>
        /// Destroys an object if it is valid.
        /// </summary>
        /// <param name="obj">The object to destroy.</param>
        public static void Destroy(Object obj, bool forceImmediate = false)
        {
            if (obj != null)
            {
#if UNITY_EDITOR
                if (Application.isPlaying && !forceImmediate)
                    Object.Destroy(obj);
                else
                    Object.DestroyImmediate(obj);
#else
                Object.Destroy(obj);
#endif
            }
        }

        /// <summary>
        /// Determines if the project require a linear color conversion or not.
        /// </summary>
        /// <remarks>
        /// It should be true on a HDRP project, and false on a Legacy project.
        /// </remarks>
        /// <returns>True if the project require a linear color; false otherwise.</returns>
        public static bool RequireLinearColorConversion()
        {
            var isGamma = false;
#if UNITY_EDITOR
            isGamma = (UnityEditor.PlayerSettings.colorSpace == ColorSpace.Gamma);
#else
#if UNITY_2019_1_OR_NEWER // We do not support Unity < 2019.1
            if (GraphicsSettings.renderPipelineAsset != null)
            {
                var srpType = GraphicsSettings.renderPipelineAsset.GetType().ToString();
                if (srpType.Contains("HDRenderPipelineAsset"))
                {
#if HDRP_10_2_OR_NEWER
                    isGamma = true;
#else
                    isGamma = false;
#endif
                }
                else if (srpType.Contains("UniversalRenderPipelineAsset") || srpType.Contains("LightweightRenderPipelineAsset"))
                {
                    isGamma = true;
                }
                else
                {
                    isGamma = false;
                }
            }
#endif
#endif
            return isGamma; // Assumed WS is Linear
        }

        /// <summary>
        /// Gets a string from a UTF8 pointer.
        /// </summary>
        /// <param name="utf8">The UTF8 pointer variable.</param>
        /// <returns>The string variable converted.</returns>
        public static string FromUTF8(IntPtr utf8)
        {
            if (utf8 == IntPtr.Zero)
            {
                return null;
            }

            unsafe
            {
                var ptr = (byte*)utf8;
                var len = 0;
                while (ptr[len] != 0)
                    len++;
                return Encoding.UTF8.GetString(ptr, len);
            }
        }

        /// <summary>
        /// Gets the Antialiasing value from a specified Camera.
        /// </summary>
        /// <param name="camera">The camera to retrieve the antialiasing value.</param>
        /// <returns>The camera's antialiasing value.</returns>
        public static int GetAntiAliasingValueFromCamera(Camera camera)
        {
            return (camera.allowMSAA && QualitySettings.antiAliasing > 0)
                ? QualitySettings.antiAliasing
                : 1;
        }

        /// <summary>
        /// Indicates whether or not the color buffer format supports an alpha channel
        /// </summary>
        /// <returns>whether the internal format supports an alpha channel</returns>
        public static bool IsValidColorBufferFormat()
        {
#if HDRP_10_2_OR_NEWER
            var hdrp = (HDRenderPipelineAsset)GraphicsSettings.renderPipelineAsset;
            return !hdrp || hdrp.currentPlatformRenderPipelineSettings.colorBufferFormat ==
                RenderPipelineSettings.ColorBufferFormat.R16G16B16A16;
#else
            return true;
#endif
        }
    }
}
