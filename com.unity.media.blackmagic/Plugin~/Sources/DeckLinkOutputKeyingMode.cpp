#include "DeckLinkOutputKeyingMode.h"

namespace MediaBlackmagic
{
    bool TryGetKeyingMode(const EOutputKeyingMode mode, _BMDDeckLinkAttributeID& bmdMode)
    {
        switch (mode)
        {
        case EOutputKeyingMode::External:
            bmdMode = BMDDeckLinkSupportsExternalKeying;
            return true;
        case EOutputKeyingMode::Internal:
            bmdMode = BMDDeckLinkSupportsInternalKeying;
            return true;
        default:
            return false;
        }
    }

    bool SupportsOutputKeying(IDeckLink* const outputDevice, const EOutputKeyingMode keying)
    {
        IDeckLinkProfileAttributes* deckLinkAttributes = nullptr;
        _BMDDeckLinkAttributeID keyingBmd;
        auto keyingModeSupported = false;

        if (TryGetKeyingMode(keying, keyingBmd) == false)
            return false;

        if (outputDevice->QueryInterface(IID_IDeckLinkProfileAttributes, (void**)&deckLinkAttributes) != S_OK)
        {
            goto cleanup;
        }

        dlbool_t keyingSupported;
        if (deckLinkAttributes->GetFlag(keyingBmd, &keyingSupported) == S_OK && keyingSupported)
        {
            keyingModeSupported = true;
        }

    cleanup:
        if (deckLinkAttributes != nullptr)
            deckLinkAttributes->Release();

        return keyingModeSupported;
    }
}
