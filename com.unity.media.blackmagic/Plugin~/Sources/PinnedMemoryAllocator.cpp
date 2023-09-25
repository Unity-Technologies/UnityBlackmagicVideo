#if _WIN64
#include "PinnedMemoryAllocator.h"
#include "PluginUtils.h"

namespace MediaBlackmagic
{
    PinnedMemoryAllocator::PinnedMemoryAllocator(ID3D11Device* const pD3DDevice, const unsigned cacheSize) :
        m_D3DDevice(pD3DDevice),
        m_RefCount(1),
        m_FrameCacheSize(cacheSize)
    {
    }

    PinnedMemoryAllocator::~PinnedMemoryAllocator()
    {
        for (auto iter = m_FrameTransfer.begin(); iter != m_FrameTransfer.end(); ++iter)
        {
            delete iter->second;
        }
        m_FrameTransfer.clear();
    }

    bool PinnedMemoryAllocator::TransferFrame(void* const  address, void* const  gpuTexture)
    {
        // Catch attempt to pin and transfer memory we didn't allocate.
        if (m_AllocatedSize.count(address) == 0)
            return false;

        if (m_FrameTransfer.count(address) == 0)
        {
            // VideoFrameTransfer prepares and pins address.
            m_FrameTransfer[address] = new VideoFrameTransfer(m_D3DDevice, m_AllocatedSize[address], address);
        }

        return m_FrameTransfer[address]->PerformFrameTransfer();
    }

    void PinnedMemoryAllocator::WaitSyncComplete(void* const  address)
    {
        if (m_AllocatedSize.count(address) && m_FrameTransfer.count(address))
            m_FrameTransfer[address]->WaitSyncComplete();
    }

    void PinnedMemoryAllocator::EndSyncComplete(void* const  address)
    {
        if (m_AllocatedSize.count(address) && m_FrameTransfer.count(address))
            m_FrameTransfer[address]->EndSyncComplete();
    }

    void PinnedMemoryAllocator::WaitAPI()
    {
        VideoFrameTransfer::WaitAPI();
    }

    void PinnedMemoryAllocator::EndAPI()
    {
        VideoFrameTransfer::EndAPI();
    }

    void PinnedMemoryAllocator::UnPinAddress(void* const  address)
    {
        // un-pin address only if it has been pinned for transfer
        if (m_FrameTransfer.count(address) > 0)
        {
            auto iter = m_FrameTransfer.find(address);
            if (iter != m_FrameTransfer.end())
            {
                delete iter->second;
                m_FrameTransfer.erase(iter);
            }
        }
    }

    // IUnknown methods
    HRESULT STDMETHODCALLTYPE PinnedMemoryAllocator::QueryInterface(REFIID /*iid*/, LPVOID* /*ppv*/)
    {
        return E_NOTIMPL;
    }

    ULONG STDMETHODCALLTYPE PinnedMemoryAllocator::AddRef(void)
    {
        return InterlockedIncrement(&m_RefCount);
    }

    ULONG STDMETHODCALLTYPE	PinnedMemoryAllocator::Release(void)
    {
        int newCount = InterlockedDecrement(&m_RefCount);
        if (newCount == 0)
            delete this;
        return newCount;
    }

    // IDeckLinkMemoryAllocator methods
    HRESULT STDMETHODCALLTYPE PinnedMemoryAllocator::AllocateBuffer(const unsigned int bufferSize, void** allocatedBuffer)
    {
        if (m_FrameCache.empty())
        {
            // Allocate memory on a page boundary
            *allocatedBuffer = VirtualAlloc(NULL, bufferSize, MEM_COMMIT | MEM_RESERVE | MEM_WRITE_WATCH, PAGE_READWRITE);

            if (!*allocatedBuffer)
                return E_OUTOFMEMORY;

            m_AllocatedSize[*allocatedBuffer] = bufferSize;
        }
        else
        {
            // Re-use most recently ReleaseBuffer'd address
            *allocatedBuffer = m_FrameCache.back();
            m_FrameCache.pop_back();
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE PinnedMemoryAllocator::ReleaseBuffer(void* const  buffer)
    {
        if (m_FrameCache.size() < m_FrameCacheSize)
        {
            m_FrameCache.push_back(buffer);
        }
        else
        {
            // No room left in cache, so un-pin (if it was pinned) and free this buffer
            UnPinAddress(buffer);
            VirtualFree(buffer, 0, MEM_RELEASE);

            m_AllocatedSize.erase(buffer);
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE PinnedMemoryAllocator::Commit()
    {
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE PinnedMemoryAllocator::Decommit()
    {
        while (!m_FrameCache.empty())
        {
            // Cleanup any frames allocated and pinned in AllocateBuffer() but not freed in ReleaseBuffer()
            UnPinAddress(m_FrameCache.back());
            VirtualFree(m_FrameCache.back(), 0, MEM_RELEASE);
            m_FrameCache.pop_back();
        }
        return S_OK;
    }
}
#endif
