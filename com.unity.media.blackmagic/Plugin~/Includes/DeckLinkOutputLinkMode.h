#pragma once

#include "../Common.h"

namespace MediaBlackmagic
{
    enum class EOutputLinkMode
    {
        Single = 1 << 0,
        Dual = 1 << 1,
        Quad = 1 << 2
    };

    bool GetBMDLinkMode(EOutputLinkMode mode, _BMDLinkConfiguration& bmdMode);
    bool GetBMDLinkModeID(EOutputLinkMode mode, _BMDDeckLinkAttributeID& bmdMode);
    bool IsLinkModeSupported(IDeckLink* device, const EOutputLinkMode mode);
}
