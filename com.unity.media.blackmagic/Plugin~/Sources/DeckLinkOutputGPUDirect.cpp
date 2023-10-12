#if _WIN64
#include "DeckLinkOutputGPUDirect.h"
#include "DeckLinkOutputDevice.h"
#include "PluginUtils.h"
#include "VideoFrameTransfer.h"

namespace MediaBlackmagic
{
    DeckLinkOutputGPUDirectDevice::DeckLinkOutputGPUDirectDevice(IDeckLinkOutput* const output)
        : m_OutputDevice(output)
        , m_D3d11Device(nullptr)
        , m_D3d11Context(nullptr)
        , m_PlayoutTexture(nullptr)
        , m_FrameCount(0)
        , m_GPUDirectAvailable(false)
        , m_PlayoutAllocator(nullptr)
    {
    }

    DeckLinkOutputGPUDirectDevice::~DeckLinkOutputGPUDirectDevice()
    {
    }

    void DeckLinkOutputGPUDirectDevice::InitializeAPIDevices(ID3D11Device* const device, ID3D11DeviceContext* const context)
    {
        m_D3d11Device = device;
        m_D3d11Context = context;

        WriteFileDebug("Info - InitializeAPIDevices.\n");
    }

    bool DeckLinkOutputGPUDirectDevice::InitializeMemoryAllocator()
    {
        m_PlayoutAllocator = new PinnedMemoryAllocator(nullptr, 1);

        if (m_PlayoutAllocator && m_OutputDevice->SetVideoOutputFrameMemoryAllocator(m_PlayoutAllocator) != S_OK)
        {
            WriteFileDebug("SetVideoOutputFrameMemoryAllocator failed.\n");
            return false;
        }

        return m_PlayoutAllocator != nullptr;
    }

    bool DeckLinkOutputGPUDirectDevice::InitializeGPUDirect(const uint32_t width, const uint32_t height, const uint32_t frameByteLength)
    {
        if (!VideoFrameTransfer::Initialize(m_D3d11Device, frameByteLength, height, (void*)m_PlayoutTexture))
        {
            WriteFileDebug("VideoFrameTransfer::initialize failed.\n");
            return false;
        }

        m_GPUDirectAvailable = VideoFrameTransfer::IsGPUDirectAvailable();
        m_PlayoutAllocator->SetD3DDevice(m_D3d11Device);

        return m_GPUDirectAvailable;
    }

    bool DeckLinkOutputGPUDirectDevice::InitializeGPUDirectTextures(const uint32_t width, const uint32_t height)
    {
        m_PlayoutTexture = CreateDefaultTexture(width, height);
        WriteFileDebug("Info - InitializeGPUDirectTextures.\n");
        return m_PlayoutTexture != nullptr;
    }

    void DeckLinkOutputGPUDirectDevice::CleanupGPUDirectResources()
    {
        WriteFileDebug("CleanupGPUDirectResources.\n");

        if (m_PlayoutTexture != nullptr)
        {
            WriteFileDebug("CleanupGPUDirectResources.\n");
            m_PlayoutTexture->Release();
            m_PlayoutTexture = nullptr;
        }

        if (m_PlayoutAllocator != nullptr)
        {
            m_PlayoutAllocator->Release();
        }

        VideoFrameTransfer::Destroy(m_D3d11Device);
    }

    bool DeckLinkOutputGPUDirectDevice::StartGPUSynchronization(void* const frameData, void* const pointer)
    {
        if (m_GPUDirectAvailable)
        {
            if (!CopyBufferResources(frameData))
            {
                WriteFileDebug("Error, copy resources failed.\n");
                return false;
            }

            if (!m_PlayoutAllocator->TransferFrame(pointer, (void*)m_PlayoutTexture))
            {
                WriteFileDebug("mPlayoutAllocator->transferFrame failed.\n");
            }

            // Wait for transfer to system memory to complete
            m_PlayoutAllocator->WaitSyncComplete(pointer);
        }

        return m_GPUDirectAvailable;
    }

    bool DeckLinkOutputGPUDirectDevice::EndGPUSynchronization(void* const pointer)
    {
        if (m_GPUDirectAvailable)
        {
            m_PlayoutAllocator->EndSyncComplete(pointer);
        }
        return m_GPUDirectAvailable;
    }

    bool DeckLinkOutputGPUDirectDevice::CopyBufferResources(void* const frameSourceData)
    {
        auto destTexture = m_PlayoutTexture;

        if (!destTexture || !frameSourceData)
        {
            WriteFileDebug("Error, incorrect input texture(s).\n");
            return false;
        }

        auto nativeSrc = static_cast<ID3D11Texture2D*>(frameSourceData);

        D3D11_TEXTURE2D_DESC desc;
        nativeSrc->GetDesc(&desc);

        D3D11_TEXTURE2D_DESC desc2;
        destTexture->GetDesc(&desc2);

        if (!destTexture || !nativeSrc)
        {
            WriteFileDebug("Error, invalid IUnknown resource(s).\n");
            return false;
        }

        if (!CopyResource(nativeSrc, destTexture))
        {
            WriteFileDebug("Error, Couldn't copy resources.\n");
            return false;
        }

        return true;
    }

    bool DeckLinkOutputGPUDirectDevice::CopyResource(ID3D11Texture2D* const nativeSrc, ID3D11Texture2D* const tex2DDest)
    {
        m_D3d11Context->CopyResource(
            static_cast<ID3D11Resource*>(tex2DDest),
            static_cast<ID3D11Resource*>(nativeSrc)
        );
        return true;
    }

    ID3D11Texture2D* DeckLinkOutputGPUDirectDevice::CreateDefaultTexture(const uint32_t width, const uint32_t height)
    {
        ID3D11Texture2D* texture = nullptr;
        D3D11_TEXTURE2D_DESC desc = { 0 };
        desc.Width = width;
        desc.Height = height;
        desc.MipLevels = 1;
        desc.ArraySize = 1;
        desc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
        desc.SampleDesc.Count = 1;
        desc.Usage = D3D11_USAGE_DEFAULT;
        desc.BindFlags = D3D11_BIND_SHADER_RESOURCE | D3D11_BIND_RENDER_TARGET;
        desc.CPUAccessFlags = 0;

        const auto r = m_D3d11Device->CreateTexture2D(&desc, NULL, &texture);

        if (((HRESULT)(r)) < 0)
        {
            WriteFileDebug("Error, CreateTexture2D.\n");
            return nullptr;
        }

        return texture;
    }
}
#endif
