using System;
using Unity.Media.Blackmagic;
using UnityEngine;

namespace Unity.Media.Blackmagic
{
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class BlackmagicOutputVideoBlitter : MonoBehaviour
    {
        [SerializeField]
        OutputVideoDeviceHandle m_OutputDevice;

        [SerializeField]
        Camera m_CameraReference;

        [SerializeField]
        bool m_Switched;

        Camera m_CameraBlitter;
        bool m_PreviousState;

        void OnEnable()
        {
            m_CameraBlitter = GetComponent<Camera>();

            if (m_CameraReference == null)
            {
                throw new Exception("Missing reference(s), cannot be null.");
            }

            if ((Application.isPlaying && !m_Switched) || (!Application.isPlaying && m_Switched))
            {
                SwitchDisplayCamera();
            }
        }

        void Update()
        {
            if (!m_OutputDevice.IsActive())
                return;

            var isUsedInEditMode = m_OutputDevice.IsUsedInEditMode();

            // Runtime editor switch to update the output video.
            if (!Application.isPlaying && m_PreviousState != isUsedInEditMode)
            {
                m_PreviousState = isUsedInEditMode;
                SwitchDisplayCamera();
            }
        }

        void SwitchDisplayCamera()
        {
            m_Switched = !m_Switched;
            var targetDisplay = m_CameraBlitter.targetDisplay;
            m_CameraBlitter.targetDisplay = m_CameraReference.targetDisplay;
            m_CameraReference.targetDisplay = targetDisplay;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (!m_OutputDevice.IsActive())
                return;

            if (m_OutputDevice.TryGetRenderTexture(out var outputTexture))
            {
                Graphics.Blit(outputTexture, null as RenderTexture);
            }
        }
    }
}
