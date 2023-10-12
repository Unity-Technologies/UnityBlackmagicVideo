#pragma once

#include "DeckLinkOutputDevice.h"
#include <stdlib.h>

namespace MediaBlackmagic
{
    void DeckLinkOutputDevice::FeedAudioSampleFrames(const float* const  samples, const int sampleCount)
    {
        if (samples == nullptr || sampleCount <= 0)
            return;
        // We assume that sampleCount will always be the same amount, so
        // we won't create multiple chunks out of a single array passed in.
        // Ideally, we'd decide on a fixed max size for chunks and don't
        // trust that the caller will always call with a reasonable number
        // of chunks. In the setup where this is being developed, Unity
        // provides 2048 samples per call (1024 per channel, in a stereo
        // config).
        AudioChunk chunk;
        {
            std::lock_guard<std::mutex> lock(m_FreeAudioChunksMutex);
            if (m_FreeAudioChunks.empty())
                chunk.Resize(sampleCount);
            else
            {
                chunk = m_FreeAudioChunks.back();
                m_FreeAudioChunks.pop_back();
            }
        }

        if (chunk.GetCapacity() < sampleCount)
        {
            chunk.Resize(sampleCount);
        }
        else
        {
            chunk.Reset();
        }

        // Necessary to avoid the error "not enough arguments for function-like macro invocation 'max'"
#undef max 
        constexpr float maxIntInFloat = static_cast<float>(std::numeric_limits<AudioSampleType>::max());
        AudioSampleType* const chunkSamples = chunk.GetCurrentSamples();
        for (int i = 0; i < sampleCount; ++i)
        {
            const float value = std::min(std::max(-1.F, samples[i]), 1.F);
            chunkSamples[i] = static_cast<AudioSampleType>(value * maxIntInFloat);
        }
        chunk.SetLength(sampleCount);

        {
            std::lock_guard<std::mutex> lock(m_AudioChunksMutex);
            m_AudioChunks.push_back(chunk);
        }
    }

    HRESULT STDMETHODCALLTYPE DeckLinkOutputDevice::RenderAudioSamples(dlbool_t preroll)
    {
        uint32_t bufferedFrameCount = 0;
        if (m_Output->GetBufferedAudioSampleFrameCount(&bufferedFrameCount) != S_OK)
            return E_FAIL;

        if (bufferedFrameCount > kBufferedAudioLevel)
        {
            if (m_PrerollingAudio)
            {
                HRESULT res = m_Output->EndAudioPreroll();
                assert(res == S_OK);
                m_PrerollingAudio = false;
            }
            return S_OK;
        }

        // Fill buffer with some noise.
        uint32_t neededFrameCount = kBufferedAudioLevel - bufferedFrameCount;

#if USE_TEST_AUDIO_SIGNAL
        if (audioBuffer.empty())
        {
            // First time here. Fill the playback buffer with some audible signal that's
            // hopefully not too harsh.
            const std::size_t sampleFrameCount = audioBuffer.capacity() / audioChannelCount;
            for (std::size_t i = 0; i < sampleFrameCount; ++i)
            {
                bool even = (i % 100) < 50;
                int32_t value = even ? 5000000 : -500000;
                for (int j = 0; j < audioChannelCount; ++j)
                    audioBuffer.push_back(value);
            }
        }
        uint32_t writtenFrameCount = 0;
        HRESULT res = output->ScheduleAudioSamples(
            audioBuffer.data(), neededFrameCount, audioStreamTime, bmdAudioSampleRate48kHz,
            &writtenFrameCount);

        if (res == S_OK)
            audioStreamTime += writtenFrameCount;
#else
        uint32_t providedFrameCount = 0;
        while (providedFrameCount < neededFrameCount)
        {
            AudioChunk chunk;
            {
                std::lock_guard<std::mutex> lock(m_AudioChunksMutex);
                if (m_AudioChunks.empty())
                    break;
                chunk = m_AudioChunks.front();
                m_AudioChunks.pop_front();
            }

            uint32_t writtenFrameCount = 0;
            // FIXME: We're using a cumulated audioStreamTime so all chunks are abutting in the
            // output. But it should be up to the caller to provide the timestamp that comes
            // with the audio chunk (so that gaps are properly detectable).
            HRESULT res = m_Output->ScheduleAudioSamples(
                chunk.GetCurrentSamples(), chunk.GetSampleCount() / m_AudioChannelCount,
                m_AudioStreamTime, bmdAudioSampleRate48kHz, &writtenFrameCount);

            if (res != S_OK)
                break;

            providedFrameCount += writtenFrameCount;
            const int writtenSampleCount = writtenFrameCount * m_AudioChannelCount;
            if (writtenSampleCount < chunk.GetSampleCount())
            {
                // Chunk only partially used. Adjust pointer/size and put it back in the list
                // for the next call.
                chunk.Consume(writtenSampleCount);
                std::lock_guard<std::mutex> lock(m_AudioChunksMutex);
                m_AudioChunks.push_front(chunk);
            }
            else
            {
                std::lock_guard<std::mutex> lock(m_FreeAudioChunksMutex);
                m_FreeAudioChunks.push_back(chunk);
            }
        }

        m_AudioStreamTime += providedFrameCount;
#endif
        return S_OK;
    }

    bool DeckLinkOutputDevice::InitializeAudioOutput(const int prerollVideoFrameCount,
        const int channelCount,
        const int sampleRate)
    {
        if (sampleRate != 48000)
        {
            m_Error = "Blackmagic audio output only supports 48kHz. Received ";
            m_Error += std::to_string(sampleRate);
            return false;
        }

        m_Output->DisableAudioOutput();

        auto res = m_Output->SetAudioCallback(this);
        assert(res == S_OK);

        if (prerollVideoFrameCount > 0)
        {
            const float videoFrameDuration = static_cast<float>(m_FrameDuration) / static_cast<float>(m_TimeScale);
            const int sampleFramesPerVideoFrame = static_cast<int>(videoFrameDuration * sampleRate);
            const int samplesPerVideoFrame = sampleFramesPerVideoFrame * channelCount;
            const int audioBytesPerVideoFrame = samplesPerVideoFrame * sizeof(float);
            float* silence = (float*)alloca(audioBytesPerVideoFrame);
            memset(silence, 0, audioBytesPerVideoFrame);
            for (auto i = 0; i < prerollVideoFrameCount; ++i)
                FeedAudioSampleFrames(silence, samplesPerVideoFrame);
        }

        res = m_Output->EnableAudioOutput(
            bmdAudioSampleRate48kHz, bmdAudioSampleType32bitInteger, channelCount,
            bmdAudioOutputStreamContinuous);

        if (res == E_INVALIDARG)
        {
            m_Error = "Unsupported audio channel count: ";
            m_Error += std::to_string(channelCount);
            return false;
        }
        if (res == E_ACCESSDENIED)
        {
            m_Error = "Audio hardware not available.";
            return false;
        }
        if (res != S_OK)
        {
            m_Error = "Can't open audio output.";
            return false;
        }

        res = m_Output->BeginAudioPreroll();
        if (res != S_OK)
        {
            m_Error = "Can't preroll audio.";
            return false;
        }

        m_AudioChannelCount = channelCount;
        m_AudioBuffer.reserve(kBufferedAudioLevel * m_AudioChannelCount);
        return true;
    }

    void DeckLinkOutputDevice::ReleaseAudioOutput()
    {
        for (auto& chunk : m_AudioChunks)
            chunk.Release();
        m_AudioChunks.clear();

        for (auto& chunk : m_FreeAudioChunks)
            chunk.Release();
        m_FreeAudioChunks.clear();
    }
}
