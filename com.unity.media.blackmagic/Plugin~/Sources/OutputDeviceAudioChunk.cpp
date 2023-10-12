#pragma once

#include "OutputDeviceAudioChunk.h"

namespace MediaBlackmagic
{
    void AudioChunk::Resize(int count)
    {
        delete[] samples;
        samples = current = new AudioSampleType[count];
        sampleCount = capacity = count;
    }

    void AudioChunk::Consume(int count)
    {
        current += count;
        sampleCount -= count;
    }

    void AudioChunk::Release()
    {
        delete[] samples;
        samples = current = nullptr;
        sampleCount = capacity = 0;
    }

    void AudioChunk::Reset()
    {
        current = samples;
    }

    void AudioChunk::SetLength(int count)
    {
        sampleCount = count;
    }

    int AudioChunk::GetCapacity() const
    {
        return capacity;
    }

    int AudioChunk::GetSampleCount() const
    {
        return sampleCount;
    }

    AudioSampleType* AudioChunk::GetCurrentSamples() const
    {
        return current;
    }
}
