using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;

namespace Unity.Media.Blackmagic
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("Blackmagic/Input Device Audio Handler")]
    public class InputDeviceAudioHandler : MonoBehaviour
    {
        [SerializeField]
        InputVideoDeviceHandle m_InputDevice;

        bool m_Registered;
        int m_Channels;
        readonly object m_Lock = new object();
        SimpleRingBuffer m_Buffer = new SimpleRingBuffer(100000);
        float[] m_TempBuffer = new float[100000];

        void OnDisable()
        {
            if (m_Registered && m_InputDevice.IsActive())
            {
                m_InputDevice.UnregisterSynchronizedAudioFrameCallback(OnAudioFrameArrived);
                m_Registered = false;
            }
        }

        void Update()
        {
            if (!m_Registered && m_InputDevice.IsActive())
            {
                m_InputDevice.RegisterSynchronizedAudioFrameCallback(OnAudioFrameArrived);
                m_Registered = true;
            }
        }

        void OnAudioFrameArrived(SynchronizedAudioFrame audioFrame)
        {
            m_Channels = audioFrame.channelCount;

            lock (m_Lock)
            {
                unsafe
                {
                    fixed (float* ptr = m_TempBuffer)
                    {
                        UnsafeUtility.MemCpy(ptr, audioFrame.data.GetUnsafeReadOnlyPtr(), audioFrame.data.Length * sizeof(float));
                    }

                    m_Buffer.Write(m_TempBuffer, audioFrame.data.Length);
                }
            }
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!m_Registered || m_Channels != channels || m_Buffer == null)
                return;

            lock (m_Lock)
            {
                if (m_Buffer.FillCount > data.Length)
                {
                    m_Buffer.Read(ref data, data.Length);
                }
            }
        }
    }
}


