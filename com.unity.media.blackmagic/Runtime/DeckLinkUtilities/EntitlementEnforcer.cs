using System;
using System.Runtime.InteropServices;
using Unity.Media.PackageRequirementsAssistant;

namespace Unity.Media.Blackmagic
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void PackageRequirementErrorCallback(IntPtr message);

    static class EntitlementEnforcerPlugin
    {
        /// <summary>
        /// Represents a callback that can be used to retrieve the plugin errors.
        /// </summary>
        public static event PackageRequirementErrorCallback PackageRequirementErrorCallback;

        /// <summary>
        /// Initializes the plugin callbacks with the managed callbacks.
        /// </summary>
        public static void InitializeCallbacks()
        {
            if (PackageRequirementErrorCallback != null)
            {
                SetRequirementErrorCallback(Marshal.GetFunctionPointerForDelegate(PackageRequirementErrorCallback));
            }
        }

        [DllImport(BlackmagicUtilities.k_PluginName)]
        public static extern void HandleEntitlementValidityKey(string key);

        [DllImport(BlackmagicUtilities.k_PluginName)]
        static extern void SetRequirementErrorCallback(IntPtr handler);
    }
    static class EntitlementEnforcer
    {
        static string m_Key;

        static public void EnforceEntitlement()
        {
            m_Key = RetrieveEntitlementValidityKey();
            EntitlementEnforcerPlugin.HandleEntitlementValidityKey(m_Key);
        }

        static string RetrieveEntitlementValidityKey()
        {
            var entitlementManager = new EntitlementManager(new BlackmagicLicenseChecker());

            return entitlementManager.LicenseValidityKey;
        }
    }
}
