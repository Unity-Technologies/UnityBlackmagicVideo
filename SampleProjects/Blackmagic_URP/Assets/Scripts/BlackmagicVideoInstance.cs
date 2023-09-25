using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Media.BlackmagicVideo
{
    /// <summary>
    /// This class is responsible for controlling the Compositing technology.
    /// </summary>
    [DisallowMultipleComponent]
    internal static class BlackmagicVideoInstance
    {
        static RenderTexture s_ReceiverVideoTexture;

        /// <summary>
        /// Gets the video texture which represents the Input Video.
        /// </summary>
        /// <remarks>
        /// The Input Video is coming from a camera connected to a Blackmagic card.
        /// </remarks>
        public static RenderTexture GetVideoTexture() => s_ReceiverVideoTexture;

        /// <summary>
        /// Allocates the video texture which represents the Input Video.
        /// </summary>
        /// <param name="width">The texture width.</param>
        /// <param name="height">The texture height.</param>
        public static void SetupVideoTexture(int width, int height)
        {
            if (s_ReceiverVideoTexture == null)
            {
                s_ReceiverVideoTexture = RenderTexture.GetTemporary(width,
                                                                    height,
                                                                    0,
                                                                    RenderTextureFormat.ARGB32,
                                                                    RenderTextureReadWrite.Linear);
            }
        }

        /// <summary>
        /// Blits the received texture into the RenderTexture class member.
        /// </summary>
        /// <param name="cmd">The CommandBuffer to execute the Blit command.</param>
        /// <param name="receivedTexture">The Source texture to Blit in the Destination texture.</param>
        public static void BlitVideoTexture(CommandBuffer cmd, Texture receivedTexture)
        {
            cmd.Blit(receivedTexture, s_ReceiverVideoTexture);
        }

        /// <summary>
        /// Releases the video texture which represents the Input Video.
        /// </summary>
        public static void ReleaseVideoTexture()
        {
            if (s_ReceiverVideoTexture != null)
            {
                RenderTexture.ReleaseTemporary(s_ReceiverVideoTexture);
                s_ReceiverVideoTexture = null;
            }
        }
    }
}
