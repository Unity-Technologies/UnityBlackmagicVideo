using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Media.Blackmagic.Editor
{
    using static Unity.Media.Blackmagic.BaseDeckLinkDevice;
    using Editor = UnityEditor.Editor;

    [CustomEditor(typeof(DeckLinkManager))]
    class DeckLinkManagerEditor : UnityEditor.Editor
    {
        static class Contents
        {
            public static readonly GUIContent DeviceContentLabel = EditorGUIUtility.TrTextContent("Device Label", "The DeckLink card(s) installed in your machine.");
            public static readonly GUIContent ConnectorMappingLabel = EditorGUIUtility.TrTextContent("Connector Mapping", "Configure how your hardware connectors are mapped (half-duplex or full duplex).");
            public static readonly GUIContent PropertiesLabel = EditorGUIUtility.TrTextContentWithIcon("Properties", "The properties of a layer or sub-layer.", $"{VirtualProductionIOWindow.IconPath}/IOProperties.png");

            public static readonly GUIContent InputDevicesHeaderLabel = EditorGUIUtility.TrTextContentWithIcon("Input devices", "The list of input devices.", $"{VirtualProductionIOWindow.IconPath}/InputDeviceSelection.png");
            public static readonly GUIContent OutputDevicesHeaderLabel = EditorGUIUtility.TrTextContentWithIcon("Output devices", "The list of output devices.", $"{VirtualProductionIOWindow.IconPath}/OutputDeviceSelection.png");

            public static readonly GUIContent AddInputDeviceLabel = EditorGUIUtility.TrTextContent("Add device", "The button to add an input device in the list.");
            public static readonly GUIContent AddOutputDeviceLabel = EditorGUIUtility.TrTextContent("Add device", "The button to add an output device in the list.");

            public static readonly GUIContent PropertiesNoDeviceSelectedLabel = EditorGUIUtility.TrTextContent("No device selected", "Select an input or output device to view or edit its properties.");
            public static readonly GUIContent InputDeviceListIsEmptyLabel = EditorGUIUtility.TrTextContent("List Is Empty", "The list of input devices is empty.");
            public static readonly GUIContent OutputDeviceListIsEmptyLabel = EditorGUIUtility.TrTextContent("List Is Empty", "The list of output devices is empty.");
            public static readonly GUIContent ConnectorMappingSupportWarningLabel = EditorGUIUtility.TrTextContent("* Connector Mapping profiles are not available on this device.");

            public static readonly GUIContent DeviceOkIcon = EditorGUIUtility.TrIconContent("winbtn_mac_max");
            public static readonly GUIContent DeviceWarningIcon = EditorGUIUtility.TrIconContent("console.warnicon.sml");
            public static readonly GUIContent DeviceErrorIcon = EditorGUIUtility.TrIconContent("console.erroricon.sml");

            public const string DeviceNone = "None";
            public const string ApiVersion = "API Version";
            public const string DeviceLabel = "Device Label";

            public const int ErrorWidth = 400;

            public static GUIContent GetIconForStatus(StatusType status)
            {
                switch (status)
                {
                    case StatusType.Ok:
                        return DeviceOkIcon;
                    case StatusType.Warning:
                        return DeviceWarningIcon;
                    case StatusType.Error:
                        return DeviceErrorIcon;
                    default:
                        return null;
                }
            }
        }

        struct DeviceElementListData
        {
            public string DeviceName;
            public VideoDeviceType DeviceType;
        }

        DeckLinkManager m_VideoIOManager;
        VideoDeviceGameObjectData m_SelectedDeviceData;
        Editor m_Editor;

        SerializedProperty m_DeckLinkCardIndex;
        int m_ConnectorMapping;
        SerializedProperty m_InputDevicesList;
        SerializedProperty m_OutputDevicesList;

        ReorderableList m_InputReorderableList;
        ReorderableList m_OutputReorderableList;

        GUIContent[] m_DeckLinkCards;
        Dictionary<int, string[]> m_ConnectorMappingValues = new Dictionary<int, string[]>();

        SimpleValuePool<int> m_InputDeviceCountPool = new SimpleValuePool<int>();
        SimpleValuePool<int> m_OutputDeviceCountPool = new SimpleValuePool<int>();

        DeckLinkConnectorMapping m_CurrentMapping;

        bool m_HasChanged;
        SerializedProperty m_PropertiesFoldout;

        public bool HasChanged => m_HasChanged;

        void Awake()
        {
            m_VideoIOManager = (DeckLinkManager)target;
            m_CurrentMapping = m_VideoIOManager.connectorMapping;
        }

        void OnEnable()
        {
            m_DeckLinkCardIndex = serializedObject.FindProperty("m_DeckLinkCardIndex");
            m_InputDevicesList = serializedObject.FindProperty("m_InputDevices");
            m_OutputDevicesList = serializedObject.FindProperty("m_OutputDevices");
            m_PropertiesFoldout = serializedObject.FindProperty("m_PropertiesFoldout");

            m_VideoIOManager.OnMappingProfilesChanged += OnMappingProfilesChanged;

            InitializeInputDevicesReorderableList();
            InitializeOutputDevicesReorderableList();

            CacheDeckLinkCardsInstalled();
            CacheConnectorMapping();

            AddCurrentDevicesIndexInUse(VideoDeviceType.Input, m_InputDeviceCountPool);
            AddCurrentDevicesIndexInUse(VideoDeviceType.Output, m_OutputDeviceCountPool);
        }

        void OnMappingProfilesChanged()
        {
            CacheConnectorMapping();
            CacheDeckLinkCardsInstalled();
        }

        void OnDisable()
        {
            if (m_Editor)
            {
                DestroyImmediate(m_Editor);
            }
        }

        public void ReloadEditorData()
        {
            CacheDeckLinkCardsInstalled();
            CacheConnectorMapping();
        }

        public override void OnInspectorGUI()
        {
            Assert.IsNotNull(m_VideoIOManager);

            serializedObject.Update();

            EditorGUILayout.Space(2);

            // There's always at least 1 card when there's no DeckLink card in the machine.
            // This 1 default card is considered as a fake card, allowing users to continue to use the Blackmagic plugin (editing devices)
            // even without any DeckLink card installed in their machine.
            if (m_VideoIOManager.m_DeckLinkCards.Count > 1)
            {
                m_DeckLinkCardIndex.intValue = EditorGUILayout.Popup(Contents.DeviceContentLabel, m_DeckLinkCardIndex.intValue, m_DeckLinkCards);
            }
            else
            {
                EditorGUILayout.LabelField(Contents.DeviceLabel, m_VideoIOManager.m_DeckLinkCards.First().Value.name);
            }

            var hasUpdateInEditorActive = DeckLinkInputDevice.ActiveDevices > 0 || DeckLinkOutputDevice.ActiveDevices > 0;
            EditorGUI.BeginDisabledGroup(hasUpdateInEditorActive);
            {
                m_ConnectorMapping = (int)m_VideoIOManager.connectorMapping;
                m_ConnectorMapping = EditorGUILayout.Popup(Contents.ConnectorMappingLabel,
                    m_ConnectorMapping,
                    m_ConnectorMappingValues[m_VideoIOManager.deckLinkCardIndex]);

                DetectConnectorMappingChanges((DeckLinkConnectorMapping)m_ConnectorMapping);

                if (!m_VideoIOManager.IsCurrentMappingCompatible())
                {
                    EditorGUILayout.HelpBox(Contents.ConnectorMappingSupportWarningLabel.text, MessageType.Warning);
                }
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.LabelField(Contents.ApiVersion, m_VideoIOManager.ApiVersion);

            EditorGUILayout.Space(4);

            m_InputReorderableList?.DoLayoutList();
            m_OutputReorderableList?.DoLayoutList();

            var headerStyle = EditorStyles.foldout;
            headerStyle.fontSize = 14;

            EditorGUI.indentLevel--;

            EditorGUILayout.Space(4);
            VirtualProductionEditorUtilities.DrawSplitter();

            EditorGUILayout.Space(2);

            m_PropertiesFoldout.boolValue = EditorGUILayout.Foldout(m_PropertiesFoldout.boolValue, Contents.PropertiesLabel, headerStyle);
            if (m_PropertiesFoldout.boolValue)
            {
                if (m_SelectedDeviceData != null)
                {
                    DrawCurrentDevice();
                }
                else
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.LabelField(Contents.PropertiesNoDeviceSelectedLabel);
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        void CacheDeckLinkCardsInstalled()
        {
            var cards = m_VideoIOManager.m_DeckLinkCards.Select((x) => x.Value.name).ToArray();
            m_DeckLinkCards = cards.Select(x => new GUIContent(x)).ToArray();
        }

        void CacheConnectorMapping()
        {
            for (var deckLinkCard = 0; deckLinkCard < m_VideoIOManager.m_DeckLinkCards.Count; ++deckLinkCard)
            {
                var profilesName = System.Enum.GetNames(typeof(DeckLinkConnectorMapping));
                var deckLinkCardObject = m_VideoIOManager.activeDeckLinkCard;
                var compatibleProfiles = deckLinkCardObject.compatibleConnectorMappings;

                for (var i = 0; i < profilesName.Length; ++i)
                {
                    var isCompatible = false;
                    foreach (var p in compatibleProfiles)
                    {
                        if (profilesName[i].Equals(p.ToString()))
                            isCompatible = true;
                    }

                    // This regex is used to add a space before each capital letter (except the first one).
                    profilesName[i] = Regex.Replace(profilesName[i], "([a-z])([A-Z])", "$1 $2");
                    if (!isCompatible)
                    {
                        profilesName[i] += " *";
                    }
                }

                if (m_ConnectorMappingValues.ContainsKey(deckLinkCard))
                {
                    m_ConnectorMappingValues[deckLinkCard] = profilesName;
                }
                else
                {
                    m_ConnectorMappingValues.Add(deckLinkCard, profilesName);
                }
            }
        }

        /// <summary>
        /// Removes all the current input and output devices from their container.
        /// </summary>
        public void RemoveAllDevices()
        {
            m_VideoIOManager.RemoveAllDevices(VideoDeviceType.Input);
            m_VideoIOManager.RemoveAllDevices(VideoDeviceType.Output);
        }

        void InitializeInputDevicesReorderableList()
        {
            m_InputReorderableList = new ReorderableList(serializedObject, m_InputDevicesList, true, true, true, true);

            m_InputReorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                var element = m_InputReorderableList.serializedProperty.GetArrayElementAtIndex(index);
                var propertyName = element.FindPropertyRelative("Name");

                if (m_VideoIOManager.GetExistingVideoDevice(propertyName.stringValue, VideoDeviceType.Input, out var videoDeviceData))
                {
                    var inputDevice = videoDeviceData.VideoDevice as DeckLinkInputDevice;

                    // Display potential error status with the actual Hardware setup
                    var status = m_VideoIOManager.GetInputVideoSignalStatus(videoDeviceData, out var statusMessage);
                    if (status != StatusType.Unused)
                    {
                        EditorGUI.LabelField(new Rect(rect.x, rect.y, 64, EditorGUIUtility.singleLineHeight),
                            Contents.GetIconForStatus(status));
                    }

                    // Display Input device (data)
                    {
                        EditorGUI.LabelField(new Rect(rect.x + 22, rect.y, 150, EditorGUIUtility.singleLineHeight),
                            propertyName.stringValue);
                    }

                    // Display available Input devices
                    {
                        var arrayNames = m_VideoIOManager.m_InputDeviceNames.ToArray();
                        DisplayVideoDeviceField(rect, element, propertyName.stringValue, VideoDeviceType.Input, arrayNames);
                    }

                    // Display potential error message with the actual Hardware setup
                    if (status != StatusType.Warning && status != StatusType.Error)
                        statusMessage = string.Empty;

                    var rectElement = new Rect(rect.x + 292, rect.y + 1, Contents.ErrorWidth, EditorGUIUtility.singleLineHeight);
                    EditorGUI.LabelField(rectElement, statusMessage);

                    // Sync our UI to the editor's state
                    if (isFocused)
                    {
                        m_InputReorderableList.onSelectCallback(m_InputReorderableList);
                        m_SelectedDeviceData = videoDeviceData;
                    }
                }
            };

            m_InputReorderableList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, Contents.InputDevicesHeaderLabel);
            };

            m_InputReorderableList.drawNoneElementCallback = rect =>
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.LabelField(rect, Contents.InputDeviceListIsEmptyLabel, EditorStyles.label);
                }
            };

            m_InputReorderableList.onAddCallback = (list) =>
            {
                var menu = new GenericMenu();
                var guiContent = Contents.AddInputDeviceLabel;

                menu.AddItem(guiContent, false, AddInputDeviceTypeCallback, null);
                menu.ShowAsContext();

                EditorUtility.SetDirty(m_VideoIOManager);
            };

            m_InputReorderableList.onSelectCallback = index =>
            {
            };

            m_InputReorderableList.onRemoveCallback = list =>
            {
                if (m_Editor)
                {
                    DestroyImmediate(m_Editor);
                }

                var deviceName = m_VideoIOManager.RemoveDeviceInstance(list.index, VideoDeviceType.Input);
                var previousIndex = ParseDeviceName(deviceName);

                m_InputDeviceCountPool.ReturnValue(previousIndex);

                EditorUtility.SetDirty(m_VideoIOManager);
            };
        }

        void InitializeOutputDevicesReorderableList()
        {
            m_OutputReorderableList = new ReorderableList(serializedObject, m_OutputDevicesList, true, true, true, true);

            m_OutputReorderableList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var element = m_OutputReorderableList.serializedProperty.GetArrayElementAtIndex(index);
                var propertyName = element.FindPropertyRelative("Name");

                if (m_VideoIOManager.GetExistingVideoDevice(propertyName.stringValue, VideoDeviceType.Output, out var videoDeviceData))
                {
                    var outputDevice = videoDeviceData.VideoDevice as DeckLinkOutputDevice;

                    // Display potential error with the actual Hardware setup
                    var status = m_VideoIOManager.GetOutputVideoSignalStatus(videoDeviceData, out var statusMessage);
                    if (status != StatusType.Unused)
                    {
                        EditorGUI.LabelField(new Rect(rect.x, rect.y, 64, EditorGUIUtility.singleLineHeight),
                            Contents.GetIconForStatus(status));
                    }

                    // Display Output device data
                    {
                        EditorGUI.LabelField(new Rect(rect.x + 22, rect.y, 150, EditorGUIUtility.singleLineHeight),
                            propertyName.stringValue);
                    }

                    // Display available Output devices
                    {
                        var arrayNames = m_VideoIOManager.m_OutputDeviceNames.ToArray();
                        DisplayVideoDeviceField(rect, element, propertyName.stringValue, VideoDeviceType.Output, arrayNames);
                    }

                    // Display potential error message with the actual Hardware setup
                    if (status != StatusType.Warning && status != StatusType.Error)
                        statusMessage = string.Empty;

                    var rectElement = new Rect(rect.x + 292, rect.y + 1, Contents.ErrorWidth, EditorGUIUtility.singleLineHeight);
                    EditorGUI.LabelField(rectElement, statusMessage);

                    // Sync our UI to the editor's state
                    if (isFocused)
                    {
                        m_OutputReorderableList.onSelectCallback(m_OutputReorderableList);
                        m_SelectedDeviceData = videoDeviceData;
                    }
                }
            };

            m_OutputReorderableList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, Contents.OutputDevicesHeaderLabel);
            };

            m_OutputReorderableList.drawNoneElementCallback = rect =>
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.LabelField(rect, Contents.OutputDeviceListIsEmptyLabel, EditorStyles.label);
                }
            };

            m_OutputReorderableList.onAddCallback = list =>
            {
                var menu = new GenericMenu();

                menu.AddItem(Contents.AddOutputDeviceLabel, false, AddOutputDeviceTypeCallback, null);
                menu.ShowAsContext();

                EditorUtility.SetDirty(m_VideoIOManager);
            };

            m_OutputReorderableList.onSelectCallback = index =>
            {
            };

            m_OutputReorderableList.onRemoveCallback = list =>
            {
                if (m_Editor)
                {
                    DestroyImmediate(m_Editor);
                }

                var deviceName = m_VideoIOManager.RemoveDeviceInstance(list.index, VideoDeviceType.Output);
                var previousIndex = ParseDeviceName(deviceName);

                m_OutputDeviceCountPool.ReturnValue(previousIndex);

                EditorUtility.SetDirty(m_VideoIOManager);
            };
        }

        void DisplayVideoDeviceField(Rect rect, SerializedProperty element, string videoDeviceName, VideoDeviceType deviceType, string[] arrayNames)
        {
            var deviceIndex = element.FindPropertyRelative("CurrentDeviceIndex");

            var listGuiContents = arrayNames.Select((x) => new GUIContent(x)).ToList();
            listGuiContents.Insert(0, new GUIContent(Contents.DeviceNone));

            var indexProperty = deviceIndex.intValue;
            indexProperty = Mathf.Clamp(indexProperty, 0, arrayNames.Count());

            if (deviceIndex.intValue >= 0)
                ++indexProperty;

            var rectElement = new Rect(rect.x + 100, rect.y + 1, 190, EditorGUIUtility.singleLineHeight);
            var newIndex = EditorGUI.Popup(rectElement, indexProperty, listGuiContents.ToArray());

            if (newIndex != indexProperty)
            {
                m_VideoIOManager.StopTheVideoDeviceIfInUse(newIndex - 1, deviceType);
                m_VideoIOManager.ChangeVideoDeviceNameData(videoDeviceName, deviceType, newIndex - 1);
                EditorUtility.SetDirty(m_VideoIOManager);
            }
        }

        void AddInputDeviceTypeCallback(object _)
        {
            var deviceName = "Device " + m_InputDeviceCountPool.GetNextValue();
            var deviceType = VideoDeviceType.Input;

            m_VideoIOManager.GetOrCreateDeviceInstance(deviceName, deviceType);
        }

        void AddOutputDeviceTypeCallback(object _)
        {
            var deviceName = "Device " + m_OutputDeviceCountPool.GetNextValue();
            var deviceType = VideoDeviceType.Output;

            m_VideoIOManager.GetOrCreateDeviceInstance(deviceName, deviceType);
        }

        void DrawCurrentDevice()
        {
            if (m_SelectedDeviceData == null)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                Editor.CreateCachedEditor(m_SelectedDeviceData.VideoDevice, null, ref m_Editor);
                if (m_Editor != null)
                {
                    m_Editor.OnInspectorGUI();
                }
            }
        }

        void DetectConnectorMappingChanges(DeckLinkConnectorMapping connectorMapping)
        {
            if (m_CurrentMapping != connectorMapping)
            {
                m_HasChanged = true;
                serializedObject.ApplyModifiedProperties();
                m_VideoIOManager.MappingConnectorProfileChanged(connectorMapping);
            }
        }

        void AddCurrentDevicesIndexInUse(VideoDeviceType deviceType, SimpleValuePool<int> deviceIndexPool)
        {
            var devicesName = m_VideoIOManager.GetAvailableDeviceNames(deviceType);
            foreach (var name in devicesName)
            {
                var lastCharIndex = ParseDeviceName(name);
                deviceIndexPool.AddUsedValue(lastCharIndex);
            }
        }

        int ParseDeviceName(string name)
        {
            var lastCharIndex = name[name.Length - 1];
            return Convert.ToInt32(lastCharIndex.ToString());
        }
    }
}
