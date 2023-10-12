using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Unity.Media.Blackmagic;
using System;

namespace Unity.VirtualProduction.BlackmagicVideo
{
    /// <summary>
    /// This class blits the Blackmagic input video into a result render texture.
    /// </summary>
    [Serializable]
    public sealed class PlaneVideoTextureToMultiPassCustomPass : CustomPass
    {
        [SerializeField]
        InputVideoDeviceHandle m_InputDevice;

        [SerializeField]
        RenderTexture m_ToKeyerRenderTexture;

        protected override void Execute(CustomPassContext ctx)
        {
            if (!m_InputDevice.IsActive())
                return;

            if (m_InputDevice.TryGetRenderTexture(out var inputTexture))
            {
                ctx.cmd.Blit(inputTexture, m_ToKeyerRenderTexture);
            }
        }
    }
}
