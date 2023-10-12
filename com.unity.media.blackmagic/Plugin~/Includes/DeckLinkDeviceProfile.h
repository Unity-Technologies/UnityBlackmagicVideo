#pragma once

#include <atomic>
#include <functional>

#include "com_ptr.h"
#include "../Common.h"
#include "../ObjectIDMap.h"
#include "../external/Unity/IUnityRenderingExtensions.h"

namespace MediaBlackmagic
{
    typedef void(UNITY_INTERFACE_API* CallbackProfileChanged)(bool);
    typedef void(UNITY_INTERFACE_API* CallbackProfileActivated)(void);

    class DeckLinkDeviceProfile : public IDeckLinkProfileCallback
    {
        using CallbackProfileChangedDevice = std::function<void(com_ptr<IDeckLinkProfile>&)>;

    public:
        DeckLinkDeviceProfile();
        virtual ~DeckLinkDeviceProfile() = default;

        inline void SetOnProfileChangedCallback(const CallbackProfileChangedDevice& callback) { m_profileChangedCallbackDevice = callback; }
        inline void SetOnProfileActivatedCallback(const CallbackProfileChangedDevice& callback) { m_profileActivatedCallbackDevice = callback; };

        inline void SetOnProfileChangedCallback(const CallbackProfileChanged& callback) { m_profileChangedHaltStreamsCallback = callback; }
        inline void SetOnProfileActivatedCallback(const CallbackProfileActivated& callback) { m_profileActivatedCallback = callback; };

        // IDeckLinkProfileCallback interface
        HRESULT		STDMETHODCALLTYPE ProfileChanging(IDeckLinkProfile* profileToBeActivated, dlbool_t streamsWillBeForcedToStop) override;
        HRESULT		STDMETHODCALLTYPE ProfileActivated(IDeckLinkProfile* activatedProfile) override;

        // IUnknown needs only a dummy implementation
        HRESULT		STDMETHODCALLTYPE QueryInterface(REFIID iid, LPVOID* ppv) override;
        ULONG		STDMETHODCALLTYPE AddRef() override;
        ULONG		STDMETHODCALLTYPE Release() override;

    private:
        CallbackProfileChangedDevice	m_profileChangedCallbackDevice;
        CallbackProfileChangedDevice	m_profileActivatedCallbackDevice;

        CallbackProfileChanged	        m_profileChangedHaltStreamsCallback;
        CallbackProfileActivated	    m_profileActivatedCallback;

        std::atomic<ULONG>		        m_refCount;
    };
}

namespace
{
    MediaBlackmagic::ObjectIDMap<MediaBlackmagic::DeckLinkDeviceProfile> s_DeckLinkDeviceProfile;

    inline MediaBlackmagic::DeckLinkDeviceProfile* GetInstanceDeckLinkProfileCallback(void* deviceProfile)
    {
        if (deviceProfile == nullptr)
            return nullptr;

        return reinterpret_cast<MediaBlackmagic::DeckLinkDeviceProfile*>(deviceProfile);
    }
}

extern "C" void UNITY_INTERFACE_EXPORT * CreateDeckLinkDeviceProfileInstance()
{
    auto instance = new MediaBlackmagic::DeckLinkDeviceProfile();
    s_DeckLinkDeviceProfile.Add(instance);
    return instance;
}

extern "C" const void UNITY_INTERFACE_EXPORT
SetOnProfileChangedCallback(void* deviceProfile, MediaBlackmagic::CallbackProfileChanged callBack)
{
    auto instance = GetInstanceDeckLinkProfileCallback(deviceProfile);
    if (instance == nullptr || callBack == nullptr)
        return;

    instance->SetOnProfileChangedCallback(callBack);
}

extern "C" const void UNITY_INTERFACE_EXPORT
SetOnProfileActivatedCallback(void* deviceProfile, MediaBlackmagic::CallbackProfileActivated callBack)
{
    auto instance = GetInstanceDeckLinkProfileCallback(deviceProfile);
    if (instance == nullptr || callBack == nullptr)
        return;

    instance->SetOnProfileActivatedCallback(callBack);
}

extern "C" const unsigned int UNITY_INTERFACE_EXPORT
DestroyDeckLinkDeviceProfileInstance(void* deviceProfile)
{
    auto instance = GetInstanceDeckLinkProfileCallback(deviceProfile);
    if (instance == nullptr)
        return 0;

    auto instanceId = s_DeckLinkDeviceProfile.GetID(instance);
    s_DeckLinkDeviceProfile.Remove(instance);
    instance->Release();

    return instanceId;
}
