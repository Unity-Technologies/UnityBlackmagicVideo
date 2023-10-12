using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Media.BlackmagicVideo
{
    /// <summary>
    /// This class draws the Input Video texture through a ScriptableRendererFeature, in fullscreen mode.
    /// </summary>
    public sealed class FullscreenVideoTextureRendererFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// This class contains all the necessary properties for the ScriptableRendererFeature.
        /// </summary>
        [System.Serializable]
        public class FullscreenVideoTextureSettings
        {
            /// <summary>
            /// Controls when the render pass executes.
            /// </summary>
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

            /// <summary>
            /// Material used to blit the Input Video texture.
            /// </summary>
            public Material blitMaterial = null;
        }

        internal class FullscreenVideoRenderPass : ScriptableRenderPass
        {
            class ShaderIDs
            {
                public static readonly string _BlitTextureID = "_BlitTex";
            }

            /// <summary>
            /// Instance of the FullscreenVideoTextureSettings.
            /// </summary>
            public FullscreenVideoTextureSettings settings = null;

            string m_ProfilerTag;

            /// <summary>
            /// This constructor is used to initialize the ProfilerTag member. </summary>
            public FullscreenVideoRenderPass(string tag)
            {
                m_ProfilerTag = tag;
            }

            /// <summary>
            /// Draws the Blackmagic Video Texture in fullscreen mode.
            /// </summary>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (Application.isPlaying
#if UNITY_EDITOR
                    && !EditorApplication.isPaused
                    && !SceneView.currentDrawingSceneView
#endif
                )
                {
                    var cmd = CommandBufferPool.Get(m_ProfilerTag);
                    var videoTexture = BlackmagicVideoInstance.GetVideoTexture();

                    settings.blitMaterial.SetTexture(ShaderIDs._BlitTextureID, videoTexture);
                    cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);

                    // DrawMesh() is used instead of Blit because there's an issue where it's currently
                    // impossible to override the Color Camera Texture of the Camera.
                    cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, settings.blitMaterial);

                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                }
            }
        }

        public FullscreenVideoTextureSettings settings = new FullscreenVideoTextureSettings();
        FullscreenVideoRenderPass m_ScriptablePass;

        /// <summary>
        /// Instantiates the FullscreenVideoRenderPass.
        /// </summary>
        public override void Create()
        {
            m_ScriptablePass = new FullscreenVideoRenderPass("FullscreenVideoTexture");
        }

        /// <summary>
        /// Enqueues the FullscreenVideoRenderPass initialized in the Create() method.
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.blitMaterial == null)
            {
                Debug.LogWarningFormat("Missing Blit Material. {0} blit pass will not execute."
                                        + "Check for missing reference in the assigned renderer.", GetType().Name);
                return;
            }

            m_ScriptablePass.renderPassEvent = settings.renderPassEvent;
            m_ScriptablePass.settings = settings;

            renderer.EnqueuePass(m_ScriptablePass);
        }
    }
}
