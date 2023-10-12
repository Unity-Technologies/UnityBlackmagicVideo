#include <algorithm>
#include "DeckLinkDeviceEnumerator.h"
#include "../Common.h"

namespace MediaBlackmagic
{
    DeckLinkDeviceEnumerator::~DeckLinkDeviceEnumerator()
    {
        clearAllDeviceNames();
        clearInputDeviceNames();
        clearOutputDeviceNames();
    }

    int DeckLinkDeviceEnumerator::CopyStringPointers(void* pointers[], int maxCount) const
    {
        auto count = std::min(maxCount, static_cast<int>(m_Names.size()));
        for (auto i = 0; i < count; i++)
            pointers[i] = const_cast<char*>(m_Names[i].c_str());
        return count;
    }

    int DeckLinkDeviceEnumerator::CopyInputDevicesPointers(void* inputDevices[], int maxCount) const
    {
        auto countInput = std::min(maxCount, static_cast<int>(m_InputDevices.size()));
        for (auto i = 0; i < countInput; i++)
            inputDevices[i] = const_cast<char*>(m_InputDevices[i].c_str());
        return countInput;
    }

    int DeckLinkDeviceEnumerator::CopyOutputDevicesPointers(void* outputDevices[], int maxCount) const
    {
        auto countOutput = std::min(maxCount, static_cast<int>(m_OutputDevices.size()));
        for (auto i = 0; i < countOutput; i++)
            outputDevices[i] = const_cast<char*>(m_OutputDevices[i].c_str());
        return countOutput;
    }

    int DeckLinkDeviceEnumerator::CopyModes(int modes[], int maxCount) const
    {
        auto count = std::min(maxCount, static_cast<int>(m_Modes.size()));
        for (auto i = 0; i < count; i++)
            modes[i] = static_cast<int>(m_Modes[i]);
        return count;
    }

    void DeckLinkDeviceEnumerator::ScanAllDeviceNames()
    {
        clearAllDeviceNames();

        IDeckLinkIterator* iterator;
        if (GetDeckLinkIterator(&iterator) != S_OK)
            return;

        IDeckLink* device;
        while (iterator->Next(&device) == S_OK)
        {
            IDeckLinkProfileAttributes* deckLinkAttributes = nullptr;
            if (device->QueryInterface(IID_IDeckLinkProfileAttributes, (void**)&deckLinkAttributes) != S_OK)
            {
                goto cleanup;
                continue;
            }

            int64_t intAttribute;
            if ((deckLinkAttributes->GetInt(BMDDeckLinkDuplex, &intAttribute) == S_OK) &&
                (((BMDDuplexMode)intAttribute) != bmdDuplexInactive))
            {
                dlstring_t name;
                ShouldOK(device->GetDisplayName(&name));
                m_Names.push_back(DlToStdString(name));
                DeleteString(name);
            }

            goto cleanup;

        cleanup:
            if (deckLinkAttributes != nullptr)
                deckLinkAttributes->Release();

            if (device != nullptr)
                device->Release();
        }
        iterator->Release();
    }

    void DeckLinkDeviceEnumerator::ScanInputDeviceNames()
    {
        clearInputDeviceNames();

        IDeckLinkIterator* iterator;
        if (GetDeckLinkIterator(&iterator) != S_OK)
            return;

        IDeckLink* device;
        while (iterator->Next(&device) == S_OK)
        {
            IDeckLinkProfileAttributes* deckLinkAttributes = nullptr;

            if (device->QueryInterface(IID_IDeckLinkProfileAttributes, (void**)&deckLinkAttributes) != S_OK)
            {
                goto cleanup;
                continue;
            }

            // Check whether device is active for the device profile
            int64_t intAttribute;
            if ((deckLinkAttributes->GetInt(BMDDeckLinkDuplex, &intAttribute) != S_OK) ||
                ((BMDDuplexMode)intAttribute == bmdDuplexInactive))
            {
                goto cleanup;
                continue;
            }

            // Determine whether device supports capture and/or playback
            if (deckLinkAttributes->GetInt(BMDDeckLinkVideoIOSupport, &intAttribute) != S_OK)
            {
                goto cleanup;
                continue;
            }

            if (((BMDVideoIOSupport)intAttribute & bmdDeviceSupportsCapture) != 0)
            {
                dlstring_t name;
                ShouldOK(device->GetDisplayName(&name));
                m_InputDevices.push_back(DlToStdString(name));
                DeleteString(name);
            }

            goto cleanup;

        cleanup:
            if (deckLinkAttributes != nullptr)
                deckLinkAttributes->Release();

            if (device != nullptr)
                device->Release();
        }
        iterator->Release();
    }

    void DeckLinkDeviceEnumerator::ScanOutputDeviceNames()
    {
        clearOutputDeviceNames();

        IDeckLinkIterator* iterator;
        if (GetDeckLinkIterator(&iterator) != S_OK)
            return;

        IDeckLink* device;
        while (iterator->Next(&device) == S_OK)
        {
            IDeckLinkProfileAttributes* deckLinkAttributes = nullptr;

            if (device->QueryInterface(IID_IDeckLinkProfileAttributes, (void**)&deckLinkAttributes) != S_OK)
            {
                goto cleanup;
                continue;
            }

            // Check whether device is active for the device profile
            int64_t intAttribute;
            if ((deckLinkAttributes->GetInt(BMDDeckLinkDuplex, &intAttribute) != S_OK) ||
                ((BMDDuplexMode)intAttribute == bmdDuplexInactive))
            {
                goto cleanup;
                continue;
            }

            // Determine whether device supports capture and/or playback
            if (deckLinkAttributes->GetInt(BMDDeckLinkVideoIOSupport, &intAttribute) != S_OK)
            {
                goto cleanup;
                continue;
            }

            if (((BMDVideoIOSupport)intAttribute & bmdDeviceSupportsPlayback) != 0)
            {
                dlstring_t name;
                ShouldOK(device->GetDisplayName(&name));
                m_OutputDevices.push_back(DlToStdString(name));
                DeleteString(name);
            }

        cleanup:
            if (deckLinkAttributes != nullptr)
                deckLinkAttributes->Release();

            if (device != nullptr)
                device->Release();
        }
        iterator->Release();
    }

    void DeckLinkDeviceEnumerator::ScanOutputModes(int deviceIndex)
    {
        clearModes();

        IDeckLinkIterator* iterator;
        if (GetDeckLinkIterator(&iterator) != S_OK)
            return;

        IDeckLink* device = nullptr;
        for (auto i = 0; i <= deviceIndex; i++)
        {
            if (device != nullptr)
                device->Release();

            if (iterator->Next(&device) != S_OK)
            {
                iterator->Release();
                return;
            }
        }

        iterator->Release();

        IDeckLinkOutput* output;
        device->QueryInterface(IID_IDeckLinkOutput, reinterpret_cast<void**>(&output));
        device->Release();

        IDeckLinkDisplayModeIterator* dmIterator;
        output->GetDisplayModeIterator(&dmIterator);
        output->Release();

        IDeckLinkDisplayMode* mode;
        while (dmIterator->Next(&mode) == S_OK)
        {
            auto displayMode = mode->GetDisplayMode();
            auto displayModeAsInt = static_cast<int>(displayMode);
            m_Modes.push_back(displayModeAsInt);
            mode->Release();
        }
        dmIterator->Release();
    }

    bool DeckLinkDeviceEnumerator::SetAllDevicesDuplexMode(bool halfDuplex)
    {
        IDeckLinkIterator* iterator;
        if (GetDeckLinkIterator(&iterator) != S_OK)
            return false;

        bool queryDeckLinkConfigurationSucceed = true;
        IDeckLink* device;

        while (iterator->Next(&device) == S_OK)
        {
            DeckLinkConfiguration* deckLinkConfiguration = nullptr;
            IDeckLinkProfileManager* manager = NULL;
            IDeckLinkProfile* profile = nullptr;
            auto bmd_profile_id = (!halfDuplex)
                                  ? bmdProfileOneSubDeviceFullDuplex
                                  : bmdProfileFourSubDevicesHalfDuplex;

            if (device->QueryInterface(IID_IDeckLinkConfiguration, (void**)&deckLinkConfiguration) != S_OK)
            {
                queryDeckLinkConfigurationSucceed = false;
                goto cleanup;
                break;
            }

            if (device->QueryInterface(IID_IDeckLinkProfileManager, (void**)&manager) != S_OK)
            {
                queryDeckLinkConfigurationSucceed = false;
                goto cleanup;
                break;
            }

            if (manager->GetProfile(bmd_profile_id, &profile) == S_OK)
            {
                auto res = profile->SetActive();
                queryDeckLinkConfigurationSucceed = (res == S_OK);
            }

            goto cleanup;

        cleanup:
            if (deckLinkConfiguration != nullptr)
                deckLinkConfiguration->Release();

            if (manager != nullptr)
                manager->Release();

            if (device != nullptr)
                device->Release();

            if (profile != nullptr)
                profile->Release();
        }

        iterator->Release();
        return queryDeckLinkConfigurationSucceed;
    }

    void DeckLinkDeviceEnumerator::clearAllDeviceNames()
    {
        m_Names.clear();
    }

    void DeckLinkDeviceEnumerator::clearInputDeviceNames()
    {
        m_InputDevices.clear();
    }

    void DeckLinkDeviceEnumerator::clearOutputDeviceNames()
    {
        m_OutputDevices.clear();
    }

    void DeckLinkDeviceEnumerator::clearModes()
    {
        m_Modes.clear();
    }
}
