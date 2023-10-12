using System;
using Unity.Media.Blackmagic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[RequireComponent(typeof(Camera))]
[ExecuteAlways]
[Serializable]
public class CustomCameraRenderGameView : MonoBehaviour
{
    static readonly string m_RenderDefaultScene = "Game View Compositing RenderCamera";

    [SerializeField]
    Camera m_CameraReference;

    [SerializeField]
    OutputVideoDeviceHandle m_OutputDevice;

    Camera m_CameraGameView = null;

    void OnEnable()
    {
        m_CameraGameView = GetComponent<Camera>();
        if (m_CameraReference != null)
        {
            m_CameraGameView.targetDisplay = m_CameraReference.targetDisplay;
        }
        else
        {
            Debug.LogWarning("Camera reference has not been assigned.");
        }

        var data = GetComponent<HDAdditionalCameraData>();
        if (data != null)
        {
            data.customRender += CustomRender;
        }
    }

    void OnDisable()
    {
        var data = GetComponent<HDAdditionalCameraData>();
        if (data != null)
        {
            data.customRender -= CustomRender;
        }
    }

    void OnDestroy()
    {
        var data = GetComponent<HDAdditionalCameraData>();
        if (data != null)
        {
            data.customRender -= CustomRender;
        }
    }

    void CustomRender(ScriptableRenderContext context, HDCamera camera)
    {
        if (camera == null || camera.camera == null || !m_OutputDevice.IsActive())
            return;

        if (m_OutputDevice.TryGetRenderTexture(out var outputTexture))
        {
            RenderDefaultScene(context, outputTexture, camera.camera.targetTexture);
        }
    }

    void RenderDefaultScene(ScriptableRenderContext context, Texture src, RenderTexture dest)
    {
        var rt = dest;
        var rtid = (rt != null)
                   ? new RenderTargetIdentifier(rt)
                   : new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);

        // Blit command
        var cmd = CommandBufferPool.Get(m_RenderDefaultScene);
        cmd.Blit(src, rtid);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}
