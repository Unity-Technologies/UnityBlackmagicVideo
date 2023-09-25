#pragma once

#include <iostream>
#include <chrono>
#include <map>
#include <condition_variable>
#include <mutex>
#include "../Common.h"

namespace MediaBlackmagic
{
    class DeckLinkProfileCallback : public IDeckLinkProfileCallback
    {
        const std::chrono::seconds kProfileActivationTimeout{ 5 };

        HRESULT GetDeckLinkProfileID(IDeckLinkProfile* profile, BMDProfileID* profileID);

    public:
        DeckLinkProfileCallback(IDeckLinkProfile* requestedProfile);
        virtual ~DeckLinkProfileCallback();

        bool WaitForProfileActivation(void);

        HRESULT STDMETHODCALLTYPE ProfileChanging(IDeckLinkProfile* profileToBeActivated, dlbool_t /*streamsWillBeForcedToStop*/) override;
        HRESULT STDMETHODCALLTYPE ProfileActivated(IDeckLinkProfile* activatedProfile) override;

        HRESULT	STDMETHODCALLTYPE QueryInterface(REFIID iid, LPVOID* ppv) override;
        ULONG	STDMETHODCALLTYPE AddRef() override;
        ULONG	STDMETHODCALLTYPE Release() override;

    private:
        std::condition_variable m_profileActivatedCondition;
        std::mutex              m_profileActivatedMutex;
        BMDProfileID            m_requestedProfileID;
        IDeckLinkProfile*       m_requestedProfile;
        bool                    m_requestedProfileActivated;
        std::atomic<ULONG>      m_refCount;
    };
}
