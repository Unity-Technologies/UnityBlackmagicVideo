#pragma once

#include "IUnityGraphics.h"

namespace MediaBlackmagic
{
    void OnPluginLoadGraphics(IUnityInterfaces* unityInterfaces);
    void OnPluginUnloadGraphics();

    static void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType);

    static void UNITY_INTERFACE_API OnRenderEvent(int eventID, void* data);

    struct InitializeGPUDirectID
    {
        void* devicePtr;
    };

    struct FeedFrameID
    {
        void* devicePtr;
        void* bufferData;
        unsigned int bcd;
    };
}
