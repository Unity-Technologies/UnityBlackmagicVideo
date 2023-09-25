using System;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// The class to retrieve all available native DeckLink devices and video formats.
    /// </summary>
    public static class DeckLinkDeviceEnumerator
    {
        const int k_MaxLength = 256;

        static readonly int[] s_OutputModes = new int[k_MaxLength];
        static readonly IntPtr[] s_InputDevices = new IntPtr[k_MaxLength];
        static readonly IntPtr[] s_OutputDevices = new IntPtr[k_MaxLength];

        /// <summary>
        /// Retrieves all available native input device names.
        /// </summary>
        /// <returns>An array containing all available input device names.</returns>
        public static string[] GetInputDeviceNames()
        {
            var count = DeckLinkDeviceEnumeratorPlugin.RetrieveInputDeviceNames(s_InputDevices, k_MaxLength);
            var inputNames = new string[count];

            for (var i = 0; i < count; i++)
            {
                inputNames[i] = BlackmagicUtilities.FromUTF8(s_InputDevices[i]);
            }

            return inputNames;
        }

        /// <summary>
        /// Retrieves all available native output device names.
        /// </summary>
        /// <returns>An array containing all available output device names.</returns>
        public static string[] GetOutputDeviceNames()
        {
            var count = DeckLinkDeviceEnumeratorPlugin.RetrieveOutputDeviceNames(s_OutputDevices, k_MaxLength);
            var outputNames = new string[count];

            for (var i = 0; i < count; i++)
            {
                outputNames[i] = BlackmagicUtilities.FromUTF8(s_OutputDevices[i]);
            }

            return outputNames;
        }

        /// <summary>
        /// Scans available output modes on a specified device.
        /// </summary>
        /// <param name="deviceIndex">The index of the device to scan.</param>
        /// <returns>An array of available SDK mode values; null otherwise.</returns>
        public static int[] GetOutputModes(int deviceIndex)
        {
            var count = DeckLinkDeviceEnumeratorPlugin.RetrieveOutputModes(deviceIndex, s_OutputModes, s_OutputModes.Length);
            return s_OutputModes;
        }

        /// <summary>
        /// Changes the mapping connector used on available devices.
        /// </summary>
        /// <param name="halfDuplex">The mapping connector uses the half-duplex profile.</param>
        /// <returns>True if the mapping connector has been successfully changed; false otherwise.</returns>
        internal static bool SetAllDevicesDuplexMode(bool halfDuplex)
        {
            return DeckLinkDeviceEnumeratorPlugin.SetAllDevicesDuplexMode(halfDuplex);
        }
    }

    /// <summary>
    /// Represents a unique combination of video resolution, framerate, and scanning mode
    /// that is supported by the Blackmagic SDK.
    /// </summary>
    public readonly struct VideoMode
    {
        /// <summary>
        /// The available video resolutions.
        /// </summary>
        public enum Resolution
        {
            /// <summary>
            /// The 720 x 486 resolution.
            /// </summary>
            fNTSC,

            /// <summary>
            /// The 720 x 576 resolution.
            /// </summary>
            fPAL,

            /// <summary>
            /// The 1920 x 1080 resolution.
            /// </summary>
            fHD1080,

            /// <summary>
            /// The 1280 x 720 resolution.
            /// </summary>
            fHD720,

            /// <summary>
            /// The 2048 x 1556 resolution.
            /// </summary>
            f2K,

            /// <summary>
            /// The 2048 x 1080 resolution.
            /// </summary>
            f2K_DCI,

            /// <summary>
            /// The 3840 x 2160 resolution.
            /// </summary>
            f2160,

            /// <summary>
            /// The 4096 x 2160 resolution.
            /// </summary>
            f4K_DCI,

            /// <summary>
            /// The 7680 x 4320 resolution.
            /// </summary>
            f4320,

            /// <summary>
            /// The 8192 x 4320 resolution.
            /// </summary>
            f8k_DCI,

            /// <summary>
            /// The 640 x 480 resolution.
            /// </summary>
            f640_x_480,

            /// <summary>
            /// The 800 x 600 resolution.
            /// </summary>
            f800_x_600,

            /// <summary>
            /// The 1440 x 900 resolution.
            /// </summary>
            f1440_x_900,

            /// <summary>
            /// The 1440 x 1080 resolution.
            /// </summary>
            f1440_x_1080,

            /// <summary>
            /// The 1600 x 1200 resolution.
            /// </summary>
            f1600_x_1200,

            /// <summary>
            /// The 1920 x 1200 resolution.
            /// </summary>
            f1920_x_1200,

            /// <summary>
            /// The 1920 x 1440 resolution.
            /// </summary>
            f1920_x_1440,

            /// <summary>
            /// The 2560 x 1440 resolution.
            /// </summary>
            f2560_x_1440,

            /// <summary>
            /// The 2560 x 1600 resolution.
            /// </summary>
            f2560_x_1600,
        }

        /// <summary>
        /// The available video frame rates.
        /// </summary>
        public enum FrameRate
        {
            /// <summary>
            /// The 23.98 frame rate.
            /// </summary>
            f23_98,

            /// <summary>
            /// The 24 frame rate.
            /// </summary>
            f24,

            /// <summary>
            /// The 25 frame rate.
            /// </summary>
            f25,

            /// <summary>
            /// The 29.97 frame rate.
            /// </summary>
            f29_97,

            /// <summary>
            /// The 30 frame rate.
            /// </summary>
            f30,

            /// <summary>
            /// The 47.95 frame rate.
            /// </summary>
            f47_95,

            /// <summary>
            /// The 48 frame rate.
            /// </summary>
            f48,

            /// <summary>
            /// The 50 frame rate.
            /// </summary>
            f50,

            /// <summary>
            /// The 59.94 frame rate.
            /// </summary>
            f59_94,

            /// <summary>
            /// The 60 frame rate.
            /// </summary>
            f60,

            /// <summary>
            /// The 95.90 frame rate.
            /// </summary>
            f95_90,

            /// <summary>
            /// The 96 frame rate.
            /// </summary>
            f96,

            /// <summary>
            /// The 98 frame rate.
            /// </summary>
            f98,

            /// <summary>
            /// The 100 frame rate.
            /// </summary>
            f100,

            /// <summary>
            /// The 119.88 frame rate.
            /// </summary>
            f119_88,

            /// <summary>
            /// The 120 frame rate.
            /// </summary>
            f120,
        }

        /// <summary>
        /// The different scanning methods used in broadcasting.
        /// </summary>
        public enum ScanMode
        {
            /// <summary>
            /// A complete frame containing all scan lines.
            /// </summary>
            Progressive,

            /// <summary>
            /// Half of a frame is transmitted at a time.
            /// </summary>
            Interlaced,
        }

        // Auto-increments, only used during construction
        static int s_CurrentIndex;

        /// <summary>
        /// The resolution of the video.
        /// </summary>
        public readonly Resolution resolution;

        /// <summary>
        /// The frame rate of the video.
        /// </summary>
        public readonly FrameRate frameRate;

        /// <summary>
        /// The scanning mode of the video.
        /// </summary>
        public readonly ScanMode scanMode;

        /// <summary>
        /// The numerical enum value of the BMDDisplayMode equivalent
        /// in the Blackmagic SDK DeckLinkAPI_h.h file.
        /// </summary>
        internal readonly int sdkValue;

        /// <summary>
        /// This video mode's index in the <see cref="VideoModeRegistry.s_Modes"/> list.
        /// </summary>
        internal readonly int index;

        internal VideoMode(Resolution resolution, FrameRate frameRate, ScanMode scanMode, int sdkValue)
        {
            this.resolution = resolution;
            this.frameRate = frameRate;
            this.scanMode = scanMode;
            this.sdkValue = sdkValue;
            index = s_CurrentIndex++;
        }
    }

    /// <summary>
    /// Enum, integer, and string conversions. Helpful shortcuts for UI code.
    /// </summary>
    static class VideoModeExtensions
    {
        /// <summary>
        /// Returns <see cref="VideoMode.index"/>.
        /// </summary>
        public static int AsInt(this VideoMode mode)
        {
            return mode.index;
        }

        /// <summary>
        /// Performs an enum to int cast.
        /// </summary>
        public static int AsInt(this VideoMode.Resolution resolution)
        {
            return (int)resolution;
        }

        /// <summary>
        /// Performs an enum to int cast.
        /// </summary>
        public static int AsInt(this VideoMode.FrameRate frameRate)
        {
            return (int)frameRate;
        }

        /// <summary>
        /// Performs an enum to int cast.
        /// </summary>
        public static int AsInt(this VideoMode.ScanMode scanMode)
        {
            return (int)scanMode;
        }

        /// <summary>
        /// Returns the element in <see cref="VideoModeRegistry.s_Modes"/> at the provided index.
        /// </summary>
        public static VideoMode AsVideoMode(this int value)
        {
            return VideoModeRegistry.s_Modes[value];
        }

        /// <summary>
        /// Performs an int to enum cast.
        /// </summary>
        public static VideoMode.Resolution AsVideoModeResolution(this int value)
        {
            return (VideoMode.Resolution)value;
        }

        /// <summary>
        /// Performs an int to enum cast.
        /// </summary>
        public static VideoMode.FrameRate AsVideoModeFrameRate(this int value)
        {
            return (VideoMode.FrameRate)value;
        }

        /// <summary>
        /// Performs an int to enum cast.
        /// </summary>
        public static VideoMode.ScanMode AsVideoModeScanMode(this int value)
        {
            return (VideoMode.ScanMode)value;
        }

        /// <summary>
        /// Wraps <see cref="VideoModeRegistry.GetModeName(VideoMode)"/>.
        /// </summary>
        public static string VideoModeName(this VideoMode mode)
        {
            return VideoModeRegistry.Instance.GetModeName(mode);
        }

        /// <summary>
        /// Wraps <see cref="VideoModeRegistry.GetResolutionName(VideoMode.Resolution)"/>.
        /// </summary>
        public static string ResolutionName(this VideoMode.Resolution resolution)
        {
            return VideoModeRegistry.Instance.GetResolutionName(resolution);
        }

        /// <summary>
        /// Wraps <see cref="VideoModeRegistry.GetFrameRateName(VideoMode.FrameRate)"/>.
        /// </summary>
        public static string FrameRateName(this VideoMode.FrameRate frameRate)
        {
            return VideoModeRegistry.Instance.GetFrameRateName(frameRate);
        }

        /// <summary>
        /// Wraps <see cref="VideoModeRegistry.GetScanModeName(VideoMode.ScanMode)"/>.
        /// </summary>
        public static string ScanModeName(this VideoMode.ScanMode scanMode)
        {
            return VideoModeRegistry.Instance.GetScanModeName(scanMode);
        }

        /// <summary>
        /// Wraps <see cref="VideoModeRegistry.GetModeFromSDK(int)"/>.
        /// </summary>
        public static VideoMode? SDKValueToVideoMode(this int value)
        {
            return VideoModeRegistry.Instance.GetModeFromSDK(value);
        }
    }

    /// <summary>
    /// Singleton class for handling video modes and querying baked-in video mode support.
    /// </summary>
    class VideoModeRegistry
    {
        /// <summary>
        /// Using an internal data structure, performs queries such as:
        /// * Is resolution supported?
        /// * Get supported framerates for a specific resolution?
        /// </summary>
        public class SupportMap
        {
            readonly int m_NumResolutions;
            readonly int m_NumFrameRates;
            readonly int m_NumScanModes;
            int m_NumRegisteredModes;
            int[] m_ResolutionBitFields;
            int[][] m_ScanModeBitFields;

            /// <summary>
            /// Performs fixed-size memory allocation up front.
            /// </summary>
            /// <remarks>
            /// For zero subsequent allocations populate/recycle the instance using
            /// <see cref="SupportMap.LoadModes(IEnumerable{VideoMode})"/> or
            /// <see cref="SupportMap.LoadSDKModeValues(IEnumerable{int})"/>.
            /// </remarks>
            public SupportMap()
            {
                m_NumResolutions = Enum.GetValues(typeof(VideoMode.Resolution)).Length;
                m_NumFrameRates = Enum.GetValues(typeof(VideoMode.FrameRate)).Length;
                m_NumScanModes = Enum.GetValues(typeof(VideoMode.ScanMode)).Length;
                m_ResolutionBitFields = new int[m_NumResolutions];

                m_ScanModeBitFields = new int[m_NumScanModes][];
                for (var i = 0; i < m_ScanModeBitFields.Length; i++)
                    m_ScanModeBitFields[i] = new int[m_NumResolutions];
            }

            /// <summary>
            /// Clears and then populates the internal data structures for the provided video modes.
            /// </summary>
            public void LoadModes(IEnumerable<VideoMode> modes)
            {
                Clear();

                foreach (var mode in modes)
                    RegisterMode(mode);
            }

            /// <summary>
            /// Clears and then populates the internal data structures for the provided video modes.
            /// </summary>
            /// <remarks>
            /// Can be used in conjunction with <see cref="DeckLinkDeviceEnumerator.GetOutputModes(int)"/>
            /// to populate for a specific device's supported video modes.
            /// </remarks>
            public void LoadSDKModeValues(IEnumerable<int> SDKModeValues)
            {
                Clear();

                foreach (var SDKModeValue in SDKModeValues)
                {
                    var mode = SDKModeValue.SDKValueToVideoMode();
                    if (mode.HasValue)
                        RegisterMode(mode.Value);
                }
            }

            void RegisterMode(VideoMode mode)
            {
                var resolutionIndex = mode.resolution.AsInt();
                var resolutionBitField = m_ResolutionBitFields[resolutionIndex];
                m_ResolutionBitFields[resolutionIndex] = BitFieldSet(resolutionBitField, mode.frameRate.AsInt());

                var scanIndex = mode.scanMode.AsInt();
                var scanBitField = m_ScanModeBitFields[scanIndex][resolutionIndex];
                m_ScanModeBitFields[scanIndex][resolutionIndex] = BitFieldSet(scanBitField, mode.frameRate.AsInt());

                m_NumRegisteredModes++;
            }

            /// <summary>
            /// Clears the internal data structures.
            /// </summary>
            public void Clear()
            {
                Array.Clear(m_ResolutionBitFields, 0, m_NumResolutions);
                m_NumRegisteredModes = 0;
            }

            public bool IsEmpty()
            {
                return m_NumRegisteredModes == 0;
            }

            /// <summary>
            /// Checks if the provided resolution is supported.
            /// </summary>
            public bool IsSupported(VideoMode.Resolution resolution)
            {
                var bitField = m_ResolutionBitFields[resolution.AsInt()];
                return bitField != 0;
            }

            /// <summary>
            /// Checks if the provided resolution supports the provided framerate.
            /// </summary>
            public bool IsSupported(VideoMode.Resolution resolution, VideoMode.FrameRate frameRate)
            {
                var bitField = m_ResolutionBitFields[resolution.AsInt()];
                return BitFieldGet(bitField, frameRate.AsInt());
            }

            /// <summary>
            /// Checks if the provided resolution and framerate combination supports the provided scanning mode.
            /// </summary>
            public bool IsSupported(VideoMode.Resolution resolution, VideoMode.FrameRate frameRate, VideoMode.ScanMode scanMode)
            {
                var bitField = m_ScanModeBitFields[scanMode.AsInt()][resolution.AsInt()];
                return BitFieldGet(bitField, frameRate.AsInt());
            }

            /// <summary>
            /// Retrieves the list of supported framerates for the provided resolution.
            /// </summary>
            /// <param name="resolution">The provided resolution.</param>
            /// <param name="frameRates">The supported framerates.</param>
            public void GetFrameRates(VideoMode.Resolution resolution, IList<VideoMode.FrameRate> frameRates)
            {
                frameRates.Clear();

                var bitField = m_ResolutionBitFields[resolution.AsInt()];
                if (bitField == 0)
                    return;

                for (var i = 0; i < m_NumFrameRates; i++)
                {
                    if (BitFieldGet(bitField, i))
                    {
                        var frameRate = i.AsVideoModeFrameRate();
                        frameRates.Add(frameRate);
                    }
                }
            }

            /// <summary>
            /// Retrieves the list of supported scanning mode for the provided resolution and framerate combination.
            /// </summary>
            /// <param name="resolution">The provided resolution.</param>
            /// <param name="frameRate">The provided framerate.</param>
            /// <param name="scanModes">The supported scanning modes.</param>
            public void GetScanModes(VideoMode.Resolution resolution, VideoMode.FrameRate frameRate, IList<VideoMode.ScanMode> scanModes)
            {
                scanModes.Clear();

                var resolutionBitField = m_ResolutionBitFields[resolution.AsInt()];
                if (resolutionBitField == 0)
                    return;

                for (var i = 0; i < m_NumScanModes; i++)
                {
                    var scanBitField = m_ScanModeBitFields[i][resolution.AsInt()];

                    if (BitFieldGet(scanBitField, frameRate.AsInt()))
                    {
                        var scanMode = i.AsVideoModeScanMode();
                        scanModes.Add(scanMode);
                    }
                }
            }

            static bool BitFieldGet(int bitField, int position)
            {
                var flag = 1 << position;
                return (bitField & flag) > 0;
            }

            static int BitFieldSet(int bitField, int position)
            {
                var flag = 1 << position;
                return bitField | flag;
            }
        }

        readonly string[] m_ModeNames;
        readonly string[] m_ResolutionNames;
        readonly string[] m_FrameRateNames;
        readonly string[] m_ScanModeNames;

        readonly SupportMap m_Support;
        readonly Dictionary<int, VideoMode> m_SDKToMode;
        readonly Dictionary<(VideoMode.Resolution, VideoMode.FrameRate), VideoMode>[] m_Combination;

        static VideoModeRegistry s_Instance;

        public static VideoModeRegistry Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = new VideoModeRegistry();
                }
                return s_Instance;
            }
        }

        /// <summary>
        /// The global set of resolution, framerate, and scanning mode combinations supported by the Blackmagic SDK.
        /// </summary>
        /// <remarks>
        /// See <see cref="SupportMap.LoadSDKModeValues(IEnumerable{int})"/>
        /// for obtaining the supported set of a specific device.
        /// </remarks>
        public SupportMap Support => m_Support;

        VideoModeRegistry()
        {
            var enumResolutions = Enum.GetValues(typeof(VideoMode.Resolution));
            m_ResolutionNames = new string[enumResolutions.Length];

            for (var i = 0; i < enumResolutions.Length; i++)
            {
                var resolutionName = Enum.GetName(typeof(VideoMode.Resolution), enumResolutions.GetValue(i));
                resolutionName = resolutionName.Substring(1);
                resolutionName = resolutionName.Replace('_', ' ');

                m_ResolutionNames[i] = resolutionName;
            }

            var enumFrameRates = Enum.GetValues(typeof(VideoMode.FrameRate));
            m_FrameRateNames = new string[enumFrameRates.Length];

            for (var i = 0; i < enumFrameRates.Length; i++)
            {
                var frameRateName = Enum.GetName(typeof(VideoMode.FrameRate), enumFrameRates.GetValue(i));
                frameRateName = frameRateName.Substring(1);
                frameRateName = frameRateName.Replace('_', '.');

                m_FrameRateNames[i] = frameRateName;
            }

            m_ScanModeNames = Enum.GetNames(typeof(VideoMode.ScanMode));

            Assert.IsTrue(s_Modes.Length <= 256, "Too many modes for DeckLinkDeviceEnumerator.GetOutputModes buffer");
            Assert.IsTrue(enumFrameRates.Length <= 31, "Too many framerates for VideoModeRegistry.SupportMap bitfields");

            m_ModeNames = new string[s_Modes.Length];
            m_Support = new SupportMap();
            m_SDKToMode = new Dictionary<int, VideoMode>();

            m_Combination = new Dictionary<(VideoMode.Resolution, VideoMode.FrameRate), VideoMode>[m_ScanModeNames.Length];
            for (var i = 0; i < m_Combination.Length; i++)
                m_Combination[i] = new Dictionary<(VideoMode.Resolution, VideoMode.FrameRate), VideoMode>();

            foreach (var mode in s_Modes)
            {
                var resolutionName = GetResolutionName(mode.resolution);
                var scanModeShortName = GetScanModeName(mode.scanMode).Substring(0, 1).ToLower();
                var frameRateName = GetFrameRateName(mode.frameRate);

                // For DCI
                if (resolutionName[resolutionName.Length - 1] == 'I')
                    scanModeShortName = string.Empty;

                var name = resolutionName + scanModeShortName + frameRateName;
                m_ModeNames[mode.index] = name;

                m_SDKToMode.Add(mode.sdkValue, mode);
                m_Combination[mode.scanMode.AsInt()].Add((mode.resolution, mode.frameRate), mode);
            }

            m_Support.LoadModes(s_Modes);

            Assert.IsTrue(s_Modes.Length == m_SDKToMode.Count, "Duplicate SDK value VideoMode definitions");

            var sum = 0;
            foreach (var supportMap in m_Combination)
                sum += supportMap.Count;
            Assert.IsTrue(s_Modes.Length == sum, "Duplicate resolution + framerate + scanmode VideoMode definitions");
        }

        /// <summary>
        /// Performs a dictionary lookup for the VideoMode matching the provided SDK value.
        /// </summary>
        /// <returns>
        /// The value is unset if no match is found.
        /// </returns>
        /// <seealso cref="VideoMode.sdkValue"/>
        public VideoMode? GetModeFromSDK(int SDKModeValue)
        {
            // Handle BMDDisplayMode::bmdModeUnknown value
            if (SDKModeValue == s_Modes.Length)
                return null;

            if (!m_SDKToMode.TryGetValue(SDKModeValue, out var mode))
                return null;
            return mode;
        }

        /// <summary>
        /// Performs a dictionary lookup for the VideoMode matching the provided components.
        /// </summary>
        /// <returns>
        /// The value is unset if no match is found.
        /// </returns>
        public VideoMode? GetMode(VideoMode.Resolution resolution, VideoMode.FrameRate frameRate, VideoMode.ScanMode scanMode)
        {
            if (!m_Combination[scanMode.AsInt()].TryGetValue((resolution, frameRate), out var mode))
                return null;
            return mode;
        }

        public string GetModeName(VideoMode mode)
        {
            var i = mode.index;
            return m_ModeNames[i];
        }

        public string GetResolutionName(VideoMode.Resolution resolution)
        {
            var i = resolution.AsInt();
            return m_ResolutionNames[i];
        }

        public string GetFrameRateName(VideoMode.FrameRate frameRate)
        {
            var i = frameRate.AsInt();
            return m_FrameRateNames[i];
        }

        public string GetScanModeName(VideoMode.ScanMode scanMode)
        {
            var i = scanMode.AsInt();
            return m_ScanModeNames[i];
        }

        public IReadOnlyCollection<string> GetAllResolutionNames()
        {
            return Array.AsReadOnly(m_ResolutionNames);
        }

        /// <summary>
        /// Mirrors the identifier names of enum BMDDisplayMode in the Blackmagic SDK DeckLinkAPI_h.h file.
        /// </summary>
        /// <remarks>
        /// For indexing to/from this list, see
        /// <see cref="VideoMode.index"/>,
        /// <see cref="VideoModeExtensions.AsVideoMode(int)"/>,
        /// <see cref="VideoModeExtensions.AsInt(VideoMode)"/>.
        /// </remarks>
        public static readonly VideoMode[] s_Modes =
        {
            new VideoMode(VideoMode.Resolution.fNTSC,       VideoMode.FrameRate.f59_94,     VideoMode.ScanMode.Interlaced,  0x6e747363),
            new VideoMode(VideoMode.Resolution.fNTSC,       VideoMode.FrameRate.f23_98,     VideoMode.ScanMode.Interlaced,  0x6e743233),
            new VideoMode(VideoMode.Resolution.fPAL,        VideoMode.FrameRate.f50,        VideoMode.ScanMode.Interlaced,  0x70616c20),
            new VideoMode(VideoMode.Resolution.fNTSC,       VideoMode.FrameRate.f59_94,     VideoMode.ScanMode.Progressive, 0x6e747370),
            new VideoMode(VideoMode.Resolution.fPAL,        VideoMode.FrameRate.f50,        VideoMode.ScanMode.Progressive, 0x70616c70),

            new VideoMode(VideoMode.Resolution.fHD1080,     VideoMode.FrameRate.f23_98,     VideoMode.ScanMode.Progressive, 0x32337073),
            new VideoMode(VideoMode.Resolution.fHD1080,     VideoMode.FrameRate.f24,        VideoMode.ScanMode.Progressive, 0x32347073),
            new VideoMode(VideoMode.Resolution.fHD1080,     VideoMode.FrameRate.f25,        VideoMode.ScanMode.Progressive, 0x48703235),
            new VideoMode(VideoMode.Resolution.fHD1080,     VideoMode.FrameRate.f29_97,     VideoMode.ScanMode.Progressive, 0x48703239),
            new VideoMode(VideoMode.Resolution.fHD1080,     VideoMode.FrameRate.f30,        VideoMode.ScanMode.Progressive, 0x48703330),
            new VideoMode(VideoMode.Resolution.fHD1080,     VideoMode.FrameRate.f47_95,     VideoMode.ScanMode.Progressive, 0x48703437),
            new VideoMode(VideoMode.Resolution.fHD1080,     VideoMode.FrameRate.f48,        VideoMode.ScanMode.Progressive, 0x48703438),
            new VideoMode(VideoMode.Resolution.fHD1080,     VideoMode.FrameRate.f50,        VideoMode.ScanMode.Progressive, 0x48703530),
            new VideoMode(VideoMode.Resolution.fHD1080,     VideoMode.FrameRate.f59_94,     VideoMode.ScanMode.Progressive, 0x48703539),
            new VideoMode(VideoMode.Resolution.fHD1080,     VideoMode.FrameRate.f60,        VideoMode.ScanMode.Progressive, 0x48703630),
            new VideoMode(VideoMode.Resolution.fHD1080,     VideoMode.FrameRate.f95_90,     VideoMode.ScanMode.Progressive, 0x48703936),
            new VideoMode(VideoMode.Resolution.fHD1080,     VideoMode.FrameRate.f96,        VideoMode.ScanMode.Progressive, 0x48703130),
            new VideoMode(VideoMode.Resolution.fHD1080,     VideoMode.FrameRate.f119_88,    VideoMode.ScanMode.Progressive, 0x48703131),
            new VideoMode(VideoMode.Resolution.fHD1080,     VideoMode.FrameRate.f120,       VideoMode.ScanMode.Progressive, 0x48703132),

            new VideoMode(VideoMode.Resolution.fHD1080,     VideoMode.FrameRate.f50,        VideoMode.ScanMode.Interlaced,  0x48693530),
            new VideoMode(VideoMode.Resolution.fHD1080,     VideoMode.FrameRate.f59_94,     VideoMode.ScanMode.Interlaced,  0x48693539),
            new VideoMode(VideoMode.Resolution.fHD1080,     VideoMode.FrameRate.f60,        VideoMode.ScanMode.Interlaced,  0x48693630),

            new VideoMode(VideoMode.Resolution.fHD720,      VideoMode.FrameRate.f50,        VideoMode.ScanMode.Progressive,  0x68703530),
            new VideoMode(VideoMode.Resolution.fHD720,      VideoMode.FrameRate.f59_94,     VideoMode.ScanMode.Progressive,  0x68703539),
            new VideoMode(VideoMode.Resolution.fHD720,      VideoMode.FrameRate.f60,        VideoMode.ScanMode.Progressive,  0x68703630),

            new VideoMode(VideoMode.Resolution.f2K,         VideoMode.FrameRate.f23_98,     VideoMode.ScanMode.Progressive,  0x326b3233),
            new VideoMode(VideoMode.Resolution.f2K,         VideoMode.FrameRate.f24,        VideoMode.ScanMode.Progressive,  0x326b3234),
            new VideoMode(VideoMode.Resolution.f2K,         VideoMode.FrameRate.f25,        VideoMode.ScanMode.Progressive,  0x326b3235),

            new VideoMode(VideoMode.Resolution.f2K_DCI,     VideoMode.FrameRate.f23_98,     VideoMode.ScanMode.Progressive,  0x32643233),
            new VideoMode(VideoMode.Resolution.f2K_DCI,     VideoMode.FrameRate.f24,        VideoMode.ScanMode.Progressive,  0x32643234),
            new VideoMode(VideoMode.Resolution.f2K_DCI,     VideoMode.FrameRate.f25,        VideoMode.ScanMode.Progressive,  0x32643235),
            new VideoMode(VideoMode.Resolution.f2K_DCI,     VideoMode.FrameRate.f29_97,     VideoMode.ScanMode.Progressive,  0x32643239),
            new VideoMode(VideoMode.Resolution.f2K_DCI,     VideoMode.FrameRate.f30,        VideoMode.ScanMode.Progressive,  0x32643330),
            new VideoMode(VideoMode.Resolution.f2K_DCI,     VideoMode.FrameRate.f47_95,     VideoMode.ScanMode.Progressive,  0x32643437),
            new VideoMode(VideoMode.Resolution.f2K_DCI,     VideoMode.FrameRate.f48,        VideoMode.ScanMode.Progressive,  0x32643438),
            new VideoMode(VideoMode.Resolution.f2K_DCI,     VideoMode.FrameRate.f50,        VideoMode.ScanMode.Progressive,  0x32643530),
            new VideoMode(VideoMode.Resolution.f2K_DCI,     VideoMode.FrameRate.f59_94,     VideoMode.ScanMode.Progressive,  0x32643539),
            new VideoMode(VideoMode.Resolution.f2K_DCI,     VideoMode.FrameRate.f60,        VideoMode.ScanMode.Progressive,  0x32643630),
            new VideoMode(VideoMode.Resolution.f2K_DCI,     VideoMode.FrameRate.f95_90,     VideoMode.ScanMode.Progressive,  0x32643935),
            new VideoMode(VideoMode.Resolution.f2K_DCI,     VideoMode.FrameRate.f96,        VideoMode.ScanMode.Progressive,  0x32643936),
            new VideoMode(VideoMode.Resolution.f2K_DCI,     VideoMode.FrameRate.f100,       VideoMode.ScanMode.Progressive,  0x32643130),
            new VideoMode(VideoMode.Resolution.f2K_DCI,     VideoMode.FrameRate.f119_88,    VideoMode.ScanMode.Progressive,  0x32643131),
            new VideoMode(VideoMode.Resolution.f2K_DCI,     VideoMode.FrameRate.f120,       VideoMode.ScanMode.Progressive,  0x32643132),

            new VideoMode(VideoMode.Resolution.f2160,       VideoMode.FrameRate.f23_98,     VideoMode.ScanMode.Progressive,  0x346b3233),
            new VideoMode(VideoMode.Resolution.f2160,       VideoMode.FrameRate.f24,        VideoMode.ScanMode.Progressive,  0x346b3234),
            new VideoMode(VideoMode.Resolution.f2160,       VideoMode.FrameRate.f25,        VideoMode.ScanMode.Progressive,  0x346b3235),
            new VideoMode(VideoMode.Resolution.f2160,       VideoMode.FrameRate.f29_97,     VideoMode.ScanMode.Progressive,  0x346b3239),
            new VideoMode(VideoMode.Resolution.f2160,       VideoMode.FrameRate.f30,        VideoMode.ScanMode.Progressive,  0x346b3330),
            new VideoMode(VideoMode.Resolution.f2160,       VideoMode.FrameRate.f47_95,     VideoMode.ScanMode.Progressive,  0x346b3437),
            new VideoMode(VideoMode.Resolution.f2160,       VideoMode.FrameRate.f48,        VideoMode.ScanMode.Progressive,  0x346b3438),
            new VideoMode(VideoMode.Resolution.f2160,       VideoMode.FrameRate.f50,        VideoMode.ScanMode.Progressive,  0x346b3530),
            new VideoMode(VideoMode.Resolution.f2160,       VideoMode.FrameRate.f59_94,     VideoMode.ScanMode.Progressive,  0x346b3539),
            new VideoMode(VideoMode.Resolution.f2160,       VideoMode.FrameRate.f60,        VideoMode.ScanMode.Progressive,  0x346b3630),
            new VideoMode(VideoMode.Resolution.f2160,       VideoMode.FrameRate.f95_90,     VideoMode.ScanMode.Progressive,  0x346b3935),
            new VideoMode(VideoMode.Resolution.f2160,       VideoMode.FrameRate.f96,        VideoMode.ScanMode.Progressive,  0x346b3936),
            new VideoMode(VideoMode.Resolution.f2160,       VideoMode.FrameRate.f100,       VideoMode.ScanMode.Progressive,  0x346b3130),
            new VideoMode(VideoMode.Resolution.f2160,       VideoMode.FrameRate.f119_88,    VideoMode.ScanMode.Progressive,  0x346b3131),
            new VideoMode(VideoMode.Resolution.f2160,       VideoMode.FrameRate.f120,       VideoMode.ScanMode.Progressive,  0x346b3132),

            new VideoMode(VideoMode.Resolution.f4K_DCI,     VideoMode.FrameRate.f23_98,     VideoMode.ScanMode.Progressive,  0x34643233),
            new VideoMode(VideoMode.Resolution.f4K_DCI,     VideoMode.FrameRate.f24,        VideoMode.ScanMode.Progressive,  0x34643234),
            new VideoMode(VideoMode.Resolution.f4K_DCI,     VideoMode.FrameRate.f25,        VideoMode.ScanMode.Progressive,  0x34643235),
            new VideoMode(VideoMode.Resolution.f4K_DCI,     VideoMode.FrameRate.f29_97,     VideoMode.ScanMode.Progressive,  0x34643239),
            new VideoMode(VideoMode.Resolution.f4K_DCI,     VideoMode.FrameRate.f30,        VideoMode.ScanMode.Progressive,  0x34643330),
            new VideoMode(VideoMode.Resolution.f4K_DCI,     VideoMode.FrameRate.f47_95,     VideoMode.ScanMode.Progressive,  0x34643437),
            new VideoMode(VideoMode.Resolution.f4K_DCI,     VideoMode.FrameRate.f48,        VideoMode.ScanMode.Progressive,  0x34643438),
            new VideoMode(VideoMode.Resolution.f4K_DCI,     VideoMode.FrameRate.f50,        VideoMode.ScanMode.Progressive,  0x34643530),
            new VideoMode(VideoMode.Resolution.f4K_DCI,     VideoMode.FrameRate.f59_94,     VideoMode.ScanMode.Progressive,  0x34643539),
            new VideoMode(VideoMode.Resolution.f4K_DCI,     VideoMode.FrameRate.f60,        VideoMode.ScanMode.Progressive,  0x34643630),
            new VideoMode(VideoMode.Resolution.f4K_DCI,     VideoMode.FrameRate.f95_90,     VideoMode.ScanMode.Progressive,  0x34643935),
            new VideoMode(VideoMode.Resolution.f4K_DCI,     VideoMode.FrameRate.f96,        VideoMode.ScanMode.Progressive,  0x34643936),
            new VideoMode(VideoMode.Resolution.f4K_DCI,     VideoMode.FrameRate.f100,       VideoMode.ScanMode.Progressive,  0x34643130),
            new VideoMode(VideoMode.Resolution.f4K_DCI,     VideoMode.FrameRate.f119_88,    VideoMode.ScanMode.Progressive,  0x34643131),
            new VideoMode(VideoMode.Resolution.f4K_DCI,     VideoMode.FrameRate.f120,       VideoMode.ScanMode.Progressive,  0x34643132),

            new VideoMode(VideoMode.Resolution.f4320,       VideoMode.FrameRate.f23_98,     VideoMode.ScanMode.Progressive,  0x386b3233),
            new VideoMode(VideoMode.Resolution.f4320,       VideoMode.FrameRate.f24,        VideoMode.ScanMode.Progressive,  0x386b3234),
            new VideoMode(VideoMode.Resolution.f4320,       VideoMode.FrameRate.f25,        VideoMode.ScanMode.Progressive,  0x386b3235),
            new VideoMode(VideoMode.Resolution.f4320,       VideoMode.FrameRate.f29_97,     VideoMode.ScanMode.Progressive,  0x386b3239),
            new VideoMode(VideoMode.Resolution.f4320,       VideoMode.FrameRate.f30,        VideoMode.ScanMode.Progressive,  0x386b3330),
            new VideoMode(VideoMode.Resolution.f4320,       VideoMode.FrameRate.f47_95,     VideoMode.ScanMode.Progressive,  0x386b3437),
            new VideoMode(VideoMode.Resolution.f4320,       VideoMode.FrameRate.f48,        VideoMode.ScanMode.Progressive,  0x386b3438),
            new VideoMode(VideoMode.Resolution.f4320,       VideoMode.FrameRate.f50,        VideoMode.ScanMode.Progressive,  0x386b3530),
            new VideoMode(VideoMode.Resolution.f4320,       VideoMode.FrameRate.f59_94,     VideoMode.ScanMode.Progressive,  0x386b3539),
            new VideoMode(VideoMode.Resolution.f4320,       VideoMode.FrameRate.f60,        VideoMode.ScanMode.Progressive,  0x386b3630),

            new VideoMode(VideoMode.Resolution.f8k_DCI,     VideoMode.FrameRate.f23_98,     VideoMode.ScanMode.Progressive,  0x38643233),
            new VideoMode(VideoMode.Resolution.f8k_DCI,     VideoMode.FrameRate.f24,        VideoMode.ScanMode.Progressive,  0x38643234),
            new VideoMode(VideoMode.Resolution.f8k_DCI,     VideoMode.FrameRate.f25,        VideoMode.ScanMode.Progressive,  0x38643235),
            new VideoMode(VideoMode.Resolution.f8k_DCI,     VideoMode.FrameRate.f29_97,     VideoMode.ScanMode.Progressive,  0x38643239),
            new VideoMode(VideoMode.Resolution.f8k_DCI,     VideoMode.FrameRate.f30,        VideoMode.ScanMode.Progressive,  0x38643330),
            new VideoMode(VideoMode.Resolution.f8k_DCI,     VideoMode.FrameRate.f47_95,     VideoMode.ScanMode.Progressive,  0x38643437),
            new VideoMode(VideoMode.Resolution.f8k_DCI,     VideoMode.FrameRate.f48,        VideoMode.ScanMode.Progressive,  0x38643438),
            new VideoMode(VideoMode.Resolution.f8k_DCI,     VideoMode.FrameRate.f50,        VideoMode.ScanMode.Progressive,  0x38643530),
            new VideoMode(VideoMode.Resolution.f8k_DCI,     VideoMode.FrameRate.f59_94,     VideoMode.ScanMode.Progressive,  0x38643539),
            new VideoMode(VideoMode.Resolution.f8k_DCI,     VideoMode.FrameRate.f60,        VideoMode.ScanMode.Progressive,  0x38643630),

            new VideoMode(VideoMode.Resolution.f640_x_480,      VideoMode.FrameRate.f60,    VideoMode.ScanMode.Progressive,  0x76676136),
            new VideoMode(VideoMode.Resolution.f800_x_600,      VideoMode.FrameRate.f60,    VideoMode.ScanMode.Progressive,  0x73766736),
            new VideoMode(VideoMode.Resolution.f1440_x_900,     VideoMode.FrameRate.f50,    VideoMode.ScanMode.Progressive,  0x77786735),
            new VideoMode(VideoMode.Resolution.f1440_x_900,     VideoMode.FrameRate.f60,    VideoMode.ScanMode.Progressive,  0x77786736),
            new VideoMode(VideoMode.Resolution.f1440_x_1080,    VideoMode.FrameRate.f50,    VideoMode.ScanMode.Progressive,  0x73786735),
            new VideoMode(VideoMode.Resolution.f1440_x_1080,    VideoMode.FrameRate.f60,    VideoMode.ScanMode.Progressive,  0x73786736),

            new VideoMode(VideoMode.Resolution.f1600_x_1200,    VideoMode.FrameRate.f50,    VideoMode.ScanMode.Progressive,  0x75786735),
            new VideoMode(VideoMode.Resolution.f1600_x_1200,    VideoMode.FrameRate.f60,    VideoMode.ScanMode.Progressive,  0x75786736),
            new VideoMode(VideoMode.Resolution.f1920_x_1200,    VideoMode.FrameRate.f50,    VideoMode.ScanMode.Progressive,  0x77757835),
            new VideoMode(VideoMode.Resolution.f1920_x_1200,    VideoMode.FrameRate.f60,    VideoMode.ScanMode.Progressive,  0x77757836),
            new VideoMode(VideoMode.Resolution.f1920_x_1440,    VideoMode.FrameRate.f50,    VideoMode.ScanMode.Progressive,  0x31393435),
            new VideoMode(VideoMode.Resolution.f1920_x_1440,    VideoMode.FrameRate.f60,    VideoMode.ScanMode.Progressive,  0x31393436),

            new VideoMode(VideoMode.Resolution.f2560_x_1440,    VideoMode.FrameRate.f50,    VideoMode.ScanMode.Progressive,  0x77716835),
            new VideoMode(VideoMode.Resolution.f2560_x_1440,    VideoMode.FrameRate.f60,    VideoMode.ScanMode.Progressive,  0x77716836),
            new VideoMode(VideoMode.Resolution.f2560_x_1600,    VideoMode.FrameRate.f50,    VideoMode.ScanMode.Progressive,  0x77717835),
            new VideoMode(VideoMode.Resolution.f2560_x_1600,    VideoMode.FrameRate.f60,    VideoMode.ScanMode.Progressive,  0x77717836),
        };
    }
}
