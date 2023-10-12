namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// Enum values of the currently supported pixel formats.
    /// </summary>
    public enum BMDPixelFormat
    {
        /// <summary>
        /// Chooses the best quality Pixel Format that is compatible with the current card.
        /// </summary>
        UseBestQuality = 0,

        /// <summary>
        /// Four 8-bit unsigned components (CCIR 601) packed into one 32-bit little-endian word.
        /// </summary>
        /// <remarks>UYVY</remarks>
        YUV8Bit = 0x32767579,

        /// <summary>
        /// Twelve 10-bit unsigned components packed into four 32-bit little-endian words.
        /// </summary>
        /// <remarks>v210</remarks>
        YUV10Bit = 0x76323130,

        /// <summary>
        /// Four 8-bit unsigned components packed into one 32-bit little-endian word. Alpha channel is valid.
        /// </summary>
        /// <remarks>ARGB32</remarks>
        ARGB8Bit = 32,

        /// <summary>
        /// Four 8-bit unsigned components packed into one 32-bit little-endian word. The alpha channel may be valid.
        /// </summary>
        /// <remarks>BGRA32</remarks>
        BGRA8Bit = 0x42475241,

        /// <summary>
        /// Three 10-bit unsigned components packed into one 32-bit big-endian word.
        /// </summary>
        /// <remarks>r210</remarks>
        RGB10Bit = 0x72323130,

        /// <summary>
        /// Three 10-bit unsigned components packed into one 32-bit little-endian word.
        /// </summary>
        /// <remarks>r10L</remarks>
        RGBXLE10Bit = 0x5231306c,

        /// <summary>
        /// Three 10-bit unsigned components packed into one 32-bit big-endian word.
        /// </summary>
        /// <remarks>r10B</remarks>
        RGBX10Bit = 0x52313062,

        /// <summary>
        /// Big-endian RGB 12-bit per component with full range (0-4095). Packed as 12-bit per component.
        /// </summary>
        /// <remarks>r12B</remarks>
        RGB12Bit = 0x52313242,

        /// <summary>
        /// Little-endian RGB 12-bit per component with full range (0-4095). Packed as 12-bit per component.
        /// </summary>
        /// <remarks>r12L</remarks>
        RGBLE12Bit = 0x5231324c,

        /// <summary>
        /// This conversion is offered for quality control purposes.
        /// Invokes only the color conversion operations, excluding any packing operations.
        /// </summary>
        NO_PACKING,

        /// <summary>
        /// This conversion is offered for quality control purposes.
        /// Invokes only a full range / studio range conversion.
        /// </summary>
        CLIP_ONLY,

        /// <summary>
        /// This conversion is offered for quality control purposes.
        /// Invokes only the linear gamma unity conversion.
        /// </summary>
        LINGAMMA_ONLY,

        /// <summary>
        /// This conversion is offered for quality control purposes.
        /// Invokes only a direct copy, avoiding conversions but enacting bit-depth changes if
        /// the texture's bit-depth varies.
        /// </summary>
        PASSTHROUGH,
    };

    /// <summary>
    /// A class that contains extension methods for <see cref="BMDPixelFormat"/>.
    /// </summary>
    public static class BMDPixelFormatExtensions
    {
        /// <summary>
        /// The width of the video texture in bytes.
        /// </summary>
        /// <param name="pixelFormat">The pixel format of the video texture.</param>
        /// <param name="width">The width of the video texture in pixels.</param>
        /// <returns>The width of the video texture in bytes.</returns>
        public static int GetByteWidth(this BMDPixelFormat pixelFormat, int width)
        {
            switch (pixelFormat)
            {
                default:
                {
                    return 0;
                }
                case BMDPixelFormat.YUV8Bit:
                {
                    return 2 * width;
                }
                case BMDPixelFormat.ARGB8Bit:
                case BMDPixelFormat.BGRA8Bit:
                {
                    return 4 * width;
                }
                case BMDPixelFormat.YUV10Bit:
                {
                    // 6 component per 4 channels, 1 byte per channel
                    const int nbSrcComponents = 6;
                    const int nbDstComponents = 4;
                    const int nbBytesPerPixel = 4;
                    var wLen = (width / nbSrcComponents) * nbDstComponents * nbBytesPerPixel;

                    // padding
                    const int blockSize = 128;
                    wLen = wLen % blockSize != 0 ? ((wLen / blockSize) + 1) * blockSize : wLen;
                    return wLen;
                }
                case BMDPixelFormat.RGB10Bit:
                case BMDPixelFormat.RGBX10Bit:
                case BMDPixelFormat.RGBXLE10Bit:
                {
                    const int nbBytesPerPixel = 4;
                    var wLen = width * nbBytesPerPixel;

                    // padding
                    const int blockSize = 256;
                    wLen = wLen % blockSize != 0 ? ((wLen / blockSize) + 1) * blockSize : wLen;
                    return wLen;
                }
                case BMDPixelFormat.RGB12Bit:
                case BMDPixelFormat.RGBLE12Bit:
                {
                    const int nbWordsPerBlock = 9;
                    const int nbPixelsPerBlock = 8;
                    const int nbBytesPerWord = 4;
                    var wLen = (width * nbWordsPerBlock * nbBytesPerWord) / nbPixelsPerBlock;
                    return wLen;
                }
            }
        }

        /// <summary>
        /// Gets the height of the video texture in bytes.
        /// </summary>
        /// <param name="pixelFormat">The pixel format of the video texture.</param>
        /// <param name="height">The height of the video texture in pixels.</param>
        /// <returns>Gets the height of the video texture in bytes.</returns>
        public static int GetByteHeight(this BMDPixelFormat pixelFormat, int height)
        {
            switch (pixelFormat)
            {
                default:
                {
                    return 0;
                }
                case BMDPixelFormat.YUV8Bit:
                case BMDPixelFormat.ARGB8Bit:
                case BMDPixelFormat.BGRA8Bit:
                case BMDPixelFormat.YUV10Bit:
                case BMDPixelFormat.RGB10Bit:
                case BMDPixelFormat.RGBX10Bit:
                case BMDPixelFormat.RGBXLE10Bit:
                case BMDPixelFormat.RGB12Bit:
                case BMDPixelFormat.RGBLE12Bit:
                {
                    return height;
                }
            }
        }

        /// <summary>
        /// Gets the depth of the video texture in bytes.
        /// </summary>
        /// <param name="pixelFormat">The pixel format of the video texture.</param>
        /// <returns>Gets the depth of the video texture in bytes.</returns>
        public static int GetByteDepth(this BMDPixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                default:
                {
                    return 0;
                }
                case BMDPixelFormat.YUV8Bit:
                case BMDPixelFormat.ARGB8Bit:
                case BMDPixelFormat.BGRA8Bit:
                case BMDPixelFormat.YUV10Bit:
                case BMDPixelFormat.RGB10Bit:
                case BMDPixelFormat.RGBX10Bit:
                case BMDPixelFormat.RGBXLE10Bit:
                case BMDPixelFormat.RGB12Bit:
                case BMDPixelFormat.RGBLE12Bit:
                {
                    return 4;
                }
            }
        }
    }
}
