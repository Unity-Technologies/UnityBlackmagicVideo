#pragma once

#include "../Common.h"

namespace MediaBlackmagic
{
    enum class EOutputKeyingMode
    {
        None = 1 << 0,
        External = 1 << 1,
        Internal = 1 << 2
    };

    bool TryGetKeyingMode(EOutputKeyingMode mode, _BMDDeckLinkAttributeID& bmdMode);
    bool SupportsOutputKeying(IDeckLink* outputDevice, EOutputKeyingMode keying);
}
