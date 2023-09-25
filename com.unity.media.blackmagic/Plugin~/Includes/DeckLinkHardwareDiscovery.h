#pragma once

#include <string>
#include <list>
#include "../Common.h"
#include "../external/Unity/IUnityRenderingExtensions.h"

namespace MediaBlackmagic
{
    struct DeckLinkCardData
    {
        std::string             name;
        int64_t                 deviceGroupID;
        std::list<std::string>  availableLogicalDevices;

        inline DeckLinkCardData(std::string pName, int64_t pDeviceGroupID)
            : name(pName), deviceGroupID(pDeviceGroupID)
        {
        }

        inline bool operator<(const DeckLinkCardData& rhs) const
        {
            return this->name < rhs.name;
        }
    };

    class DeckLinkHardwareDiscovery final
    {
    public:
        DeckLinkHardwareDiscovery();
        ~DeckLinkHardwareDiscovery() = default;

        bool InitializeDeckLinkCards();
        bool IsDeviceUnique(IDeckLink* deckLink, int64_t& uniqueID);
        void DetectsMultipleIdenticalCards(std::string& name);
        void AddLogicalDevice(const std::string& name, int64_t groupID);

        const std::string& GetBlackmagicAPIVersion();
        const std::string& GetDeckLinkCardNameByIndex(int index);
        const std::string& GetDeckLinkCardLogicalDevice(int cardIndex, int logicalDeviceIndex);

        int GetDeckLinkCardsCount();
        int64_t GetDeckLinkDeviceGroupIDByIndex(int index);
        int GetDeckLinkCardLogicalDevices(int index);

    private:
        const std::string k_DetectionError;

        std::string m_APIVersion;
        std::list<DeckLinkCardData> m_ActiveCards;
    };
}

namespace
{
    namespace { MediaBlackmagic::DeckLinkHardwareDiscovery hardwareDiscovery;  }

    extern "C" const void UNITY_INTERFACE_EXPORT * GetBlackmagicAPIVersionPlugin()
    {
        const auto& apiVersion = hardwareDiscovery.GetBlackmagicAPIVersion();
        return (apiVersion.empty()) ? nullptr : apiVersion.c_str();
    }

    extern "C" const bool UNITY_INTERFACE_EXPORT InitializeDeckLinkCards()
    {
        return hardwareDiscovery.InitializeDeckLinkCards();
    }

    extern "C" const int UNITY_INTERFACE_EXPORT GetDeckLinkCardsCount()
    {
        return hardwareDiscovery.GetDeckLinkCardsCount();
    }

    extern "C" const void UNITY_INTERFACE_EXPORT * GetDeckLinkCardNameByIndex(int index)
    {
        const auto& hardwareModel = hardwareDiscovery.GetDeckLinkCardNameByIndex(index);

        return (hardwareModel.empty()) ? nullptr : hardwareModel.c_str();
    }

    extern "C" const int64_t UNITY_INTERFACE_EXPORT GetDeckLinkDeviceGroupIDByIndex(int index)
    {
        return hardwareDiscovery.GetDeckLinkDeviceGroupIDByIndex(index);
    }

    extern "C" const int UNITY_INTERFACE_EXPORT GetDeckLinkCardLogicalDevicesCount(int index)
    {
        return hardwareDiscovery.GetDeckLinkCardLogicalDevices(index);
    }

    extern "C" const void UNITY_INTERFACE_EXPORT * GetDeckLinkCardLogicalDeviceName(int indexCard, int indexLogicalDevice)
    {
        const auto& logicalDeviceName = hardwareDiscovery.GetDeckLinkCardLogicalDevice(indexCard, indexLogicalDevice);
        return logicalDeviceName.c_str();
    }
}
