using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;

namespace Unity.Media.Blackmagic
{
    class BufferedFrame : IDisposable
    {
        public enum Status
        {
            Uninitialized,
            Queued,
            Presented,
        }

        public Status CurrentStatus { get; set; } = Status.Uninitialized;
        public long frameDuration { get; private set; }
        public Timecode timecode { get; private set; }

        public NativeArray<byte> texture { get; private set; }
        public BMDFieldDominance videoFieldDominance { get; private set; }

        public NativeArray<byte> audio { get; private set; }
        public int audioLength { get; private set; }
        public BMDAudioSampleType audioSampleType { get; private set; }
        public int audioChannelCount { get; private set; }

        public BufferedFrame(InputVideoFormat format)
        {
            texture = new NativeArray<byte>(
                format.byteWidth * format.byteHeight,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory
            );

            // Currently the audio configuration is hard-coded, so we know the required audio buffer size.
            // If the audio config is made changeable, we must surface the selected configuration from the plugin.
            const int audioChannels = 2;
            const int bytesPerSample = 2;
            const int sampleRate = 48000;
            const int minFrameRate = 24;

            audio = new NativeArray<byte>(
                audioChannels * bytesPerSample * sampleRate / minFrameRate,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory
            );
        }

        public void Dispose()
        {
            if (texture.IsCreated)
            {
                texture.Dispose();
                texture = default;
            }
            if (audio.IsCreated)
            {
                audio.Dispose();
                audio = default;
            }
        }

        public void CopyFrom(in InputVideoFrame videoFrame, in InputAudioFrame? audioFrame, ThreadedMemcpy memcpy)
        {
            // use the timecode if available, otherwise we generate timecode from the steam time
            frameDuration = videoFrame.frameDuration;
            timecode = videoFrame.timecode ?? new Timecode(videoFrame.frameDuration, videoFrame.streamTimestamp);

            unsafe
            {
                videoFieldDominance = videoFrame.fieldDominance;

                if (texture.Length >= videoFrame.size)
                {
                    memcpy.MemCpy(texture.GetUnsafePtr(), (void*)videoFrame.data, videoFrame.size);
                }

                if (audioFrame != null)
                {
                    audioSampleType = audioFrame.Value.sampleType;
                    audioChannelCount = audioFrame.Value.channelCount;
                    audioLength = (int)audioFrame.Value.size;

                    if (audio.Length >= audioLength)
                    {
                        memcpy.MemCpy(audio.GetUnsafePtr(), (void*)audioFrame.Value.data, audioLength);
                    }
                }
                else
                {
                    audioLength = 0;
                }
            }

            CurrentStatus = Status.Queued;
        }
    }
}
