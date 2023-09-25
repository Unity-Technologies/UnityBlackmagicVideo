using System.Collections;
using System.Collections.Generic;
using Unity.Media.Blackmagic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class SimpleCompositingCustomPass : CustomPass
{
    class ShaderIDs
    {
        public const string _BackgroundParameterID = "_Background";
        public const string _BlitVideoPassID = "_BlitTexture";
    }

    internal const string k_ShaderPath = "Hidden/BlackmagicVideo/Shader/FullScreenBlitTexture";

    [LabelOverride("Output Device")]
    [SerializeField]
    OutputVideoDeviceHandle m_OutputDevice;

    [LabelOverride("Input Device")]
    [SerializeField]
    InputVideoDeviceHandle m_InputDevice;

    [SerializeField]
    Material fullscreenPassMaterial;

    [SerializeField]
    bool fetchColorBuffer;

    // Manual compositing
    RTHandle customBuffer;
    int compositingPass;
    int copyPass;
    int fadeValueId;
    int backGroundId;

    // Simple Fullscreen
    Material m_Material;
    int m_VideoFullscreenPass;
    int m_BackGroundId;

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        // Manual Compositing
        fadeValueId = Shader.PropertyToID("_FadeValue");
        backGroundId = Shader.PropertyToID("_Background");

        customBuffer = RTHandles.Alloc(Vector2.one, TextureXR.slices, dimension: TextureXR.dimension,
                                       colorFormat: GraphicsFormat.R16G16B16A16_SFloat, useDynamicScale: true,
                                       name: "Custom Compositing Buffer");

        compositingPass = fullscreenPassMaterial.FindPass("Compositing");
        copyPass = fullscreenPassMaterial.FindPass("Copy");

        // Simple Fullscreen
        var shader = Shader.Find(k_ShaderPath);
        if (shader != null)
        {
            m_Material = new Material(shader);
        }
        else
        {
            Debug.LogError("Cannot create required material because shader "
                            + k_ShaderPath + " could not be found");
            return;
        }

        m_BackGroundId = Shader.PropertyToID(ShaderIDs._BackgroundParameterID);
        m_VideoFullscreenPass = m_Material.FindPass(ShaderIDs._BlitVideoPassID);
    }

    protected override void Execute(CustomPassContext ctx)
    {
        var isPlaying = Application.isPlaying;
        var updateOutput = (isPlaying || m_OutputDevice.IsActive());
        var updateInput = (isPlaying || m_InputDevice.IsActive());

        // Do compositing & blit the final result.
        if (updateInput && updateOutput)
        {
            if (fetchColorBuffer)
            {
                ResolveMSAAColorBuffer(ctx.cmd, ctx.hdCamera);
                SetRenderTargetAuto(ctx.cmd);
            }

            fullscreenPassMaterial.SetTexture("_CustomBuffer", customBuffer);
            fullscreenPassMaterial.SetTexture("_OverLayer", ctx.cameraColorBuffer);

            m_InputDevice.TryGetRenderTexture(out var inputRt);
            fullscreenPassMaterial.SetTexture(backGroundId, inputRt);
            fullscreenPassMaterial.SetFloat(fadeValueId, fadeValue);

            CoreUtils.SetRenderTarget(ctx.cmd, customBuffer, ClearFlag.All);
            CoreUtils.DrawFullScreen(ctx.cmd, fullscreenPassMaterial, shaderPassId: compositingPass);

            SetRenderTargetAuto(ctx.cmd);
            CoreUtils.DrawFullScreen(ctx.cmd, fullscreenPassMaterial, shaderPassId: copyPass);
        }
        // There's no input device or it's not set to be updated in editor mode.
        else if (updateOutput && m_OutputDevice.TryGetRenderTexture(out var outputTexture))
        {
            // Nothing to do, the GameView is already valid and the output device's RenderTexture
            // already contains the scene.
        }
        // There's no output device or it's not set to be updated in editor mode, so we only blit the input device.
        else if (updateInput && m_InputDevice.TryGetRenderTexture(out var inputTexture))
        {
            BlitRenderTextureToScreen(ctx, inputTexture);
        }
    }

    public override IEnumerable<Material> RegisterMaterialForInspector() { yield return fullscreenPassMaterial; }

    protected override void Cleanup()
    {
        customBuffer.Release();
    }

    void BlitRenderTextureToScreen(CustomPassContext ctx, RenderTexture texture)
    {
        m_Material.SetTexture(m_BackGroundId, texture);
        CoreUtils.DrawFullScreen(ctx.cmd, m_Material, shaderPassId: m_VideoFullscreenPass);
    }
}
