#if _WIN64
#pragma once

#include <vector>

#include "VideoFrameTransfer.h"
#include "../Common.h"

namespace MediaBlackmagic
{
    class PinnedMemoryAllocator : public IDeckLinkMemoryAllocator
    {
    public:
        PinnedMemoryAllocator(ID3D11Device* pD3DDevice, unsigned cacheSize);
        virtual ~PinnedMemoryAllocator();

        bool TransferFrame(void* address, void* gpuTexture);
        void WaitSyncComplete(void* address);
        void EndSyncComplete(void* address);
        void WaitAPI();
        void EndAPI();
        void UnPinAddress(void* address);

        inline void SetD3DDevice(ID3D11Device* pD3DDevice) { m_D3DDevice = pD3DDevice; }

        // IUnknown methods
        virtual HRESULT STDMETHODCALLTYPE	QueryInterface(REFIID iid, LPVOID* ppv);
        virtual ULONG STDMETHODCALLTYPE		AddRef(void);
        virtual ULONG STDMETHODCALLTYPE		Release(void);

        // IDeckLinkMemoryAllocator methods
        virtual HRESULT STDMETHODCALLTYPE	AllocateBuffer(unsigned int bufferSize, void** allocatedBuffer);
        virtual HRESULT STDMETHODCALLTYPE	ReleaseBuffer(void* buffer);
        virtual HRESULT STDMETHODCALLTYPE	Commit();
        virtual HRESULT STDMETHODCALLTYPE	Decommit();

    private:
        ID3D11Device* m_D3DDevice;
        LONG m_RefCount;
        std::map<void*, VideoFrameTransfer*> m_FrameTransfer;
        std::map<void*, unsigned long> m_AllocatedSize;
        std::vector<void*> m_FrameCache;
        unsigned m_FrameCacheSize;
    };
}
#endif
