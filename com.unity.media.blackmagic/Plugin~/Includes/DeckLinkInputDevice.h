#pragma once

#include <atomic>
#include <mutex>
#include <vector>

#include "../Common.h"
#include "DeckLinkDeviceUtilities.h"
#include "../external/Unity/IUnityRenderingExtensions.h"
#include "../external/Unity/IUnityGraphics.h"

namespace MediaBlackmagic
{
    enum class InputError
    {
        NoError,
        IncompatiblePixelFormatAndVideoMode,
        AudioPacketInvalid,
        DeviceAlreadyUsed,
        NoInputSource
    };

    class DeckLinkInputDevice final : private DeckLinkInputCallback
    {
    public:
        DeckLinkInputDevice();
        virtual ~DeckLinkInputDevice();

        struct InputVideoFormatData
        {
            int32_t deviceIndex;
            int32_t mode;
            int32_t width;
            int32_t height;
            int32_t frameRateNumerator;
            int32_t frameRateDenominator;
            int32_t fieldDominance;
            int32_t formatCode;
            int32_t colorSpaceCode;
            int32_t transferFunction;
            char formatName[32];
        };

        typedef void(UNITY_INTERFACE_API* FrameErrorCallback)(
            int32_t deviceIndex,
            EDeviceStatus status,
            InputError error,
            const char* message
        );
        typedef void(UNITY_INTERFACE_API* VideoFormatChangedCallback)(InputVideoFormatData format, const char* message);
        typedef void(UNITY_INTERFACE_API* FrameArrivedCallback)(
            int32_t deviceIndex,
            uint8_t* videoData,
            int64_t videoDataSize,
            int32_t videoWidth,
            int32_t videoHeight,
            int32_t videoPixelFormat,
            int32_t videoFieldDominance,
            int64_t videoFrameDuration,
            int64_t videoHardwareReferenceTimestamp,
            int64_t videoStreamTimestamp,
            uint32_t videoTimecode,
            uint8_t* audioData,
            int32_t audioSampleType,
            int32_t audioChannelCount,
            int32_t audioSampleCount,
            int64_t audioTimestamp
        );

        static void SetFameErrorCallback(const FrameErrorCallback& callback) { s_FrameErrorCallback = callback; }
        static void SetVideoFormatChangedCallback(const VideoFormatChangedCallback& callback) { s_VideoFormatChangedCallback = callback; }
        static void SetFrameArrivedCallback(const FrameArrivedCallback& callback) { s_FrameArrivedCallback = callback; }

        inline bool IsInitialized() const { return m_Initialized; }
        inline UnityGfxRenderer GetGraphicsAPI() const { return m_GraphicsAPI; }

        void Start(
            int deviceIndex,
            int deviceSelected,
            int formatIndex,
            int pixelFormat,
            bool enablePassThrough,
            UnityGfxRenderer graphicsAPI,
            InputVideoFormatData* selectedFormat);

        void Stop();
        bool GetHasInputSource() const;

        void SetTextureData(uint8_t* textureData);
        uint8_t* GetTextureData();
        void LockQueue();
        void UnlockQueue();

        HRESULT STDMETHODCALLTYPE  QueryInterface(REFIID iid, LPVOID* ppv) override;
        ULONG STDMETHODCALLTYPE    AddRef() override;
        ULONG STDMETHODCALLTYPE    Release() override;

        HRESULT STDMETHODCALLTYPE  VideoInputFormatChanged(BMDVideoInputFormatChangedEvents events,
                                                           IDeckLinkDisplayMode* mode,
                                                           BMDDetectedVideoInputFormatFlags flags) override;

        HRESULT STDMETHODCALLTYPE  VideoInputFrameArrived(IDeckLinkVideoInputFrame* videoFrame,
                                                          IDeckLinkAudioInputPacket* audioPacket) override;

    private:
        static FrameErrorCallback           s_FrameErrorCallback;
        static VideoFormatChangedCallback   s_VideoFormatChangedCallback;
        static FrameArrivedCallback         s_FrameArrivedCallback;

        int                     m_Index;
        bool                    m_Initialized;
        std::atomic<ULONG>      m_RefCount;
        DeckLinkInput*          m_Input;
        IDeckLinkConfiguration* m_Configuration;
        IDeckLinkDisplayMode*   m_DisplayMode;
        BMDPixelFormat          m_DesiredPixelFormat;
        BMDPixelFormat          m_CurrentPixelFormat;
        bool                    m_InvalidPixelFormat;
        BMDDisplayModeFlags     m_ColorSpace;
        HDRMetadata             m_HDRMetadata;
        bool                    m_HasInputSource;
        uint8_t*                m_TextureData;
        mutable std::mutex      m_QueueLock;
        UnityGfxRenderer        m_GraphicsAPI;

        _BMDAudioSampleRate     m_AudioSampleRate = _BMDAudioSampleRate::bmdAudioSampleRate48kHz;
        _BMDAudioSampleType     m_AudioSampleType = _BMDAudioSampleType::bmdAudioSampleType16bitInteger;
        const int               m_ChannelCount = 2;

        HRESULT         DetectInputSource(IDeckLinkVideoInputFrame* videoFrame);
        std::uint32_t   GetVideoTimecode(IDeckLinkVideoInputFrame* frame);
        std::int64_t    GetVideoHardwareReferenceTimestamp(IDeckLinkVideoInputFrame* frame);
        std::int64_t    GetVideoStreamTimestamp(IDeckLinkVideoInputFrame* frame);
        std::int64_t    GetAudioPacketTimestamp(IDeckLinkAudioInputPacket* packet);

        bool            InitializeInput(int deviceIndex,
                                        int deviceSelected,
                                        int formatIndex,
                                        int pixelFormat,
                                        bool enablePassThrough,
                                        UnityGfxRenderer graphicsAPI);

        BMDPixelFormat  DetermineBestSupportedQuality(const BMDPixelFormat ranking[], int length);
        BMDPixelFormat  GetBestSupportedQuality(BMDDetectedVideoInputFormatFlags detectedSignalFlags);

        std::string     UpdateVideoFormatData(BMDVideoInputFormatChangedEvents notificationEvents,
                                              IDeckLinkDisplayMode* newDisplayMode,
                                              BMDDetectedVideoInputFormatFlags detectedSignalFlags);
        bool            UpdatePixelFormatData(BMDDetectedVideoInputFormatFlags detectedSignalFlags);

        dlbool_t        IsPixelFormatSupportedInCurrentMode(BMDPixelFormat pix, BMDDisplayMode displayMode);
        bool            EnablePassThrough(IDeckLink* deckLinkDevice, bool enabled);
        void            IngestHDRMetadata(HDRMetadata& dest, IDeckLinkVideoFrameMetadataExtensions* ptr);
        void            InvokeFormatChangedCallback(std::string changeDescription);
        void            GetVideoFormat(InputVideoFormatData* format);
        void            EnablesVideoFormatFlag();
        bool            IsRGBPixelFormat(BMDPixelFormat pixelFormat) const;
    };
}
