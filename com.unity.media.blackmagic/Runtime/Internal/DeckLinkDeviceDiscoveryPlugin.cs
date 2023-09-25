using System;
using System.Runtime.InteropServices;

namespace Unity.Media.Blackmagic
{
    static class DeckLinkDeviceDiscoveryPlugin
    {
        /// <summary>
        /// The managed callback that is called when a device is added or removed.
        /// </summary>
        /// <param name="deviceName">The name of the device.</param>
        /// <param name="deviceType">The type of the device.</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CallbackDevice(IntPtr deviceName, int deviceType);

        /// <summary>
        /// The plugin callback that creates a DeckLink Discovery instance.
        /// </summary>
        /// <returns>A pointer to the instance created.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern IntPtr CreateDeckLinkDeviceDiscoveryInstance();

        /// <summary>
        /// Stores the current connector mapping for each DeckLink card based on the groupID.
        /// </summary>
        /// <remarks>
        /// Each card is storing the current connector mapping in a List.
        /// It is updated every time the connector mapping is changed.
        /// </remarks>
        /// <param name="deviceDiscovery">The DeviceDiscovery instance.</param>
        /// <param name="groupIDs">An array containing the groupID(s) used.</param>
        /// <param name="profiles">An array containing the connector(s) mapping used.</param>
        /// <param name="length">The number of devices in use.</param>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern void AddConnectorMapping(IntPtr deviceDiscovery, Int64[] groupIDs, int[] profiles, int length);

        /// <summary>
        /// The plugin callback that creates a DeckLink Discovery instance.
        /// </summary>
        /// <returns>A pointer to the instance created.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern IntPtr CreateDeckLinkDeviceDiscoveryInstance(int[] values, int length);

        /// <summary>
        /// Initializes the 'OnDeviceArrived' plugin callback, of a specified instance.
        /// </summary>
        /// <param name="deviceDiscovery">The DeviceDiscovery instance.</param>
        /// <param name="callBack">The managed callback to call.</param>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern void SetDeckLinkOnDeviceArrived(IntPtr deviceDiscovery, IntPtr callBack);

        /// <summary>
        /// Initializes the 'OnDeviceRemoved' plugin callback, of a specified instance.
        /// </summary>
        /// <param name="deviceDiscovery">The DeviceDiscovery instance.</param>
        /// <param name="callBack">The managed callback to call.</param>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern void SetDeckLinkOnDeviceRemoved(IntPtr deviceDiscovery, IntPtr callBack);

        /// <summary>
        /// The plugin callback that destroys a DeckLink Discovery instance.
        /// </summary>
        /// <param name="deviceDiscovery">The DeviceDiscovery instance.</param>
        /// <returns>The destroyed instance id.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern int DestroyDeckLinkDeviceDiscoveryInstance(IntPtr deviceDiscovery);

        /// <summary>
        /// The mapping connector used on available devices.
        /// </summary>
        /// <param name="deviceDiscovery">The instance of the device discovery.</param>
        /// <param name="halfDuplex">Determines if the mapping connector is half-duplex or full-duplex.</param>
        /// <returns>The mapping connector has been successfully changed or not.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern bool ChangeAllDevicesConnectorMapping(IntPtr deviceDiscovery, int profile, Int64 groupID);

        /// <summary>
        /// Determines if the DeckLink card has connector mapping profiles or not.
        /// </summary>
        /// <param name="deviceDiscovery">The instance of the device discovery.</param>
        /// <returns>The DeckLink card has connector mapping profiles or not.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern bool HasConnectorMappingProfiles(IntPtr deviceDiscovery);

        /// <summary>
        /// Reloads all the DeckLink devices when the device cannot change the Connector Mapping profile.
        /// </summary>
        /// <param name="deviceDiscovery">The instance of the device discovery.</param>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern void ReloadAllDeckLinkDevicesEvent(IntPtr deviceDiscovery);

        /// <summary>
        /// Determines if the profile is compatible on the current DeckLink card.
        /// </summary>
        /// <param name="deviceDiscovery">The instance of the device discovery.</param>
        /// <param name="profile">The tested profile.</param>
        /// <returns>True if the profile is compatible, false otherwise.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern bool IsConnectorMappingProfileCompatible(IntPtr deviceDiscovery, int profile);

        /// <summary>
        /// Determines if the link mode is compatible on the current DeckLink card.
        /// </summary>
        /// <param name="deviceDiscovery">The instance of the device discovery.</param>
        /// <param name="linkMode">The tested link mode.</param>
        /// <returns>True if the link mode is compatible, false otherwise.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern bool IsLinkModeCompatible(IntPtr deviceDiscovery, int linkMode, Int64 groupID);

        /// <summary>
        /// Determines if the keying mode is compatible on the current DeckLink card.
        /// </summary>
        /// <param name="deviceDiscovery">The instance of the device discovery.</param>
        /// <param name="linkMode">The tested keying mode.</param>
        /// <returns>True if the keying mode is compatible, false otherwise.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern bool IsKeyingModeCompatible(IntPtr deviceDiscovery, int keyingMode, Int64 groupID);
    }
}
