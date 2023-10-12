#if _WIN64
#pragma once

#include <chrono>
#include <vector>

#include "../Common.h"

namespace MediaBlackmagic
{
    typedef struct
    {
        int    index;
        void*  source;
        void*  dest;
        size_t size;
    } copy_params;

    typedef struct
    {
        copy_params* parameters;
        void**      startSemaphore;
        void**      stopSemaphore;
    }theaded_data_t;

    class ThreadedMemcpy final
    {
    public:
        void Initialize();
        void Destroy();
        void MemcpyOperation(void* dest, const void* src, const size_t bytes);

    private:
        std::vector<void*>       m_Threads;
        std::vector<void*>       m_StartSemaphores;
        std::vector<void*>       m_StopSemaphores;
        std::vector<copy_params> m_Parameters;
        int                      m_ThreadCount = 0;

        static unsigned long thread_copy_operation(LPVOID param);
    };
}
#endif
