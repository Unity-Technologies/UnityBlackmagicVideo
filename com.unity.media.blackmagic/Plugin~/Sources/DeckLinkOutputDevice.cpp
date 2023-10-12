#include <iostream>
#include "DeckLinkOutputDevice.h"
#include "DeckLinkDeviceUtilities.h"
#include "PluginUtils.h"

namespace MediaBlackmagic
{
    const std::string DeckLinkOutputDevice::k_FrameDisplayedLate = "Frame was displayed late.";
    const std::string DeckLinkOutputDevice::k_FrameDropped = "Frame was dropped.";
    const std::string DeckLinkOutputDevice::k_FrameFlushed = "Frame was flushed.";
    const std::string DeckLinkOutputDevice::k_FrameSucceeded = "Frame was completed.";

    DeckLinkOutputDevice::DeckLinkOutputDevice() :
        m_Frame(false, nullptr, m_ColorSpace),
        m_DisplayMode(nullptr),
        m_PixelFormat(bmdFormatUnspecified),
        m_ColorSpace(bmdColorspaceRec709),
        m_Output(nullptr),
        m_Index(-1),
        m_Initialized(false),
        m_FrameDuration(0),
        m_TimeScale(1),
        m_LateFrameCount(0),
        m_DroppedFrameCount(0),
        m_OutputFormatData(),
        m_RefCount(1),
        m_Error(""),
        m_AudioStreamTime(0),
        m_PrerollingAudio(false),
        m_AudioChannelCount(0),
        m_Queued(0),
        m_Completed(0),
        m_DefaultScheduleTime(0.0f),
        m_IsAsync(true),
        m_KeyingMode(EOutputKeyingMode::None),
        m_DeckLinkKeyer(nullptr),
        m_FrameErrorCallback(nullptr),
        m_FrameCompletedCallback(nullptr),
        m_Configuration(nullptr),
        m_UseGPUDirect(false),
        m_IsGPUDirectAvailable(false),
        m_Stopped(false)
#if _WIN64
        ,m_OutputGPUDirect(nullptr)
#endif
    {
#if _WIN64
        m_ThreadedMemcpy.Initialize();
#endif
//InitLog();
    }

    DeckLinkOutputDevice::~DeckLinkOutputDevice()
    {
#if _WIN64
        m_ThreadedMemcpy.Destroy();
#endif

        // Internal objects should have been released.
        assert(m_Output == nullptr);
        assert(m_DisplayMode == nullptr);
    }

    DeckLinkOutputDevice::IntPair DeckLinkOutputDevice::GetFrameDimensions() const
    {
        if (m_DisplayMode != nullptr)
        {
            return { m_DisplayMode->GetWidth(), m_DisplayMode->GetHeight() };
        }
        return { 1, 1 };
    }

    std::int64_t DeckLinkOutputDevice::GetFrameDuration() const
    {
        return flicksPerSecond * m_FrameDuration / m_TimeScale;
    }

    void DeckLinkOutputDevice::GetFrameRate(std::int32_t& numerator, std::int32_t& denominator) const
    {
        BMDTimeValue duration = 0;
        BMDTimeScale scale = 0;
        if (m_DisplayMode != nullptr && m_DisplayMode->GetFrameRate(&duration, &scale) != S_OK)
        {
            denominator = 0;
            numerator = 0;
            return;
        }
        denominator = static_cast<int32_t>(duration);
        numerator = static_cast<int32_t>(scale);
    }

    bool DeckLinkOutputDevice::IsProgressive() const
    {
        return (m_DisplayMode && m_DisplayMode->GetFieldDominance() == bmdProgressiveFrame);
    }

    bool DeckLinkOutputDevice::IsReferenceLocked() const
    {
        assert(m_Output != nullptr);
        BMDReferenceStatus stat;
        ShouldOK(m_Output->GetReferenceStatus(&stat));
        return stat & bmdReferenceLocked;
    }

    const std::string& DeckLinkOutputDevice::GetErrorString() const
    {
        return m_Error;
    }

    const std::string& DeckLinkOutputDevice::RetrievePixelFormat()
    {
        std::lock_guard<std::mutex> lock(m_Mutex);

        // Cache the Pixel Format value if it hasn't changed.
        if (!m_OutputFormatData.pixelFormatChanged)
        {
            return m_OutputFormatData.pixelFormatValue;
        }

        IDeckLinkStatus* deckLinkStatus = NULL;
        if (m_Output->QueryInterface(IID_IDeckLinkStatus, (void**)&deckLinkStatus) != S_OK)
        {
            return m_OutputFormatData.pixelFormatValue;
        }

        if (!GetDeckLinkStatus(deckLinkStatus,
            bmdDeckLinkStatusLastVideoOutputPixelFormat,
            m_OutputFormatData.pixelFormatValue).empty())
        {
            m_OutputFormatData.pixelFormatChanged = false;
        }

        deckLinkStatus->Release();

        return m_OutputFormatData.pixelFormatValue;
    }

    void DeckLinkOutputDevice::StartAsyncMode(
        int deviceIndex,
        int deviceSelected,
        BMDDisplayMode mode,
        int pixelFormat,
        int colorSpace,
        int transferFunction,
        int preroll,
        bool enableAudio,
        int audioChannelCount,
        int audioSampleRate,
        bool useGPUDirect)
    {
        assert(m_Output == nullptr);
        assert(m_DisplayMode == nullptr);
        assert(m_Frame.m_VideoFrame == nullptr);

        if (!InitializeOutput(
            deviceIndex,
            deviceSelected,
            mode,
            pixelFormat,
            colorSpace,
            transferFunction,
            preroll,
            enableAudio,
            audioChannelCount,
            audioSampleRate,
            useGPUDirect))
            return;
        m_IsAsync = true;

        // Prerolling
        if (preroll > 0)
        {
            std::lock_guard<std::mutex> lock(m_Mutex);

            auto frameUsedToPrerolled = m_OutputVideoFrameQueue.front();
            m_OutputVideoFrameQueue.push_back(frameUsedToPrerolled);
            m_OutputVideoFrameQueue.pop_front();
            m_Frame.m_VideoFrame = frameUsedToPrerolled;

            for (auto i = 0; i < preroll; i++)
            {
                const auto isHDR = m_ColorSpace == bmdDisplayModeColorspaceRec2020;
                if (isHDR)
                {
                    ScheduleFrame(&m_Frame);
                }
                else
                {
                    ScheduleFrame(m_Frame.m_VideoFrame);
                }
            }
        }

        if (m_PrerollingAudio)
        {
            m_Output->EndAudioPreroll();
            m_PrerollingAudio = false;
        }

        // Access denied: the SDI port is already used by another software.
        if (m_Output->StartScheduledPlayback(0, m_TimeScale, 1) != S_OK)
            return;

        m_Initialized = true;
    }

    void DeckLinkOutputDevice::StartManualMode(
        int deviceIndex,
        int deviceSelected,
        BMDDisplayMode mode,
        int pixelFormat,
        int colorSpace,
        int transferFunction,
        int preroll,
        bool enableAudio,
        int audioChannelCount,
        int audioSampleRate,
        bool useGPUDirect)
    {
        assert(m_Output == nullptr);
        assert(m_DisplayMode == nullptr);

        if (!InitializeOutput(
            deviceIndex,
            deviceSelected,
            mode,
            pixelFormat,
            colorSpace,
            transferFunction,
            preroll,
            enableAudio,
            audioChannelCount,
            audioSampleRate,
            useGPUDirect))
            return;
        m_IsAsync = false;

        //Prerolling
        if (preroll > 0)
        {
            auto newFrame = m_OutputVideoFrameQueue.front();
            m_OutputVideoFrameQueue.push_back(newFrame);
            m_OutputVideoFrameQueue.pop_front();

            if (newFrame == nullptr)
                return;

            for (auto i = 0; i < preroll; i++)
            {
                const auto isHDR = m_ColorSpace == bmdDisplayModeColorspaceRec2020;
                if (isHDR)
                {
                    auto newHDRFrame = new DeckLinkOutputDevice::HDRVideoFrame(true, newFrame, m_ColorSpace);
                    ScheduleFrame(newHDRFrame);

                    m_HDRFrames.push_back(newHDRFrame);
                }
                else
                {
                    ScheduleFrame(newFrame);
                }
            }
        }

        // Access denied: the SDI port is already used by another software.
        if (m_Output->StartScheduledPlayback(0, m_TimeScale, 1) != S_OK)
            return;

        m_Initialized = true;
    }

    void DeckLinkOutputDevice::Stop()
    {
        std::unique_lock<std::mutex> lock(m_Mutex);

        // First stop the output stream, so frame and displayMode may be released.
        if (m_Output != nullptr)
        {
            m_Output->StopScheduledPlayback(0, nullptr, 0);
            {
                // Wait for scheduled playback to complete
                m_PlaybackStoppedCondition.wait_for(lock, std::chrono::milliseconds(200), [this] { return m_Stopped == true; });
            }

            // TODO: There is a weird crash in the Blackmagic API when we are running our new Unit Tests
            // with the Test Runner Unity API on macOS. It never happens when we are running our device(s)
            // in 'Update In Editor' mode or in Playmode. Dirty fix right now is to not call the 2 methods below.
            // It's not a big problem because we are immediately Release the device, but we'll have to find a better way
            // to fix that.

            m_Output->DisableVideoOutput();
            m_Output->DisableAudioOutput();

            m_Output->FlushBufferedAudioSamples();
            m_Output->SetScheduledFrameCompletionCallback(nullptr);
            m_Output->SetAudioCallback(nullptr);
        }

        while (!m_OutputVideoFrameQueue.empty())
        {
            auto frame = m_OutputVideoFrameQueue.front();
            if (frame != nullptr)
            {
                // TODO: frames are currently not released correctly.
                // This ensures that the video frames are finally deallocated.

                int maxValue = frame->Release();
                while (maxValue > 0 && frame->Release() > 0)
                {
                    maxValue--;
                }
                frame = nullptr;
            }
            m_OutputVideoFrameQueue.pop_front();
        }

        auto it = m_HDRFrames.begin();

        while (it != m_HDRFrames.end())
        {
            it = m_HDRFrames.erase(it);
        }

#if _WIN64
        if (m_OutputGPUDirect != nullptr)
        {
            m_OutputGPUDirect->CleanupGPUDirectResources();
            delete m_OutputGPUDirect;
            m_OutputGPUDirect = nullptr;
        }
#endif

        if (m_DisplayMode != nullptr)
        {
            m_DisplayMode->Release();
            m_DisplayMode = nullptr;
        }

        if (m_Configuration != nullptr)
        {
            m_Configuration->Release();
            m_Configuration = nullptr;
        }

        DisableKeying();
        ReleaseAudioOutput();

        // We release frame and displayMode before the output object, to avoid leaks.
        if (m_Output != nullptr)
        {
            m_Output->Release();
            m_Output = nullptr;
        }

        m_Initialized = false;
    }

    void DeckLinkOutputDevice::FeedFrame(void* frameData, unsigned int timecode)
    {
        assert(m_Output != nullptr);
        assert(m_DisplayMode != nullptr);

        unsigned int count;
        m_Output->GetBufferedVideoFrameCount(&count);

        if ((!m_Error.empty() && (count > kMaxBufferedFrames)) || m_Stopped)
            return;

        const auto isHDR = m_ColorSpace == bmdDisplayModeColorspaceRec2020;

        if (!isHDR)
        {
            FeedFrameSDR(frameData, timecode);
        }
        else
        {
            FeedFrameHDR(frameData, timecode);
        }
    }

    void DeckLinkOutputDevice::FeedFrameSDR(void* frameData, unsigned int timecode)
    {
        std::unique_lock<std::mutex> lock(m_Mutex);

        auto newFrame = m_OutputVideoFrameQueue.front();
        m_OutputVideoFrameQueue.push_back(newFrame);
        m_OutputVideoFrameQueue.pop_front();

        if (newFrame == nullptr)
            return;

        newFrame->SetFlags(0);
        SetTimecode(newFrame, timecode);

        const auto width = m_DisplayMode->GetWidth();
        const auto height = m_DisplayMode->GetHeight();
        void* pointer = nullptr;

        ShouldOK(newFrame->GetBytes(&pointer));
        const std::uint32_t byteLen = GetFrameByteLength(width) * height;

#if _WIN64
        if (m_OutputGPUDirect != nullptr && m_IsGPUDirectAvailable)
        {
            m_OutputGPUDirect->StartGPUSynchronization(frameData, pointer);
        }
        else if (m_OutputGPUDirect == nullptr)
        {
            m_ThreadedMemcpy.MemcpyOperation(pointer, frameData, byteLen);
        }
        else
        {
            if (m_FrameErrorCallback != nullptr)
                m_FrameErrorCallback(m_Index, "GPUDirect initialization failed.", EDeviceStatus::Error);
            return;
        }
#else
        std::memcpy(pointer, frameData, byteLen);
#endif

        if (IsAsyncMode())
        {
            // Async mode: Replace the frame_ object with it.
            m_Frame.m_VideoFrame = newFrame;
        }
        else
        {
            // Note: We don't have to use the mutex because nothing here
            // conflicts with the completion callback.

            unsigned int count;
            m_Output->GetBufferedVideoFrameCount(&count);

            if (count < kMaxBufferedFrames)
            {
                // Manual mode: Immediately schedule it.
                ScheduleFrame(newFrame);
            }
            else
            {
                // We shouldn't push too many frames to the scheduler.
                if (m_FrameErrorCallback != nullptr && count > kMaxBufferedFrames)
                {
                    m_FrameErrorCallback(m_Index, "Overqueuing frames (not pushed).", EDeviceStatus::Warning);
                }
            }
        }

#if _WIN64
        if (m_OutputGPUDirect != nullptr && m_IsGPUDirectAvailable)
        {
            m_OutputGPUDirect->EndGPUSynchronization(pointer);
        }
#endif
    }

    void DeckLinkOutputDevice::FeedFrameHDR(void* frameData, unsigned int timecode)
    {
        std::unique_lock<std::mutex> lock(m_Mutex);

        auto newMutableFrame = m_OutputVideoFrameQueue.front();
        m_OutputVideoFrameQueue.push_back(newMutableFrame);
        m_OutputVideoFrameQueue.pop_front();

        if (newMutableFrame == nullptr)
            return;

        newMutableFrame->SetFlags(0);
        SetTimecode(newMutableFrame, timecode);

        // Allocate a new frame for the fed data.
        auto newFrame = new DeckLinkOutputDevice::HDRVideoFrame(true, newMutableFrame, m_ColorSpace);
        if (newFrame->m_VideoFrame == nullptr)
        {
            delete newFrame;
            return;
        }

        if (m_HDRFrames.size() > kMaxBufferedFrames)
        {
            auto it = m_HDRFrames.begin();
            while (it != m_HDRFrames.end())
            {
                it = m_HDRFrames.erase(it);
            }
        }

        m_HDRFrames.push_back(newFrame);


        const auto width = m_DisplayMode->GetWidth();
        const auto height = m_DisplayMode->GetHeight();
        void* pointer = nullptr;
        
        ShouldOK(newFrame->GetBytes(&pointer));
        const std::uint32_t byteLen = GetFrameByteLength(width) * height;

#if _WIN64
        if (m_OutputGPUDirect != nullptr && m_IsGPUDirectAvailable)
        {
            m_OutputGPUDirect->StartGPUSynchronization(frameData, pointer);
        }
        else if (m_OutputGPUDirect == nullptr)
        {
            m_ThreadedMemcpy.MemcpyOperation(pointer, frameData, byteLen);
        }
        else
        {
            if (m_FrameErrorCallback != nullptr)
                m_FrameErrorCallback(m_Index, "GPUDirect initialization failed.", EDeviceStatus::Error);
            return;
        }
#else
        std::memcpy(pointer, frameData, byteLen);
#endif

        if (IsAsyncMode())
        {
            m_Frame.m_VideoFrame = newFrame->m_VideoFrame;
        }
        else
        {
            // Note: We don't have to use the mutex because nothing here
            // conflicts with the completion callback.

            unsigned int count;
            m_Output->GetBufferedVideoFrameCount(&count);

            if (count < kMaxBufferedFrames)
            {
                // Manual mode: Immediately schedule it.
                ScheduleFrame(newFrame);
            }
            else
            {
                // We shouldn't push too many frames to the scheduler.
                if (m_FrameErrorCallback != nullptr && count > kMaxBufferedFrames)
                {
                    m_FrameErrorCallback(m_Index, "Overqueuing frames (not pushed).", EDeviceStatus::Warning);
                }
            }
        }

#if _WIN64
        if (m_OutputGPUDirect != nullptr && m_IsGPUDirectAvailable)
        {
            m_OutputGPUDirect->EndGPUSynchronization(pointer);
        }
#endif
    }

    void DeckLinkOutputDevice::WaitFrameCompletion(std::int64_t frameNumber)
    {
        // Wait for completion of a specified frame.
        std::unique_lock<std::mutex> lock(m_Mutex);
        auto res = m_Condition.wait_for(
            lock, std::chrono::milliseconds(200),
            [=]() { return m_Completed >= frameNumber; }
        );

        if (!res && m_FrameErrorCallback != nullptr)
        {
            m_FrameErrorCallback(m_Index, "Failed to synchronize to output refreshing.", EDeviceStatus::Error);
        }
    }

    HRESULT STDMETHODCALLTYPE DeckLinkOutputDevice::QueryInterface(REFIID iid, LPVOID* ppv)
    {
        if (iid == IID_IUnknown)
        {
            *ppv = this;
            AddRef();
            return S_OK;
        }

        if (iid == IID_IDeckLinkVideoOutputCallback)
        {
            *ppv = (IDeckLinkVideoOutputCallback*)this;
            AddRef();
            return S_OK;
        }

        if (iid == IID_IDeckLinkAudioOutputCallback)
        {
            *ppv = (IDeckLinkAudioOutputCallback*)this;
            AddRef();
            return S_OK;
        }

        *ppv = nullptr;
        return E_NOINTERFACE;
    }

    ULONG STDMETHODCALLTYPE DeckLinkOutputDevice::AddRef()
    {
        return m_RefCount.fetch_add(1);
    }

    ULONG STDMETHODCALLTYPE DeckLinkOutputDevice::Release()
    {
        auto val = m_RefCount.fetch_sub(1);
        if (val == 1)
            delete this;
        return val;
    }

    std::uint32_t DeckLinkOutputDevice::GetBackingFrameByteWidth() const
    {
        assert(m_DisplayMode != nullptr);
        return GetFrameByteLength(m_DisplayMode->GetWidth());
    }

    std::uint32_t DeckLinkOutputDevice::GetBackingFrameByteHeight() const
    {
        assert(m_DisplayMode != nullptr);
        return m_DisplayMode->GetHeight();
    }

    std::uint32_t DeckLinkOutputDevice::GetBackingFrameByteDepth() const
    {
        assert(m_DisplayMode != nullptr);
        switch (m_PixelFormat) {
        default:
        case bmdFormatUnspecified:
            assert(false); // Invalid Pixel Format
            return 0;
        case bmdFormat8BitYUV:      // UYVY
        case bmdFormat10BitYUV:     // V210
        case bmdFormat8BitARGB:     // ARGB32
        case bmdFormat8BitBGRA:     // BGRA32
        case bmdFormat10BitRGB:     // R210
        case bmdFormat10BitRGBXLE:  // R10L
        case bmdFormat10BitRGBX:    // R10B
        case bmdFormat12BitRGB:     // R12B
        case bmdFormat12BitRGBLE:   // R12L
            return 4; // RGBA32 (R8G8B8A8)
        }
    }

    std::uint32_t DeckLinkOutputDevice::GetFrameByteLength(const std::uint32_t width) const
    {
        std::uint32_t ret = 0;
        switch (m_PixelFormat) {
        case bmdFormat8BitYUV:
            ret = width * 2;
            break;
        case bmdFormat8BitARGB:
        case bmdFormat8BitBGRA:
            ret = width * 4;
            break;
        case bmdFormat10BitYUV:
        {
            // 6 component per 4 channels, 1 byte per channel
            const std::uint32_t nbComponents = 6;
            const std::uint32_t nbChannels = 4;
            const std::uint32_t bytesPerPixel = 4;
            ret = (width / nbComponents) * nbChannels * bytesPerPixel;
            // Padding
            const std::uint32_t nbPaddingBytes = 128;
            ret = ret % nbPaddingBytes != 0 ? ((ret / nbPaddingBytes) + 1) * nbPaddingBytes : ret;
        }
        case bmdFormat10BitRGB:
        case bmdFormat10BitRGBXLE:
        case bmdFormat10BitRGBX:
        {
            const std::uint32_t nbBytesPerPixel = 4;
            ret = width * nbBytesPerPixel;
            // Padding
            const std::uint32_t nbPaddingBytes = 256;
            ret = ret % nbPaddingBytes != 0 ? ((ret / nbPaddingBytes) + 1) * nbPaddingBytes : ret;
        }
        case bmdFormat12BitRGB:
        case bmdFormat12BitRGBLE:
        {
            const std::uint32_t nbWordsPerBlock = 9;
            const std::uint32_t nbPixelsPerBlock = 8;
            const std::uint32_t nbBytesPerWord = 4;
            ret = (width * nbWordsPerBlock * nbBytesPerWord) / nbPixelsPerBlock;
        }
        break;
        default:
            assert(!"Not Implemented!");
            break;
        }
        return ret;
    }

    HRESULT STDMETHODCALLTYPE
        DeckLinkOutputDevice::ScheduledFrameCompleted(IDeckLinkVideoFrame* completedFrame,
            BMDOutputFrameCompletionResult result)
    {
        auto cbValid = m_FrameErrorCallback != nullptr;

        switch (result)
        {
        case bmdOutputFrameDisplayedLate:
            if (cbValid) m_FrameErrorCallback(m_Index, k_FrameDisplayedLate.c_str(), EDeviceStatus::Warning);
            m_LateFrameCount++;
            break;
        case bmdOutputFrameDropped:
            if (cbValid) m_FrameErrorCallback(m_Index, k_FrameDropped.c_str(), EDeviceStatus::Error);
            m_DroppedFrameCount++;
            break;
        case bmdOutputFrameFlushed:
            if (cbValid) m_FrameErrorCallback(m_Index, k_FrameFlushed.c_str(), EDeviceStatus::Error);
            break;
        default:
            if (cbValid) m_FrameErrorCallback(m_Index, k_FrameSucceeded.c_str(), EDeviceStatus::Ok);
            break;
        }

        // Increment the frame count and notify the main thread.
        if (m_FrameCompletedCallback != nullptr)
        {
            m_FrameCompletedCallback(m_Index, m_Completed);
        }

        m_Completed++;
        m_Condition.notify_all();

        // Async mode: Schedule the next frame.
        if (IsAsyncMode() && !m_Stopped)
        {
            std::lock_guard<std::mutex> lock(m_Mutex);
            const auto isHDR = m_ColorSpace == bmdDisplayModeColorspaceRec2020;
            if (isHDR)
            {
                ScheduleFrame(&m_Frame);
            }
            else
            {
                ScheduleFrame(m_Frame.m_VideoFrame);
            }
#if _WIN64
            if (m_OutputGPUDirect != nullptr && !m_IsGPUDirectAvailable && cbValid)
            {
                m_FrameErrorCallback(m_Index, "GPUDirect initialization failed.", EDeviceStatus::Error);
            }
#endif
        }

        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE DeckLinkOutputDevice::ScheduledPlaybackHasStopped()
    {
        {
            std::lock_guard<std::mutex> lock(m_Mutex);
            m_Stopped = true;
        }
        m_PlaybackStoppedCondition.notify_one();
        return S_OK;
    }

    IDeckLinkMutableVideoFrame* DeckLinkOutputDevice::AllocateFrame()
    {
        auto width = m_DisplayMode->GetWidth();
        auto height = m_DisplayMode->GetHeight();
        IDeckLinkMutableVideoFrame* frame = nullptr;

        int flags = bmdFrameFlagDefault;
        if (m_ColorSpace == bmdDisplayModeColorspaceRec2020)
        {
            flags = bmdFrameContainsHDRMetadata;
        }

        auto res = m_Output->CreateVideoFrame(width, height, GetFrameByteLength(width),
            m_PixelFormat, flags, &frame);
        //assert(res == S_OK && frame != nullptr);
      return frame;
    }

    void DeckLinkOutputDevice::CopyFrameData(IDeckLinkMutableVideoFrame* frame, const void* data)
    {
        auto width = m_DisplayMode->GetWidth();
        auto height = m_DisplayMode->GetHeight();
        void* pointer = nullptr;

        ShouldOK(frame->GetBytes(&pointer));
        const std::uint32_t byteLen = GetFrameByteLength(width) * height;

#if _WIN64
        m_ThreadedMemcpy.MemcpyOperation(pointer, data, byteLen);
#else
        std::memcpy(pointer, data, byteLen);
#endif
    }

    void DeckLinkOutputDevice::SetTimecode(IDeckLinkMutableVideoFrame* frame,
        unsigned int timecode) const
    {
        // Extract time components from a given BCD value.
        auto h = ((timecode >> 28) & 0x3U) * 10 + ((timecode >> 24) & 0xfU);
        auto m = ((timecode >> 20) & 0x7U) * 10 + ((timecode >> 16) & 0xfU);
        auto s = ((timecode >> 12) & 0x7U) * 10 + ((timecode >> 8) & 0xfU);
        auto f = ((timecode >> 4) & 0x3U) * 10 + ((timecode) & 0xfU);

        auto even = (timecode & 0x80U) != 0; // Even field flag
        auto drop = (timecode * 0x40U) != 0; // Drop frame timecode flag

        frame->SetTimecodeFromComponents(even ? bmdTimecodeRP188VITC2 : bmdTimecodeRP188VITC1,
            h, m, s, f,
            drop ? bmdTimecodeIsDropFrame : bmdTimecodeFlagDefault);
    }

    void DeckLinkOutputDevice::ScheduleFrame(IDeckLinkVideoFrame* frame)
    {
        m_Queued = m_Queued + static_cast<int>(m_DefaultScheduleTime);
        auto time = m_FrameDuration * m_Queued++;

        if (m_Output->ScheduleVideoFrame(frame, time, m_FrameDuration, m_TimeScale) == S_FALSE)
        {
            if (m_FrameErrorCallback != nullptr)
            {
                m_FrameErrorCallback(m_Index, "Failed to schedule a video frame.", EDeviceStatus::Error);
            }
        }
    }

    bool DeckLinkOutputDevice::InitializeOutput(
        int deviceIndex,
        int deviceSelected,
        BMDDisplayMode mode,
        int pixelFormat,
        int colorSpace,
        int transferFunction,
        int preroll,
        bool enableAudio,
        int audioChannelCount,
        int audioSampleRate,
        bool useGPUDirect)
    {
        // Set frame allocation width
        m_PixelFormat = (BMDPixelFormat)pixelFormat;
        m_ColorSpace = (BMDDisplayModeFlags)colorSpace;
        m_UseGPUDirect = useGPUDirect;
        HDRVideoFrame::SetTransferFunction(transferFunction);

        // Device iterator
        IDeckLinkIterator* iterator;
        auto res = GetDeckLinkIterator(&iterator);

        if (res != S_OK)
        {
            m_Error = "Could not find DeckLink driver.";
            return false;
        }

        // Iterate until reaching the specified index.
        IDeckLink* device = nullptr;
        for (auto i = 0; i <= deviceSelected; i++)
        {
            if (device != nullptr)
                device->Release();
            res = iterator->Next(&device);

            if (res != S_OK)
            {
                m_Error = "Invalid device index.";
                iterator->Release();
                return false;
            }
        }

        iterator->Release(); // The iterator is no longer needed.

        // Output interface of the specified device
        res = device->QueryInterface(
            IID_IDeckLinkOutput,
            reinterpret_cast<void**>(&m_Output)
        );

        device->Release(); // The device object is no longer needed.

        if (res != S_OK)
        {
            m_Error = "Device has no output.";
            return false;
        }

        // Display mode iterator
        IDeckLinkDisplayModeIterator* dmIterator;
        res = m_Output->GetDisplayModeIterator(&dmIterator);
        assert(res == S_OK);

        auto displayModeFound = false;
        while (dmIterator->Next(&m_DisplayMode) == S_OK)
        {
            if (m_DisplayMode->GetDisplayMode() == mode)
            {
                displayModeFound = true;
                break;
            }
            else
            {
                m_DisplayMode->Release();
            }
        }

        if (!displayModeFound)
        {
            if (m_FrameErrorCallback != nullptr)
            {
                m_Error = "Unsupported display mode (video format, framerate, and scanning mode combination).";
                m_FrameErrorCallback(m_Index, m_Error.c_str(), EDeviceStatus::Error);
            }

            dmIterator->Release();
            return false;
        }

        // Get the frame rate defined in the display mode.
        res = m_DisplayMode->GetFrameRate(&m_FrameDuration, &m_TimeScale);
        assert(res == S_OK);

        dmIterator->Release(); // The iterator is no longer needed.

#if _WIN64
        if (useGPUDirect)
        {
            m_OutputGPUDirect = new DeckLinkOutputGPUDirectDevice(m_Output);
            m_IsGPUDirectAvailable = m_OutputGPUDirect->InitializeMemoryAllocator();
        }
#endif

        // Enable the video output.
        res = m_Output->EnableVideoOutput(m_DisplayMode->GetDisplayMode(), bmdVideoOutputRP188);

        for (uint32_t i = 0; i < kAllocatedBufferedFrames; i++)
        {
            auto newFrame = AllocateFrame();
            if (newFrame == nullptr)
            {
                WriteFileDebug("m_Output->CreateVideoFrame failed.\n");
                break;
            }

            newFrame->AddRef();
            m_OutputVideoFrameQueue.push_back(newFrame);
        }

        // Set this object as a frame completion callback.
        res = m_Output->SetScheduledFrameCompletionCallback(this);
        assert(res == S_OK);

        if (res != S_OK)
        {
            if (m_FrameErrorCallback != nullptr)
            {
                m_Error = "Can't open output device (possibly already used).";
                m_FrameErrorCallback(m_Index, m_Error.c_str(), EDeviceStatus::Error);
            }
            return false;
        }

        if (!enableAudio || InitializeAudioOutput(preroll, audioChannelCount, audioSampleRate))
        {
            m_Index = deviceIndex;
            return true;
        }

        m_Output->DisableVideoOutput();
        return false;
    }

    bool DeckLinkOutputDevice::IsValidConfiguration() const
    {
        dlbool_t isSupported(false);
        if (m_Output != nullptr && m_DisplayMode != nullptr)
        {
            auto mode = bmdSupportedVideoModeDefault;
            if (m_KeyingMode != EOutputKeyingMode::None)
            {
                mode = bmdSupportedVideoModeKeying;
            }

            auto res = m_Output->DoesSupportVideoMode(
                bmdVideoConnectionUnspecified,
                m_DisplayMode->GetDisplayMode(),
                m_PixelFormat,
                bmdNoVideoOutputConversion,
                mode,
                NULL,
                &isSupported
            );
            assert(res == S_OK);
        }
        return isSupported;
    }

    bool DeckLinkOutputDevice::InitializeKeyerParameters(const EOutputKeyingMode mode)
    {
        auto success = S_FALSE;
        auto keyingModeInitialized = false;
        m_KeyingMode = mode;

        if (m_DeckLinkKeyer == nullptr && m_Output != nullptr && m_Output->QueryInterface(IID_IDeckLinkKeyer, (void**)&m_DeckLinkKeyer) == S_OK)
        {
            keyingModeInitialized = true;
            success = m_DeckLinkKeyer->Enable(m_KeyingMode == EOutputKeyingMode::External);
            assert(S_OK == success);

            if (success == S_OK)
            {
                success = m_DeckLinkKeyer->SetLevel(255);
                assert(S_OK == success);
            }
        }

        return keyingModeInitialized && success == S_OK;
    }

    bool DeckLinkOutputDevice::SupportsKeying(const EOutputKeyingMode keying)
    {
        auto ret = SupportsOutputKeying(reinterpret_cast<IDeckLink*>(m_Output), keying);

        dlbool_t supp;
        auto doesSupport = !m_Output->DoesSupportVideoMode(
            bmdVideoConnectionUnspecified,
            m_DisplayMode->GetDisplayMode(),
            m_PixelFormat,
            bmdNoVideoOutputConversion,
            bmdSupportedVideoModeKeying,
            nullptr,
            &supp);

        return ret && doesSupport && supp;
    }

    bool DeckLinkOutputDevice::ChangeKeyingMode(const EOutputKeyingMode mode)
    {
        auto success = S_FALSE;

        if (m_DeckLinkKeyer == nullptr && m_Output->QueryInterface(IID_IDeckLinkKeyer, (void**)&m_DeckLinkKeyer) != S_OK)
            return false;

        m_KeyingMode = mode;
        if (m_Output != nullptr && m_DeckLinkKeyer != nullptr && SupportsKeying(mode))
        {
            success = m_DeckLinkKeyer->Enable(m_KeyingMode == EOutputKeyingMode::External);
            assert(S_OK == success);

            if (success == S_OK)
            {
                success = m_DeckLinkKeyer->SetLevel(255);
                assert(S_OK == success);
            }
        }
        return success == S_OK;
    }

    bool DeckLinkOutputDevice::DisableKeying()
    {
        auto keyingModeDestroyed = false;

        if (m_DeckLinkKeyer != nullptr)
        {
            m_DeckLinkKeyer->Disable();
            m_DeckLinkKeyer->Release();
            m_DeckLinkKeyer = nullptr;
            keyingModeDestroyed = true;
            m_KeyingMode = EOutputKeyingMode::None;
        }
        return keyingModeDestroyed;
    }

    bool DeckLinkOutputDevice::IsSupportedLinkMode(const EOutputLinkMode mode)
    {
        return IsLinkModeSupported(reinterpret_cast<IDeckLink*>(m_Output), mode);
    }

    bool DeckLinkOutputDevice::SetLinkConfiguration(const EOutputLinkMode mode)
    {
        auto success = false;
        _BMDLinkConfiguration bmdMode;

        if (!GetBMDLinkMode(mode, bmdMode))
            goto cleanup;

        if (m_Configuration == nullptr &&
            (m_Output->QueryInterface(IID_IDeckLinkConfiguration, (void**)&m_Configuration) != S_OK ||
                m_Configuration == nullptr))
            goto cleanup;

        if (m_Configuration->SetInt(bmdDeckLinkConfigSDIOutputLinkConfiguration, bmdMode) == S_OK)
            success = true;

    cleanup:
        return success;
    }

    // HDRVideoFrame wrapper
    MediaBlackmagic::HDRMetadata DeckLinkOutputDevice::HDRVideoFrame::m_Metadata;

    DeckLinkOutputDevice::HDRVideoFrame::HDRVideoFrame(const bool handleAllocation, IDeckLinkMutableVideoFrame* videoFrame, BMDDisplayModeFlags& deviceColorspace) :
        m_HandleAllocation(handleAllocation),
        m_VideoFrame(videoFrame),
        m_DeviceColorspace(deviceColorspace)
    {
        m_Metadata.EOTF = static_cast<uint32_t>(EOTF::HLG);
        m_Metadata.referencePrimaries = kDefaultRec2020Colorimetrics;
        m_Metadata.maxDisplayMasteringLuminance = kDefaultMaxDisplayMasteringLuminance;
        m_Metadata.minDisplayMasteringLuminance = kDefaultMinDisplayMasteringLuminance;
        m_Metadata.maxCLL = kDefaultMaxCLL;
        m_Metadata.maxFALL = kDefaultMaxFALL;
    }

    void DeckLinkOutputDevice::HDRVideoFrame::SetTransferFunction(const uint32_t eotf)
    {
        m_Metadata.EOTF = eotf;
    }

    // IUnknown interface
#define CompareREFIID(iid1, iid2) (memcmp(&iid1, &iid2, sizeof(REFIID)) == 0)
    HRESULT STDMETHODCALLTYPE DeckLinkOutputDevice::HDRVideoFrame::QueryInterface(REFIID iid, LPVOID* ppv)
    {
        auto iunknown = IID_IUnknown;
        auto ret = E_NOINTERFACE;

        if (ppv == nullptr)
        {
            return E_INVALIDARG;
        }

        if (CompareREFIID(iid, iunknown))
        {
            *ppv = static_cast<IDeckLinkVideoFrame*>(this);
            ret = S_OK;
        }
        else if (CompareREFIID(iid, IID_IDeckLinkVideoFrame))
        {
            *ppv = static_cast<IDeckLinkVideoFrame*>(this);
            ret = S_OK;
        }
        else if (CompareREFIID(iid, IID_IDeckLinkVideoFrameMetadataExtensions))
        {
            *ppv = static_cast<IDeckLinkVideoFrameMetadataExtensions*>(this);
            ret = S_OK;
        }
        else
        {
            *ppv = nullptr;
            ret = E_INVALIDARG;
        }

        if (ret == E_INVALIDARG)
        {
            if (ppv != nullptr && m_VideoFrame != nullptr)
            {
                return m_VideoFrame->QueryInterface(iid, ppv);
            }
        }
        else
        {
            AddRef();
        }

        return ret;
    }

    ULONG STDMETHODCALLTYPE DeckLinkOutputDevice::HDRVideoFrame::AddRef()
    {
        return m_VideoFrame->AddRef();
    }

    ULONG STDMETHODCALLTYPE DeckLinkOutputDevice::HDRVideoFrame::Release()
    {
        ULONG ret = 0;

        // TO DO: this code is commented due to leak and crash issue in Async Mode & BT.2020
        // In the future, we should investigate a more efficient way to manage our own frame pool.

        /*if (m_VideoFrame != nullptr)
        {
            ret = m_VideoFrame->Release();
        }
        if (ret == 0)
        {
            m_VideoFrame = nullptr;
            if (m_HandleAllocation) delete this;
        }*/
        return ret;
    }

    // IDeckLinkVideoFrame interface
    long STDMETHODCALLTYPE DeckLinkOutputDevice::HDRVideoFrame::GetWidth()
    {
        return m_VideoFrame->GetWidth();
    }

    long STDMETHODCALLTYPE DeckLinkOutputDevice::HDRVideoFrame::GetHeight()
    {
        return m_VideoFrame->GetHeight();
    }

    long STDMETHODCALLTYPE DeckLinkOutputDevice::HDRVideoFrame::GetRowBytes()
    {
        return m_VideoFrame->GetRowBytes();
    }

    BMDPixelFormat STDMETHODCALLTYPE DeckLinkOutputDevice::HDRVideoFrame::GetPixelFormat()
    {
        return m_VideoFrame->GetPixelFormat();
    }

    BMDFrameFlags STDMETHODCALLTYPE DeckLinkOutputDevice::HDRVideoFrame::GetFlags()
    {
        auto ret = m_VideoFrame->GetFlags();
        if (m_DeviceColorspace == bmdDisplayModeColorspaceRec2020)
        {
            ret |= bmdFrameContainsHDRMetadata;
        }
        return ret;
    }

    HRESULT STDMETHODCALLTYPE DeckLinkOutputDevice::HDRVideoFrame::GetBytes(void** buffer)
    {
        return m_VideoFrame->GetBytes(buffer);
    }

    HRESULT STDMETHODCALLTYPE DeckLinkOutputDevice::HDRVideoFrame::GetTimecode(const BMDTimecodeFormat format, IDeckLinkTimecode** timecode)
    {
        return m_VideoFrame->GetTimecode(format, timecode);
    }

    HRESULT STDMETHODCALLTYPE DeckLinkOutputDevice::HDRVideoFrame::GetAncillaryData(IDeckLinkVideoFrameAncillary** ancillary)
    {
        return m_VideoFrame->GetAncillaryData(ancillary);
    }

    /// IDeckLinkVideoFrameMetadataExtensions methods
    HRESULT DeckLinkOutputDevice::HDRVideoFrame::GetInt(const BMDDeckLinkFrameMetadataID metadataID, int64_t* value)
    {
        auto result = S_OK;

        switch (metadataID)
        {
        case bmdDeckLinkFrameMetadataHDRElectroOpticalTransferFunc:
            *value = static_cast<int64_t>(m_Metadata.EOTF);
            break;

        case bmdDeckLinkFrameMetadataColorspace:
            *value = 0;
            if (m_DeviceColorspace == bmdDisplayModeColorspaceRec601)
                *value = bmdColorspaceRec601;
            else if (m_DeviceColorspace == bmdDisplayModeColorspaceRec709)
                *value = bmdColorspaceRec709;
            else if (m_DeviceColorspace == bmdDisplayModeColorspaceRec2020)
                *value = bmdColorspaceRec2020;
            break;

        default:
            value = nullptr;
            result = E_INVALIDARG;
        }

        return result;
    }

    HRESULT DeckLinkOutputDevice::HDRVideoFrame::GetFloat(const BMDDeckLinkFrameMetadataID metadataID, double* value)
    {
        auto result = S_OK;

        switch (metadataID)
        {
        case bmdDeckLinkFrameMetadataHDRDisplayPrimariesRedX:
            *value = m_Metadata.referencePrimaries.RedX;
            break;

        case bmdDeckLinkFrameMetadataHDRDisplayPrimariesRedY:
            *value = m_Metadata.referencePrimaries.RedY;
            break;

        case bmdDeckLinkFrameMetadataHDRDisplayPrimariesGreenX:
            *value = m_Metadata.referencePrimaries.GreenX;
            break;

        case bmdDeckLinkFrameMetadataHDRDisplayPrimariesGreenY:
            *value = m_Metadata.referencePrimaries.GreenY;
            break;

        case bmdDeckLinkFrameMetadataHDRDisplayPrimariesBlueX:
            *value = m_Metadata.referencePrimaries.BlueX;
            break;

        case bmdDeckLinkFrameMetadataHDRDisplayPrimariesBlueY:
            *value = m_Metadata.referencePrimaries.BlueY;
            break;

        case bmdDeckLinkFrameMetadataHDRWhitePointX:
            *value = m_Metadata.referencePrimaries.WhiteX;
            break;

        case bmdDeckLinkFrameMetadataHDRWhitePointY:
            *value = m_Metadata.referencePrimaries.WhiteY;
            break;

        case bmdDeckLinkFrameMetadataHDRMaxDisplayMasteringLuminance:
            *value = m_Metadata.maxDisplayMasteringLuminance;
            break;

        case bmdDeckLinkFrameMetadataHDRMinDisplayMasteringLuminance:
            *value = m_Metadata.minDisplayMasteringLuminance;
            break;

        case bmdDeckLinkFrameMetadataHDRMaximumContentLightLevel:
            *value = m_Metadata.maxCLL;
            break;

        case bmdDeckLinkFrameMetadataHDRMaximumFrameAverageLightLevel:
            *value = m_Metadata.maxFALL;
            break;

        default:
            value = nullptr;
            result = E_INVALIDARG;
        }

        return result;
    }

    HRESULT DeckLinkOutputDevice::HDRVideoFrame::GetFlag(const BMDDeckLinkFrameMetadataID metadataID, dlbool_t* value)
    {
        return E_INVALIDARG;
    }

    HRESULT DeckLinkOutputDevice::HDRVideoFrame::GetString(const BMDDeckLinkFrameMetadataID metadataID, dlstring_t* value)
    {
        return E_INVALIDARG;
    }

    HRESULT    DeckLinkOutputDevice::HDRVideoFrame::GetBytes(const BMDDeckLinkFrameMetadataID metadataID, void* buffer, uint32_t* bufferSize)
    {
        *bufferSize = 0;
        return E_INVALIDARG;
    }

#if _WIN64
    void DeckLinkOutputDevice::InitializeGPUDirectResources(ID3D11Device* d3d11Device, ID3D11DeviceContext* d3d11Context)
    {
        if (m_OutputGPUDirect != nullptr)
        {
            const auto dimensions = GetFrameDimensions();
            const auto width = std::get<0>(dimensions);
            const auto height = std::get<1>(dimensions);
            const auto widthByteLength = GetFrameByteLength(width) / 4;

            m_OutputGPUDirect->InitializeAPIDevices(d3d11Device, d3d11Context);

            if (m_IsGPUDirectAvailable)
            {
                auto initTextures = m_OutputGPUDirect->InitializeGPUDirectTextures(widthByteLength, height);
                auto initAPI = m_OutputGPUDirect->InitializeGPUDirect(widthByteLength, height, widthByteLength);

                m_IsGPUDirectAvailable = initTextures && initAPI;
            }
        }
    }
#endif
}
