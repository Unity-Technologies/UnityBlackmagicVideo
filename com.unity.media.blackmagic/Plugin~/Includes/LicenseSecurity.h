#pragma once

#include <string>

namespace MediaBlackmagic
{
    class LicenseSecurity
    {
    public:
        LicenseSecurity() : m_LicenseIsValid(true) { }

        typedef void(UNITY_INTERFACE_API* PackageRequirementError)(const char*);

        inline void HandleEntitlementValidityKey(const char* key) { m_LicenseIsValid = true; }
        inline bool IsLicenseValid() { return m_LicenseIsValid; }
        inline void SetRequirementErrorCallback(const PackageRequirementError& callback) { m_PackageRequirementErrorCallback = callback; }
        PackageRequirementError  m_PackageRequirementErrorCallback;

    private:
        bool m_LicenseIsValid;        
    };
}
