using System;
using System.Threading;
using Unity.Media.Blackmagic;
using UnityEngine;

namespace Unity.Media.Blackmagic
{
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Blackmagic/Input Device Audio Handler")]
    public class InputDeviceAudioHandler : MonoBehaviour
    {
        [SerializeField]
        InputVideoDeviceHandle m_InputDevice;

        [SerializeField]
        bool m_SynchronizeAudioToVideo = true;

        SimpleRingBuffer m_RingBuffer;
        float[] m_CopyFrame;
        bool m_Registered;

        void OnEnable()
        {
            m_RingBuffer = new SimpleRingBuffer(100000);
            m_CopyFrame = new float[8192];
        }

        void Update()
        {
            if (!m_Registered && m_InputDevice.IsActive())
            {
                m_InputDevice.RegisterAudioFrameCallback(OnAudioFrameArrived, m_SynchronizeAudioToVideo);
                m_Registered = true;
            }
        }

        void OnDisable()
        {
            if (m_Registered && m_InputDevice.IsActive())
            {
                m_InputDevice.UnregisterAudioFrameCallback(OnAudioFrameArrived);
            }
        }

        void OnAudioFrameArrived(IntPtr byteData, long timestamp, int sampleCount, int bytesPerSample)
        {
            unsafe
            {
                switch (bytesPerSample)
                {
                    case 2:
                    {
                        Int16* intPtr = (Int16*)byteData;
                        for (int i = 0; i < sampleCount; ++i)
                        {
                            m_CopyFrame[i] = (float)*(intPtr + i) / (float)Int16.MaxValue;
                        }
                        break;
                    }
                    case 4:
                    {
                        Int32* intPtr = (Int32*)byteData;
                        for (int i = 0; i < sampleCount; ++i)
                        {
                            m_CopyFrame[i] = (float)*(intPtr + i) / (float)Int32.MaxValue;
                        }
                        break;
                    }
                }

                lock (m_RingBuffer)
                {
                    m_RingBuffer.Write(m_CopyFrame, (int)sampleCount);
                }
            }
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (m_RingBuffer == null)
                return;

            lock (m_RingBuffer)
            {
                if (m_RingBuffer.FillCount > data.Length)
                {
                    m_RingBuffer.Read(ref data, data.Length);
                }
            }
        }
    }
}


