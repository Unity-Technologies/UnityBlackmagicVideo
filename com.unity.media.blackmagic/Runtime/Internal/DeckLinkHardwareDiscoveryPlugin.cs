using System;
using System.Runtime.InteropServices;

namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// This class contains static methods to retrieve API and hardware informations
    /// from the plugin.
    /// </summary>
    public static class DeckLinkHardwareDiscoveryPlugin
    {
        /// <summary>
        /// Retrieves the api version of the plugin.
        /// </summary>
        /// <returns>A pointer containing the API version of the plugin.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern IntPtr GetBlackmagicAPIVersionPlugin();

        /// <summary>
        /// Retrieves a list of the DeckLink cards installed in the current machine.
        /// </summary>
        /// <returns>True if succeeded; false otherwise.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern bool InitializeDeckLinkCards();

        /// <summary>
        /// Retrieves how much DeckLink cards are installed in the current machine.
        /// </summary>
        /// <returns>The number of DeckLink cards installed in the current machine.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern int GetDeckLinkCardsCount();

        /// <summary>
        /// Retrieves the DeckLink card name from an index.
        /// </summary>
        /// <param name="index">The index of the DeckLink card.</param>
        /// <returns>A pointer containing the DeckLink card name.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern IntPtr GetDeckLinkCardNameByIndex(int index);

        /// <summary>
        /// Retrieves the DeckLink card unique ID.
        /// </summary>
        /// <param name="index">The index of the DeckLink card.</param>
        /// <returns>A pointer containing the DeckLink card name.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern Int64 GetDeckLinkDeviceGroupIDByIndex(int index);

        /// <summary>
        /// Retrieves the amount of logical devices for a specific DeckLink card.
        /// </summary>
        /// <param name="index">The index of the DeckLink card.</param>
        /// <returns>The number of logical devices available for the specified DeckLink card.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern int GetDeckLinkCardLogicalDevicesCount(int index);

        /// <summary>
        /// Retrieves the logical device name based on the specified index.
        /// </summary>
        /// <param name="indexCard">The index of the DeckLink card.</param>
        /// <param name="indexLogicalDevice">The index of the logical device.</param>
        /// <returns>The logical device name.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern IntPtr GetDeckLinkCardLogicalDeviceName(int indexCard, int indexLogicalDevice);
    }
}
