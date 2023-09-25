using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Media.Blackmagic
{
    struct OutputGPUDirectPlugin
    {
        [DllImport(BlackmagicUtilities.k_PluginName)]
        extern public static IntPtr GetRenderEventFunc();

        [DllImport(BlackmagicUtilities.k_PluginName)]
        extern public static bool IsGPUDirectAvailable();
    }

    /// <summary>
    /// Represents the device informations sent to the Low Level Native plugin
    /// during initialization.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    struct InitializeGPUDirectID
    {
        /// <summary>
        /// Pointer to the Output device.
        /// </summary>
        public IntPtr devicePtr;
    }

    /// <summary>
    /// Represents the frame informations sent to the Low Level Native plugin each frame.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    struct FeedFrameID
    {
        /// <summary>
        /// Pointer to the Output device.
        /// </summary>
        public IntPtr devicePtr;

        /// <summary>
        /// The frame data.
        /// </summary>
        public IntPtr bufferData;

        /// <summary>
        /// The frame timecode.
        /// </summary>
        public uint bcd;
    }

    /// <summary>
    /// Events sent to the Low Level Native plugin.
    /// </summary>
    enum BlackmagicOutputEventID
    {
        /// <summary>
        /// Intializes GPUDirect.
        /// </summary>
        Initialize = 0,

        /// <summary>
        /// Send frame informations to the GPUDirect.
        /// </summary>
        FeedFrameTexture = 1,

        /// <summary>
        /// Caches if GPUDirect is compatible on the current machine.
        /// </summary>
        IsCompatible = 2
    };

    struct BufferedFrameTexture
    {
        public RenderTexture texture;
        public Timecode? timecode;
    }

    class OutputGPUDirect : IDisposable
    {
        CommandBuffer m_CommandBuffer;
        InitializeGPUDirectID m_InitializeID;
        FeedFrameID m_FeedFrameID;

        ~OutputGPUDirect()
        {
            Dispose();
        }

        /// <summary>
        /// Releases the command buffer.
        /// </summary>
        public unsafe void Dispose()
        {
            DisposeCommandBuffer();
        }

        void DisposeCommandBuffer()
        {
            if (m_CommandBuffer != null)
            {
                m_CommandBuffer.Release();
                m_CommandBuffer = null;
            }
        }

        /// <summary>
        /// Initializes the GPUDirect resources.
        /// </summary>
        /// <param name="device">A pointer on the targeted Output device.</param>
        public unsafe void Setup(IntPtr device)
        {
            DisposeCommandBuffer();
            m_CommandBuffer = new CommandBuffer();

            fixed (InitializeGPUDirectID* encoderPtr = &m_InitializeID)
            {
                m_InitializeID.devicePtr = device;

                ExecuteOutputDeviceCommand(BlackmagicOutputEventID.Initialize, "OutputDevice Initialize", (IntPtr)encoderPtr);
            }
        }

        /// <summary>
        /// Sends a pointer on the RenderTexture to the DeckLink card.
        /// </summary>
        /// <param name="device">A pointer on the targeted Output device.</param>
        /// <param name="texture">The RenderTexture to send to the DeckLink card.</param>
        /// <param name="bcd">The timecode to send to the DeckLink card.</param>
        public unsafe void FeedFrameTexture(IntPtr device, RenderTexture texture, uint bcd)
        {
            if (device == IntPtr.Zero)
                return;

            fixed (FeedFrameID* encoderPtr = &m_FeedFrameID)
            {
                m_FeedFrameID.devicePtr = device;
                m_FeedFrameID.bufferData = texture.GetNativeTexturePtr();
                m_FeedFrameID.bcd = bcd;

                ExecuteOutputDeviceCommand(BlackmagicOutputEventID.FeedFrameTexture, "OutputDevice FeedFrame", (IntPtr)encoderPtr);
            }
        }

        /// <summary>
        /// Determines if GPUDirect is compatible on the current machine.
        /// </summary>
        /// <param name="commandName">The name of the CommandBuffer.</param>
        public static void CacheIfGPUDirectIsAvailable(string commandName)
        {
            var commandBuffer = new CommandBuffer();
            commandBuffer.name = commandName;

            switch (SystemInfo.graphicsDeviceType)
            {
                case GraphicsDeviceType.Direct3D11:
                    commandBuffer.IssuePluginEventAndData(
                        OutputGPUDirectPlugin.GetRenderEventFunc(),
                        (int)BlackmagicOutputEventID.IsCompatible,
                        IntPtr.Zero);
                    break;
                default:
                    break;
            }

            Graphics.ExecuteCommandBuffer(commandBuffer);

            commandBuffer.Release();
        }

        void ExecuteOutputDeviceCommand(BlackmagicOutputEventID id, string commandName, IntPtr data)
        {
            m_CommandBuffer.Clear();
            m_CommandBuffer.name = commandName;

            switch (SystemInfo.graphicsDeviceType)
            {
                case GraphicsDeviceType.Direct3D11:
                    m_CommandBuffer.IssuePluginEventAndData(OutputGPUDirectPlugin.GetRenderEventFunc(), (int)id, data);
                    break;
                default:
                    Debug.LogWarning("GPUDirect is not compatible on this Render API.");
                    break;
            }

            Graphics.ExecuteCommandBuffer(m_CommandBuffer);
        }
    }
}
