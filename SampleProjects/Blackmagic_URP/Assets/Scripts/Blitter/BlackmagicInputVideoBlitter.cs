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
    public sealed class BlackmagicInputVideoBlitter : MonoBehaviour
    {
        [SerializeField]
        InputVideoDeviceHandle m_InputDevice;

        Material m_Material;
        Mesh m_Mesh;

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

            // Blitter shader material
            var shader = Shader.Find("Hidden/BlackmagicVideo/Shader/SimpleBlit");
            m_Material = new Material(shader);

            // Register the camera render callback.
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            OnBeginCameraRendering(camera);
        }

        void OnBeginCameraRendering(Camera camera)
        {
            if (m_Mesh == null || camera != GetComponent<Camera>() || !m_InputDevice.IsActive())
                return;

            if (m_InputDevice.TryGetRenderTexture(out var inputTexture))
            {
                m_Material.SetTexture("_MainTex", inputTexture);
                Graphics.DrawMesh(m_Mesh, transform.localToWorldMatrix, m_Material, 0, camera);
            }
        }

        void OnDisable()
        {
            if (m_Mesh != null)
            {
                // Unregister the camera render callback.
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

                // Destroy temporary objects.
                DestroyImmediate(m_Mesh);
                DestroyImmediate(m_Material);
                m_Mesh = null;
                m_Material = null;
            }
        }
    }
}
