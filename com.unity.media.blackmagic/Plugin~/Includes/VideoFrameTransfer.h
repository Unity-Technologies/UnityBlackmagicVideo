#if _WIN64
#pragma once

#include <stdexcept>
#include <map>

// NVIDIA GPU Direct For Video with DirectX11 requires the following two headers.
// See the NVIDIA website to check if your graphics card is supported.
#include <DVPAPI.h>
#include <dvpapi_d3d11.h>

namespace MediaBlackmagic
{
    struct SyncInfo;

    class VideoFrameTransfer
    {
    public:
        VideoFrameTransfer(ID3D11Device* pD3DDevice, unsigned long memSize, void* address);
        ~VideoFrameTransfer();

        static bool IsGPUDirectCompatible(ID3D11Device* pD3DDevice);
        static bool Initialize(ID3D11Device* pD3DDevice, unsigned width, unsigned height, void* playbackTexture);
        static bool Destroy(ID3D11Device* pD3DDevice);
        static void WaitAPI();
        static void EndAPI();

        inline static bool IsGPUDirectAvailable() { return m_Initialized; }

        bool PerformFrameTransfer();
        void WaitSyncComplete();
        void EndSyncComplete();

    private:
        static bool isNvidiaDvpAvailable();
        static bool initializeMemoryLocking(unsigned memSize);

        void*           m_Buffer;
        unsigned long	m_MemSize;
        static bool		m_Initialized;
        static bool		m_UseDvp;
        static unsigned	m_Width;
        static unsigned	m_Height;
        static void*    m_CaptureTexture;

        // NVIDIA GPU Direct for Video support
        SyncInfo*       m_ExtSync;
        SyncInfo*       m_GpuSync;
        DVPBufferHandle	m_DvpSysMemHandle;
        ID3D11Device*   m_D3DDevice;

        static DVPBufferHandle	m_DvpCaptureTextureHandle;
        static DVPBufferHandle	m_DvpPlaybackTextureHandle;
        static uint32_t			m_BufferAddrAlignment;
        static uint32_t			m_BufferGpuStrideAlignment;
        static uint32_t			m_SemaphoreAddrAlignment;
        static uint32_t			m_SemaphoreAllocSize;
        static uint32_t			m_SemaphorePayloadOffset;
        static uint32_t			m_SemaphorePayloadSize;
    };
}
#endif
