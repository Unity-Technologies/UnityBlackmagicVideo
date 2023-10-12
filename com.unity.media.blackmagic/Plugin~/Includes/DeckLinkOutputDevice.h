#pragma once

#include <atomic>
#include <chrono>
#include <condition_variable>
#include <mutex>
#include <tuple>
#include <list>
#include <vector>
#include <string>
#include <deque>
#include <algorithm>
#include <condition_variable>

#include "../Common.h"
#include "OutputDeviceAudioChunk.h"
#include "../external/Unity/IUnityRenderingExtensions.h"
#include "DeckLinkOutputLinkMode.h"
#include "DeckLinkOutputKeyingMode.h"
#include "DeckLinkDeviceUtilities.h"

#if _WIN64
#include "ThreadedMemcpy.h"
#include "d3d11.h"
#include "PinnedMemoryAllocator.h"
#include "DeckLinkOutputGPUDirect.h"
#endif

namespace MediaBlackmagic
{
    struct HDRMetadata;

    const uint32_t k_BufferedFrameNum = 1;

    class DeckLinkOutputDevice final : private IDeckLinkVideoOutputCallback,
                                       private IDeckLinkAudioOutputCallback
    {
    public:
        class HDRVideoFrame : public IDeckLinkVideoFrame, public IDeckLinkVideoFrameMetadataExtensions
        {
        public:
            HDRVideoFrame(bool handleAllocation, IDeckLinkMutableVideoFrame* videoFrame, BMDDisplayModeFlags& deviceColorspace);

            // IUnknown interface
            virtual HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, LPVOID* ppv);
            virtual ULONG STDMETHODCALLTYPE AddRef(void);
            virtual ULONG STDMETHODCALLTYPE Release(void);

            // IDeckLinkVideoFrame interface
            virtual long STDMETHODCALLTYPE GetWidth(void);
            virtual long STDMETHODCALLTYPE GetHeight(void);
            virtual long STDMETHODCALLTYPE GetRowBytes(void);
            virtual BMDPixelFormat STDMETHODCALLTYPE GetPixelFormat(void);
            virtual BMDFrameFlags STDMETHODCALLTYPE GetFlags(void);
            virtual HRESULT STDMETHODCALLTYPE GetBytes(void** buffer);
            virtual HRESULT STDMETHODCALLTYPE GetTimecode(BMDTimecodeFormat format, IDeckLinkTimecode** timecode);
            virtual HRESULT STDMETHODCALLTYPE GetAncillaryData(IDeckLinkVideoFrameAncillary** ancillary);

            // IDeckLinkVideoFrameMetadataExtensions interface
            virtual HRESULT STDMETHODCALLTYPE GetInt(BMDDeckLinkFrameMetadataID metadataID, int64_t* value);
            virtual HRESULT STDMETHODCALLTYPE GetFloat(BMDDeckLinkFrameMetadataID metadataID, double* value);
            virtual HRESULT STDMETHODCALLTYPE GetFlag(BMDDeckLinkFrameMetadataID metadataID, dlbool_t* value);
            virtual HRESULT STDMETHODCALLTYPE GetString(BMDDeckLinkFrameMetadataID metadataID, dlstring_t* value);
            virtual HRESULT STDMETHODCALLTYPE GetBytes(BMDDeckLinkFrameMetadataID metadataID, void* buffer, uint32_t* bufferSize);

            // Metadata support
            IDeckLinkMutableVideoFrame* m_VideoFrame;
            bool m_HandleAllocation;
            BMDDisplayModeFlags& m_DeviceColorspace;

            static void SetTransferFunction(uint32_t eotf);
            static HDRMetadata m_Metadata;
        };

        using IntPair = std::tuple<int, int>;
        typedef void(UNITY_INTERFACE_API* FrameError)(int deviceIndex, const char*, EDeviceStatus);
        typedef void(UNITY_INTERFACE_API* FrameCompleted)(int deviceIndex, int64_t frameNumber);

        DeckLinkOutputDevice();
        virtual ~DeckLinkOutputDevice();

        inline void SetFameErrorCallback(const FrameError& callback) { m_FrameErrorCallback = callback; }
        inline void SetFrameCompletedCallback(const FrameCompleted& callback) { m_FrameCompletedCallback = callback; }
        inline void SetDefaultScheduleTime(const float value) { m_DefaultScheduleTime = value; }

        const std::string&  GetErrorString() const;
        std::int64_t        GetFrameDuration() const;
        void                GetFrameRate(std::int32_t& numerator, std::int32_t& denominator) const;
        IntPair             GetFrameDimensions() const;
        const std::string&  RetrievePixelFormat();

        bool  IsProgressive() const;
        bool  IsReferenceLocked() const;

        inline unsigned int CountDroppedFrames() const { return m_DroppedFrameCount; }
        inline unsigned int CountLateFrames() const { return m_LateFrameCount; }
        inline bool IsAsyncMode() const { return m_IsAsync; }
        inline bool IsInitialized() const { return m_Initialized; }

        void  Stop();
        void  FeedFrame(void* frameData, unsigned int timecode);
        void  WaitFrameCompletion(std::int64_t frameNumber);
        void  FeedAudioSampleFrames(const float* samples, int sampleCount);

        void  StartAsyncMode(int deviceIndex,
                             int deviceSelected,
                             BMDDisplayMode mode,
                             int pixelFormat,
                             int colorSpace,
                             int transferFunction,
                             int preroll,
                             bool enableAudio,
                             int audioChannelCount,
                             int audioSampleRate,
                             bool useGPUDirect);

        void  StartManualMode(int deviceIndex,
                              int deviceSelected,
                              BMDDisplayMode mode,
                              int pixelFormat,
                              int colorSpace,
                              int transferFunction,
                              int preroll,
                              bool enableAudio,
                              int audioChannelCount,
                              int audioSampleRate,
                              bool useGPUDirect);

        bool IsValidConfiguration() const;
        bool SupportsKeying(EOutputKeyingMode keying);
        bool ChangeKeyingMode(EOutputKeyingMode mode);
        bool InitializeKeyerParameters(EOutputKeyingMode mode);
        bool DisableKeying();

        bool IsSupportedLinkMode(EOutputLinkMode mode);
        bool SetLinkConfiguration(EOutputLinkMode mode);

        // IDeckLinkVideoOutputCallback implementation
        HRESULT STDMETHODCALLTYPE ScheduledFrameCompleted(IDeckLinkVideoFrame* completedFrame,
                                                          BMDOutputFrameCompletionResult result) override;
        HRESULT STDMETHODCALLTYPE ScheduledPlaybackHasStopped() override;

        // IDeckLinkAudioOutputCallback implementation
        HRESULT STDMETHODCALLTYPE RenderAudioSamples(dlbool_t preroll) override;

        // IUnknown implementation
        HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, LPVOID* ppv) override;
        ULONG STDMETHODCALLTYPE AddRef() override;
        ULONG STDMETHODCALLTYPE Release() override;

        // Output packed matrix dimensions
        std::uint32_t GetBackingFrameByteWidth() const;
        std::uint32_t GetBackingFrameByteHeight() const;
        std::uint32_t GetBackingFrameByteDepth() const;

    private:
        const uint32_t kBufferedAudioLevel = (bmdAudioSampleRate48kHz / 2); // 0.5 seconds
        const uint32_t kMaxBufferedFrames = 10;
        const uint32_t kAllocatedBufferedFrames = 5;

        static const std::string k_FrameDisplayedLate;
        static const std::string k_FrameDropped;
        static const std::string k_FrameFlushed;
        static const std::string k_FrameSucceeded;

        struct OutputFormatData
        {
            std::string pixelFormatValue;
            bool        pixelFormatChanged;

            OutputFormatData() : pixelFormatValue(""), pixelFormatChanged(true) {}
        };

        std::list<HDRVideoFrame*>  m_HDRFrames;

        HDRVideoFrame               m_Frame;
        IDeckLinkDisplayMode*       m_DisplayMode;
        BMDPixelFormat              m_PixelFormat;
        BMDDisplayModeFlags         m_ColorSpace;
        IDeckLinkOutput*            m_Output;
        int                         m_Index;
        bool                        m_Initialized;

        BMDTimeValue                m_FrameDuration;
        BMDTimeScale                m_TimeScale;
        unsigned int                m_DroppedFrameCount;
        unsigned int                m_LateFrameCount;
        FrameError                  m_FrameErrorCallback;
        FrameCompleted              m_FrameCompletedCallback;

        OutputFormatData            m_OutputFormatData;
        std::atomic<ULONG>          m_RefCount;
        std::string                 m_Error;
        std::vector<int32_t>        m_AudioBuffer;
        BMDTimeValue                m_AudioStreamTime;
        bool                        m_PrerollingAudio;
        int                         m_AudioChannelCount;
        EOutputKeyingMode           m_KeyingMode;
        bool                        m_IsAsync;
        IDeckLinkKeyer*             m_DeckLinkKeyer;
        
        // We'll want single-reader/single-writer lock-free containers for this eventually.  The
        // reader needs to be able to put back a chunk if it has been partially consumed or, if it
        // proves too difficult, we can keep the incompletely-used chunk separately.

        std::condition_variable m_Condition;
        std::list<AudioChunk>   m_FreeAudioChunks;
        std::list<AudioChunk>   m_AudioChunks;
        std::mutex              m_AudioChunksMutex;
        std::mutex              m_FreeAudioChunksMutex;

        std::mutex              m_Mutex;
        std::int64_t            m_Queued;
        std::int64_t            m_Completed;
        float                   m_DefaultScheduleTime;
        IDeckLinkConfiguration* m_Configuration;
        std::deque<IDeckLinkMutableVideoFrame*>	m_OutputVideoFrameQueue;
        bool                    m_UseGPUDirect;
        bool                    m_IsGPUDirectAvailable;
        std::condition_variable	m_PlaybackStoppedCondition;
        bool                    m_Stopped;

#if _WIN64
        DeckLinkOutputGPUDirectDevice* m_OutputGPUDirect;
        ThreadedMemcpy                 m_ThreadedMemcpy;

        public:
            void InitializeGPUDirectResources(ID3D11Device* d3d11Device, ID3D11DeviceContext* d3d11Context);
        private:
#endif

        IDeckLinkMutableVideoFrame* AllocateFrame();

        void CopyFrameData(IDeckLinkMutableVideoFrame* frame, const void* data);
        void SetTimecode(IDeckLinkMutableVideoFrame* frame, unsigned int timecode) const;
        void ScheduleFrame(IDeckLinkVideoFrame* frame);

        void FeedFrameHDR(void* frameData, unsigned int timecode);
        void FeedFrameSDR(void* frameData, unsigned int timecode);

        bool InitializeOutput(
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
            bool useGPUDirect
        );

        bool InitializeAudioOutput(int prerollVideoFrameCount, int channelCount, int sampleRate);
        void ReleaseAudioOutput();

        std::uint32_t GetFrameByteLength(std::uint32_t width) const;
    };
}
