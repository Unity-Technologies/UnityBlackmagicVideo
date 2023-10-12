using System;

namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// An enum defining the supported audio sample types.
    /// </summary>
    public enum BMDAudioSampleType
    {
        /// <summary>
        /// A 16-bit integer sample.
        /// </summary>
        Int16 = 16,

        /// <summary>
        /// A 32-bit integer sample.
        /// </summary>
        Int32 = 32,
    }

    /// <summary>
    /// A class that contains extension methods for <see cref="BMDAudioSampleType"/>.
    /// </summary>
    public static class BMDAudioSampleTypeExtensions
    {
        /// <summary>
        /// Gets the number of bytes per sample for the audio sample type.
        /// </summary>
        /// <param name="type">The type of the audio samples.</param>
        /// <returns>The number of bytes per sample.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="type"/> is invalid.</exception>
        public static int GetBytesPerSample(this BMDAudioSampleType type)
        {
            switch (type)
            {
                case BMDAudioSampleType.Int16:
                    return 2;
                case BMDAudioSampleType.Int32:
                    return 4;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}
