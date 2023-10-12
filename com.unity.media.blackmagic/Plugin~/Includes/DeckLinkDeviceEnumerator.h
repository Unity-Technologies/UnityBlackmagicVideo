#pragma once

#include <vector>
#include <string>
#include "../Common.h"
#include "../external/Unity/IUnityRenderingExtensions.h"

namespace MediaBlackmagic
{
    class DeckLinkDeviceEnumerator final
    {
        using DeviceNames = std::vector<std::string>;
        using DeviceModes = std::vector<int>;

    public:
        ~DeckLinkDeviceEnumerator();

        int  CopyStringPointers(void* pointers[], int maxCount) const;
        int  CopyInputDevicesPointers(void* inputDevices[], int maxCount) const;
        int  CopyOutputDevicesPointers(void* outputDevices[], int maxCount) const;
        int  CopyModes(int modes[], int maxCount) const;

        void ScanAllDeviceNames();
        void ScanInputDeviceNames();
        void ScanOutputDeviceNames();

        void ScanOutputModes(int deviceIndex);
        bool SetAllDevicesDuplexMode(bool halfDuplex);

    private:
        DeviceNames m_InputDevices;
        DeviceNames m_OutputDevices;
        DeviceNames m_Names;
        DeviceModes m_Modes;

        void clearAllDeviceNames();
        void clearInputDeviceNames();
        void clearOutputDeviceNames();
        void clearModes();
    };
}

#pragma region Enumeration plugin functions

namespace { MediaBlackmagic::DeckLinkDeviceEnumerator enumerator; }

extern "C" int UNITY_INTERFACE_EXPORT RetrieveInputDeviceNames(void* inputDevices[], int maxCount)
{
    enumerator.ScanInputDeviceNames();
    return enumerator.CopyInputDevicesPointers(inputDevices, maxCount);
}

extern "C" int UNITY_INTERFACE_EXPORT RetrieveOutputDeviceNames(void* outputDevices[], int maxCount)
{
    enumerator.ScanOutputDeviceNames();
    return enumerator.CopyOutputDevicesPointers(outputDevices, maxCount);
}

extern "C" int UNITY_INTERFACE_EXPORT RetrieveDeviceNames(void* names[], int maxCount)
{
    enumerator.ScanAllDeviceNames();
    return enumerator.CopyStringPointers(names, maxCount);
}

extern "C" int UNITY_INTERFACE_EXPORT RetrieveOutputModes(int deviceIndex, int modes[], int maxCount)
{
    enumerator.ScanOutputModes(deviceIndex);
    return enumerator.CopyModes(modes, maxCount);
}

extern "C" bool UNITY_INTERFACE_EXPORT SetAllDevicesDuplexMode(bool halfDuplex)
{
    return enumerator.SetAllDevicesDuplexMode(halfDuplex);
}

#pragma endregion
