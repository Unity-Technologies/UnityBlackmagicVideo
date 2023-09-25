#if _WIN64
#include "ThreadedMemcpy.h"
#include <thread>

namespace MediaBlackmagic
{
    void ThreadedMemcpy::Initialize()
    {
        m_ThreadCount = std::thread::hardware_concurrency();
        assert(m_ThreadCount != 0);

        m_StartSemaphores.reserve(m_ThreadCount);
        m_StopSemaphores.reserve(m_ThreadCount);
        m_Threads.resize(m_ThreadCount);
        m_Parameters.resize(m_ThreadCount);

        for (int i = 0; i < m_ThreadCount; i++)
        {
            m_StartSemaphores.push_back(std::move(CreateSemaphore(NULL, 0, 1, NULL)));
            m_StopSemaphores.push_back(std::move(CreateSemaphore(NULL, 0, 1, NULL)));
            m_Parameters[i].index = i;

            const auto indexData = m_Parameters[i].index;
            auto threadedData = new theaded_data_t();
            threadedData->startSemaphore = &m_StartSemaphores[indexData];
            threadedData->stopSemaphore = &m_StopSemaphores[indexData];
            threadedData->parameters = &m_Parameters[i];

            m_Threads[i] = CreateThread(0, 0, thread_copy_operation, threadedData, 0, NULL);
        }
    }

    void ThreadedMemcpy::Destroy()
    {
        for (int i = 0; i < m_ThreadCount; i++)
        {
            TerminateThread(m_Threads[i], 0);
            CloseHandle(m_StartSemaphores[i]);
            CloseHandle(m_StopSemaphores[i]);
        }
    }

    void ThreadedMemcpy::MemcpyOperation(void* const dest, const void* const src, const size_t bytes)
    {
        // Setup parameters for each thread.
        for (int i = 0; i < m_ThreadCount; i++)
        {
            m_Parameters[i].dest = (char*)dest + i * bytes / m_ThreadCount;
            m_Parameters[i].source = (char*)src + i * bytes / m_ThreadCount;
            m_Parameters[i].size = (i + 1) * bytes / m_ThreadCount - i * bytes / m_ThreadCount;
        }

        // Release semaphores to start the computation.
        for (int i = 0; i < m_ThreadCount; i++)
        {
            ReleaseSemaphore(m_StartSemaphores[i], 1, NULL);
        }

        // Wait for all threads to finish their computation.
        WaitForMultipleObjects(m_ThreadCount, &m_StopSemaphores[0], 1, 0xFFFFFFFF);
    }

    unsigned long ThreadedMemcpy::thread_copy_operation(LPVOID param)
    {
        auto theaded_data = *static_cast<theaded_data_t*>(param);
        auto parameters = theaded_data.parameters;

        delete param;

        while (1)
        {
            WaitForSingleObject(*theaded_data.startSemaphore, 0xFFFFFFFF);
            memcpy(parameters->dest, parameters->source, parameters->size);
            ReleaseSemaphore(*theaded_data.stopSemaphore, 1, NULL);
        }

        return 0;
    }
}
#endif
