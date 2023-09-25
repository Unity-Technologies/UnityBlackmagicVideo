using System;
using UnityEngine;
using UnityEngine.Playables;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Unity.Media.Blackmagic
{
    abstract class BaseDeckLinkDevice : MonoBehaviour
    {
        internal enum StatusType
        {
            Ok,
            Warning,
            Error,
            Unused
        }

        protected const int k_MinQueueLength = 1;
        protected const int k_MaxQueueLength = 8;

        /// <summary>
        /// Count of input devices currently updating in editor.
        /// </summary>
        public static int ActiveDevices => s_ActiveDevices;

        static int s_ActiveDevices;

        /// <summary>
        /// The device executes this event when a frame has been processed and is ready to be displayed.
        /// </summary>
        public static event Action OnFrameProcessed = delegate { };

        [SerializeField]
        int m_DeviceSelection = -1;

        [SerializeField]
        int m_OldDeviceSelection = -1;

        [SerializeField, Range(k_MinQueueLength, k_MaxQueueLength)]
        int m_QueueLength = 3;

        [SerializeField]
        RenderTexture m_TargetRenderTexture;

        bool m_UpdateInEditor;
        internal bool m_LifeCycleNeedsUpdate;
        PlayableGraph m_Graph;

        protected int m_DeviceIndex = -1;

        internal protected (string, StatusType) m_FrameStatus;
        internal protected bool m_Initialized;
        internal protected bool m_RequiresReinit = false;

        [SerializeField]
        internal bool m_WorkingSpaceConversion = false;

        bool updateInEditorActive = false;

        internal int DeviceIndex
        {
            get => m_DeviceIndex;
            set => m_DeviceIndex = value;
        }

        /// <summary>
        /// The maximum number of received frames in the queue.
        /// </summary>
        /// <remarks>
        /// A bigger value can be useful to avoid potential dropped frames.
        /// </remarks>
        public int QueueLength
        {
            get => m_QueueLength;
            protected set => m_QueueLength = Mathf.Clamp(value, k_MinQueueLength, k_MaxQueueLength);
        }

        /// <summary>
        /// The texture containing the blit operations.
        /// </summary>
        internal RenderTexture CaptureRenderTexture
        {
            get => m_TargetRenderTexture;
            set => m_TargetRenderTexture = value;
        }

        /// <summary>
        /// Determines if the device is in use or not.
        /// </summary>
        /// <returns>True if used; false otherwise.</returns>
        public virtual bool IsActive => (Application.isPlaying || UpdateInEditor) && DeviceSelection >= 0 && m_Initialized;

        internal (string, StatusType) FrameStatus => m_FrameStatus;

        internal abstract VideoDeviceType DeviceType { get; }

        internal abstract bool UpdateSettings { get; }

        /// <summary>
        /// Enables the use of the device in Editor mode.
        /// </summary>
        public bool UpdateInEditor
        {
            get => m_UpdateInEditor && GetGraph().IsPlaying();
            internal set
            {
                if (m_UpdateInEditor != value)
                {
                    m_UpdateInEditor = value;
                    m_LifeCycleNeedsUpdate = true;

                    // The PlayerGraph variable is used to force constant calls to the "Update" function.
                    if (value)
                        GetGraph().Play();
                    else
                        GetGraph().Stop();
                }
            }
        }

        /// <summary>
        /// The index of the Blackmagic logical device.
        /// </summary>
        /// <remarks>
        /// Depending on the connector mapping, up to 2 logical devices may share the same physical SDI port.
        /// </remarks>
        public int DeviceSelection
        {
            get => m_DeviceSelection;
            set
            {
                if (m_DeviceSelection != value)
                {
                    m_DeviceSelection = value;

                    if (m_Initialized)
                        m_RequiresReinit = true;

                    m_LifeCycleNeedsUpdate = true;
                }
            }
        }

        internal int OldDeviceSelection
        {
            get => m_OldDeviceSelection;
            set => m_OldDeviceSelection = value;
        }

        PlayableGraph GetGraph()
        {
            if (!m_Graph.IsValid())
            {
                m_Graph = PlayableGraph.Create("BlackmagicEditorUpdate");
            }

            return m_Graph;
        }

        protected virtual void Update()
        {
            if (m_LifeCycleNeedsUpdate)
            {
                m_LifeCycleNeedsUpdate = false;
                UpdateLifeCycle();
            }
        }

        void UpdateLifeCycle()
        {
            var shouldInitialize = m_UpdateInEditor || Application.isPlaying;
            if (shouldInitialize && !m_Initialized)
            {
                m_Initialized = Initialize();
                if (m_Initialized)
                {
                    s_ActiveDevices++;
                }
            }

            if (!m_UpdateInEditor && !Application.isPlaying && m_Initialized)
            {
                s_ActiveDevices--;
                Cleanup();
                m_Initialized = false;
            }
        }

        protected virtual void OnEnable()
        {
            m_LifeCycleNeedsUpdate = true;

#if UNITY_EDITOR
            EditorSceneManager.sceneSaving += BeforeSceneSaved;
            EditorSceneManager.sceneSaved += AfterSceneSaved;
#endif
        }

        protected virtual void OnDisable()
        {
            if (m_Initialized)
            {
                s_ActiveDevices--;
                Cleanup();
                m_Initialized = false;
            }

#if UNITY_EDITOR
            EditorSceneManager.sceneSaving -= BeforeSceneSaved;
            EditorSceneManager.sceneSaved -= AfterSceneSaved;
#endif
        }

        protected virtual void OnDestroy()
        {
            if (m_Graph.IsValid())
            {
                m_Graph.Destroy();
            }
        }

        public virtual void PerformUpdate()
        {
            UpdateResources();
        }

        protected abstract void UpdateResources();

        protected abstract bool Initialize();

        protected abstract void Cleanup();

        protected static void InvokeOnFrameProcessedEvent()
        {
            OnFrameProcessed.Invoke();
        }

#if UNITY_EDITOR
        void BeforeSceneSaved(UnityEngine.SceneManagement.Scene scene, string path)
        {
            updateInEditorActive = UpdateInEditor;
        }

        void AfterSceneSaved(UnityEngine.SceneManagement.Scene scene)
        {
            if (updateInEditorActive)
            {
                UpdateInEditor = updateInEditorActive;
                GetGraph().Play();
            }

            EditorApplication.QueuePlayerLoopUpdate();
            updateInEditorActive = false;
        }

#endif
    }
}
