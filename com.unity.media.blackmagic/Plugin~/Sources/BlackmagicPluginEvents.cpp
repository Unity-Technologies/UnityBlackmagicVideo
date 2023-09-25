#if _WIN64
#include <windows.h>
#include <iostream>
#include <sstream>
#include <fstream>

#include "d3d11.h"
#include "IUnityRenderingExtensions.h"
#include "IUnityGraphicsD3D11.h"

#include "BlackmagicPluginEvents.h"
#include "PluginUtils.h"
#include "DeckLinkOutputDevice.h"

enum class BlackmagicOutputEventID
{
    Initialize = 0,
    FeedFrameTexture = 1,
    IsCompatible = 2
};

namespace MediaBlackmagic
{
    static IUnityInterfaces*       s_UnityInterfaces = nullptr;
    static IUnityGraphics*         s_UnityGraphics = nullptr;
    static IUnityGraphicsD3D11*    s_UnityGraphicsD3D11 = nullptr;
    static IUnknown*               s_GraphicsDevice = nullptr;
    static bool                    s_Initialized = false;
    static std::atomic<bool>       s_IsGPUDirectAvailable = false;

#pragma region Low Level Plugin Interface
    void OnPluginLoadGraphics(IUnityInterfaces* unityInterfaces)
    {
        if (unityInterfaces)
        {
            s_UnityInterfaces = unityInterfaces;

            const auto unityGraphics = s_UnityInterfaces->Get<IUnityGraphics>();
            if (unityGraphics)
            {
                s_UnityGraphics = unityGraphics;
                unityGraphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);

                OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize);
            }
        }
    }

    void OnPluginUnloadGraphics()
    {
        if (s_UnityGraphics != nullptr)
        {
            s_UnityGraphics->UnregisterDeviceEventCallback(OnGraphicsDeviceEvent);
        }
    }

    static bool GetRenderDeviceInterface(UnityGfxRenderer renderer)
    {
        switch (renderer)
        {
        case UnityGfxRenderer::kUnityGfxRendererD3D11:
            s_UnityGraphicsD3D11 = s_UnityInterfaces->Get<IUnityGraphicsD3D11>();
            s_GraphicsDevice = s_UnityGraphicsD3D11->GetDevice();
            return true;
        default:
            MediaBlackmagic::WriteFileDebug("Error, graphics API not supported.\n");
            return false;
        }
    }

    // Override function to receive graphics event 
    static void UNITY_INTERFACE_API
        OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
    {
        MediaBlackmagic::WriteFileDebug("OnGraphicsDeviceEvent\n");

        if (eventType == kUnityGfxDeviceEventInitialize && !s_Initialized)
        {
            auto renderer = s_UnityInterfaces->Get<IUnityGraphics>()->GetRenderer();

            if (!GetRenderDeviceInterface(renderer))
                return;

            s_Initialized = true;
        }
        else if (eventType == kUnityGfxDeviceEventShutdown)
        {
            s_Initialized = false;
            s_UnityGraphicsD3D11 = nullptr;
            s_GraphicsDevice = nullptr;
        }
    }

    extern "C" UnityRenderingEventAndData UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
        GetRenderEventFunc()
    {
        return OnRenderEvent;
    }

    void Initialize(void* data);
    void FeedFrameTexture(void* data);
    void CacheIfGPUDirectIsAvailable();

    // Plugin function to handle a specific rendering event
    static void UNITY_INTERFACE_API OnRenderEvent(int eventID, void* data)
    {
        auto event = static_cast<BlackmagicOutputEventID>(eventID);
        switch (event)
        {
        case BlackmagicOutputEventID::Initialize:
        {
            Initialize(data);
            break;
        }
        case BlackmagicOutputEventID::FeedFrameTexture:
        {
            FeedFrameTexture(data);
            break;
        }
        case BlackmagicOutputEventID::IsCompatible:
        {
            CacheIfGPUDirectIsAvailable();
            break;
        }
        default:
            break;
        }
    }
#pragma endregion

#pragma region Render event commands
    // Verify if the data parameters and the D3D11 Device are valid.
    bool AreParametersValid(void* data)
    {
        if (!data)
        {
            MediaBlackmagic::WriteFileDebug("Error, Data send is null.\n");
            return false;
        }

        if (!s_GraphicsDevice)
        {
            MediaBlackmagic::WriteFileDebug("Error, s_D3D11Device is null.\n");
            return false;
        }

        return true;
    }

    void Initialize(void* data)
    {
        WriteFileDebug("OnRenderEvent: Initialize\n");

        if (!AreParametersValid(data))
            return;

        auto outputData = static_cast<InitializeGPUDirectID*>(data);
        auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputData->devicePtr);
        if (instance != nullptr)
        {
            auto dimensions = instance->GetFrameDimensions();

            ID3D11Device* d3d11Device = static_cast<ID3D11Device*>(s_GraphicsDevice);
            ID3D11DeviceContext* d3d11Context;
            d3d11Device->GetImmediateContext(&d3d11Context);

            instance->InitializeGPUDirectResources(d3d11Device, d3d11Context);
        }
    }

    void FeedFrameTexture(void* data)
    {
        if (!AreParametersValid(data))
            return;

        auto outputData = static_cast<FeedFrameID*>(data);
        if (outputData != nullptr)
        {
            auto instance = reinterpret_cast<MediaBlackmagic::DeckLinkOutputDevice*>(outputData->devicePtr);
            if (instance != nullptr)
            {
                instance->FeedFrame(outputData->bufferData, outputData->bcd);
            }
        }
    }

    void CacheIfGPUDirectIsAvailable()
    {
        if (!s_GraphicsDevice)
            return;

        ID3D11Device* d3d11Device = static_cast<ID3D11Device*>(s_GraphicsDevice);
        ID3D11DeviceContext* d3d11Context;
        d3d11Device->GetImmediateContext(&d3d11Context);

        s_IsGPUDirectAvailable = VideoFrameTransfer::IsGPUDirectCompatible(d3d11Device);
    }
#pragma endregion

#pragma region Extern functions
    extern "C" bool UNITY_INTERFACE_EXPORT IsGPUDirectAvailable()
    {
        return s_IsGPUDirectAvailable;
    }
#pragma endregion
}

#else
#include "IUnityInterface.h"
    
namespace MediaBlackmagic
{
    void OnPluginLoadGraphics(IUnityInterfaces* unityInterfaces)
    {
    }

    void OnPluginUnloadGraphics()
    {
    }
}

#endif
