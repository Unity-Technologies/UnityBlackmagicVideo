using Unity.Media.Blackmagic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity.Media.BlackmagicVideo
{
    using static BlackmagicVideoInstance;

    /// <summary>
    /// This class blits the Blackmagic input video into a RenderTexture.
    /// </summary>
    public sealed class BlitBlackmagicVideoTextureRendererFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// This class contains all the necessary properties for the ScriptableRendererFeature.
        /// </summary>
        [System.Serializable]
        public class BlitBlackmagicVideoTextureSettings
        {
            /// <summary>
            /// Controls when the render pass executes.
            /// </summary>
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            public string deviceName = "Device 1";
        }

        BlitBlackmagicVideoTextureSettings settings = new BlitBlackmagicVideoTextureSettings();

        class CustomRenderPass : ScriptableRenderPass
        {
            /// <summary>
            /// Getter/Setter for the Blackmagic DeckLinkInputDevice component.
            /// </summary>
            public InputVideoDeviceHandle inputDevice
            {
                get { return m_InputDevice; }
                set { m_InputDevice = value; }
            }

            InputVideoDeviceHandle m_InputDevice;

            string m_ProfilerTag;

            /// <summary>
            /// Use this constructor to initialize the ProfilerTag member.
            /// </summary>
            public CustomRenderPass(string profilerTag)
            {
                this.m_ProfilerTag = profilerTag;
            }

            /// <summary>
            /// Initializes the Blackmagic input Video Texture.
            /// </summary>
            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                SetupVideoTexture(Screen.width, Screen.height);
            }

            /// <summary>
            /// Blits the Blackmagic Video Texture into a RenderTexture.
            /// </summary>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (inputDevice != null && m_InputDevice.IsActive() && m_InputDevice.TryGetRenderTexture(out var inputTexture))
                {
                    var cmd = CommandBufferPool.Get(m_ProfilerTag);
                    BlitVideoTexture(cmd, inputTexture);
                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                }
            }

            /// <summary>
            /// Frees the Blackmagic input Video Texture.
            /// </summary>
            public override void FrameCleanup(CommandBuffer cmd)
            {
                BlackmagicVideoInstance.ReleaseVideoTexture();
            }
        }

        CustomRenderPass m_ScriptablePass;

        [SerializeField]
        InputVideoDeviceHandle m_InputDevice;

        /// <summary>
        /// Instantiates the CustomRenderPass.
        /// </summary>
        public override void Create()
        {
            m_ScriptablePass = new CustomRenderPass("BlitVideoTexture");
            m_ScriptablePass.inputDevice = m_InputDevice;
            m_ScriptablePass.renderPassEvent = settings.renderPassEvent;
        }

        /// <summary>
        /// Enqueues the CustomRenderPass initialized in the Create() method.
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_ScriptablePass);
        }
    }
}
