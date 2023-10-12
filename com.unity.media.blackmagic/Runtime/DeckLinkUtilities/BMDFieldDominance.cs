namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// An enum defining the field dominance modes for a frame.
    /// </summary>
    public enum BMDFieldDominance
    {
        /// <summary>
        /// The field dominance is unkown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The frame uses the lower scan lines of the image first, followed by the upper scan lines.
        /// </summary>
        LowerFieldFirst = 0x6c6f7772,

        /// <summary>
        /// The frame uses the upper scan lines of the image first, followed by the lower scan lines.
        /// </summary>
        UpperFieldFirst = 0x75707072,

        /// <summary>
        /// The frame uses all scan lines at once.
        /// </summary>
        ProgressiveFrame = 0x70726f67,

        /// <summary>
        /// The frame is a progressive frame represented using an upper and lower field.
        /// </summary>
        ProgressiveSegmentedFrame = 0x70736620,
    }
}
