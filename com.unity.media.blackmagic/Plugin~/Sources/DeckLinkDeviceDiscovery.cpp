#include <stdexcept>
#include "DeckLinkDeviceDiscovery.h"
#include "DeckLinkProfileCallback.h"
#include "../Common.h"
#include <algorithm>

namespace MediaBlackmagic
{
    DeckLinkDeviceDiscovery::DeckLinkDeviceDiscovery() :
        m_deckLinkDiscovery(nullptr),
        m_refCount(1),
        m_deviceArrivedCallback(nullptr),
        m_deviceRemovedCallback(nullptr),
        m_deviceNameArrivedCallback(nullptr),
        m_deviceNameRemovedCallback(nullptr)
    {
    }

    DeckLinkDeviceDiscovery::~DeckLinkDeviceDiscovery()
    {
        for (auto& device : m_ActiveDevices)
            device.Release();

        m_ActiveDevices.clear();
    }

    HRESULT DeckLinkDeviceDiscovery::DeckLinkDeviceArrived(IDeckLink* deckLink)
    {
        if (deckLink == nullptr)
            return S_OK;

        dlstring_t name;
        if (deckLink->GetDisplayName(&name) == S_OK)
        {
            deckLink->AddRef();

            const auto stdName = DlToStdString(name);

            m_ActiveDevices.emplace_back(deckLink, stdName);
            auto& latestDevice = m_ActiveDevices.back();

            const auto hasConnectorMapping = SetDeviceProfile(deckLink, latestDevice);
            latestDevice.hasConnectorMapping = hasConnectorMapping;

            if (!stdName.empty())
            {
                const auto deviceType = CheckDeckLinkDeviceType(deckLink);
                m_deviceNameArrivedCallback(stdName.c_str(), static_cast<int>(deviceType));
            }

            m_ActiveDevices.sort();

            DeleteString(name);
        }

        return S_OK;
    }

    HRESULT DeckLinkDeviceDiscovery::DeckLinkDeviceRemoved(IDeckLink* deckLink)
    {
        if (deckLink == nullptr)
            return S_OK;

        auto position = std::find(m_ActiveDevices.begin(), m_ActiveDevices.end(), deckLink);
        m_ActiveDevices.erase(position);
        deckLink->Release();

        if (m_deviceNameRemovedCallback)
        {
            dlstring_t name;
            deckLink->GetDisplayName(&name);

            auto stdName = DlToStdString(name);
            auto deviceType = CheckDeckLinkDeviceType(deckLink);

            m_deviceNameRemovedCallback(stdName.c_str(), static_cast<int>(deviceType));
            DeleteString(name);
        }

        return S_OK;
    }

    bool DeckLinkDeviceDiscovery::EnableDeviceNotifications()
    {
        if (GetDeckLinkDiscovery((void**)&m_deckLinkDiscovery) != S_OK)
            return false;

        auto result = E_FAIL;
        if (m_deckLinkDiscovery)
        {
            result = m_deckLinkDiscovery->InstallDeviceNotifications(this);
        }

        return result == S_OK;
    }

    bool DeckLinkDeviceDiscovery::DisableDeviceNotifications()
    {
        if (m_deckLinkDiscovery)
        {
            m_deckLinkDiscovery->UninstallDeviceNotifications();
            return true;
        }
        return false;
    }

    void DeckLinkDeviceDiscovery::AddConnectorMapping(int64_t groupID, EConnectorMapping profile)
    {
        DeckLinkCardInfo cardInfo;
        cardInfo.connectorMapping = profile;
        cardInfo.groupID = groupID;

        m_ConnectorMappings.push_back(std::move(cardInfo));
    }

    HRESULT DeckLinkDeviceDiscovery::QueryInterface(REFIID iid, LPVOID* ppv)
    {
        auto result = E_NOINTERFACE;

        if (ppv == NULL)
            return E_INVALIDARG;

        // Initialise the return result
        *ppv = NULL;

        // Obtain the IUnknown interface and compare it the provided REFIID
        if (iid == IID_IUnknown)
        {
            *ppv = this;
            AddRef();
            result = S_OK;
        }
        else if (iid == IID_IDeckLinkDeviceNotificationCallback)
        {
            *ppv = static_cast<IDeckLinkDeviceNotificationCallback*>(this);
            AddRef();
            result = S_OK;
        }

        return result;
    }

    ULONG DeckLinkDeviceDiscovery::AddRef(void)
    {
        return ++m_refCount;
    }

    ULONG DeckLinkDeviceDiscovery::Release(void)
    {
        auto newRefValue = --m_refCount;
        if (newRefValue == 0)
            delete this;

        return newRefValue;
    }

    DeckLinkDeviceDiscovery::EDeviceDiscoveryType
        DeckLinkDeviceDiscovery::CheckDeckLinkDeviceType(IDeckLink* deckLinkDevice) const
    {
        IDeckLinkProfileAttributes* deckLinkAttributes = nullptr;
        auto deviceStatus = static_cast<int>(EDeviceDiscoveryType::Undefined);

        if (deckLinkDevice->QueryInterface(IID_IDeckLinkProfileAttributes,
            (void**)&deckLinkAttributes) != S_OK)
        {
            return static_cast<EDeviceDiscoveryType>(deviceStatus);
        }

        int64_t intAttribute;
        if ((deckLinkAttributes->GetInt(BMDDeckLinkDuplex, &intAttribute) != S_OK) ||
            ((BMDDuplexMode)intAttribute == bmdDuplexInactive) ||
            deckLinkAttributes->GetInt(BMDDeckLinkVideoIOSupport, &intAttribute) != S_OK)
        {
            deckLinkAttributes->Release();
            return static_cast<EDeviceDiscoveryType>(deviceStatus);
        }

        if (((BMDVideoIOSupport)intAttribute & bmdDeviceSupportsCapture) != 0)
        {
            deviceStatus |= static_cast<int>(EDeviceDiscoveryType::Input);
        }

        if (((BMDVideoIOSupport)intAttribute & bmdDeviceSupportsPlayback) != 0)
        {
            deviceStatus |= static_cast<int>(EDeviceDiscoveryType::Output);
        }

        deckLinkAttributes->Release();
        return static_cast<EDeviceDiscoveryType>(deviceStatus);
    }

    bool DeckLinkDeviceDiscovery::ChangeAllDevicesConnectorMapping(const EConnectorMapping profile, const int64_t groupID)
    {
        std::list<DeckLinkCardInfo>::iterator it;
        for (it = m_ConnectorMappings.begin(); it != m_ConnectorMappings.end(); ++it)
        {
            if (it->groupID == groupID)
            {
                it->connectorMapping = profile;
            }
        }

        auto queryDeckLinkConfigurationSucceed = true;

        // Change the active profile (Connector Mapping) for all devices
        for (auto& deviceInfo : m_ActiveDevices)
        {
            if (!IsDeviceMatchingGroupID(deviceInfo.device, groupID))
            {
                ReloadDeckLinkDevicesEvent(*deviceInfo.device);
                continue;
            }

            auto listProfiles = deviceInfo.compatibleProfiles;
            auto profileIsCompatible =
                std::find(std::begin(listProfiles), std::end(listProfiles), profile) != std::end(listProfiles);

            if (!profileIsCompatible || !SetDeviceProfile(deviceInfo.device, deviceInfo))
            {
                queryDeckLinkConfigurationSucceed = false;
            }
            ReloadDeckLinkDevicesEvent(*deviceInfo.device);
        }

        return queryDeckLinkConfigurationSucceed;
    }

    bool DeckLinkDeviceDiscovery::IsLinkModeCompatible(const EOutputLinkMode linkMode, const int64_t groupID)
    {
        // Can be optimized, but this is the easier way to determine the compatibility of
        // a specific feature without having to create and instantiate a Device in C#.
        for (auto& deviceInfo : m_ActiveDevices)
        {
            if (!IsDeviceMatchingGroupID(deviceInfo.device, groupID))
                continue;

            if (IsLinkModeSupported(deviceInfo.device, linkMode))
                return true;
        }
        return false;
    }

    bool DeckLinkDeviceDiscovery::IsKeyingModeCompatible(const EOutputKeyingMode keyingMode, const int64_t groupID)
    {
        // Can be optimized, but this is the easier way to determine the compatibility of
        // a specific feature without having to create and instantiate a Device in C#.
        for (auto& deviceInfo : m_ActiveDevices)
        {
            if (!IsDeviceMatchingGroupID(deviceInfo.device, groupID))
                continue;

            if (SupportsOutputKeying(deviceInfo.device, keyingMode))
                return true;
        }
        return false;
    }

    bool DeckLinkDeviceDiscovery::IsDeviceMatchingGroupID(IDeckLink* const deckLink, const int64_t deviceGroupID)
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

        deckLinkAttributes->Release();

        return groupID == deviceGroupID;
    }

    bool DeckLinkDeviceDiscovery::TryGetConnectorMappingProfile(const EConnectorMapping profile, _BMDProfileID* bmdProfile)
    {
        switch (profile)
        {
        case EConnectorMapping::FourSubDevicesHalfDuplex:
            *bmdProfile = bmdProfileFourSubDevicesHalfDuplex;
            return true;
        case EConnectorMapping::OneSubDeviceFullDuplex:
            *bmdProfile = bmdProfileOneSubDeviceFullDuplex;
            return true;
        case EConnectorMapping::OneSubDeviceHalfDuplex:
            *bmdProfile = bmdProfileOneSubDeviceHalfDuplex;
            return true;
        case EConnectorMapping::TwoSubDevicesFullDuplex:
            *bmdProfile = bmdProfileTwoSubDevicesFullDuplex;
            return true;
        case EConnectorMapping::TwoSubDevicesHalfDuplex:
            *bmdProfile = bmdProfileTwoSubDevicesHalfDuplex;
            return true;
        default:
            return false;
        }
    }

    bool DeckLinkDeviceDiscovery::TryGetConnectorMappingProfile(const _BMDProfileID bmdProfile, EConnectorMapping* profile)
    {
        switch (bmdProfile)
        {
        case _BMDProfileID::bmdProfileFourSubDevicesHalfDuplex:
            *profile = EConnectorMapping::FourSubDevicesHalfDuplex;
            return true;
        case _BMDProfileID::bmdProfileOneSubDeviceFullDuplex:
            *profile = EConnectorMapping::OneSubDeviceFullDuplex;
            return true;
        case _BMDProfileID::bmdProfileOneSubDeviceHalfDuplex:
            *profile = EConnectorMapping::OneSubDeviceHalfDuplex;
            return true;
        case _BMDProfileID::bmdProfileTwoSubDevicesFullDuplex:
            *profile = EConnectorMapping::TwoSubDevicesFullDuplex;
            return true;
        case _BMDProfileID::bmdProfileTwoSubDevicesHalfDuplex:
            *profile = EConnectorMapping::TwoSubDevicesHalfDuplex;
            return true;
        default:
            return false;
        }
    }

    bool DeckLinkDeviceDiscovery::TryGetConnectorMappingProfileDeckLinkCard(IDeckLink* const deckLink, EConnectorMapping& profile)
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

        for (auto const& l : m_ConnectorMappings)
        {
            if (l.groupID == groupID)
            {
                profile = l.connectorMapping;
                deckLinkAttributes->Release();
                return true;
            }
        }

        deckLinkAttributes->Release();

        return false;
    }

    bool DeckLinkDeviceDiscovery::SetDeviceProfile(IDeckLink* device, DeviceInfo& deviceInfo)
    {
        auto queryDeckLinkConfigurationSucceed = true;
        IDeckLinkProfileManager* manager = nullptr;
        IDeckLinkProfileIterator* deckLinkProfileIterator = nullptr;
        IDeckLinkProfile* deckLinkProfile = nullptr;
        _BMDProfileID bmd_profile_id;

        EConnectorMapping profileCard;
        if (!TryGetConnectorMappingProfileDeckLinkCard(device, profileCard))
            return false;

        if (TryGetConnectorMappingProfile(profileCard, &bmd_profile_id) == false ||
            device->QueryInterface(IID_IDeckLinkProfileManager, (void**)&manager) != S_OK)
        {
            queryDeckLinkConfigurationSucceed = false;
            goto cleanup;
        }

        // Get Profile Iterator and enumerate the profiles for the device
        if ((manager->GetProfiles(&deckLinkProfileIterator) != S_OK) || (deckLinkProfileIterator == nullptr))
        {
            queryDeckLinkConfigurationSucceed = false;
            goto cleanup;
        }

        // We detect again all the compatible profiles, since they may have changed from the previous iteration.
        deviceInfo.compatibleProfiles.clear();

        while (deckLinkProfileIterator->Next(&deckLinkProfile) == S_OK)
        {
            // Get the current profile ID
            _BMDProfileID profileID;
            if (GetDeckLinkProfileID(deckLinkProfile, &profileID) != S_OK)
            {
                deckLinkProfile->Release();
                queryDeckLinkConfigurationSucceed = false;
                break;
            }

            // Add the current profile ID to the list of compatible profiles
            EConnectorMapping profile;
            if (TryGetConnectorMappingProfile(profileID, &profile))
            {
                deviceInfo.compatibleProfiles.push_back(profile);
            }

            // If the profile is active and the current selected one, we can discard this iteration.
            dlbool_t isActive;
            auto result = deckLinkProfile->IsActive(&isActive);
            if (result != S_OK || isActive)
            {
                deckLinkProfile->Release();
                continue;
            }

            // If the current profile is the one we are looking for.
            if (profileID == bmd_profile_id)
            {
                auto profileCallback = new DeckLinkProfileCallback(deckLinkProfile);
                manager->SetCallback(profileCallback);

                // We set the current profile as active.
                auto result = deckLinkProfile->SetActive();

                // We keep track of the new current profile, and we release the previous reference.
                if (deviceInfo.deckLinkProfile != deckLinkProfile)
                {
                    if (deviceInfo.deckLinkProfile != nullptr)
                    {
                        deviceInfo.deckLinkProfile->Release();
                    }
                    deviceInfo.deckLinkProfile = deckLinkProfile;
                }

                // We make sure the new profile is really active.
                if (result == E_ACCESSDENIED)
                {
                    queryDeckLinkConfigurationSucceed = false;
                }
                else if (result == E_FAIL)
                {
                    queryDeckLinkConfigurationSucceed = false;
                }
                else
                {
                    if (!profileCallback->WaitForProfileActivation())
                    {
                        queryDeckLinkConfigurationSucceed = false;
                    }
                }
            }

            // We release the current profile iteration.
            deckLinkProfile->Release();
        }

    cleanup:

        if (manager != nullptr)
            manager->Release();

        if (deckLinkProfileIterator != nullptr)
            deckLinkProfileIterator->Release();

        return queryDeckLinkConfigurationSucceed;
    }

    HRESULT DeckLinkDeviceDiscovery::GetDeckLinkProfileID(IDeckLinkProfile* profile, _BMDProfileID* profileID)
    {
        IDeckLinkProfileAttributes* profileAttributes = nullptr;
        HRESULT	result;

        result = profile->QueryInterface(IID_IDeckLinkProfileAttributes, (void**)&profileAttributes);
        if (result != S_OK)
            *profileID = (_BMDProfileID)0;
        else
        {
            int64_t	profileIDInt;
            result = profileAttributes->GetInt(BMDDeckLinkProfileID, &profileIDInt);
            *profileID = (_BMDProfileID)((result == S_OK) ? profileIDInt : 0);
            profileAttributes->Release();
        }

        return result;
    }

    bool DeckLinkDeviceDiscovery::HasConnectorMappingProfiles()
    {
        auto hasConnectorMapping = false;
        for (const auto& deviceInfo : m_ActiveDevices)
        {
            if (deviceInfo.hasConnectorMapping)
            {
                hasConnectorMapping = deviceInfo.hasConnectorMapping;
            }
        }
        return hasConnectorMapping;
    }

    bool DeckLinkDeviceDiscovery::IsConnectorMappingProfileCompatible(const EConnectorMapping profile)
    {
        for (const auto& deviceInfo : m_ActiveDevices)
        {
            const auto device = deviceInfo.device;
            const auto listProfiles = deviceInfo.compatibleProfiles;

            auto profileIsCompatible =
                std::find(std::begin(listProfiles), std::end(listProfiles), profile) != std::end(listProfiles);

            if (profileIsCompatible)
            {
                return true;
            }
        }
        return false;
    }

    void DeckLinkDeviceDiscovery::ReloadDeckLinkDevicesEvent(IDeckLink& device)
    {
        dlstring_t name;
        if (device.GetDisplayName(&name) == S_OK)
        {
            const auto stdName = DlToStdString(name);
            const auto deviceType = CheckDeckLinkDeviceType(&device);

            if (!stdName.empty())
            {
                m_deviceNameArrivedCallback(stdName.c_str(), static_cast<int>(deviceType));
            }
            DeleteString(name);
        }
    }

    void DeckLinkDeviceDiscovery::ReloadAllDeckLinkDevicesEvent()
    {
        for (auto& deviceInfo : m_ActiveDevices)
        {
            ReloadDeckLinkDevicesEvent(*deviceInfo.device);
        }
    }
}
