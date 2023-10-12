using System;
using System.Runtime.InteropServices;

namespace Unity.Media.Blackmagic
{
    static class DeckLinkDeviceEnumeratorPlugin
    {
        /// <summary>
        /// Retrieves all available input device names.
        /// </summary>
        /// <param name="inputDevices">The pointer that holds the available input device names.</param>
        /// <param name="maxCount">The maximum input device names that can be retrieve.</param>
        /// <returns>The number of input device names retrieved.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern int RetrieveInputDeviceNames(IntPtr[] inputDevices, int maxCount);

        /// <summary>
        /// Retrieves all available output device names.
        /// </summary>
        /// <param name="outputDevices">The pointer that holds the available output device names.</param>
        /// <param name="maxCount">The maximum output device names that can be retrieve.</param>
        /// <returns>The number of output device names retrieved.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern int RetrieveOutputDeviceNames(IntPtr[] outputDevices, int maxCount);

        /// <summary>
        /// Native function to scan the available output formats on a specified device.
        /// </summary>
        /// <param name="device"> The index of the actual device used. </param>
        /// <param name="modes"> The array used to store the available output modes. </param>
        /// <param name="maxCount"> The length of the given collection object. </param>
        /// <returns> The number of output formats copied in the collection object. </returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern int RetrieveOutputModes(int device, int[] modes, int maxCount);

        /// <summary>
        /// Mapping connector used on available devices.
        /// </summary>
        /// <param name="halfDuplex">Determines if the mapping connector is half-duplex or full-duplex.</param>
        /// <returns>The mapping connector has been successfully changed or not.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern bool SetAllDevicesDuplexMode(bool halfDuplex);
    }
}
