#pragma once

#include "DeckLinkInputDevice.h"

namespace MediaBlackmagic
{
    DeckLinkInputDevice::FrameErrorCallback DeckLinkInputDevice::s_FrameErrorCallback = nullptr;
    DeckLinkInputDevice::VideoFormatChangedCallback DeckLinkInputDevice::s_VideoFormatChangedCallback = nullptr;
    DeckLinkInputDevice::FrameArrivedCallback DeckLinkInputDevice::s_FrameArrivedCallback = nullptr;

    DeckLinkInputDevice::DeckLinkInputDevice() :
        m_Index(-1),
        m_Initialized(false),
        m_RefCount(1),
        m_Input(nullptr),
        m_Configuration(nullptr),
        m_DisplayMode(nullptr),
        m_DesiredPixelFormat(bmdFormatUnspecified),
        m_CurrentPixelFormat(bmdFormatUnspecified),
        m_InvalidPixelFormat(false),
        m_ColorSpace(0),
        m_HasInputSource(false),
        m_TextureData(nullptr),
        m_GraphicsAPI(kUnityGfxRendererD3D11)
    {
    }

    DeckLinkInputDevice::~DeckLinkInputDevice()
    {
        m_Index = -1;

        // Internal objects should have been released.
        assert(m_Input == nullptr);
        assert(m_DisplayMode == nullptr);
    }

    bool DeckLinkInputDevice::GetHasInputSource() const
    {
        return m_HasInputSource;
    }

    void DeckLinkInputDevice::SetTextureData(uint8_t* textureData)
    {
        m_TextureData = textureData;
    }

    uint8_t* DeckLinkInputDevice::GetTextureData()
    {
        return m_TextureData;
    }

    void DeckLinkInputDevice::LockQueue()
    {
        m_QueueLock.lock();
    }

    void DeckLinkInputDevice::UnlockQueue()
    {
        m_QueueLock.unlock();
    }

    void DeckLinkInputDevice::Start(const int deviceIndex,
                                    const int deviceSelected,
                                    const int formatIndex,
                                    const int pixelFormat,
                                    const bool enablePassThrough,
                                    const UnityGfxRenderer graphicsAPI,
                                    InputVideoFormatData* selectedFormat)
    {
        assert(m_Input == nullptr);
        assert(m_DisplayMode == nullptr);

        if (!InitializeInput(deviceIndex, deviceSelected, formatIndex, pixelFormat, enablePassThrough, graphicsAPI))
            return;

        GetVideoFormat(selectedFormat);

        ShouldOK(m_Input->StartStreams());
    }

    void DeckLinkInputDevice::Stop()
    {
        // First stop the output stream, so displayMode may be released.
        if (m_Input != nullptr)
        {
            m_Input->StopStreams();
            m_Input->SetCallback(nullptr);
            m_Input->DisableVideoInput();
        }

        // Release the internal objects.
        if (m_Configuration != nullptr)
        {
            m_Configuration->Release();
            m_Configuration = nullptr;
        }

        if (m_DisplayMode != nullptr)
        {
            m_DisplayMode->Release();
            m_DisplayMode = nullptr;
        }

        // We relase frame and displayMode before the output object, to avoid leaks.
        if (m_Input != nullptr)
        {
            m_Input->Release();
            m_Input = nullptr;
        }

        m_Initialized = false;
    }

    HRESULT STDMETHODCALLTYPE DeckLinkInputDevice::QueryInterface(REFIID iid, LPVOID* ppv)
    {
        if (iid == IID_IUnknown)
        {
            *ppv = this;
            return S_OK;
        }

        if (iid == IID_DeckLinkInputCallback)
        {
            *ppv = (DeckLinkInputCallback*)this;
            return S_OK;
        }

        *ppv = nullptr;
        return E_NOINTERFACE;
    }

    ULONG STDMETHODCALLTYPE DeckLinkInputDevice::AddRef()
    {
        return m_RefCount.fetch_add(1);
    }

    ULONG STDMETHODCALLTYPE DeckLinkInputDevice::Release()
    {
        auto val = m_RefCount.fetch_sub(1);
        if (val == 1)
            delete this;
        return val;
    }

    HRESULT STDMETHODCALLTYPE DeckLinkInputDevice::VideoInputFormatChanged(
        BMDVideoInputFormatChangedEvents events,
        IDeckLinkDisplayMode* mode,
        BMDDetectedVideoInputFormatFlags flags)
    {
        if (m_DisplayMode != nullptr && m_DisplayMode->GetDisplayMode() == mode->GetDisplayMode())
            return S_OK;

        m_DisplayMode->Release();
        m_DisplayMode = mode;
        mode->AddRef();

        const auto changeString = UpdateVideoFormatData(events, mode, flags);
        auto isPixelFormatValid = UpdatePixelFormatData(flags);

        if (!IsPixelFormatSupportedInCurrentMode(m_CurrentPixelFormat, mode->GetDisplayMode()) ||
            !isPixelFormatValid)
        {
            m_InvalidPixelFormat = true;

            if (s_FrameErrorCallback != nullptr)
            {
                s_FrameErrorCallback(m_Index, EDeviceStatus::Error, InputError::IncompatiblePixelFormatAndVideoMode, "Incompatible pixel format and video mode.");
            }
            return S_FALSE;
        }

        EnablesVideoFormatFlag();

        // Pause video capture
        auto res = m_Input->StopStreams();
        assert(res == S_OK);

        res = m_Input->DisableVideoInput();
        assert(res == S_OK);

        // Flush any queued video frames
        res = m_Input->FlushStreams();
        assert(res == S_OK);

        // TO DO: we keep getting notified that the "preferred" input is 10-bits when we
        // do want 8-bits, at least for now.
        res = m_Input->EnableVideoInput(m_DisplayMode->GetDisplayMode(),
            (BMDPixelFormat)m_CurrentPixelFormat,
#if _WIN64
            bmdVideoInputEnableFormatDetection
#else
            bmdVideoInputFlagDefault
#endif
        );

        if (res != S_OK)
        {
            if (s_FrameErrorCallback != nullptr)
            {
                s_FrameErrorCallback(m_Index, EDeviceStatus::Error, InputError::DeviceAlreadyUsed, "Can't start input device (possibly already used).");
            }

            m_Initialized = false;
            return res;
        }

        // enable audio
        res = m_Input->EnableAudioInput(m_AudioSampleRate, m_AudioSampleType, m_ChannelCount);
        assert(res == S_OK);

        // Notify the clients of the format change
        InvokeFormatChangedCallback(changeString);

        // Start video capture
        res = m_Input->StartStreams();
        assert(res == S_OK);

        m_Initialized = true;

        return res;
    }

    HRESULT STDMETHODCALLTYPE DeckLinkInputDevice::VideoInputFrameArrived(
        IDeckLinkVideoInputFrame* videoFrame,
        IDeckLinkAudioInputPacket* const audioPacket)
    {
        if (videoFrame == nullptr)
        {
            if (s_FrameErrorCallback != nullptr)
            {
                s_FrameErrorCallback(m_Index, EDeviceStatus::Error, InputError::NoInputSource, "Video frame is invalid.");
            }
            return S_FALSE;
        }
        if (audioPacket == nullptr)
        {
            if (s_FrameErrorCallback != nullptr)
            {
                s_FrameErrorCallback(m_Index, EDeviceStatus::Error, InputError::AudioPacketInvalid, "Audio packet is invalid.");
            }
            return S_FALSE;
        }
        if (m_InvalidPixelFormat)
        {
            if (s_FrameErrorCallback != nullptr)
            {
                s_FrameErrorCallback(m_Index, EDeviceStatus::Error, InputError::IncompatiblePixelFormatAndVideoMode, "Incompatible pixel format and video mode.");
            }
            return S_FALSE;
        }

        if (S_FALSE == DetectInputSource(videoFrame))
            return S_OK;

        // Retrieve the video data.
        std::uint8_t* videoData;
        ShouldOK(videoFrame->GetBytes(reinterpret_cast<void**>(&videoData)));

        BMDTimeValue duration;
        BMDTimeScale scale;
        ShouldOK(m_DisplayMode->GetFrameRate(&duration, &scale));

        const auto videoWidth = static_cast<int32_t>(videoFrame->GetWidth());
        const auto videoHeight = static_cast<int32_t>(videoFrame->GetHeight());
        const auto videoSize = videoFrame->GetRowBytes() * videoHeight;
        const auto videoPixelFormat = static_cast<int32_t>(m_CurrentPixelFormat);
        const auto videoFieldDominance = static_cast<int32_t>(m_DisplayMode->GetFieldDominance());
        const auto videoFrameDuration = flicksPerSecond * duration / scale;
        const auto videoHardwareReferenceTimestamp = GetVideoHardwareReferenceTimestamp(videoFrame);
        const auto videoStreamTimestamp = GetVideoStreamTimestamp(videoFrame);
        const auto videoTimecode = GetVideoTimecode(videoFrame);

        // Read the HDR metadata if available.
        if (videoFrame->GetFlags() & bmdFrameContainsHDRMetadata)
        {
            IDeckLinkVideoFrameMetadataExtensions* ptr(nullptr);
            auto res = videoFrame->QueryInterface(IID_IDeckLinkVideoFrameMetadataExtensions, reinterpret_cast<void**>(&ptr));
            assert(res == S_OK && ptr != nullptr);

            if (res == S_OK && ptr != nullptr)
            {
                IngestHDRMetadata(m_HDRMetadata, ptr);
            }

            if (ptr != nullptr)
            {
                ptr->Release();
            }
        }

        // Retrieve the audio data.
        uint8_t* audioData;
        int32_t  audioSampleCount;
        int64_t  audioTimestamp;

        const auto audioSampleType = static_cast<int32_t>(m_AudioSampleType);
        const auto audioChannelCount = static_cast<int32_t>(m_ChannelCount);

        if (audioPacket != nullptr)
        {
            ShouldOK(audioPacket->GetBytes(reinterpret_cast<void**>(&audioData)));
            audioSampleCount = static_cast<int32_t>(audioPacket->GetSampleFrameCount());
            audioTimestamp = GetAudioPacketTimestamp(audioPacket);
        }
        else
        {
            audioData = nullptr;
            audioSampleCount = 0;
            audioTimestamp = 0;
        }

        // Invoke the frame received callback.
        if (s_FrameArrivedCallback != nullptr)
        {
            s_FrameArrivedCallback(
                m_Index,
                videoData,
                videoSize,
                videoWidth,
                videoHeight,
                videoPixelFormat,
                videoFieldDominance,
                videoFrameDuration,
                videoHardwareReferenceTimestamp,
                videoStreamTimestamp,
                videoTimecode,
                audioData,
                audioSampleType,
                audioChannelCount,
                audioSampleCount,
                audioTimestamp
            );
        }

        // Everything went well, this callback gives the information to the C# manager.
        if (s_FrameErrorCallback != nullptr)
        {
            s_FrameErrorCallback(m_Index, EDeviceStatus::Ok, InputError::NoError, "");
        }

        return S_OK;
    }

    HRESULT DeckLinkInputDevice::DetectInputSource(IDeckLinkVideoInputFrame* videoFrame)
    {
        if ((videoFrame->GetFlags() & bmdFrameHasNoInputSource))
        {
            if (s_FrameErrorCallback != nullptr)
            {
                s_FrameErrorCallback(m_Index, EDeviceStatus::Error, InputError::NoInputSource, "No input device signal found.");
            }
            m_HasInputSource = false;
            return S_FALSE;
        }

        m_HasInputSource = true;
        return S_OK;
    }

    std::uint32_t DeckLinkInputDevice::GetVideoTimecode(IDeckLinkVideoInputFrame* frame)
    {
        IDeckLinkTimecode* timecode = nullptr;
        std::uint32_t bcdTime;

        if (frame->GetTimecode(bmdTimecodeRP188VITC1, &timecode) == S_OK)
            bcdTime = 0;
        else if (frame->GetTimecode(bmdTimecodeRP188VITC2, &timecode) == S_OK)
            bcdTime = 0x80U; // Even field flag
        else
            return 0xffffffffU;

        bcdTime |= timecode->GetBCD();

        // Drop frame flag
        if (timecode->GetFlags() & bmdTimecodeIsDropFrame)
            bcdTime |= 0x40;

        timecode->Release();
        return bcdTime;
    }

    std::int64_t DeckLinkInputDevice::GetVideoHardwareReferenceTimestamp(IDeckLinkVideoInputFrame* frame)
    {
        const BMDTimeScale timeScale = static_cast<BMDTimeScale>(flicksPerSecond);
        BMDTimeValue frameTime;
        BMDTimeValue frameDuration;
        HRESULT hr = frame->GetHardwareReferenceTimestamp(timeScale, &frameTime, &frameDuration);
        return SUCCEEDED(hr) ? frameTime : -1;
    }

    std::int64_t DeckLinkInputDevice::GetVideoStreamTimestamp(IDeckLinkVideoInputFrame* frame)
    {
        const BMDTimeScale timeScale = static_cast<BMDTimeScale>(flicksPerSecond);
        BMDTimeValue frameTime;
        BMDTimeValue frameDuration;
        HRESULT hr = frame->GetStreamTime(&frameTime, &frameDuration, timeScale);
        return SUCCEEDED(hr) ? frameTime : -1;
    }

    std::int64_t DeckLinkInputDevice::GetAudioPacketTimestamp(IDeckLinkAudioInputPacket* packet)
    {
        const BMDTimeScale timeScale = static_cast<BMDTimeScale>(flicksPerSecond);
        BMDTimeValue frameTime;
        HRESULT hr = packet->GetPacketTime(&frameTime, timeScale);
        return SUCCEEDED(hr) ? frameTime : -1;
    }

    bool DeckLinkInputDevice::InitializeInput(int deviceIndex,
                                              int deviceSelected,
                                              int formatIndex,
                                              int pixelFormat,
                                              bool enablePassThrough,
                                              UnityGfxRenderer graphicsAPI)
    {
        // Device iterator
        IDeckLinkIterator* iterator;
        auto res = GetDeckLinkIterator(&iterator);

        if (res != S_OK)
        {
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
                iterator->Release();
                return false;
            }
        }

        iterator->Release(); // The iterator is no longer needed.

        // Input interface of the specified device
        res = device->QueryInterface(IID_DeckLinkInput, reinterpret_cast<void**>(&m_Input));

        EnablePassThrough(device, enablePassThrough);

        device->Release();

        if (res != S_OK)
        {
            return false;
        }

        // Display mode iterator
        IDeckLinkDisplayModeIterator* dmIterator;
        res = m_Input->GetDisplayModeIterator(&dmIterator);

        assert(res == S_OK);

        // Iterate until reaching the specified index.
        for (auto i = 0; i <= formatIndex; i++)
        {
            if (m_DisplayMode != nullptr)
            {
                m_DisplayMode->Release();
                m_DisplayMode = nullptr;
            }
            res = dmIterator->Next(&m_DisplayMode);

            if (res != S_OK)
            {
                dmIterator->Release();
                m_DisplayMode = nullptr;
                return false;
            }
        }

        dmIterator->Release(); // The iterator is no longer needed.

        // Set this object as a frame input callback.
        res = m_Input->SetCallback(this);
        assert(res == S_OK);

        m_InvalidPixelFormat = false;

        // Try getting best quality
        if (pixelFormat == bmdFormatUnspecified)
        {
            m_DesiredPixelFormat = bmdFormatUnspecified;
            m_CurrentPixelFormat = bmdFormat8BitYUV;
        }
        else
        {
            m_DesiredPixelFormat = (BMDPixelFormat)pixelFormat;
            m_CurrentPixelFormat = bmdFormat8BitYUV;
        }

        EnablesVideoFormatFlag();

        // By default, we are always creating the device in YUV8 and NTSC until the VideoInputFormatChanged
        // callback is triggered, with the real video settings.
        if (!IsPixelFormatSupportedInCurrentMode(bmdFormat8BitYUV, m_DisplayMode->GetDisplayMode()))
        {
            return false;
        }

        // Enable the video input.
        res = m_Input->EnableVideoInput(m_DisplayMode->GetDisplayMode(),
                                        m_CurrentPixelFormat,
                                        bmdVideoInputEnableFormatDetection);

        if (res != S_OK)
        {
            // TODO: Rewrite the 'Error Callback' system, because this callback is triggered too soon (before the plugin creation).
            if (s_FrameErrorCallback != nullptr)
            {
                s_FrameErrorCallback(m_Index, EDeviceStatus::Error, InputError::DeviceAlreadyUsed, "Can't start input device (possibly already used).");
            }
            return false;
        }

        // enable audio
        res = m_Input->EnableAudioInput(m_AudioSampleRate, m_AudioSampleType, m_ChannelCount);
        if (res != S_OK)
        {
            // todo : report error
            return false;
        }

        m_Index = deviceIndex;
        m_GraphicsAPI = graphicsAPI;
        m_Initialized = true;

        return true;
    }

    BMDPixelFormat DeckLinkInputDevice::DetermineBestSupportedQuality(const BMDPixelFormat ranking[], const int length)
    {
        for (size_t i = 0; i < length; ++i)
        {
            auto pixelFormat = ranking[i];
            if (IsPixelFormatSupportedInCurrentMode(pixelFormat, m_DisplayMode->GetDisplayMode()))
                return pixelFormat;
        }
        return bmdFormatUnspecified;
    }

    BMDPixelFormat DeckLinkInputDevice::GetBestSupportedQuality(const BMDDetectedVideoInputFormatFlags detectedSignalFlags)
    {
        const int pixelFormatCount = 7;

        // If the incoming video pixel format is YUV, we must try to pick a YUV pixel format
        if (detectedSignalFlags & bmdDetectedVideoInputYCbCr422)
        {
            if (detectedSignalFlags & bmdDetectedVideoInput8BitDepth)
            {
                static const BMDPixelFormat ranking[] = { bmdFormat8BitYUV, bmdFormat10BitYUV, bmdFormat10BitRGB, bmdFormat10BitRGBX, bmdFormat10BitRGBXLE, bmdFormat12BitRGB, bmdFormat12BitRGBLE };
                return DetermineBestSupportedQuality(ranking, pixelFormatCount);
            }
            else if (detectedSignalFlags & bmdDetectedVideoInput10BitDepth)
            {
                static const BMDPixelFormat ranking[] = { bmdFormat10BitYUV, bmdFormat8BitYUV, bmdFormat10BitRGB, bmdFormat10BitRGBX, bmdFormat10BitRGBXLE, bmdFormat12BitRGB, bmdFormat12BitRGBLE };
                return DetermineBestSupportedQuality(ranking, pixelFormatCount);
            }
            else if (detectedSignalFlags & bmdDetectedVideoInput12BitDepth)
            {
                static const BMDPixelFormat ranking[] = { bmdFormat10BitYUV, bmdFormat8BitYUV, bmdFormat12BitRGB, bmdFormat12BitRGBLE, bmdFormat10BitRGB, bmdFormat10BitRGBXLE, bmdFormat10BitRGBX, };
                return DetermineBestSupportedQuality(ranking, pixelFormatCount);
            }
            else
            {
                static const BMDPixelFormat ranking[] = { bmdFormat10BitYUV, bmdFormat8BitYUV, bmdFormat10BitRGB, bmdFormat10BitRGBX, bmdFormat10BitRGBXLE, bmdFormat12BitRGB, bmdFormat12BitRGBLE };
                return DetermineBestSupportedQuality(ranking, pixelFormatCount);
            }
        }
        // If the incoming video pixel format is RGB, we must try to pick an RGB pixel format
        else if (detectedSignalFlags & bmdDetectedVideoInputRGB444)
        {
            if (detectedSignalFlags & bmdDetectedVideoInput8BitDepth)
            {
                static const BMDPixelFormat ranking[] = { bmdFormat10BitRGB, bmdFormat10BitRGBXLE, bmdFormat10BitRGBX, bmdFormat12BitRGB, bmdFormat12BitRGBLE, bmdFormat10BitYUV , bmdFormat8BitYUV };
                return DetermineBestSupportedQuality(ranking, pixelFormatCount);
            }
            else if (detectedSignalFlags & bmdDetectedVideoInput10BitDepth)
            {
                static const BMDPixelFormat ranking[] = { bmdFormat10BitRGB, bmdFormat10BitRGBXLE, bmdFormat10BitRGBX, bmdFormat12BitRGB, bmdFormat12BitRGBLE, bmdFormat10BitYUV , bmdFormat8BitYUV };
                return DetermineBestSupportedQuality(ranking, pixelFormatCount);
            }
            else if (detectedSignalFlags & bmdDetectedVideoInput12BitDepth)
            {
                static const BMDPixelFormat ranking[] = { bmdFormat12BitRGB, bmdFormat12BitRGBLE, bmdFormat10BitRGB, bmdFormat10BitRGBXLE, bmdFormat10BitRGBX, bmdFormat10BitYUV , bmdFormat8BitYUV };
                return DetermineBestSupportedQuality(ranking, pixelFormatCount);
            }
            else
            {
                static const BMDPixelFormat ranking[] = { bmdFormat10BitRGB, bmdFormat10BitRGBXLE, bmdFormat10BitRGBX, bmdFormat12BitRGB, bmdFormat12BitRGBLE, bmdFormat10BitYUV , bmdFormat8BitYUV };
                return DetermineBestSupportedQuality(ranking, pixelFormatCount);
            }
        }

        return bmdFormatUnspecified;
    }

    std::string DeckLinkInputDevice::UpdateVideoFormatData(BMDVideoInputFormatChangedEvents notificationEvents,
                                                    IDeckLinkDisplayMode* newDisplayMode,
                                                    BMDDetectedVideoInputFormatFlags detectedSignalFlags)
    {
        std::string str("Input video format changed.");

        // Check for video field changes
        if (notificationEvents & bmdVideoInputFieldDominanceChanged)
        {
            str += " Field dominance changed to ";

            switch (newDisplayMode->GetFieldDominance())
            {
            case bmdUnknownFieldDominance:
                str += "'Unknown'.";
                break;
            case bmdLowerFieldFirst:
                str += "'Lower field first'.";
                break;
            case bmdUpperFieldFirst:
                str += "'Upper field first'.";
                break;
            case bmdProgressiveFrame:
                str += "'Progressive'.";
                break;
            case bmdProgressiveSegmentedFrame:
                str += "'Progressive segmented frame'.";
                break;
            }
        }

        // Check if the pixel format has changed
        if (notificationEvents & bmdVideoInputColorspaceChanged)
        {
            str += " Color space changed.";
        }

        // Check if the video mode has changed
        if (notificationEvents & bmdVideoInputDisplayModeChanged)
        {
            // Obtain the name of the video mode
            dlstring_t displayModeString;
            newDisplayMode->GetName(&displayModeString);

            str += " Display mode changed to ";
            str += DlToStdString(displayModeString);

            // Release the video mode name string
            DeleteString(displayModeString);

            auto flags = newDisplayMode->GetFlags();
            m_ColorSpace = bmdDisplayModeColorspaceRec709;
            if (flags & bmdDisplayModeColorspaceRec601)
                m_ColorSpace = bmdDisplayModeColorspaceRec601;
            else if (flags & bmdDisplayModeColorspaceRec709)
                m_ColorSpace = bmdDisplayModeColorspaceRec709;
            else if (flags & bmdDisplayModeColorspaceRec2020)
                m_ColorSpace = bmdDisplayModeColorspaceRec2020;
        }

        return str;
    }

    bool DeckLinkInputDevice::UpdatePixelFormatData(const BMDDetectedVideoInputFormatFlags detectedSignalFlags)
    {
        if (m_DesiredPixelFormat == bmdFormatUnspecified)
        {
            m_CurrentPixelFormat = GetBestSupportedQuality(detectedSignalFlags);
            return true;
        }

        const auto isRGBPixelFormat = IsRGBPixelFormat(m_DesiredPixelFormat);
        const auto isYUVValid = (detectedSignalFlags & bmdDetectedVideoInputYCbCr422) && !isRGBPixelFormat;
        const auto isRGBValid = (detectedSignalFlags & bmdDetectedVideoInputRGB444) && isRGBPixelFormat;

        const auto isValid = (isYUVValid || isRGBValid);
        if (isValid)
        {
            m_CurrentPixelFormat = m_DesiredPixelFormat;
        }

        return isValid;
    }

    dlbool_t DeckLinkInputDevice::IsPixelFormatSupportedInCurrentMode(BMDPixelFormat pix, BMDDisplayMode displayMode)
    {
        dlbool_t isSupported;
        auto res = m_Input->DoesSupportVideoMode(
            bmdVideoConnectionUnspecified,
            displayMode,
            pix,
            bmdNoVideoInputConversion,
            bmdSupportedVideoModeDefault,
            nullptr,
            &isSupported
        );
        assert(res == S_OK);
        return isSupported;
    }

    bool DeckLinkInputDevice::EnablePassThrough(IDeckLink* deckLinkDevice, bool enabled)
    {
        IDeckLinkProfileAttributes* deckLinkAttributes = nullptr;
        auto queryDeckLinkConfigurationSucceed = false;
        auto pushedAttrib = false;
        auto pushedConfig = false;

        if (m_Configuration == nullptr &&
            (deckLinkDevice->QueryInterface(IID_IDeckLinkConfiguration, (void**)&m_Configuration) != S_OK ||
            m_Configuration == nullptr))
            goto cleanup;

        if (deckLinkDevice->QueryInterface(IID_IDeckLinkProfileAttributes, (void**)&deckLinkAttributes) != S_OK)
            goto cleanup;

        int64_t videoIOSupport;
        if (deckLinkAttributes->GetInt(BMDDeckLinkVideoIOSupport, &videoIOSupport) != S_OK)
            goto cleanup;

        if ((BMDVideoIOSupport)videoIOSupport & bmdDeviceSupportsPlayback)
        {
            int64_t dummyInt;
            if ((m_Configuration->GetInt(bmdDeckLinkConfigCapturePassThroughMode, &dummyInt) != S_OK))
                goto cleanup;

            auto mode = (enabled) ? bmdDeckLinkCapturePassthroughModeDirect : bmdDeckLinkCapturePassthroughModeDisabled;
            if (m_Configuration->SetInt(bmdDeckLinkConfigCapturePassThroughMode, mode) != S_OK)
                goto cleanup;

            queryDeckLinkConfigurationSucceed = true;
        }

    cleanup:

        if (deckLinkAttributes != nullptr)
            deckLinkAttributes->Release();

        return queryDeckLinkConfigurationSucceed;
    }

    void DeckLinkInputDevice::IngestHDRMetadata(HDRMetadata& dest, IDeckLinkVideoFrameMetadataExtensions* const ptr)
    {
        auto res = S_OK;

        dllonglong lldata;
        res |= ptr->GetInt(bmdDeckLinkFrameMetadataHDRElectroOpticalTransferFunc, &lldata);
        dest.EOTF = static_cast<uint32_t>(lldata);

        double ddata;
        if (S_OK != ptr->GetFloat(bmdDeckLinkFrameMetadataHDRDisplayPrimariesRedX, &ddata)) ddata = kDefaultRec2020Colorimetrics.RedX;
        dest.referencePrimaries.RedX = ddata;
        if (S_OK != ptr->GetFloat(bmdDeckLinkFrameMetadataHDRDisplayPrimariesRedY, &ddata)) ddata = kDefaultRec2020Colorimetrics.RedY;
        dest.referencePrimaries.RedY = ddata;
        if (S_OK != ptr->GetFloat(bmdDeckLinkFrameMetadataHDRDisplayPrimariesGreenX, &ddata)) ddata = kDefaultRec2020Colorimetrics.GreenX;
        dest.referencePrimaries.GreenX = ddata;
        if (S_OK != ptr->GetFloat(bmdDeckLinkFrameMetadataHDRDisplayPrimariesGreenY, &ddata)) ddata = kDefaultRec2020Colorimetrics.GreenY;
        dest.referencePrimaries.GreenY = ddata;
        if (S_OK != ptr->GetFloat(bmdDeckLinkFrameMetadataHDRDisplayPrimariesBlueX, &ddata)) ddata = kDefaultRec2020Colorimetrics.BlueX;
        dest.referencePrimaries.BlueX = ddata;
        if (S_OK != ptr->GetFloat(bmdDeckLinkFrameMetadataHDRDisplayPrimariesBlueY, &ddata)) ddata = kDefaultRec2020Colorimetrics.BlueY;
        dest.referencePrimaries.BlueY = ddata;
        if (S_OK != ptr->GetFloat(bmdDeckLinkFrameMetadataHDRWhitePointX, &ddata)) ddata = kDefaultRec2020Colorimetrics.WhiteX;
        dest.referencePrimaries.WhiteX = ddata;
        if (S_OK != ptr->GetFloat(bmdDeckLinkFrameMetadataHDRWhitePointY, &ddata)) ddata = kDefaultRec2020Colorimetrics.WhiteY;
        dest.referencePrimaries.WhiteY = ddata;
        if (S_OK != ptr->GetFloat(bmdDeckLinkFrameMetadataHDRMaxDisplayMasteringLuminance, &ddata)) ddata = kDefaultMaxDisplayMasteringLuminance;
        dest.maxDisplayMasteringLuminance = ddata;
        if (S_OK != ptr->GetFloat(bmdDeckLinkFrameMetadataHDRMinDisplayMasteringLuminance, &ddata)) ddata = kDefaultMinDisplayMasteringLuminance;
        dest.minDisplayMasteringLuminance = ddata;
        if (S_OK != ptr->GetFloat(bmdDeckLinkFrameMetadataHDRMaximumContentLightLevel, &ddata)) ddata = kDefaultMaxCLL;
        dest.maxCLL = ddata;
        if (S_OK != ptr->GetFloat(bmdDeckLinkFrameMetadataHDRMaximumFrameAverageLightLevel, &ddata)) ddata = kDefaultMaxFALL;
        dest.maxFALL = ddata;

        auto cs = static_cast<BMDColorspace>(bmdColorspaceRec709);
        res |= ptr->GetInt(bmdDeckLinkFrameMetadataColorspace, &lldata);
        cs = static_cast<BMDColorspace>(lldata);

        auto csFlag = bmdDisplayModeColorspaceRec709;
        switch (cs)
        {
        default:
        case bmdColorspaceRec709:
            csFlag = bmdDisplayModeColorspaceRec709;
            break;
        case bmdColorspaceRec601:
            csFlag = bmdDisplayModeColorspaceRec601;
            break;
        case bmdColorspaceRec2020:
            csFlag = bmdDisplayModeColorspaceRec2020;
            break;
        }

        // Notify the clients of the format change
        if (res == S_OK && csFlag != m_ColorSpace)
        {
            m_ColorSpace = csFlag;
            InvokeFormatChangedCallback("Input color space changed.");
        }
    }

    void DeckLinkInputDevice::InvokeFormatChangedCallback(std::string changeDescription)
    {
        if (s_VideoFormatChangedCallback == nullptr)
            return;

        InputVideoFormatData format;
        GetVideoFormat(&format);

        s_VideoFormatChangedCallback(
            format,
            changeDescription.c_str()
        );
    }

    void DeckLinkInputDevice::GetVideoFormat(InputVideoFormatData* format)
    {
        BMDTimeValue duration;
        BMDTimeScale scale;
        ShouldOK(m_DisplayMode->GetFrameRate(&duration, &scale));

        dlstring_t name;
        ShouldOK(m_DisplayMode->GetName(&name));
        const auto formatName = DlToStdString(name);
        DeleteString(name);

        format->deviceIndex = m_Index;
        format->mode = static_cast<int32_t>(m_DisplayMode->GetDisplayMode());
        format->width = static_cast<int32_t>(m_DisplayMode->GetWidth());
        format->height = static_cast<int32_t>(m_DisplayMode->GetHeight());
        format->frameRateNumerator = static_cast<int32_t>(scale);
        format->frameRateDenominator = static_cast<int32_t>(duration);
        format->fieldDominance = static_cast<int32_t>(m_DisplayMode->GetFieldDominance());
        format->formatCode = static_cast<int32_t>(m_CurrentPixelFormat);
        format->colorSpaceCode = static_cast<int32_t>(m_ColorSpace);
        format->transferFunction = static_cast<int32_t>(m_HDRMetadata.EOTF);

#if defined(_WIN32)
        strncpy_s(format->formatName, formatName.c_str(), 32);
#else
        strncpy(format->formatName, formatName.c_str(), 32);
#endif
    }

    void DeckLinkInputDevice::EnablesVideoFormatFlag()
    {
        if (m_Configuration == nullptr)
        {
            m_Input->QueryInterface(IID_IDeckLinkConfiguration, (void**)&m_Configuration);
        }
        assert(m_Configuration != nullptr);

        m_Configuration->SetFlag(bmdDeckLinkConfig444SDIVideoOutput, IsRGBPixelFormat(m_CurrentPixelFormat));
    }

    bool DeckLinkInputDevice::IsRGBPixelFormat(BMDPixelFormat pixeFormat) const
    {
        assert(m_DisplayMode != nullptr);
        switch (pixeFormat)
        {
        case bmdFormatUnspecified:
            assert(false); // Invalid Pixel Format
            return false;
        case bmdFormat10BitRGB:
        case bmdFormat10BitRGBXLE:
        case bmdFormat10BitRGBX:
        case bmdFormat12BitRGB:
        case bmdFormat12BitRGBLE:
        case bmdFormat8BitARGB:
        case bmdFormat8BitBGRA:
            return true;
        default:
            return false;
        }
    }
}
