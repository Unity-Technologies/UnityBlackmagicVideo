#if _WIN64
#include "VideoFrameTransfer.h"
#include "PluginUtils.h"

namespace MediaBlackmagic
{

#define MEM_RD32(a) (*(const volatile unsigned int *)(a))
#define MEM_WR32(a, d) do { *(volatile unsigned int *)(a) = (d); } while (0)

    // Initialize static members
    bool								VideoFrameTransfer::m_Initialized = false;
    bool								VideoFrameTransfer::m_UseDvp = false;
    unsigned							VideoFrameTransfer::m_Width = 0;
    unsigned							VideoFrameTransfer::m_Height = 0;
    void* VideoFrameTransfer::m_CaptureTexture = 0;

    // NVIDIA specific static members
    DVPBufferHandle						VideoFrameTransfer::m_DvpCaptureTextureHandle = 0;
    DVPBufferHandle						VideoFrameTransfer::m_DvpPlaybackTextureHandle = 0;
    uint32_t							VideoFrameTransfer::m_BufferAddrAlignment = 0;
    uint32_t							VideoFrameTransfer::m_BufferGpuStrideAlignment = 0;
    uint32_t							VideoFrameTransfer::m_SemaphoreAddrAlignment = 0;
    uint32_t							VideoFrameTransfer::m_SemaphoreAllocSize = 0;
    uint32_t							VideoFrameTransfer::m_SemaphorePayloadOffset = 0;
    uint32_t							VideoFrameTransfer::m_SemaphorePayloadSize = 0;

    bool VideoFrameTransfer::isNvidiaDvpAvailable()
    {
        return m_Initialized;
    }

    bool VideoFrameTransfer::IsGPUDirectCompatible(ID3D11Device* pD3DDevice)
    {
        if (pD3DDevice == nullptr)
            return false;

        auto hr = (dvpInitD3D11Device(pD3DDevice, 0));
        if (DVP_STATUS_OK == hr)
        {
            dvpCloseD3D11Device(pD3DDevice);
            return true;
        }

        return false;
    }

    bool VideoFrameTransfer::Initialize(ID3D11Device* pD3DDevice, unsigned width, unsigned height, void* playbackTexture)
    {
        m_Initialized = false;
        m_UseDvp = true;
        m_Width = width;
        m_Height = height;

        if (!initializeMemoryLocking(m_Width * m_Height * 4))
            return false;

        if (m_UseDvp)
        {
            auto hr = (dvpInitD3D11Device(pD3DDevice, 0));
            if (DVP_STATUS_OK != hr)
            {
                WriteFileDebug("dvpInitD3D11Device failed.\n");
                return false;
            }

            hr = dvpGetRequiredConstantsD3D11Device(
                &m_BufferAddrAlignment,
                &m_BufferGpuStrideAlignment,
                &m_SemaphoreAddrAlignment,
                &m_SemaphoreAllocSize,
                &m_SemaphorePayloadOffset,
                &m_SemaphorePayloadSize,
                pD3DDevice);

            if (DVP_STATUS_OK != hr)
            {
                WriteFileDebug("dvpGetRequiredConstantsD3D11Device failed.\n");
                return false;
            }

            hr = dvpCreateGPUD3D11Resource((ID3D11Resource*)playbackTexture, &m_DvpPlaybackTextureHandle);
            if (DVP_STATUS_OK != hr)
            {
                WriteFileDebug("dvpCreateGPUD3D11Resource2 failed.\n");
                return false;
            }
        }

        m_Initialized = true;

        return true;
    }

    bool VideoFrameTransfer::Destroy(ID3D11Device* pD3DDevice)
    {
        m_Initialized = false;

        if (!m_Initialized)
            return false;

        if (m_UseDvp)
        {
            auto hr = dvpFreeBuffer(m_DvpPlaybackTextureHandle);
            if (DVP_STATUS_OK != hr)
            {
                WriteFileDebug("dvpFreeBuffer2 failed.\n");
                return false;
            }

            if (pD3DDevice != nullptr)
            {
                hr = dvpCloseD3D11Device(pD3DDevice);
                if (DVP_STATUS_OK != hr)
                {
                    WriteFileDebug("dvpCloseD3D11Device failed.\n");
                    return false;
                }
            }
        }

        m_Initialized = false;

        return true;
    }

    bool VideoFrameTransfer::initializeMemoryLocking(unsigned memSize)
    {
        // Increase the process working set size to allow pinning of memory.
        static SIZE_T	dwMin = 0, dwMax = 0;
        HANDLE hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_SET_QUOTA, FALSE, GetCurrentProcessId());
        if (!hProcess)
            return false;

        // Retrieve the working set size of the process.
        if (!dwMin && !GetProcessWorkingSetSize(hProcess, &dwMin, &dwMax))
            return false;

        // Allow for 80 frames to be locked
        BOOL res = SetProcessWorkingSetSize(hProcess, memSize * 80 + dwMin, memSize * 80 + (dwMax - dwMin));
        if (!res)
            return false;

        CloseHandle(hProcess);
        return true;
    }

    // SyncInfo sets up a semaphore which is shared between the GPU and CPU and used to
    // synchronise access to DVP buffers.
    struct SyncInfo
    {
        SyncInfo(uint32_t semaphoreAllocSize, uint32_t semaphoreAddrAlignment);
        ~SyncInfo();

        volatile uint32_t* mSem;
        volatile uint32_t	mReleaseValue;
        volatile uint32_t	mAcquireValue;
        DVPSyncObjectHandle	mDvpSync;
    };

    SyncInfo::SyncInfo(uint32_t semaphoreAllocSize, uint32_t semaphoreAddrAlignment)
    {
        mSem = (uint32_t*)_aligned_malloc(semaphoreAllocSize, semaphoreAddrAlignment);

        // Initialise
        mSem[0] = 0;
        mReleaseValue = 0;
        mAcquireValue = 0;

        // Setup DVP sync object and import it
        DVPSyncObjectDesc syncObjectDesc;
        syncObjectDesc.externalClientWaitFunc = NULL;
        syncObjectDesc.sem = (uint32_t*)mSem;

        const auto hr = dvpImportSyncObject(&syncObjectDesc, &mDvpSync);
        if (DVP_STATUS_OK != hr)
        {
            WriteFileDebug("dvpImportSyncObject failed.\n");
            return;
        }
    }

    SyncInfo::~SyncInfo()
    {
        const auto hr = dvpFreeSyncObject(mDvpSync);
        if (DVP_STATUS_OK != hr)
        {
            WriteFileDebug("dvpImportSyncObject failed.\n");
            return;
        }
        _aligned_free((void*)mSem);
    }

    VideoFrameTransfer::VideoFrameTransfer(ID3D11Device* pD3DDevice, unsigned long memSize, void* address) :
        m_Buffer(address),
        m_MemSize(memSize),
        m_ExtSync(NULL),
        m_GpuSync(NULL),
        m_DvpSysMemHandle(0),
        m_D3DDevice(nullptr)
    {
        if (m_UseDvp)
        {
            // Pin the memory
            if (!VirtualLock(m_Buffer, m_MemSize))
                WriteFileDebug("Error pinning memory with VirtualLock\n");

            // Create necessary sysmem and gpu sync objects
            m_ExtSync = new SyncInfo(m_SemaphoreAllocSize, m_SemaphoreAddrAlignment);
            m_GpuSync = new SyncInfo(m_SemaphoreAllocSize, m_SemaphoreAddrAlignment);

            // Cache the device
            m_D3DDevice = pD3DDevice;

            // Register system memory buffers with DVP
            DVPSysmemBufferDesc sysMemBuffersDesc;
            sysMemBuffersDesc.width = m_Width;
            sysMemBuffersDesc.height = m_Height;
            sysMemBuffersDesc.stride = m_Width * 4;
            sysMemBuffersDesc.format = DVP_RGBA;
            sysMemBuffersDesc.type = DVP_UNSIGNED_BYTE;
            sysMemBuffersDesc.size = m_MemSize;
            sysMemBuffersDesc.bufAddr = m_Buffer;

            auto hr = dvpCreateBuffer(&sysMemBuffersDesc, &m_DvpSysMemHandle);
            if (DVP_STATUS_OK != hr)
            {
                WriteFileDebug("dvpCreateBuffer failed.\n");
                return;
            }

            hr = dvpBindToD3D11Device(m_DvpSysMemHandle, m_D3DDevice);
            if (DVP_STATUS_OK != hr)
            {
                WriteFileDebug("dvpBindToD3D11Device failed.\n");
                return;
            }

            WriteFileDebug("Info: dvpBindToD3D11Device success.\n");
        }
    }

    VideoFrameTransfer::~VideoFrameTransfer()
    {
        if (m_UseDvp)
        {
            if (m_D3DDevice != nullptr)
            {
                auto hr = dvpUnbindFromD3D11Device(m_DvpSysMemHandle, m_D3DDevice);
                if (DVP_STATUS_OK != hr)
                {
                    WriteFileDebug("dvpUnbindFromD3D11Device failed.\n");
                    return;
                }
            }

            auto hr = dvpDestroyBuffer(m_DvpSysMemHandle);
            if (DVP_STATUS_OK != hr)
            {
                WriteFileDebug("dvpDestroyBuffer failed.\n");
                return;
            }

            delete m_ExtSync;
            delete m_GpuSync;

            VirtualUnlock(m_Buffer, m_MemSize);
        }
    }

    bool VideoFrameTransfer::PerformFrameTransfer()
    {
        if (m_UseDvp)
        {
            // NVIDIA DVP transfers
            DVPStatus status;

            m_GpuSync->mReleaseValue++;

            dvpBegin();

            // Copy from GPU texture to system memory
            dvpMapBufferWaitDVP(m_DvpPlaybackTextureHandle);

            status = dvpMemcpyLined(m_DvpPlaybackTextureHandle, m_ExtSync->mDvpSync, m_ExtSync->mReleaseValue, DVP_TIMEOUT_IGNORED,
                m_DvpSysMemHandle, m_GpuSync->mDvpSync, m_GpuSync->mReleaseValue, 0, m_Height);
            dvpMapBufferEndDVP(m_DvpPlaybackTextureHandle);

            dvpEnd();
            return (status == DVP_STATUS_OK);
        }

        return true;
    }

    void VideoFrameTransfer::WaitSyncComplete()
    {
        if (!m_UseDvp)
            return;

        // Acquire the GPU semaphore
        m_GpuSync->mAcquireValue++;

        // Increment the release value
        m_ExtSync->mReleaseValue++;

        dvpBegin();
        DVPStatus status = dvpSyncObjClientWaitPartial(m_GpuSync->mDvpSync, m_GpuSync->mAcquireValue, DVP_TIMEOUT_IGNORED);
        dvpEnd();
    }

    void VideoFrameTransfer::EndSyncComplete()
    {
        if (!m_UseDvp)
            return;

        // Update semaphore
        if (m_ExtSync != nullptr)
        {
            MEM_WR32(m_ExtSync->mSem, m_ExtSync->mReleaseValue);
        }
    }

    void VideoFrameTransfer::WaitAPI()
    {
        if (!m_UseDvp)
            return;

        dvpMapBufferWaitAPI(m_DvpPlaybackTextureHandle);
    }

    void VideoFrameTransfer::EndAPI()
    {
        if (!m_UseDvp)
            return;

        dvpMapBufferEndAPI(m_DvpPlaybackTextureHandle);
    }
}
#endif
