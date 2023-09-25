using System;
using Unity.Media.Blackmagic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// Component responsible for keeping captured frames presented on a target display.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class BlackmagicCompositingBlitter : MonoBehaviour
    {
        class ShaderIDs
        {
            public const string _MainTex = "_MainTex";
            public const string _SceneTex = "_SceneTex";
            public const string _VideoTex = "_VideoTex";
        }

        [SerializeField]
        InputVideoDeviceHandle m_InputDevice;

        [SerializeField]
        OutputVideoDeviceHandle m_OutputDevice;

        [SerializeField]
        Material m_SimpleBlitMaterial;

        [SerializeField]
        Material m_CompositingMaterial;

        Mesh m_Mesh;
        RenderTexture m_CompositingVideoTexture;

        void OnEnable()
        {
            InitializeMeshOutput();
        }

        void InitializeMeshOutput()
        {
            // Index-only triangle mesh
            m_Mesh = new Mesh();
            m_Mesh.vertices = new Vector3[3];
            m_Mesh.triangles = new int[] { 0, 1, 2 };
            m_Mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            m_Mesh.UploadMeshData(true);

            // Register the camera render callback.
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            OnBeginCameraRendering(camera);
        }

        void OnBeginCameraRendering(Camera camera)
        {
            if (m_Mesh == null || camera != GetComponent<Camera>())
                return;

            var isPlaying = Application.isPlaying;
            var updateOutput = (isPlaying || m_OutputDevice.IsActive());
            var updateInput = (isPlaying || m_InputDevice.IsActive());

            // Do compositing & blit the final result.
            if (updateOutput && updateInput && m_CompositingMaterial != null)
            {
                DoCompositingAndBlitRenderTextureToScreen(camera.targetTexture, m_InputDevice, m_OutputDevice, camera);
            }
            // There's no input device or it's not set to be updated in editor mode, so we only blit the output device.
            else if (updateOutput && m_OutputDevice.TryGetRenderTexture(out var outputTexture))
            {
                BlitRenderTextureToScreen(outputTexture, camera);
            }
            // There's no output device or it's not set to be updated in editor mode, so we only blit the input device.
            else if (updateInput && m_SimpleBlitMaterial != null && m_InputDevice.TryGetRenderTexture(out var inputTexture))
            {
                BlitRenderTextureToScreen(inputTexture, camera);
            }
        }

        void DoCompositingAndBlitRenderTextureToScreen(RenderTexture source,
                                                       InputVideoDeviceHandle inputDevice,
                                                       OutputVideoDeviceHandle outputDevice,
                                                       Camera camera)
        {
            if (m_CompositingVideoTexture == null)
            {
                m_CompositingVideoTexture = new RenderTexture(Screen.width,
                                                              Screen.height,
                                                              0,
                                                              RenderTextureFormat.ARGB32,
                                                              RenderTextureReadWrite.Linear);
            }

            // Lazy update for Input and Output RenderTextures.
            if (m_OutputDevice.TryGetRenderTexture(out var outputTexture))
            {
                m_CompositingMaterial.SetTexture(ShaderIDs._SceneTex, outputTexture);
            }

            if (inputDevice.TryGetRenderTexture(out var intputTexture))
            {
                m_CompositingMaterial.SetTexture(ShaderIDs._VideoTex, intputTexture);
            }

            // Create the compositing final RenderTexture.
            Graphics.Blit(source, m_CompositingVideoTexture, m_CompositingMaterial);

            // Final blit to the RenderTexture, sent to the Blackmagic plugin.
            // It overrides the default blit, to not invert the y axis.
            Graphics.Blit(m_CompositingVideoTexture, outputTexture);

            // Final blit to the screen.
            if (m_SimpleBlitMaterial != null)
            {
                BlitRenderTextureToScreen(m_CompositingVideoTexture, camera);
            }
        }

        void BlitRenderTextureToScreen(Texture texture, Camera camera)
        {
            m_SimpleBlitMaterial.SetTexture(ShaderIDs._MainTex, texture);
            Graphics.DrawMesh(m_Mesh, transform.localToWorldMatrix, m_SimpleBlitMaterial, 0, camera);
        }

        void OnDisable()
        {
            if (m_CompositingVideoTexture != null)
            {
                m_CompositingVideoTexture.Release();
                m_CompositingVideoTexture = null;
            }

            if (m_Mesh != null)
            {
                // Unregister the camera render callback.
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

                // Destroy temporary objects.
                DestroyImmediate(m_Mesh);
                m_Mesh = null;
            }
        }
    }
}
