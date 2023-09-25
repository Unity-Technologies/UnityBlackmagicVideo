using System;
using Unity.Media.Blackmagic;
using UnityEngine;

namespace Unity.Media.Blackmagic
{
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class BlackmagicCompositingBlitter : MonoBehaviour
    {
        class ShaderIDs
        {
            public const string _CompositingShaderName = "Custom/SimpleCompositing";
            public const string _FinalBlitShaderName = "Custom/SimpleBlit";

            public const string _SceneTex = "_SceneTex";
            public const string _VideoTex = "_VideoTex";
        }

        [SerializeField]
        OutputVideoDeviceHandle m_OutputDevice;

        [SerializeField]
        InputVideoDeviceHandle m_InputDevice;

        Material m_CompositingMaterial;
        RenderTexture m_CompositingVideoTexture;

        void OnEnable()
        {
            var compositingShader = Shader.Find(ShaderIDs._CompositingShaderName);
            if (compositingShader != null)
            {
                m_CompositingMaterial = new Material(compositingShader);
            }
        }

        void OnDisable()
        {
            if (m_CompositingMaterial != null)
            {
                DestroyImmediate(m_CompositingMaterial);
            }

            if (m_CompositingVideoTexture != null)
            {
                m_CompositingVideoTexture.Release();
                m_CompositingVideoTexture = null;
            }
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var isPlaying = Application.isPlaying;
            var updateOutput = (isPlaying || m_OutputDevice.IsActive());
            var updateInput = (isPlaying || m_InputDevice.IsActive());

            // Do compositing & blit the final result.
            if (updateOutput && updateInput)
            {
                DoCompositingAndBlitRenderTextureToScreen(source, m_InputDevice, m_OutputDevice);
            }
            // There's no input device or it's not set to be updated in editor mode, so we only blit the output device.
            else if (updateOutput && m_OutputDevice.TryGetRenderTexture(out var outputTexture))
            {
                BlitRenderTextureToScreen(outputTexture);
            }
            // There's no output device or it's not set to be updated in editor mode, so we only blit the input device.
            else if (updateInput && m_InputDevice.TryGetRenderTexture(out var inputTexture))
            {
                BlitRenderTextureToScreen(inputTexture);
            }
            // No active devices, this call is necessary to not invalidate the destination target.
            else
            {
                Graphics.Blit(source, destination);
            }
        }

        void DoCompositingAndBlitRenderTextureToScreen(RenderTexture source,
                                                       InputVideoDeviceHandle inputDevice,
                                                       OutputVideoDeviceHandle outputDevice)
        {
            // Lazy texture initialization, to retrieve the current Width and Height.
            if (m_CompositingVideoTexture == null)
            {
                m_CompositingVideoTexture = new RenderTexture(Screen.width,
                                                              Screen.height,
                                                              0,
                                                              RenderTextureFormat.ARGB32,
                                                              RenderTextureReadWrite.Linear);
            }

            // Lazy update for Input and Output RenderTextures.
            if (outputDevice.TryGetRenderTexture(out var outputTexture))
            {
                m_CompositingMaterial.SetTexture(ShaderIDs._SceneTex, outputTexture);
            }

            if (inputDevice.TryGetRenderTexture(out var inputTexture))
            {
                m_CompositingMaterial.SetTexture(ShaderIDs._VideoTex, inputTexture);
            }

            // Create the compositing final RenderTexture.
            Graphics.Blit(source, m_CompositingVideoTexture, m_CompositingMaterial);

            // Final blit to the RenderTexture, sent to the Blackmagic plugin.
            // It overrides the default blit, to not invert the y axis.
            Graphics.Blit(m_CompositingVideoTexture, outputTexture);

            // Final blit to the screen.
            BlitRenderTextureToScreen(m_CompositingVideoTexture);
        }

        void BlitRenderTextureToScreen(Texture texture)
        {
            Graphics.Blit(texture, null as RenderTexture);
        }
    }
}
