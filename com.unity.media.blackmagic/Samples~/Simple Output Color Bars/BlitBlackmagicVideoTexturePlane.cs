using System;
using Unity.Media.Blackmagic;
using UnityEngine;

[ExecuteAlways]
public class BlitBlackmagicVideoTexturePlane : MonoBehaviour
{
    [SerializeField]
    Texture m_ColorBars;

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
        // Renderer override
        if (targetRenderer != null)
        {
            // Material property block lazy initialization
            if (m_PropertyBlock == null)
                m_PropertyBlock = new MaterialPropertyBlock();

            // Read-modify-write
            {
                targetRenderer.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetTexture(targetMaterialProperty, m_ColorBars);
                targetRenderer.SetPropertyBlock(m_PropertyBlock);
            }
        }
    }
}
