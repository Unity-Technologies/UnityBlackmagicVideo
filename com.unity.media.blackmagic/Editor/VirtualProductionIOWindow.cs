using UnityEngine;
using UnityEditor;
using UnityEngine.Assertions;

namespace Unity.Media.Blackmagic.Editor
{
    class VirtualProductionIOWindow : EditorWindow
    {
        public static readonly string IconPath = "Packages/com.unity.media.blackmagic/Editor/Icons";

        static class Contents
        {
            public static readonly GUIContent VideoIOWindowLabel = EditorGUIUtility.TrTextContentWithIcon("Blackmagic Video Manager", "Manager which handles the video I/O hardware and devices.", $"{IconPath}/InputOutputDevices.png");
            public static readonly GUIContent DeckLinkHeaderLabel = EditorGUIUtility.TrTextContent("DeckLink Devices", "A list of all DeckLink devices in the Scene.");
            static public readonly GUIContent DeviceMessagesLabel = EditorGUIUtility.TrTextContent("Messages", "Display current project limitations");
            public static readonly GUIContent EnableVideoManagerLabel = EditorGUIUtility.TrTextContent("Enable Video Manager", "Enables the Blackmagic Video Manager and displays connected devices.");
            public const string WindowName = "Window/Virtual Production/Blackmagic Video Manager";
            public const string VideoManagerGameObjectName = "Blackmagic Video Manager";
            public const string RemoveVideoProfiles = "Remove Video Profiles";
            public const string ResetMsgWindowName = "Window/Virtual Production/Reset Devices Messages";
            public const string HDRPAlphaWarningMessage = "HDRP Color Buffer Format does not support Alpha channel.";
        }

        // Internal class to ensure Editor coherence with AVIO/Device updates.
        [InitializeOnLoad]
        static class DevicesUICallbacks
        {
            static DevicesUICallbacks()
            {
                DeckLinkInputDevice.OnFrameProcessed += PropagateRequiredRepaint;
                DeckLinkOutputDevice.OnFrameProcessed += PropagateRequiredRepaint;
            }

            static void PropagateRequiredRepaint()
            {
                if (!Application.isPlaying)
                {
                    SceneView.RepaintAll();
                }
            }
        }

        static VirtualProductionIOWindow s_Window;

        DeckLinkManagerEditor m_VideoIOManagerEditor;
        GameObject m_VideoIOGameObject;
        DeckLinkManager m_VideoIOManager;
        bool m_EnableVideoIOManager;
        bool m_ResetVideoIOManager;
        float m_TimeSinceLastRepaint;
        Vector2 m_ScrollPosition = Vector2.zero;
        bool m_FoldoutDeckLinkHeader = true;

        bool m_FoldoutErrorHeader = true;

        static bool isIgnoredAlphaWarning
        {
            get => EditorPrefs.GetBool("VirtualProduction.IgnoreAlphaWarning", false);
            set => EditorPrefs.SetBool("VirtualProduction.IgnoreAlphaWarning", value);
        }

        [MenuItem(Contents.WindowName, false, 10500)]
        static void Init()
        {
            s_Window = (VirtualProductionIOWindow)GetWindow(typeof(VirtualProductionIOWindow));
            s_Window.Show();
        }

        [MenuItem(Contents.ResetMsgWindowName, false, 10600)]
        static void ResetDevicesMessages()
        {
            isIgnoredAlphaWarning = false;
        }

        void ResetVideoManager()
        {
            BlackmagicUtilities.Destroy(m_VideoIOManager);
            BlackmagicUtilities.Destroy(m_VideoIOGameObject);
            BlackmagicUtilities.Destroy(m_VideoIOManagerEditor, true);

            m_VideoIOManager = null;
            m_VideoIOGameObject = null;
            m_VideoIOManagerEditor = null;
        }

        void UpdateVideoIOManager()
        {
            var cachedManager = m_VideoIOManager;

            if (DeckLinkManager.TryGetInstance(out m_VideoIOManager))
            {
                if (cachedManager != m_VideoIOManager)
                {
                    m_VideoIOManager.AllocatePinnedInstanceManagerIfNeeded();
                    m_VideoIOGameObject = m_VideoIOManager.gameObject;
                    m_VideoIOGameObject.hideFlags = HideFlags.HideInHierarchy;
                    m_EnableVideoIOManager = m_VideoIOManager.transform.gameObject.activeSelf;

                    BlackmagicUtilities.Destroy(m_VideoIOManagerEditor, true);
                    m_VideoIOManagerEditor = null;
                }

                CreateEditorIfNeeded();
            }
        }

        void CreateEditorIfNeeded()
        {
            Assert.IsNotNull(m_VideoIOManager);

            // HasChanged => There's no way to recreate a Reorderable List programmatically. We need to regenerate the editor.
            if (m_VideoIOManagerEditor == null || m_VideoIOManagerEditor.target == null || m_VideoIOManagerEditor.HasChanged)
            {
                BlackmagicUtilities.Destroy(m_VideoIOManagerEditor);
                m_VideoIOManagerEditor = (DeckLinkManagerEditor)UnityEditor.Editor.CreateEditor(m_VideoIOManager);
            }
        }

        void Update()
        {
            const float repaintDelayThreshold = 0.25f;

            m_TimeSinceLastRepaint += Time.deltaTime;

            if (m_ResetVideoIOManager)
            {
                m_ResetVideoIOManager = false;
                ResetVideoManager();
            }
            else if (m_TimeSinceLastRepaint > repaintDelayThreshold)
            {
                Repaint();
            }
        }

        void OnEnable()
        {
            titleContent = Contents.VideoIOWindowLabel;
        }

        void OnDisable()
        {
            BlackmagicUtilities.Destroy(m_VideoIOManagerEditor);
            m_VideoIOManagerEditor = null;
        }

        void OnGUI()
        {
            m_TimeSinceLastRepaint = 0;
            EditorGUILayout.Space(2);

            UpdateVideoIOManager();
            DrawToggleEnableVideoIOManager();

            m_ScrollPosition = GUILayout.BeginScrollView(m_ScrollPosition);
            DrawMessageZone();

            if (AddOrChangeVideoIOManagerState())
            {
                DrawVideoIOContent();
            }
            else
            {
                if (m_VideoIOManager != null && GUILayout.Button(Contents.RemoveVideoProfiles))
                {
                    m_VideoIOManager.OnDisable();
                    m_VideoIOManagerEditor?.RemoveAllDevices();
                    m_ResetVideoIOManager = true;
                }
            }

            GUILayout.EndScrollView();
        }

        void DrawToggleEnableVideoIOManager()
        {
            var enableCompositor = false;
            if (m_VideoIOManager != null)
            {
                enableCompositor = m_VideoIOManager.enabled;
            }

            m_EnableVideoIOManager = EditorGUILayout.Toggle(Contents.EnableVideoManagerLabel, enableCompositor);

            EditorGUI.EndDisabledGroup();

            if (enableCompositor != m_EnableVideoIOManager && m_VideoIOManager != null)
            {
                EditorUtility.SetDirty(m_VideoIOManager);
            }
        }

        bool AddOrChangeVideoIOManagerState()
        {
            if (!m_EnableVideoIOManager)
            {
                if (m_VideoIOManager != null && m_VideoIOManager.enabled)
                {
                    m_VideoIOManager.transform.gameObject.SetActive(false);
                    m_VideoIOManager.enabled = false;
                }

                return false;
            }

            if (m_VideoIOManager == null)
            {
                m_VideoIOGameObject = new GameObject(Contents.VideoManagerGameObjectName);
                m_VideoIOGameObject.hideFlags = HideFlags.HideInHierarchy;
                m_VideoIOManager = m_VideoIOGameObject.AddComponent<DeckLinkManager>();
                m_VideoIOManager.AllocatePinnedInstanceManagerIfNeeded();

                CreateEditorIfNeeded();
                EditorUtility.SetDirty(m_VideoIOManager);
            }
            m_VideoIOManager.enabled = true;
            m_VideoIOManager.transform.gameObject.SetActive(true);
            m_VideoIOManagerEditor.ReloadEditorData();

            return true;
        }

        /// <summary>
        /// Draws the message zone if there are any pending messages from devices to be shown.
        /// </summary>
        void DrawMessageZone()
        {
            var displayAlphaError = !isIgnoredAlphaWarning && !BlackmagicUtilities.IsValidColorBufferFormat();

            if (!m_EnableVideoIOManager || !displayAlphaError) return;
            VirtualProductionEditorUtilities.DrawGUIContentHeader(Contents.DeviceMessagesLabel, ref m_FoldoutErrorHeader, false);
            if (m_FoldoutErrorHeader)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(Contents.HDRPAlphaWarningMessage, MessageType.Warning, true);
                if (GUILayout.Button("Ignore", GUILayout.ExpandWidth(false), GUILayout.Height(38)))
                {
                    isIgnoredAlphaWarning = true;
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        void DrawVideoIOContent()
        {
            using (new EditorGUI.DisabledScope(false))
            {
                var headerStyle = EditorStyles.helpBox;
                headerStyle.fontSize = 14;

                using (new EditorGUI.DisabledScope(!m_VideoIOManager.enabled))
                {
                    // Draw black header for DeckLink devices
                    VirtualProductionEditorUtilities.DrawGUIContentHeader(Contents.DeckLinkHeaderLabel, ref m_FoldoutDeckLinkHeader, false);
                    if (m_FoldoutDeckLinkHeader)
                    {
                        EditorGUI.indentLevel++;
                        DrawDevicesEditor();
                        EditorGUI.indentLevel--;
                    }
                }
            }
        }

        void DrawDevicesEditor()
        {
            Assert.IsNotNull(m_VideoIOManagerEditor);
            using (new EditorGUI.DisabledScope(!m_VideoIOManager.enabled))
            {
                m_VideoIOManagerEditor.OnInspectorGUI();
            }
        }
    }
}
