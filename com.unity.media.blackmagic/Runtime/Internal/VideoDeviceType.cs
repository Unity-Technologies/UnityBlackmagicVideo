using System;
using UnityEngine;

namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// Defines the type of a video device.
    /// </summary>
    public enum VideoDeviceType
    {
        /// <summary>
        /// The device is of type DeckLinkInputDevice.
        /// </summary>
        Input = 1 << 0,

        /// <summary>
        /// The device is of type DeckLinkOutputDevice.
        /// </summary>
        Output = 1 << 1
    }
}
