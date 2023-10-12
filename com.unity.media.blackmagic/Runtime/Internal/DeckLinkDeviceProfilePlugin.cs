using System;
using System.Runtime.InteropServices;

namespace Unity.Media.Blackmagic
{
    static class DeckLinkDeviceProfilePlugin
    {
        /// <summary>
        /// Represents the managed callback used when a mapping connector profile has been selected.
        /// </summary>
        /// <param name="streamsHasStopped">The device has stopped or not.</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CallbackProfileChanged(bool streamsHasStopped);

        /// <summary>
        /// Represents the managed callback used when a mapping connector profile has changed.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CallbackProfileActivated();

        /// <summary>
        /// The plugin callback that creates a DeckLink Device Profile instance.
        /// </summary>
        /// <returns>A pointer to the instance created.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern IntPtr CreateDeckLinkDeviceProfileInstance();

        /// <summary>
        /// Initializes the plugin callback that is triggered when a mapping connector profile is changed.
        /// </summary>
        /// <param name="deviceProfile">The DeviceProfile instance.</param>
        /// <param name="callBack">The managed callback to called.</param>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern void SetOnProfileChangedCallback(IntPtr deviceProfile, IntPtr callBack);

        /// <summary>
        /// Initializes the plugin callback that is triggered when a mapping connector profile is changed and saved.
        /// </summary>
        /// <param name="deviceProfile">The DeviceProfile instance.</param>
        /// <param name="callBack">The managed callback to called.</param>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern void SetOnProfileActivatedCallback(IntPtr deviceProfile, IntPtr callBack);

        /// <summary>
        /// The plugin callback that destroys a Device Profile instance.
        /// </summary>
        /// <param name="deviceProfile">The DeviceProfile instance.</param>
        /// <returns>The device has been successfully destroyed.</returns>
        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern bool DestroyDeckLinkDeviceProfileInstance(IntPtr deviceProfile);
    }
}
