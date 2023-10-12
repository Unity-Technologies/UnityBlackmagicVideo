#include "DeckLinkProfileCallback.h"

namespace MediaBlackmagic
{
    HRESULT DeckLinkProfileCallback::GetDeckLinkProfileID(IDeckLinkProfile* profile, BMDProfileID* profileID)
    {
        IDeckLinkProfileAttributes* profileAttributes = nullptr;
        HRESULT result;

        result = profile->QueryInterface(IID_IDeckLinkProfileAttributes, (void**)&profileAttributes);
        if (result != S_OK)
            *profileID = (BMDProfileID)0;
        else
        {
            int64_t	profileIDInt;

            // Get Profile ID attribute
            result = profileAttributes->GetInt(BMDDeckLinkProfileID, &profileIDInt);

            *profileID = (BMDProfileID)((result == S_OK) ? profileIDInt : 0);

            profileAttributes->Release();
        }

        return result;
    }

    DeckLinkProfileCallback::DeckLinkProfileCallback(IDeckLinkProfile* requestedProfile) : m_refCount(1), m_requestedProfile(requestedProfile), m_requestedProfileActivated(false)
    {
        m_requestedProfile->AddRef();

        GetDeckLinkProfileID(m_requestedProfile, &m_requestedProfileID);
    }

    DeckLinkProfileCallback::~DeckLinkProfileCallback()
    {
        m_requestedProfile->Release();
    }

    bool DeckLinkProfileCallback::WaitForProfileActivation(void)
    {
        dlbool_t isActiveProfile = false;

        // Check whether requested profile is already the active profile, then we can return without waiting
        if ((m_requestedProfile->IsActive(&isActiveProfile) == S_OK) && isActiveProfile)
        {
            return true;
        }
        else
        {
            std::unique_lock<std::mutex> lock(m_profileActivatedMutex);
            if (m_requestedProfileActivated)
                return true;
            else
                // Wait until the ProfileActivated callback occurs
                return m_profileActivatedCondition.wait_for(lock, kProfileActivationTimeout, [&] { return m_requestedProfileActivated; });
        }
    }

    HRESULT STDMETHODCALLTYPE DeckLinkProfileCallback::ProfileChanging(IDeckLinkProfile* profileToBeActivated, dlbool_t /*streamsWillBeForcedToStop*/)
    {
        // The profile change is stalled until we return from the callback. 
        // We don't want to block this callback, otherwise the wait condition may timeout

        BMDProfileID profileID;
        GetDeckLinkProfileID(profileToBeActivated, &profileID);

        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE DeckLinkProfileCallback::ProfileActivated(IDeckLinkProfile* activatedProfile)
    {
        BMDProfileID activatedProfileID;

        GetDeckLinkProfileID(activatedProfile, &activatedProfileID);

        if (activatedProfileID == m_requestedProfileID)
        {
            {
                std::lock_guard<std::mutex> lock(m_profileActivatedMutex);
                m_requestedProfileActivated = true;
            }
            m_profileActivatedCondition.notify_one();
        }

        return S_OK;
    }

    HRESULT	STDMETHODCALLTYPE DeckLinkProfileCallback::QueryInterface(REFIID iid, LPVOID* ppv)
    {
        *ppv = nullptr;
        return E_NOINTERFACE;
    }

    ULONG STDMETHODCALLTYPE	DeckLinkProfileCallback::AddRef()
    {
        return ++m_refCount;
    }

    ULONG STDMETHODCALLTYPE	DeckLinkProfileCallback::Release()
    {
        ULONG refCount = --m_refCount;
        if (refCount == 0)
            delete this;

        return refCount;
    }
}
