#if LIVE_CAPTURE_4_0_0_OR_NEWER
using System;
using System.Threading;
using Unity.LiveCapture;
using UnityEngine;

namespace Unity.Media.Blackmagic
{
    [Serializable]
    [SyncProvider("Blackmagic/Input Device")]
    class DeckLinkInputSyncProvider : SyncProvider
    {
        const int k_Timeout = 200;

        [NonSerialized]
        DeckLinkInputDevice m_Device;
        [NonSerialized]
        AutoResetEvent m_FrameReceivedEvent;
        [NonSerialized]
        int m_SyncCount;

        [SerializeField]
        string m_DeviceName = "Device 1";

        /// <inheritdoc />
        public override string Name => TryGetVideoDevice(m_DeviceName, out var device) ? device.name : $"{m_DeviceName} (Not Found)";

        /// <inheritdoc />
        public override FrameRate SyncRate => TryGetVideoDevice(m_DeviceName, out var device) ? device.GetFrameRate() : default;

        /// <inheritdoc />
        protected override void OnStart()
        {
            base.OnStart();

            m_FrameReceivedEvent = new AutoResetEvent(false);
            m_SyncCount = 0;
        }

        /// <inheritdoc />
        protected override void OnStop()
        {
            base.OnStop();

            m_FrameReceivedEvent.Dispose();
            m_FrameReceivedEvent = null;

            if (m_Device != null)
            {
                m_Device.RemoveVideoFrameCallback(OnVideoFrameReceived);
                m_Device = null;
            }
        }

        /// <inheritdoc />
        protected override bool OnWaitForNextPulse(out int pulseCount)
        {
            if (TryGetVideoDevice(m_DeviceName, out var device) && m_Device != device)
            {
                if (m_Device != null)
                {
                    m_Device.RemoveVideoFrameCallback(OnVideoFrameReceived);
                }

                m_Device = device;

                if (m_Device != null)
                {
                    m_Device.AddVideoFrameCallback(OnVideoFrameReceived);
                }
            }

            if (m_Device == null || !m_Device.IsActive)
            {
                Status = SyncStatus.NotSynchronized;
                pulseCount = 0;
                return false;
            }

            if (!m_FrameReceivedEvent.WaitOne(k_Timeout))
            {
                Debug.LogWarning("Sync provider timed out while waiting for next frame.");
                Status = SyncStatus.NotSynchronized;
                pulseCount = 0;
                return false;
            }

            pulseCount = Interlocked.Exchange(ref m_SyncCount, 0);
            return true;
        }

        void OnVideoFrameReceived(InputVideoFrame frame)
        {
            Interlocked.Add(ref m_SyncCount, 1);
            m_FrameReceivedEvent.Set();
        }

        static bool TryGetVideoDevice(string name, out DeckLinkInputDevice device)
        {
            if (!string.IsNullOrEmpty(name) && DeckLinkManager.TryGetInstance(out var videoIOManager))
            {
                if (videoIOManager.GetDeviceDataByName(name, VideoDeviceType.Input, out var deviceComponent))
                {
                    if (deviceComponent is DeckLinkInputDevice castDevice)
                    {
                        device = castDevice;
                        return true;
                    }
                }
            }

            device = default;
            return false;
        }
    }
}
#endif
