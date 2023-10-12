#include "DeckLinkOutputLinkMode.h"

namespace MediaBlackmagic
{
    bool GetBMDLinkMode(const EOutputLinkMode mode, _BMDLinkConfiguration& bmdMode)
    {
        switch (mode)
        {
        case EOutputLinkMode::Single:
            bmdMode = _BMDLinkConfiguration::bmdLinkConfigurationSingleLink;
            break;
        case EOutputLinkMode::Dual:
            bmdMode = _BMDLinkConfiguration::bmdLinkConfigurationDualLink;
            break;
        case EOutputLinkMode::Quad:
            bmdMode = _BMDLinkConfiguration::bmdLinkConfigurationQuadLink;
            break;
        default:
            return false;
        }
        return true;
    }

    bool GetBMDLinkModeID(const EOutputLinkMode mode, _BMDDeckLinkAttributeID& bmdMode)
    {
        switch (mode)
        {
        case EOutputLinkMode::Single:
            break;
        case EOutputLinkMode::Dual:
            bmdMode = _BMDDeckLinkAttributeID::BMDDeckLinkSupportsDualLinkSDI;
            break;
        case EOutputLinkMode::Quad:
            bmdMode = _BMDDeckLinkAttributeID::BMDDeckLinkSupportsQuadLinkSDI;
            break;
        default:
            return false;
        }
        return true;
    }

    bool IsLinkModeSupported(IDeckLink* const device, const EOutputLinkMode mode)
    {
        // Always true as SingleLink is always supported.
        if (mode == EOutputLinkMode::Single)
            return true;

        auto supported = false;
        IDeckLinkProfileAttributes* deckLinkAttributes = nullptr;
        _BMDDeckLinkAttributeID bmdModeID;

        if (!GetBMDLinkModeID(mode, bmdModeID))
            goto cleanup;

        if (device->QueryInterface(IID_IDeckLinkProfileAttributes, (void**)&deckLinkAttributes) != S_OK)
            goto cleanup;

        int64_t intAttribute;
        if (deckLinkAttributes->GetInt(BMDDeckLinkVideoIOSupport, &intAttribute) != S_OK)
            goto cleanup;

        // Determines if the device is an output device.
        if (((BMDVideoIOSupport)intAttribute & bmdDeviceSupportsPlayback) == 0)
            goto cleanup;

        // Determines if the Link mode is compatible.
        dlbool_t flag;
        if (deckLinkAttributes->GetFlag(bmdModeID, &flag) == S_OK && flag)
            supported = true;

    cleanup:
        if (deckLinkAttributes != nullptr)
            deckLinkAttributes->Release();

        return supported;
    }
}
