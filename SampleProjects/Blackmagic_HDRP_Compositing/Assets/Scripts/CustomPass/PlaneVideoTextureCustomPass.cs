using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Unity.Media.Blackmagic;
using System;

namespace Unity.VirtualProduction.BlackmagicVideo
{
    /// <summary>
    /// This class blits the Blackmagic input video into a Plane texture.
    /// </summary>
    [Serializable]
    sealed class PlaneVideoTextureCustomPass : CustomPass
    {
        [SerializeField]
        string m_TargetMaterialProperty = "_MainTex";

        [SerializeField]
        InputVideoDeviceHandle m_InputDevice;

        [SerializeField]
        Renderer m_TargetRenderer = null;

        [SerializeField]
        Texture m_NoSignal = null;

        MaterialPropertyBlock m_PropertyBlock;
        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            m_PropertyBlock = new MaterialPropertyBlock();
        }

        protected override void Execute(CustomPassContext ctx)
        {
            if (!m_InputDevice.IsActive())
            {
                SetTexturePropertyBlock(m_NoSignal);
                return;
            }

            if (m_InputDevice.TryGetRenderTexture(out var renderTexture))
            {
                SetTexturePropertyBlock(renderTexture);
            }
        }

        void SetTexturePropertyBlock(Texture texture)
        {
            m_TargetRenderer.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetTexture(m_TargetMaterialProperty, texture);
            m_TargetRenderer.SetPropertyBlock(m_PropertyBlock);
        }
    }
}
