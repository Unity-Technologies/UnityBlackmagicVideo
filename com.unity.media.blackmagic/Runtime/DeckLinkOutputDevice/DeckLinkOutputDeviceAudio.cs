using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Unity.Media.Blackmagic
{
    public enum AudioOutputMode
    {
        /// <summary>
        /// Audio is not used.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Audio comes from a selected AudioListener in the scene.
        /// </summary>
        AudioListener = 1,

        /// <summary>
        /// Audio comes from an input device.
        /// </summary>
        SameAsInput = 2,

        /// <summary>
        /// Audio comes from the (singleton) AudioRenderer. 
        /// </summary>
        MainOutput = 3
    }

    partial class DeckLinkOutputDevice
    {
        struct AudioData
        {
            public bool EnableAudio;
            public int AudioChannelCount;
            public int AudioSampleRate;
        }

        [SerializeField]
        internal AudioOutputMode m_AudioOutputMode;

        [SerializeField]
        AudioListener m_AudioListener;

        [SerializeField]
        internal DeckLinkInputDevice m_AudioSameAsInputDevice;

        AudioOutputDevice m_AudioListenerOutputDevice;
        bool m_AudioRendererStarted;

        readonly Dictionary<AudioSpeakerMode, int> audioChannelCountMap = new Dictionary<AudioSpeakerMode, int>
        {
            {AudioSpeakerMode.Mono, 1},
            {AudioSpeakerMode.Stereo, 2},
            {AudioSpeakerMode.Quad, 4},
            {AudioSpeakerMode.Surround, 5},
            {AudioSpeakerMode.Mode5point1, 6},
            {AudioSpeakerMode.Mode7point1, 8},
            {AudioSpeakerMode.Prologic, 2}
        };

        void UpdateAudio()
        {
            if (m_AudioRendererStarted)
            {
                int sampleCount = AudioRenderer.GetSampleCountForCaptureFrame();
                using (var samples = new NativeArray<float>(sampleCount, Allocator.Temp))
                {
                    if (AudioRenderer.Render(samples))
                    {
                        unsafe
                        {
                            m_Plugin.FeedAudioSampleFrames((float*)samples.GetUnsafeReadOnlyPtr(), sampleCount);
                        }
                    }
                }
            }
        }

        AudioData GetEnableAudioAndSampleRate()
        {
            var enableAudio = m_AudioOutputMode != AudioOutputMode.Disabled;
            var audioChannelCount = 0;
            if (enableAudio && !audioChannelCountMap.TryGetValue(AudioSettings.speakerMode, out audioChannelCount))
            {
                Debug.LogWarning($"Unknown audio speaker mode {AudioSettings.speakerMode}. Disabling audio.");
                enableAudio = false;
            }
            var audioSampleRate = AudioSettings.outputSampleRate;

            return new AudioData
            {
                EnableAudio = enableAudio,
                AudioChannelCount = audioChannelCount,
                AudioSampleRate = audioSampleRate
            };
        }

        void SetupAudioOutput()
        {
            switch (m_AudioOutputMode)
            {
                case AudioOutputMode.AudioListener when m_AudioListener == null:
                {
                    Debug.LogWarning("Audio enabled without setting an AudioListener. Audio temporarily disabled.");
                    return;
                }
                case AudioOutputMode.AudioListener:
                {
                    m_AudioListenerOutputDevice = m_AudioListener.gameObject.AddComponent<AudioOutputDevice>();
                    m_AudioListenerOutputDevice.Init(m_Plugin);
                    return;
                }
                case AudioOutputMode.MainOutput:
                {
                    m_AudioRendererStarted = AudioRenderer.Start();
                    if (!m_AudioRendererStarted)
                        Debug.LogError("Main audio output not started. It may already be in use.");
                    return;
                }
                case AudioOutputMode.SameAsInput when m_AudioSameAsInputDevice != null:
                {
                    m_AudioSameAsInputDevice.AddSynchronizedAudioFrameCallback(OnSynchronizedAudioFrame);
                    break;
                }
            }
        }

        void CleanupAudioOutput()
        {
            if (m_AudioOutputMode == AudioOutputMode.MainOutput && m_AudioRendererStarted)
            {
                AudioRenderer.Stop();
            }
            else if (!Application.isPlaying && m_AudioOutputMode == AudioOutputMode.AudioListener && m_AudioListenerOutputDevice)
            {
                DestroyImmediate(m_AudioListenerOutputDevice);
            }
            else if (m_AudioOutputMode == AudioOutputMode.SameAsInput && m_AudioSameAsInputDevice != null)
            {
                m_AudioSameAsInputDevice.RemoveSynchronizedAudioFrameCallback(OnSynchronizedAudioFrame);
            }
        }

        unsafe void OnSynchronizedAudioFrame(SynchronizedAudioFrame frame)
        {
            m_Plugin.FeedAudioSampleFrames((float*)frame.data.GetUnsafeReadOnlyPtr(), frame.data.Length);
        }
    }

    class AudioOutputDevice : MonoBehaviour
    {
        internal void Init(DeckLinkOutputDevicePlugin outputDevicePlugin)
        {
            m_Plugin = outputDevicePlugin;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            unsafe
            {
                fixed (float* samples = data)
                {
                    m_Plugin.FeedAudioSampleFrames(samples, data.Length);
                }
            }
        }

        DeckLinkOutputDevicePlugin m_Plugin;
    }
}
