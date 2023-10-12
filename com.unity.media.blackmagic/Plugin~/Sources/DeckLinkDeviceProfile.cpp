#include "DeckLinkDeviceProfile.h"

namespace MediaBlackmagic
{
    DeckLinkDeviceProfile::DeckLinkDeviceProfile() :
        m_refCount(1),
        m_profileChangedCallbackDevice(nullptr),
        m_profileActivatedCallbackDevice(nullptr),
        m_profileChangedHaltStreamsCallback(nullptr),
        m_profileActivatedCallback(nullptr)
    {
    }

    HRESULT DeckLinkDeviceProfile::ProfileChanging(IDeckLinkProfile* profileToBeActivated, dlbool_t streamsWillBeForcedToStop)
    {
        // When streamsWillBeForcedToStop is true, the profile to be activated is incompatible with the current
        // profile and capture will be stopped by the DeckLink driver. It is better to notify the
        // controller to gracefully stop capture, so that the UI is set to a known state.
        if (m_profileChangedCallbackDevice && streamsWillBeForcedToStop)
        {
            if (streamsWillBeForcedToStop)
            {
                com_ptr<IDeckLinkProfile> profile(profileToBeActivated);
                m_profileChangedCallbackDevice(profile);
            }
        }

        if (m_profileChangedHaltStreamsCallback)
        {
            m_profileChangedHaltStreamsCallback(streamsWillBeForcedToStop);
        }

        return S_OK;
    }

    HRESULT DeckLinkDeviceProfile::ProfileActivated(IDeckLinkProfile* activatedProfile)
    {
        // New profile activated
        if (m_profileActivatedCallbackDevice)
        {
            com_ptr<IDeckLinkProfile> profile(activatedProfile);
            m_profileActivatedCallbackDevice(profile);
        }

        if (m_profileActivatedCallback)
        {
            m_profileActivatedCallback();
        }

        return S_OK;
    }

    HRESULT DeckLinkDeviceProfile::QueryInterface(REFIID iid, LPVOID* ppv)
    {
        HRESULT result = E_NOINTERFACE;

        if (ppv == nullptr)
            return E_INVALIDARG;

        // Initialise the return result
        *ppv = nullptr;

        // Obtain the IUnknown interface and compare it the provided REFIID
        if (iid == IID_IUnknown)
        {
            *ppv = this;
            AddRef();
            result = S_OK;
        }
        else if (iid == IID_IDeckLinkProfileCallback)
        {
            *ppv = static_cast<IDeckLinkProfileCallback*>(this);
            AddRef();
            result = S_OK;

        }

        return result;
    }

    ULONG DeckLinkDeviceProfile::AddRef(void)
    {
        return ++m_refCount;
    }

    ULONG DeckLinkDeviceProfile::Release(void)
    {
        ULONG newRefValue = --m_refCount;

        if (newRefValue == 0)
            delete this;

        return newRefValue;
    }
}
