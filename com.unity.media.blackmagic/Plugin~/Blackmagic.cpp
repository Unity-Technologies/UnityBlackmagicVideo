#include "ObjectIDMap.h"
#include "Includes/BlackmagicPluginEvents.h"
#include "Includes/DeckLinkInputDevice.h"
#include "Includes/DeckLinkOutputDevice.h"
#include "external/Unity/IUnityInterface.h"
#include "external/Unity/IUnityProfiler.h"
#include "external/Unity/IUnityRenderingExtensions.h"
#include "LicenseSecurity.h"
#include "Includes/PluginUtils.h"

#pragma region Local functions

namespace
{
    static IUnityProfiler* s_UnityProfiler = NULL;
    static const UnityProfilerMarkerDesc* s_TextureUpdateLockMarker = NULL;
    static MediaBlackmagic::LicenseSecurity s_LicenseSecurity;
    const std::string k_LicenseInvalid = "Your Unity license is not compatible with the Blackmagic Video package.";

    extern "C" const void UNITY_INTERFACE_EXPORT
        HandleEntitlementValidityKey(void* key)
    {
        if (key == nullptr)
            return;

        s_LicenseSecurity.HandleEntitlementValidityKey(reinterpret_cast<char*>(key));
    }

    extern "C" void UNITY_INTERFACE_EXPORT SetRequirementErrorCallback(MediaBlackmagic::LicenseSecurity::PackageRequirementError callback)
    {
        if (callback == nullptr)
            return;
        s_LicenseSecurity.SetRequirementErrorCallback(callback);
    }

    // ID-DeckLinkInputDevice map
    MediaBlackmagic::ObjectIDMap<MediaBlackmagic::DeckLinkInputDevice> s_InputDeviceMap;

    // Callback for texture update events
    static void UNITY_INTERFACE_API TextureUpdateCallback(int eventID, void* data)
    {
        if (data == nullptr || s_InputDeviceMap.ObjectCount() <= 0)
            return;

        auto event = static_cast<UnityRenderingExtEventType>(eventID);
        if (event == kUnityRenderingExtEventUpdateTextureBeginV2)
        {
            auto params = reinterpret_cast<UnityRenderingExtTextureUpdateParamsV2*>(data);
            auto inputDevice = s_InputDeviceMap[params->userData];
            if (inputDevice == nullptr)
                return;

            params->texData = inputDevice->GetTextureData();

            const auto graphicsAPI = inputDevice->GetGraphicsAPI();

            if (graphicsAPI != kUnityGfxRendererOpenGLCore)
            {
                inputDevice->LockQueue();
            }

            if (s_UnityProfiler != NULL)
            {
                s_UnityProfiler->BeginSample(s_TextureUpdateLockMarker);
            }
        }
        else if (event == kUnityRenderingExtEventUpdateTextureEndV2)
        {
            auto params = reinterpret_cast<UnityRenderingExtTextureUpdateParamsV2*>(data);
            auto inputDevice = s_InputDeviceMap[params->userData];
            if (inputDevice == nullptr)
                return;

            if (s_UnityProfiler != NULL)
            {
                s_UnityProfiler->EndSample(s_TextureUpdateLockMarker);
            }

            const auto graphicsAPI = inputDevice->GetGraphicsAPI();
            if (graphicsAPI != kUnityGfxRendererOpenGLCore)
            {
                inputDevice->UnlockQueue();
            }
        }
    }
}

#pragma endregion

#pragma region Plugin common functions

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* unityInterfaces)
{
    MediaBlackmagic::WriteFileDebug("Load plugin\n", false);

    s_UnityProfiler = unityInterfaces->Get<IUnityProfiler>();

    if (s_UnityProfiler != NULL)
    {
        s_UnityProfiler->CreateMarker(&s_TextureUpdateLockMarker, "TextureUpdateLock", kUnityProfilerCategoryOther, kUnityProfilerMarkerFlagDefault, 0);
    }

    MediaBlackmagic::OnPluginLoadGraphics(unityInterfaces);
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload()
{
    MediaBlackmagic::WriteFileDebug("Unload plugin\n");

    s_UnityProfiler = NULL;

    MediaBlackmagic::OnPluginUnloadGraphics();
}

extern "C" UnityRenderingEventAndData UNITY_INTERFACE_EXPORT GetTextureUpdateCallback()
{
    return TextureUpdateCallback;
}

#pragma endregion

#pragma region Input Device plugin functions

extern "C" void UNITY_INTERFACE_EXPORT SetInputFrameErrorCallback(MediaBlackmagic::DeckLinkInputDevice::FrameErrorCallback callback)
{
    MediaBlackmagic::DeckLinkInputDevice::SetFameErrorCallback(callback);
}

extern "C" void UNITY_INTERFACE_EXPORT SetVideoFormatChangedCallback(MediaBlackmagic::DeckLinkInputDevice::VideoFormatChangedCallback callback)
{
    MediaBlackmagic::DeckLinkInputDevice::SetVideoFormatChangedCallback(callback);
}

extern "C" void UNITY_INTERFACE_EXPORT SetFrameArrivedCallback(MediaBlackmagic::DeckLinkInputDevice::FrameArrivedCallback callback)
{
    MediaBlackmagic::DeckLinkInputDevice::SetFrameArrivedCallback(callback);
}

extern "C" void UNITY_INTERFACE_EXPORT * CreateInputDevice(int deviceIndex, int deviceSelected, int format, int pixelFormat, bool enablePassThrough, int graphicsAPI, MediaBlackmagic::DeckLinkInputDevice::InputVideoFormatData* selectedFormat)
{
    const auto instance = new MediaBlackmagic::DeckLinkInputDevice();
    s_InputDeviceMap.Add(instance);

    auto graphicsAPIEnum = static_cast<UnityGfxRenderer>(graphicsAPI);
    instance->Start(deviceIndex, deviceSelected, format, pixelFormat, enablePassThrough, graphicsAPIEnum, selectedFormat);

    return instance;
}

extern "C" void UNITY_INTERFACE_EXPORT DestroyInputDevice(void* inputDevice)
{
    if (inputDevice == nullptr)
        return;
    const auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkInputDevice*>(inputDevice);
    if (instance == nullptr)
        return;

    s_InputDeviceMap.Remove(instance);
    instance->Stop();
    instance->Release();
}

extern "C" bool UNITY_INTERFACE_EXPORT IsInputDeviceInitialized(void* inputDevice)
{
    if (inputDevice == nullptr)
        return false;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkInputDevice*>(inputDevice);
    if (instance == nullptr)
        return false;
    return instance->IsInitialized();
}

extern "C" void UNITY_INTERFACE_EXPORT SetTextureUpdateSource(void* inputDevice, uint8_t* textureData)
{
    if (inputDevice == nullptr)
        return;
    const auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkInputDevice*>(inputDevice);
    if (instance == nullptr)
        return;

    instance->SetTextureData(textureData);
}

extern "C" void UNITY_INTERFACE_EXPORT LockInputDeviceQueue(void* inputDevice)
{
    if (inputDevice == nullptr)
        return;
    const auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkInputDevice*>(inputDevice);
    if (instance == nullptr)
        return;

    instance->LockQueue();
}

extern "C" void UNITY_INTERFACE_EXPORT UnlockInputDeviceQueue(void* inputDevice)
{
    if (inputDevice == nullptr)
        return;
    const auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkInputDevice*>(inputDevice);
    if (instance == nullptr)
        return;
    
    instance->UnlockQueue();
}

extern "C" unsigned int UNITY_INTERFACE_EXPORT GetInputDeviceID(void* inputDevice)
{
    if (inputDevice == nullptr)
        return 0;
    const auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkInputDevice*>(inputDevice);
    if (instance == nullptr)
        return 0;

    return s_InputDeviceMap.GetID(instance);
}

extern "C" bool UNITY_INTERFACE_EXPORT GetHasInputSource(void* inputDevice)
{
    if (inputDevice == nullptr)
        return false;
    const auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkInputDevice*>(inputDevice);
    if (instance == nullptr)
        return false;

    return instance->GetHasInputSource();
}

#pragma endregion

#pragma region Output Device plugin functions

extern "C" void UNITY_INTERFACE_EXPORT * CreateAsyncOutputDevice(int deviceIndex,
                                                                 int deviceSelected,
                                                                 int displayMode,
                                                                 int pixelFormat,
                                                                 int colorSpace,
                                                                 int transferFunction,
                                                                 int preroll,
                                                                 bool enableAudio,
                                                                 int audioChannelCount,
                                                                 int audioSampleRate,
                                                                 bool useGPUDirect)
{
    auto instance = new MediaBlackmagic::DeckLinkOutputDevice();
    auto mode = static_cast<BMDDisplayMode>(displayMode);
    instance->StartAsyncMode(deviceIndex, deviceSelected, mode, pixelFormat, colorSpace, transferFunction, preroll, enableAudio, audioChannelCount, audioSampleRate, useGPUDirect);
    return instance;
}

extern "C" void UNITY_INTERFACE_EXPORT * CreateManualOutputDevice(int deviceIndex,
                                                                  int deviceSelected,
                                                                  int displayMode,
                                                                  int pixelFormat,
                                                                  int colorSpace,
                                                                  int transferFunction,
                                                                  int preroll,
                                                                  bool enableAudio,
                                                                  int audioChannelCount,
                                                                  int audioSampleRate,
                                                                  bool useGPUDirect)
{
    auto instance = new MediaBlackmagic::DeckLinkOutputDevice();
    auto mode = static_cast<BMDDisplayMode>(displayMode);
    instance->StartManualMode(deviceIndex, deviceSelected, mode, pixelFormat, colorSpace, transferFunction, preroll, enableAudio, audioChannelCount, audioSampleRate, useGPUDirect);
    return instance;
}

extern "C" void UNITY_INTERFACE_EXPORT DestroyOutputDevice(void* outputDevice)
{
    if (outputDevice == nullptr)
        return;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    if (instance == nullptr)
        return;
    instance->Stop();
    instance->Release();
}

extern "C" bool UNITY_INTERFACE_EXPORT IsOutputDeviceInitialized(void* outputDevice)
{
    if (outputDevice == nullptr)
        return false;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    if (instance == nullptr)
        return false;
    return instance->IsInitialized();
}

extern "C" int UNITY_INTERFACE_EXPORT GetOutputDeviceFrameWidth(void* outputDevice)
{
    if (outputDevice == nullptr)
        return 0;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    if (instance == nullptr)
        return 0;
    return std::get<0>(instance->GetFrameDimensions());
}

extern "C" int UNITY_INTERFACE_EXPORT GetOutputDeviceFrameHeight(void* outputDevice)
{
    if (outputDevice == nullptr)
        return 0;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    if (instance == nullptr)
        return 0;
    return std::get<1>(instance->GetFrameDimensions());
}

extern "C" void UNITY_INTERFACE_EXPORT GetOutputDeviceFrameRate(void* outputDevice, std::int32_t* numerator, std::int32_t* denominator)
{
    if (outputDevice == nullptr)
        return;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    if (instance == nullptr)
        return;
    return instance->GetFrameRate(*numerator, *denominator);
}

extern "C" void UNITY_INTERFACE_EXPORT * GetOutputDevicePixelFormat(void* outputDevice)
{
    if (outputDevice == nullptr)
        return nullptr;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    if (instance == nullptr)
        return nullptr;

    return const_cast<char*>(instance->RetrievePixelFormat().c_str());
}

extern "C" std::int64_t UNITY_INTERFACE_EXPORT GetOutputDeviceFrameDuration(void* outputDevice)
{
    if (outputDevice == nullptr)
        return 0;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    if (instance == nullptr)
        return 0;
    return instance->GetFrameDuration();
}

extern "C" int UNITY_INTERFACE_EXPORT IsOutputDeviceProgressive(void* outputDevice)
{
    if (outputDevice == nullptr)
        return 0;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    if (instance == nullptr)
        return 0;
    return instance->IsProgressive() ? 1 : 0;
}

extern "C" void UNITY_INTERFACE_EXPORT GetOutputDeviceBackingFrameByteDimensions(void* outputDevice, std::uint32_t & w, std::uint32_t & h, std::uint32_t & d)
{
    w = h = d = 0;
    if (outputDevice == nullptr)
        return;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    if (instance == nullptr)
        return;
    w = instance->GetBackingFrameByteWidth();
    h = instance->GetBackingFrameByteHeight();
    d = instance->GetBackingFrameByteDepth();
}

extern "C" int UNITY_INTERFACE_EXPORT IsOutputDeviceReferenceLocked(void* outputDevice)
{
    if (outputDevice == nullptr)
        return 0;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    if (instance == nullptr)
        return 0;
    return instance->IsReferenceLocked() ? 1 : 0;
}

extern "C" void UNITY_INTERFACE_EXPORT FeedFrameToOutputDevice(void* outputDevice, void* frameData, unsigned int timecode)
{
    if (outputDevice == nullptr)
        return;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    if (instance == nullptr)
        return;
    instance->FeedFrame(frameData, timecode);
}

extern "C" void UNITY_INTERFACE_EXPORT WaitOutputDeviceCompletion(void* outputDevice, std::int64_t frameNumber)
{
    if (outputDevice == nullptr)
        return;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    if (instance == nullptr)
        return;
    instance->WaitFrameCompletion(frameNumber);
}

extern "C" const unsigned int UNITY_INTERFACE_EXPORT CountDroppedOutputDeviceFrames(void* outputDevice)
{
    if (outputDevice == nullptr)
        return 0;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    if (instance == nullptr)
        return 0;
    return instance->CountDroppedFrames();
}

extern "C" const unsigned int UNITY_INTERFACE_EXPORT CountLateOutputDeviceFrames(void* outputDevice)
{
    if (outputDevice == nullptr)
        return 0;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    if (instance == nullptr)
        return 0;
    return instance->CountLateFrames();
}

extern "C" void UNITY_INTERFACE_EXPORT
FeedAudioSampleFramesToOutputDevice(MediaBlackmagic::DeckLinkOutputDevice * outputDevice, float* sampleFrames, int sampleCount)
{
    if (outputDevice == nullptr)
        return;

    outputDevice->FeedAudioSampleFrames(sampleFrames, sampleCount);
}

extern "C" const void UNITY_INTERFACE_EXPORT * GetOutputDeviceError(void* outputDevice)
{
    if (outputDevice == nullptr)
        return nullptr;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    if (instance == nullptr)
        return nullptr;
    const auto& error = instance->GetErrorString();
    return error.empty() ? nullptr : error.c_str();
}

extern "C" void UNITY_INTERFACE_EXPORT SetFrameErrorCallback(void* outputDevice,
    MediaBlackmagic::DeckLinkOutputDevice::FrameError callback)
{
    if (outputDevice == nullptr || callback == nullptr)
        return;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    instance->SetFameErrorCallback(callback);
}

extern "C" void UNITY_INTERFACE_EXPORT SetFrameCompletedCallback(void* outputDevice,
    MediaBlackmagic::DeckLinkOutputDevice::FrameCompleted callback)
{
    if (outputDevice == nullptr || callback == nullptr)
        return;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    instance->SetFrameCompletedCallback(callback);
}

extern "C" bool UNITY_INTERFACE_EXPORT IsValidConfiguration(void* outputDevice)
{
    if (outputDevice == nullptr)
        return false;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    return instance->IsValidConfiguration();
}

extern "C" bool UNITY_INTERFACE_EXPORT IsOutputKeyingCompatible(void* outputDevice, int keying)
{
    if (outputDevice == nullptr)
        return false;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    return instance->SupportsKeying(static_cast<MediaBlackmagic::EOutputKeyingMode>(keying));
}

extern "C" bool UNITY_INTERFACE_EXPORT InitializeOutputKeyerParameters(void* outputDevice, int keying)
{
    if (outputDevice == nullptr)
        return false;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    return instance->InitializeKeyerParameters(static_cast<MediaBlackmagic::EOutputKeyingMode>(keying));
}

extern "C" bool UNITY_INTERFACE_EXPORT ChangeKeyingMode(void* outputDevice, int keying)
{
    if (outputDevice == nullptr)
        return false;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    return instance->ChangeKeyingMode(static_cast<MediaBlackmagic::EOutputKeyingMode>(keying));
}

extern "C" bool UNITY_INTERFACE_EXPORT DisableKeying(void* outputDevice)
{
    if (outputDevice == nullptr)
        return false;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    return instance->DisableKeying();
}

extern "C" void UNITY_INTERFACE_EXPORT SetDefaultScheduleTime(void* outputDevice, float defaultTime)
{
    if (outputDevice == nullptr)
        return;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    instance->SetDefaultScheduleTime(defaultTime);
}

extern "C" bool UNITY_INTERFACE_EXPORT IsOutputLinkCompatible(void* outputDevice, int mode)
{
    if (outputDevice == nullptr)
        return false;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    return instance->IsSupportedLinkMode(static_cast<MediaBlackmagic::EOutputLinkMode>(mode));
}

extern "C" bool UNITY_INTERFACE_EXPORT SetOutputLinkMode(void* outputDevice, int mode)
{
    if (outputDevice == nullptr)
        return false;
    auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputDevice);
    return instance->SetLinkConfiguration(static_cast<MediaBlackmagic::EOutputLinkMode>(mode));
}

#pragma endregion
