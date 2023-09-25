using System;
using Unity.Media.Blackmagic;
using UnityEngine;

[ExecuteAlways]
public class BlitBlackmagicVideoTexturePlane : MonoBehaviour
{
    [SerializeField]
    InputVideoDeviceHandle m_InputDevice;

    [SerializeField]
    Renderer targetRenderer;

    MaterialPropertyBlock m_PropertyBlock;

    static readonly string targetMaterialProperty = "_MainTex";

    void OnEnable()
    {
        if (!targetRenderer || String.IsNullOrEmpty(targetMaterialProperty))
        {
            throw new System.Exception("Params cannot be null");
        }
    }

    void Update()
    {
        if (!m_InputDevice.IsActive())
            return;

        // Renderer override
        if (targetRenderer != null)
        {
            // Material property block lazy initialization
            if (m_PropertyBlock == null)
                m_PropertyBlock = new MaterialPropertyBlock();

            // Read-modify-write
            if (m_InputDevice.TryGetRenderTexture(out var inputTexture))
            {
                targetRenderer.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetTexture(targetMaterialProperty, inputTexture);
                targetRenderer.SetPropertyBlock(m_PropertyBlock);
            }
        }
    }
}
