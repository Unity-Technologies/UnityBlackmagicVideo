using Unity.Media.Blackmagic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;

namespace Unity.VirtualProduction.BlackmagicVideo
{
    /// <summary>
    /// This class is drawing the Input Video texture through a Custom Pass.
    /// The texture is drawn in fullscreen mode.
    /// </summary>
    [Serializable]
    sealed class FullScreenVideoTextureCustomPass : CustomPass
    {
        class ShaderIDs
        {
            public const string _BackgroundParameterID = "_Background";
            public const string _BlitVideoPassID = "_BlitTexture";
        }

        internal const string k_ShaderPath = "Hidden/BlackmagicVideo/Shader/FullScreenBlitTexture";

        [SerializeField]
        InputVideoDeviceHandle m_InputDevice;

        Material m_Material;
        int m_VideoFullscreenPass;
        int m_BackGroundId;

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            var shader = Shader.Find(k_ShaderPath);
            if (shader != null)
            {
                m_Material = new Material(shader);
            }
            else
            {
                Debug.LogError("Cannot create required material because shader " + k_ShaderPath + " could not be found");
                return;
            }

            m_BackGroundId = Shader.PropertyToID(ShaderIDs._BackgroundParameterID);
            m_VideoFullscreenPass = m_Material.FindPass(ShaderIDs._BlitVideoPassID);
        }

        protected override void Execute(CustomPassContext ctx)
        {
            if (!m_InputDevice.IsActive() || !m_Material)
                return;

            if (m_InputDevice.TryGetRenderTexture(out var renderTexture))
            {
                m_Material.SetTexture(m_BackGroundId, renderTexture);
                CoreUtils.DrawFullScreen(ctx.cmd, m_Material, shaderPassId: m_VideoFullscreenPass);
            }
        }
    }
}
