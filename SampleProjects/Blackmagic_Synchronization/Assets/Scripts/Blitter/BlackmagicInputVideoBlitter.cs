using System;
using Unity.Media.Blackmagic;
using UnityEngine;

namespace Unity.LiveCapture.VideoIO.Blackmagic
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class BlackmagicInputVideoBlitter : MonoBehaviour
    {
        [SerializeField]
        InputVideoDeviceHandle m_InputDevice;

        [SerializeField]
        Camera m_CameraReference;

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!m_InputDevice.IsActive())
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (m_InputDevice.TryGetRenderTexture(out var inputTexture))
            {
                Graphics.Blit(inputTexture, null as RenderTexture);
            }
        }
    }
}
