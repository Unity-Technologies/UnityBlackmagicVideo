#pragma once

#define NOMINMAX

#include "DeckLinkAPI.h"
#include "platform.h"
#include <cassert>
#include <cstdint>
#include <cstdio>

namespace MediaBlackmagic
{
    enum class EDeviceStatus
    {
        Ok,
        Warning,
        Error,
        Unused
    };

    // https://github.com/OculusVR/Flicks
    // A flick is a unit of time equal to exactly 1/705,600,000 of a second.
    const std::int64_t flicksPerSecond = 705600000;

    inline void ShouldOK(HRESULT result)
    {
#if defined(_DEBUG)
        assert(result == S_OK);
#endif
    }

    inline void DebugLog(const char* message)
    {
#if defined(_DEBUG)

        static int count = 0;

#ifdef _WIN_VER
        // FIXME: Assuming the console redirection done here is only applicable in the Windows world.
        if (count == 0)
        {
            AllocConsole();
            FILE* pConsole;
            freopen_s(&pConsole, "CONOUT$", "wb", stdout);
        }
#endif

        std::printf("MediaBlackmagic (%04d): %s\n", count++, message);

#endif
    }
}
