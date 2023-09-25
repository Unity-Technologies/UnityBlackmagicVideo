namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// Determines how to use the Link mode feature.
    /// These values mirror black magic's EOutputLinkMode.
    /// </summary>
    public enum LinkMode
    {
        /// <summary>Single Link mode.</summary>
        Single = 1 << 0, // 1
        /// <summary>Dual Link mode.</summary>
        Dual = 1 << 1,   // 2
        /// <summary>Quad Link mode.</summary>
        Quad = 1 << 2    // 4
    }

    static class OutputLinkModeUtilities
    {
        internal static bool IsLinkModeCompatible(DeckLinkConnectorMapping connectorMapping)
        {
            switch (connectorMapping)
            {
                case DeckLinkConnectorMapping.OneSubDeviceFullDuplex:
                case DeckLinkConnectorMapping.OneSubDeviceHalfDuplex:
                    return true;
                case DeckLinkConnectorMapping.FourSubDevicesHalfDuplex:
                case DeckLinkConnectorMapping.TwoSubDevicesFullDuplex:
                case DeckLinkConnectorMapping.TwoSubDevicesHalfDuplex:
                default:
                    return false;
            }
        }
    }
}
