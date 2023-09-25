using System;
using Unity.Media.Blackmagic;
using UnityEngine;

namespace Unity.Media.VideoIO.Blackmagic
{
    public class InputDeviceAudioHandler : MonoBehaviour
    {
        [SerializeField]
        InputVideoDeviceHandle m_InputDevice;

        SimpleRingBuffer m_RingBuffer;
        float[] m_CopyFrame;
        bool m_Registered;

        void Start()
        {
            // todo : create audiosource if there is none

            m_RingBuffer = new SimpleRingBuffer(48000);
            m_CopyFrame = new float[8192];
        }

        void Update()
        {
            if (!m_Registered && m_InputDevice.IsActive())
            {
                m_InputDevice.RegisterAudioFrameCallback(OnAudioFrameArrived);
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

        void OnAudioFrameArrived(InputAudioFrame audioFrame)
        {
            unsafe
            {
                switch (audioFrame.sampleType.GetBytesPerSample())
                {
                    case 2:
                        {
                            Int16* intPtr = (Int16*)audioFrame.data;
                            for (int i = 0; i < audioFrame.sampleCount; ++i)
                            {
                                m_CopyFrame[i] = (float)*(intPtr + i) / (float)Int16.MaxValue;
                            }
                            break;
                        }
                    case 4:
                        {
                            Int32* intPtr = (Int32*)audioFrame.data;
                            for (int i = 0; i < audioFrame.sampleCount; ++i)
                            {
                                m_CopyFrame[i] = (float)*(intPtr + i) / (float)Int32.MaxValue;
                            }
                            break;
                        }
                }

                lock (m_RingBuffer)
                {
                    m_RingBuffer.Write(m_CopyFrame, (int)audioFrame.sampleCount);
                }
            }
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (m_RingBuffer == null)
                return;

            lock (m_RingBuffer)
            {
                if (m_RingBuffer.IsReady && m_RingBuffer.FillCount > data.Length)
                    m_RingBuffer.Read(ref data, data.Length);
            }
        }
    }
}


