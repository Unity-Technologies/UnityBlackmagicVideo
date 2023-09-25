#pragma once

#include <list>
#include <atomic>
#include <functional>

#include "com_ptr.h"
#include "../Common.h"
#include "../ObjectIDMap.h"
#include "../external/Unity/IUnityRenderingExtensions.h"
#include "DeckLinkOutputLinkMode.h"
#include "DeckLinkOutputKeyingMode.h"

namespace MediaBlackmagic
{
    typedef void(UNITY_INTERFACE_API* CallbackDeviceName)(const char*, int);

    enum class EConnectorMapping
    {
        FourSubDevicesHalfDuplex = 0,
        OneSubDeviceFullDuplex = 1,
        OneSubDeviceHalfDuplex = 2,
        TwoSubDevicesFullDuplex = 3,
        TwoSubDevicesHalfDuplex = 4
    };

    struct DeckLinkCardInfo
    {
        int64_t groupID;
        EConnectorMapping connectorMapping;
    };

    struct DeviceInfo
    {
        IDeckLink* device;
        bool hasConnectorMapping;
        std::string name;
        std::list<EConnectorMapping> compatibleProfiles;
        IDeckLinkProfile* deckLinkProfile;

        DeviceInfo(IDeckLink* pDevice, std::string pName)
            : device(pDevice), hasConnectorMapping(false), name(pName), deckLinkProfile(nullptr)
        {
        }

        inline bool operator<(const DeviceInfo& rhs)
        {
            return this->name < rhs.name;
        }

        inline bool operator==(const IDeckLink* rhs)
        {
            return this->device == rhs;
        }

        inline void Release()
        {
            if (deckLinkProfile != nullptr)
                deckLinkProfile->Release();

            if (device != nullptr)
                device->Release();
        }
    };

    inline bool operator<(const DeviceInfo& lhs, const DeviceInfo& rhs)
    {
        return lhs.name < rhs.name;
    }

    class DeckLinkDeviceDiscovery : public IDeckLinkDeviceNotificationCallback
    {
        enum class EDeviceDiscoveryType : int
        {
            Undefined = 0,
            Input = 1 << 0,
            Output = 1 << 1,

            //Combinations
            InputOutput = Input | Output
        };

        using CallbackDevice = std::function<void(com_ptr<IDeckLink>&)>;

        const uint32_t k_ReservedCapacity = 4;

    public:
        DeckLinkDeviceDiscovery();
        virtual ~DeckLinkDeviceDiscovery();

        inline void SetOnDeviceArrival(const CallbackDevice& callback) { m_deviceArrivedCallback = callback; }
        inline void SetOnDeviceRemoval(const CallbackDevice& callback) { m_deviceRemovedCallback = callback; }

        inline void SetOnDeviceArrival(const CallbackDeviceName& callback) { m_deviceNameArrivedCallback = callback; }
        inline void SetOnDeviceRemoval(const CallbackDeviceName& callback) { m_deviceNameRemovedCallback = callback; }

        bool EnableDeviceNotifications();
        bool DisableDeviceNotifications();
        bool ChangeAllDevicesConnectorMapping(EConnectorMapping profile, int64_t groupID);
        bool HasConnectorMappingProfiles();
        bool IsConnectorMappingProfileCompatible(EConnectorMapping profile);
        void ReloadAllDeckLinkDevicesEvent();
        void AddConnectorMapping(int64_t groupID, EConnectorMapping profile);

        bool IsLinkModeCompatible(EOutputLinkMode linkMode, int64_t groupID);
        bool IsKeyingModeCompatible(EOutputKeyingMode keyingMode, int64_t groupID);
        bool IsDeviceMatchingGroupID(IDeckLink* deckLink, int64_t deviceGroupID);

        EDeviceDiscoveryType CheckDeckLinkDeviceType(IDeckLink* device) const;

        // IDeckLinkDeviceArrivalNotificationCallback interface
        HRESULT		STDMETHODCALLTYPE DeckLinkDeviceArrived(IDeckLink* deckLinkDevice) override;
        HRESULT		STDMETHODCALLTYPE DeckLinkDeviceRemoved(IDeckLink* deckLinkDevice) override;

        // IUnknown needs only a dummy implementation
        HRESULT		STDMETHODCALLTYPE QueryInterface(REFIID iid, LPVOID* ppv) override;
        ULONG		STDMETHODCALLTYPE AddRef() override;
        ULONG		STDMETHODCALLTYPE Release() override;

    private:
        std::list<DeviceInfo>       m_ActiveDevices;
        com_ptr<IDeckLinkDiscovery> m_deckLinkDiscovery;

        CallbackDevice              m_deviceArrivedCallback;
        CallbackDevice              m_deviceRemovedCallback;

        CallbackDeviceName			m_deviceNameArrivedCallback;
        CallbackDeviceName          m_deviceNameRemovedCallback;

        std::atomic<ULONG>          m_refCount;
        std::list<DeckLinkCardInfo> m_ConnectorMappings;

        bool    SetDeviceProfile(IDeckLink* device, DeviceInfo& deviceInfo);
        HRESULT GetDeckLinkProfileID(IDeckLinkProfile* profile, _BMDProfileID* profileID);
        bool    TryGetConnectorMappingProfile(EConnectorMapping profile, _BMDProfileID* bmdProfile);
        bool    TryGetConnectorMappingProfile(_BMDProfileID bmdProfile, EConnectorMapping* profile);
        void    ReloadDeckLinkDevicesEvent(IDeckLink& device);
        int     AddCompatibleConnectorMappingProfiles(DeviceInfo& deviceInfo);
        bool    TryGetConnectorMappingProfileDeckLinkCard(IDeckLink* deckLink, EConnectorMapping& profile);
    };
}

namespace
{
    MediaBlackmagic::ObjectIDMap<MediaBlackmagic::DeckLinkDeviceDiscovery> s_DeckLinkDeviceDiscovery;

    inline MediaBlackmagic::DeckLinkDeviceDiscovery* GetInstanceDeckLinkDeviceDiscovery(void* deviceDiscovery)
    {
        if (deviceDiscovery == nullptr)
            return nullptr;

        return reinterpret_cast<MediaBlackmagic::DeckLinkDeviceDiscovery*>(deviceDiscovery);
    }
}

extern "C" void UNITY_INTERFACE_EXPORT * CreateDeckLinkDeviceDiscoveryInstance()
{
    auto instance = new MediaBlackmagic::DeckLinkDeviceDiscovery();
    s_DeckLinkDeviceDiscovery.Add(instance);
    return instance;
}

extern "C" void UNITY_INTERFACE_EXPORT AddConnectorMapping(void* deviceDiscovery,
                                                           int64_t groupIDs[],
                                                           MediaBlackmagic::EConnectorMapping profiles[],
                                                           int length)
{
    auto instance = GetInstanceDeckLinkDeviceDiscovery(deviceDiscovery);
    if (instance == nullptr)
    {
        assert(!"Invalid DeckLinkDeviceDiscovery instance.");
        return;
    }

    for (int i = 0; i < length; ++i)
    {
        instance->AddConnectorMapping(groupIDs[i], profiles[i]);
    }
}

extern "C" const void UNITY_INTERFACE_EXPORT
SetDeckLinkOnDeviceArrived(void* deviceDiscovery, MediaBlackmagic::CallbackDeviceName callBack)
{
    auto instance = GetInstanceDeckLinkDeviceDiscovery(deviceDiscovery);
    if (instance == nullptr || callBack == nullptr)
        return;

    instance->SetOnDeviceArrival(callBack);
    instance->EnableDeviceNotifications();
}

extern "C" const void UNITY_INTERFACE_EXPORT
SetDeckLinkOnDeviceRemoved(void* deviceDiscovery, MediaBlackmagic::CallbackDeviceName callBack)
{
    auto instance = GetInstanceDeckLinkDeviceDiscovery(deviceDiscovery);
    if (instance == nullptr || callBack == nullptr)
        return;

    instance->SetOnDeviceRemoval(callBack);
}

extern "C" const bool UNITY_INTERFACE_EXPORT
ChangeAllDevicesConnectorMapping(void* deviceDiscovery, int profile, int64_t groupID)
{
    auto instance = GetInstanceDeckLinkDeviceDiscovery(deviceDiscovery);
    if (instance == nullptr)
        return false;

    auto connectorMapping = static_cast<MediaBlackmagic::EConnectorMapping>(profile);
    return instance->ChangeAllDevicesConnectorMapping(connectorMapping, groupID);
}

extern "C" const unsigned int UNITY_INTERFACE_EXPORT
DestroyDeckLinkDeviceDiscoveryInstance(void* deviceDiscovery)
{
    auto instance = GetInstanceDeckLinkDeviceDiscovery(deviceDiscovery);
    if (instance == nullptr)
        return 0;

    auto instanceId = s_DeckLinkDeviceDiscovery.GetID(instance);
    s_DeckLinkDeviceDiscovery.Remove(instance);
    instance->DisableDeviceNotifications();
    instance->Release();

    return instanceId;
}

extern "C" const bool UNITY_INTERFACE_EXPORT
HasConnectorMappingProfiles(void* deviceDiscovery)
{
    auto instance = GetInstanceDeckLinkDeviceDiscovery(deviceDiscovery);
    if (instance == nullptr)
        return 0;

    return instance->HasConnectorMappingProfiles();
}

extern "C" const void UNITY_INTERFACE_EXPORT
ReloadAllDeckLinkDevicesEvent(void* deviceDiscovery)
{
    auto instance = GetInstanceDeckLinkDeviceDiscovery(deviceDiscovery);
    if (instance == nullptr)
        return;

    instance->ReloadAllDeckLinkDevicesEvent();
}

extern "C" const bool UNITY_INTERFACE_EXPORT
IsConnectorMappingProfileCompatible(void* deviceDiscovery, int profile)
{
    auto instance = GetInstanceDeckLinkDeviceDiscovery(deviceDiscovery);
    if (instance == nullptr)
        return false;

    const auto connectorMapping = static_cast<MediaBlackmagic::EConnectorMapping>(profile);
    return instance->IsConnectorMappingProfileCompatible(connectorMapping);
}

extern "C" const bool UNITY_INTERFACE_EXPORT
IsLinkModeCompatible(void* deviceDiscovery, int linkMode, int64_t groupID)
{
    auto instance = GetInstanceDeckLinkDeviceDiscovery(deviceDiscovery);
    if (instance == nullptr)
        return false;

    const auto mode = static_cast<MediaBlackmagic::EOutputLinkMode>(linkMode);
    return instance->IsLinkModeCompatible(mode, groupID);
}

extern "C" const bool UNITY_INTERFACE_EXPORT
IsKeyingModeCompatible(void* deviceDiscovery, int keyingMode, int64_t groupID)
{
    auto instance = GetInstanceDeckLinkDeviceDiscovery(deviceDiscovery);
    if (instance == nullptr)
        return false;

    const auto mode = static_cast<MediaBlackmagic::EOutputKeyingMode>(keyingMode);
    return instance->IsKeyingModeCompatible(mode, groupID);
}

