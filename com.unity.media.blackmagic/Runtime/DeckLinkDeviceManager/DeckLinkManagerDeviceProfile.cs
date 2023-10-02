using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// Contains the connector mappings currently supported in the package.
    /// </summary>
    public enum DeckLinkConnectorMapping
    {
        /// <summary>
        /// Supports 4 independent sub-devices, that can be either Input or Output.
        /// </summary>
        /// <remarks>
        /// It doesn't support Keying, 8K resolution and Loop Through.
        /// </remarks>
        FourSubDevicesHalfDuplex,

        /// <summary>
        /// Supports 1 sub-device for the input and 1 sub-device for the Output.
        /// </summary>
        /// <remarks>
        /// It doesn't support 8k resolution. It does support Keying and Loop Through.
        /// When using connector 1, the signal will be looped through connector 3.
        /// When using connector 2, the signal will be looped through connector 4.
        /// </remarks>
        OneSubDeviceFullDuplex,

        /// <summary>
        /// Supports 1 sub-device, that can be either Input or Output.
        /// </summary>
        /// <remarks>
        /// It does support 8k resolution and Keying (Internal only).
        /// It doesn't support Loop Through.
        /// </remarks>
        OneSubDeviceHalfDuplex,

        /// <summary>
        /// Supports 2 sub-devices for the input and 2 sub-devices for the Output.
        /// </summary>
        /// <remarks>
        /// It doesn't support 8k resolution and Loop Through. It does support Keying.
        /// </remarks>
        TwoSubDevicesFullDuplex,

        /// <summary>
        /// Supports 2 sub-device, that can be either Input or Output.
        /// </summary>
        TwoSubDevicesHalfDuplex
    }

    /// <summary>
    /// The partial class which contains the devices profile callbacks.
    /// </summary>
    partial class DeckLinkManager
    {
        static IntPtr s_DeckLinkDeviceProfile = IntPtr.Zero;
        static DeckLinkDeviceProfilePlugin.CallbackProfileChanged s_ProfileChanged;
        static DeckLinkDeviceProfilePlugin.CallbackProfileActivated s_ProfileActivated;

        [SerializeField]
        internal int m_DeckLinkCardIndex;

        [SerializeField]
        internal List<DeckLinkConnectorMapping> m_DevicesConnectorMapping = new List<DeckLinkConnectorMapping>();

        internal int deckLinkCardIndex => m_DeckLinkCardIndex;

        internal DeckLinkConnectorMapping connectorMapping => m_DevicesConnectorMapping[m_DeckLinkCardIndex];

        internal DeckLinkConnectorMapping getConnectorMapping(int index) => m_DevicesConnectorMapping[index];

        void InitializeDeckLinkDeviceProfile()
        {
            s_DeckLinkDeviceProfile = DeckLinkDeviceProfilePlugin.CreateDeckLinkDeviceProfileInstance();
            if (s_DeckLinkDeviceProfile == IntPtr.Zero)
            {
                Debug.LogError("[DeckLinkDeviceDiscovery] - Failed to create a new Instance.");
            }
            else
            {
                s_ProfileChanged = OnProfileChanged;
                DeckLinkDeviceProfilePlugin.SetOnProfileChangedCallback(s_DeckLinkDeviceProfile,
                    Marshal.GetFunctionPointerForDelegate(s_ProfileChanged));

                s_ProfileActivated = OnProfileActivated;
                DeckLinkDeviceProfilePlugin.SetOnProfileActivatedCallback(s_DeckLinkDeviceProfile,
                    Marshal.GetFunctionPointerForDelegate(s_ProfileActivated));
            }
        }

        // TODO to be implemented.
        [MonoPInvokeCallback(typeof(DeckLinkDeviceProfilePlugin.CallbackProfileChanged))]
        static void OnProfileChanged(bool _)
        {
        }

        // TODO to be implemented.
        [MonoPInvokeCallback(typeof(DeckLinkDeviceProfilePlugin.CallbackProfileActivated))]
        static void OnProfileActivated()
        {
        }

        static void ClearDeckLinkDeviceProfileIfNeeded()
        {
            if (s_DeckLinkDeviceProfile != IntPtr.Zero)
            {
                DeckLinkDeviceProfilePlugin.DestroyDeckLinkDeviceProfileInstance(s_DeckLinkDeviceProfile);
                s_DeckLinkDeviceProfile = IntPtr.Zero;
            }
        }
    }
}
