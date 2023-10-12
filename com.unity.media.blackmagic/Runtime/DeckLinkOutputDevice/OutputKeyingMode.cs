using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// Determines how to use the keying feature.
    /// </summary>
    public enum KeyingMode
    {
        /// <summary>
        /// Keying is not used.
        /// </summary>
        None = 1 << 0,

        /// <summary>
        /// Send the fill and key informations to an external keyer.
        /// </summary>
        External = 1 << 1,

        /// <summary>
        /// Compose a foreground key frame over an incoming background video feed.
        /// </summary>
        Internal = 1 << 2
    }

    static class OutputKeyingModeUtilities
    {
        internal static bool IsKeyingModeCompatible(DeckLinkConnectorMapping connectorMapping)
        {
            switch (connectorMapping)
            {
                case DeckLinkConnectorMapping.OneSubDeviceFullDuplex:
                case DeckLinkConnectorMapping.OneSubDeviceHalfDuplex:
                case DeckLinkConnectorMapping.TwoSubDevicesFullDuplex:
                    return true;
                case DeckLinkConnectorMapping.FourSubDevicesHalfDuplex:
                case DeckLinkConnectorMapping.TwoSubDevicesHalfDuplex:
                default:
                    return false;
            }
        }
    }
}
