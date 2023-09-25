using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// The class which holds input and output devices data.
    /// </summary>
    /// <remarks>
    /// It's represented as a unique instance (Singleton).
    /// </remarks>
    [AddComponentMenu("")]
    [ExecuteAlways]
    public partial class DeckLinkManager : MonoBehaviour
    {
        static DeckLinkManager s_VideoIOManagerInstance;

        /// <summary>
        ///  Tries to access an existing DeckLinkManager instance in the Scene.
        /// </summary>
        /// <param name="manager">The DecklinkManager instance.</param>
        /// <returns>True if an instance exists, false otherwise.</returns>
        internal static bool TryGetInstance(out DeckLinkManager manager)
        {
            if (s_VideoIOManagerInstance == null)
            {
                DeckLinkManager[] instances;
                s_VideoIOManagerInstance = TryGetInstances(out instances) ? instances[0] : null;
            }

            manager = s_VideoIOManagerInstance;
            return manager != null;
        }

        static bool TryGetInstances(out DeckLinkManager[] instances)
        {
            instances = Resources.FindObjectsOfTypeAll(typeof(DeckLinkManager)) as DeckLinkManager[];
            return instances != null && instances.Length > 0;
        }

        /// <summary>
        /// Enables or not the DeckLinkManager instance in the Scene.
        /// </summary>
        public static bool EnableVideoManager
        {
            get
            {
                return s_VideoIOManagerInstance?.enabled ?? false;
            }
            set
            {
                if (value && s_VideoIOManagerInstance == null)
                {
                    if (!TryGetInstance(out var _))
                        return;
                }

                if (s_VideoIOManagerInstance && s_VideoIOManagerInstance.enabled != value)
                {
                    s_VideoIOManagerInstance.transform.gameObject.SetActive(value);
                    s_VideoIOManagerInstance.enabled = value;
                }
            }
        }

        GCHandle m_HandlePinnedManager;

        void OnEnable()
        {
            // Necessary for the static callbacks, as they can't call the 'FindObjectsOfTypeAll'
            // outside of the Main Thread.
            TryGetInstance(out _);

            // Devices and Data are released and recreated during OnEnable / OnDisable
            // because we cannot keep a Blackmagic object reference after an Assembly Reload,
            // it breaks the C# & C++ callbacks mechanism.
            ResetDeviceData(false);

            InitializeDeckLinkDeviceProfile();

            InitializeDeckLinkDeviceDiscovery();
            InitializeDevices(m_DevicesConnectorMapping[0]);
        }

        internal void OnDisable()
        {
            ResetDeviceData(true);
            ClearDeckLinkDiscoveryDevice();
            ClearDeckLinkDeviceProfileIfNeeded();
        }

        internal void MappingConnectorProfileChanged(DeckLinkConnectorMapping connectorMapping)
        {
            if (m_DevicesConnectorMapping.Count == 0)
                return;

            if (m_DevicesConnectorMapping[m_DeckLinkCardIndex] == connectorMapping)
                return;

            m_DevicesConnectorMapping[m_DeckLinkCardIndex] = connectorMapping;

            // Clear current devices (data and names), destroy C++ object
            // (bound to the current duplex mode)
            ResetDeviceData();
            ChangedDevicesDuplexMode(connectorMapping, m_DeckLinkCardIndex);
            InitializeDevices(m_DevicesConnectorMapping[0]);
        }

        internal void MappingConnectorProfileChanged(DeckLinkConnectorMapping connectorMapping, int index)
        {
            if (m_DevicesConnectorMapping.Count == 0)
                return;

            // Clear current devices (data and names), destroy C++ object
            // (bound to the current duplex mode)
            ResetDeviceData();

            InitializeDevices(m_DevicesConnectorMapping[0]);
            ChangedDevicesDuplexMode(connectorMapping, index);
        }

        [MonoPInvokeCallback(typeof(PackageRequirementErrorCallback))]
        static void OnPackageRequirementErrorTriggered(IntPtr message)
        {
            if (message == IntPtr.Zero)
                return;

            var error = Marshal.PtrToStringAnsi(message);
            Debug.LogError(error);
        }

        void OnDestroy()
        {
            s_VideoIOManagerInstance = null;

            if (m_HandlePinnedManager.IsAllocated)
            {
                m_HandlePinnedManager.Free();
            }
        }

        internal void AllocatePinnedInstanceManagerIfNeeded()
        {
            if (s_VideoIOManagerInstance != null && m_HandlePinnedManager == null)
            {
                m_HandlePinnedManager = GCHandle.Alloc(s_VideoIOManagerInstance, GCHandleType.Pinned);
            }
        }
    }
}
