#include <iostream>
#include "DeckLinkHardwareDiscovery.h"
#include "../Common.h"

namespace MediaBlackmagic
{
    DeckLinkHardwareDiscovery::DeckLinkHardwareDiscovery()
        : k_DetectionError("Detection error")
    {

    }

    const std::string& DeckLinkHardwareDiscovery::GetBlackmagicAPIVersion()
    {
        IDeckLinkIterator* deckLinkIterator;
        m_APIVersion = k_DetectionError;

        if (GetDeckLinkIterator(&deckLinkIterator) != S_OK)
        {
            return m_APIVersion;
        }

        IDeckLinkAPIInformation* deckLinkAPIInformation;
        if (deckLinkIterator->QueryInterface(IID_IDeckLinkAPIInformation, (void**)&deckLinkAPIInformation) == S_OK)
        {
            int64_t deckLinkVersion;
            int	dlVerMajor, dlVerMinor, dlVerPoint;

            deckLinkAPIInformation->GetInt(BMDDeckLinkAPIVersion, &deckLinkVersion);

            dlVerMajor = (deckLinkVersion & 0xFF000000) >> 24;
            dlVerMinor = (deckLinkVersion & 0x00FF0000) >> 16;
            dlVerPoint = (deckLinkVersion & 0x0000FF00) >> 8;

            m_APIVersion = std::to_string(dlVerMajor) + "." +
                         std::to_string(dlVerMinor) + "." +
                         std::to_string(dlVerPoint);
        }

        if (deckLinkAPIInformation != nullptr)
            deckLinkAPIInformation->Release();
        deckLinkIterator->Release();
        return m_APIVersion;
    }

    bool DeckLinkHardwareDiscovery::InitializeDeckLinkCards()
    {
        m_ActiveCards.clear();

        IDeckLinkIterator* deckLinkIterator;
        if (GetDeckLinkIterator(&deckLinkIterator) != S_OK)
            return false;

        IDeckLink* deckLink = nullptr;

        while (deckLinkIterator->Next(&deckLink) == S_OK)
        {
            dlstring_t deviceNameString;
            if (deckLink->GetModelName(&deviceNameString) == S_OK)
            {
                int64_t deviceGroupID = 0;
                if (IsDeviceUnique(deckLink, deviceGroupID))
                {
                    auto cardName = DlToStdString(deviceNameString);
                    DetectsMultipleIdenticalCards(cardName);

                    m_ActiveCards.push_back(DeckLinkCardData(cardName, deviceGroupID));
                }

                dlstring_t name;
                if (deckLink->GetDisplayName(&name) == S_OK)
                {
                    auto logicalDeviceName = DlToStdString(name);
                    AddLogicalDevice(logicalDeviceName, deviceGroupID);
                    DeleteString(name);
                }
                DeleteString(deviceNameString);
            }
            deckLink->Release();
        }

        m_ActiveCards.sort();

        deckLinkIterator->Release();
        return m_ActiveCards.size() > 0;
    }

    void DeckLinkHardwareDiscovery::AddLogicalDevice(const std::string& name, const int64_t groupID)
    {
        for (auto& l : m_ActiveCards)
        {
            if (l.deviceGroupID == groupID)
            {
                l.availableLogicalDevices.push_back(name);
            }
        }
    }

    bool DeckLinkHardwareDiscovery::IsDeviceUnique(IDeckLink* const deckLink, int64_t& deviceGroupID)
    {
        IDeckLinkProfileAttributes* deckLinkAttributes = nullptr;
        if (deckLink->QueryInterface(IID_IDeckLinkProfileAttributes, (void**)&deckLinkAttributes) != S_OK)
            return false;

        int64_t groupID = 0;
        if (deckLinkAttributes->GetInt(BMDDeckLinkDeviceGroupID, &groupID) != S_OK)
        {
            deckLinkAttributes->Release();
            return false;
        }

        auto isUnique = true;
        for (auto const& l : m_ActiveCards)
        {
            if (l.deviceGroupID == groupID)
                isUnique = false;
        }

        deviceGroupID = groupID;
        deckLinkAttributes->Release();

        return isUnique;
    }

    void DeckLinkHardwareDiscovery::DetectsMultipleIdenticalCards(std::string& name)
    {
        auto identicalCard = 1;
        for (auto & l : m_ActiveCards)
        {
            if (l.name.compare(name) == 0)
            {
                l.name += " (" + std::to_string(identicalCard) + ")";
                identicalCard++;
            }
        }

        if (identicalCard > 1)
        {
            name += " (" + std::to_string(identicalCard) + ")";
        }
    }

    int DeckLinkHardwareDiscovery::GetDeckLinkCardsCount()
    {
        return (int)m_ActiveCards.size();
    }

    int64_t DeckLinkHardwareDiscovery::GetDeckLinkDeviceGroupIDByIndex(int index)
    {
        if (index >= m_ActiveCards.size())
            return -1;

        auto cardData = std::next(m_ActiveCards.begin(), index);
        return cardData->deviceGroupID;
    }

    const std::string& DeckLinkHardwareDiscovery::GetDeckLinkCardNameByIndex(int index)
    {
        if (index >= m_ActiveCards.size())
            return k_DetectionError;

        auto cardData = std::next(m_ActiveCards.begin(), index);
        return cardData->name;
    }

    int DeckLinkHardwareDiscovery::GetDeckLinkCardLogicalDevices(int index)
    {
        if (index >= m_ActiveCards.size())
            return 0;

        auto cardData = std::next(m_ActiveCards.begin(), index);
        return (int)cardData->availableLogicalDevices.size();
    }

    const std::string& DeckLinkHardwareDiscovery::GetDeckLinkCardLogicalDevice(int cardIndex, int logicalDeviceIndex)
    {
        if (cardIndex >= m_ActiveCards.size())
            return k_DetectionError;

        auto cardData = std::next(m_ActiveCards.begin(), cardIndex);

        if (logicalDeviceIndex > cardData->availableLogicalDevices.size())
            return k_DetectionError;

        auto logicalDeviceName = std::next(cardData->availableLogicalDevices.begin(), logicalDeviceIndex);
        return *logicalDeviceName;
    }
}
