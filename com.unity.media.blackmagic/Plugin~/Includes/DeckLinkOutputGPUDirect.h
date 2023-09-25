#if _WIN64
#pragma once

#include <deque>
#include "../Common.h"
#include "../external/Unity/IUnityRenderingExtensions.h"
#include "DeckLinkDeviceUtilities.h"

#include "ThreadedMemcpy.h"
#include "d3d11.h"
#include "PinnedMemoryAllocator.h"

namespace MediaBlackmagic
{
    class DeckLinkOutputGPUDirectDevice final
    {
    public:
        DeckLinkOutputGPUDirectDevice(IDeckLinkOutput* output);
        virtual ~DeckLinkOutputGPUDirectDevice();

        void InitializeAPIDevices(ID3D11Device* device, ID3D11DeviceContext* context);
        bool InitializeMemoryAllocator();
        bool InitializeGPUDirect(uint32_t width, uint32_t height, uint32_t frameByteLength);
        bool InitializeGPUDirectTextures(uint32_t width, uint32_t height);
        void CleanupGPUDirectResources();

        bool StartGPUSynchronization(void* frameData, void* pointer);
        bool EndGPUSynchronization(void* pointer);

    private:
        const uint32_t kAllocatedBufferedFrames = 10;
        
        IDeckLinkOutput*       m_OutputDevice;
        ID3D11Device*          m_D3d11Device;
        ID3D11DeviceContext*   m_D3d11Context;
        ID3D11Texture2D*       m_PlayoutTexture;
        uint64_t               m_FrameCount;
        bool                   m_GPUDirectAvailable;
        PinnedMemoryAllocator* m_PlayoutAllocator;

        bool CopyBufferResources(void* frameSourceData);
        bool CopyResource(ID3D11Texture2D* nativeSrc, ID3D11Texture2D* tex2DDest);
        ID3D11Texture2D* CreateDefaultTexture(uint32_t width, uint32_t height);
    };
}
#endif
