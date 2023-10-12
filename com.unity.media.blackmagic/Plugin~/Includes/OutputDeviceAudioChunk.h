#pragma once

#include <algorithm>

#define USE_TEST_AUDIO_SIGNAL 0

namespace MediaBlackmagic
{
    typedef int32_t AudioSampleType;

    class AudioChunk final
    {
    public:
        void Resize(int count);
        void Consume(int count);
        void Release();
        void Reset();

        void SetLength(int count);
        int  GetCapacity() const;
        int  GetSampleCount() const;

        AudioSampleType* GetCurrentSamples() const;

    private:
        AudioSampleType* samples = nullptr;
        AudioSampleType* current = nullptr;
        int              sampleCount = 0;
        int              capacity = 0;
    };
}
