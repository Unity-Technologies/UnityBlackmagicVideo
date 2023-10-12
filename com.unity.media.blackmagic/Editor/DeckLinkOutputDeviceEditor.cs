using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.Assertions;
using UnityEditor.SceneManagement;

namespace Unity.Media.Blackmagic.Editor
{
    [CustomEditor(typeof(DeckLinkOutputDevice))]
    class DeckLinkOutputDeviceEditor : UnityEditor.Editor
    {
        static class Contents
        {
            public static GUIContent CameraSelectionLabel = new GUIContent("Camera", "The camera used to retrieve the RenderTexture, sent to the configured output SDI port.");
            public static GUIContent VideoModeLabel = new GUIContent("Video Mode", "The currently applied video mode of the device.");
            public static GUIContent VideoModeFoldoutLabel = new GUIContent("Video Mode Configuration", "Configure video media format options.");
            public static GUIContent DeviceSettingsFoldoutLabel = new GUIContent("Device Settings", "Configure Output Device settings regarding timing and latency.");
            public static GUIContent AudioConfigFoldoutLabel = new GUIContent("Audio Configurations", "Configure audio output.");
            public static GUIContent MediaModeLabel = new GUIContent("Media Mode", "The current source of video mode.");
            public static GUIContent CustomConfigLabel = new GUIContent("Custom", "Set Resolution, Frame Rate, and Scanning Mode explicitly.");
            public static GUIContent SameConfigAsInputLabel = new GUIContent("Same as Input", "Match the video mode of an input device.");
            public static GUIContent InputDeviceLabel = new GUIContent("Input Device", "The input device used to match video mode with.");
            public static GUIContent ResolutionSelectionLabel = new GUIContent("Resolution", "The resolution of the frames sent.");
            public static GUIContent FrameRateSelectionLabel = new GUIContent("Frame Rate", "The framerate of the frames sent. The available framerates depend on the selected video resolution.");
            public static GUIContent ScanModeLabel = new GUIContent("Scanning Mode", "Frame scanning mode. The available scanning modes depend on the selected video resolution and framerate.");
            public static GUIContent InterlacedLabel = new GUIContent("Interlaced", "Alternates between skipping every odd or even row each frame.");
            public static GUIContent ProgressiveLabel = new GUIContent("Progressive", "Always transmits the entire frame.");
            public static GUIContent QueueLengthLabel = new GUIContent("Queue Length", "Maximum number of frames in the output queue. A bigger value can be useful to avoid potential dropped frames.");
            public static GUIContent FilterModeLabel = new GUIContent("Filter Mode", "The filtering used for the allocated RenderTexture. The different options have different performance costs and image quality.");
            public static GUIContent PrerollLengthLabel = new GUIContent("Pre-Roll Length", "Maximum number of frames in the output queue. A bigger value can be useful to avoid potential dropped frames.");
            public static GUIContent LowLatencyModeLabel = new GUIContent("Low Latency", "When enabled and for every frame in the queue, wait for completion every time. Using this mode will result in a large performance hit and should be used sparingly.");
            public static GUIContent SyncModeLabel = new GUIContent("Sync Mode", "Output frames are asynchronously scheduled by the completion callback. If disabled, output frames are directly scheduled by Unity, "
                + "it can guarantee that all frames are to be scheduled on time but needs to be explicitly synchronized to output refreshing.");
            public static GUIContent TimecodeModeLabel = new GUIContent("Timecode Mode", "Determines the source of the timecode used by the output." +
                "\"Auto\" calculates the timecode from the frame number." +
                "\"Same as Input\" uses the timecode of the Input Device, if this output is configured as \"Same as Input\".");
            public static GUIContent TimecodeSourceLabel = new GUIContent("Timecode Source", "The timecode source whose last timecode is used as the output timecode.");
            public static GUIContent SynchronizerLabel = new GUIContent("Timecode Source", "The synchronizer whose most recent presentation timecode is used as the output timecode.");
            public static GUIContent UseGPUDirectLabel = new GUIContent("Use GPUDirect", "If your graphic card is compatible, the GPU frame is directly passed to the DeckLink device, without copying it in RAM before.");

            public static GUIContent KeyingModeLabel = new GUIContent("Keying Mode", "The video keying mode between internal keying and external keying.");
            public static GUIContent InternalKeyingLabel = new GUIContent("Internal", "Compose a foreground key frame over an incoming background video feed.");
            public static GUIContent ExternalKeyingLabel = new GUIContent("External", "Send the fill and key information to an external keyer.");
            public static GUIContent NoneKeyingLabel = new GUIContent("None", "The texture that contains the final compositing, sent to the configured SDI port.");

            public static GUIContent LinkModeLabel = new GUIContent("Link Mode", "The Link mode that determines how the video is sent over the SDI port.");
            public static GUIContent SingleLinkModeLabel = new GUIContent("Single Link", "The video signal is sent over one SDI port.");
            public static GUIContent DualLinkModeLabel = new GUIContent("Dual Link", "The video signal is sent over two SDI ports.");
            public static GUIContent QuadLinkModeLabel = new GUIContent("Quad Link", "The video signal is sent over four SDI ports.");

            public static GUIContent TargetTextureLabel = new GUIContent("Preview Texture", "The texture that contains the final compositing, sent to the configured SDI port.");
            public static GUIContent UpdateInEditorLabel = new GUIContent("Update in Editor", "Keeps the Video source updated in the Editor.");
            public static GUIContent AudioOutputModeLabel = new GUIContent("Audio Output Mode", "Selects where the audio samples will be taken from in the scene.");
            public static GUIContent AudioListenerLabel = new GUIContent("Audio Listener", "Audio listener that will be used for supplying the audio content.");
            public static GUIContent ReferenceStatusLabel = new GUIContent("Reference Status", "Determines if a Genlock signal has been found.");
            public static GUIContent GenlockEnabledLabel = new GUIContent("Genlock enabled", "Genlock has been found.");
            public static GUIContent GenlockDisabledLabel = new GUIContent("Genlock disabled", "Genlock has not been found.");
            public static GUIContent RequestedPixelFormatLabel = new GUIContent("Requested Format", "Request a specific output pixel format.");
            public static GUIContent RequestedColorSpaceLabel = new GUIContent("Color Space", "Request a specific output color space.");
            public static GUIContent RequestedTransferFunctionLabel = new GUIContent("Transfer Function", "BT2020 transfer function to apply.");
            public static GUIContent WorkingSpaceConversionLabel = new GUIContent("Convert Color Space", "Convert the Camera Colors assuming Unity working space (709).");

            public static GUIContent ResolutionSupportWarningLabel = new GUIContent("* This resolution is not available on this device.");
            public static GUIContent FrameRateSupportWarningLabel = new GUIContent("* This framerate is not available for the specified resolution on this device.");
            public static GUIContent ScanModeSupportWarningLabel = new GUIContent("* This scanning mode is not available for the specified resolution and framerate on this device.");
            public static GUIContent KeyingSupportWarningLabel = new GUIContent("This keying mode is not compatible for the specified pixel format.");
            public static GUIContent FollowInputNeedsDeviceLabel = new GUIContent("The video mode configuration must be set to \"Same as Input\" when using this timecode mode.");
            public static GUIContent DeviceBoundTwiceWarningLabel = new GUIContent("In Two Sub Devices Full Duplex mode, you can't have an input device and an output device with keying, bound to the same logical device at the same time.");

            public static GUIContent InputNoDevicesWarningLabel = new GUIContent("No input devices to choose from to match Video Mode.");
            public static GUIContent InputNoDeviceSelectedWarningLabel = new GUIContent("No input device selected.");
            public static GUIContent InputNoLogicalDeviceWarningLabel = new GUIContent("This input device has no associated logical device. Video Mode cannot be matched.");
            public static GUIContent InputNotActiveWarningLabel = new GUIContent("This input device is not active. Video Mode will be matched once Play Mode is entered or that device is activated in editor.");
            public static GUIContent InputUnknownFormatWarningLabel = new GUIContent("Unable to match unknown Video Mode received from this input device.");

            public static GUIContent AudioInputNoDevicesWarningLabel = new GUIContent("No input devices to choose from to receive audio packets.");
            public static GUIContent AudioInputNoLogicalDeviceWarningLabel = new GUIContent("This input device has no associated logical device. Audio packets cannot be retrieved.");
            public static GUIContent AudioInputNotActiveWarningLabel = new GUIContent("This input device is not active. Audio packets will be retrieved once Play Mode is entered or that device is activated in editor.");
            public static GUIContent AudioInputUnknownFormatWarningLabel = new GUIContent("Unable to retrieve audio packets from this input device.");

            public static GUIContent InputDeviceNotApplicableLabel = new GUIContent(BlackmagicUtilities.k_SignalVideoNotDefined, "Waiting on live signal in input device.");
            public static GUIContent LinkModeSupportWarningLabel = new GUIContent("This link mode is not compatible for the specified device or the selected connector mapping.");
            public static GUIContent DeckLinkManagerErrorLabel = new GUIContent("The DeckLink manager is invalid.");
            public static GUIContent GPUDirectNotCompatibleWarningLabel = new GUIContent("GPUDirect is not compatible with your GPU.");
            public static GUIContent GPUDirectNotCompatibleGraphicsAPIWarningLabel = new GUIContent("GPUDirect is not compatible with your current Graphics API, it only works for D3D11.");

            public const string DeviceNone = "None";
            public const string DeviceNameLabel = "Device";
            public const string TimecodeLabel = "Timecode";
            public const string DeviceNamePrefix = "Output";
            public const string PixelFormatReceived = "Pixel Format";
        }

        SerializedProperty m_CameraSelection;
        SerializedProperty m_SDKDisplayMode;
        SerializedProperty m_SameVideoModeAsInput;
        SerializedProperty m_SameVideoModeAsInputDevice;
        SerializedProperty m_QueueLength;
        SerializedProperty m_FilterMode;
        SerializedProperty m_PrerollLength;
        SerializedProperty m_KeyingMode;
        SerializedProperty m_LinkMode;
        SerializedProperty m_LowLatencyMode;
        SerializedProperty m_RequestedSyncMode;
        SerializedProperty m_TimecodeMode;
        SerializedProperty m_UseGPUDirect;
#if LIVE_CAPTURE_4_0_0_OR_NEWER
        SerializedProperty m_TimecodeSource;
        SerializedProperty m_Synchronizer;
#endif
        SerializedProperty m_TargetRenderTexture;
        SerializedProperty m_AudioOutputMode;
        SerializedProperty m_AudioListener;
        SerializedProperty m_AudioSameAsInputDevice;
        SerializedProperty m_RequestedPixelFormat;
        SerializedProperty m_RequestedColorSpace;
        SerializedProperty m_RequestedTransferFunction;

        string m_DeviceName;
        int m_DeviceSelectionCached;

        // Item1 determines if Keying is available or not.
        // Item2 determines if Link Mode are available or not.
        (bool, bool) m_IsKeyingAndLinkModeSupported;

        DeckLinkOutputDevice m_Target;

        SerializedProperty m_VideoModeFoldout;
        SerializedProperty m_DeviceSettingsFoldout;
        SerializedProperty m_AudioConfigFoldout;

        GUIContent m_VideoModeName = new GUIContent();
        GUIContent[] m_AllResolutionLabels;
        int[] m_AllResolutionValues;
        GUIContent[] m_FrameRateLabels;
        int[] m_FrameRateValues;
        GUIContent[] m_ScanModeLabels;
        GUIContent[] m_KeyingModeLabels;
        GUIContent[] m_PixelFormatLabels;
        GUIContent[] m_ColorSpaceLabels;
        GUIContent[] m_TransferFunctionLabels;
        GUIContent[] m_AudioOutputModeLabels;
        GUIContent[] m_LinkModeLabels;
        GUIContent[] m_AsyncModeLabels;
        GUIContent[] m_FilterModeLabels;
        GUIContent[] m_ConfigModeLabels = { Contents.CustomConfigLabel, Contents.SameConfigAsInputLabel };

        // Link Mode caching and compatible tests.
        Dictionary<int, Tuple<LinkMode, bool>> m_CompatibleLinkModes = new Dictionary<int, Tuple<LinkMode, bool>>();
        Dictionary<LinkMode, int> m_KeyModeIndex = new Dictionary<LinkMode, int>();
        Dictionary<int, BMDColorSpace> m_ColorSpaceMappings = new Dictionary<int, BMDColorSpace>();
        Dictionary<int, BMDTransferFunction> m_TransferFunctionMappings = new Dictionary<int, BMDTransferFunction>();
        Dictionary<int, BMDPixelFormat> m_PixelFormatMappings = new Dictionary<int, BMDPixelFormat>();

        VideoModeRegistry.SupportMap m_SupportedModes = new VideoModeRegistry.SupportMap();

        // UI index for selection and further translation to internal enums.
        const int Unset = -1;
        int m_ActualDeviceIndex = Unset;
        int m_ActualSDKDisplayMode = Unset;
        int m_Resolution = Unset;
        int m_FrameRate = Unset;
        int m_ScanMode = Unset;
        int m_KeyMode = Unset;
        int m_RequestedPixelFormatInt = Unset;
        int m_RequestedColorSpaceInt = Unset;
        int m_RequestedTransferFunctionInt = Unset;
        int m_LinkModeEnumIndex = Unset;
        int m_AsyncModeInt = Unset;
        int m_FilterModeInt = Unset;
        int m_AudioOutputModeInt = Unset;

        bool m_CanSetScanMode;
        DeckLinkManager m_DeckLinkManager;
        GUIStyle m_InlineFoldoutStyle;

        public override bool RequiresConstantRepaint()
        {
            return true;
        }

        void OnEnable()
        {
            m_Target = (DeckLinkOutputDevice)target;

            m_VideoModeFoldout = serializedObject.FindProperty("m_VideoModeFoldout");
            m_DeviceSettingsFoldout = serializedObject.FindProperty("m_DeviceSettingsFoldout");
            m_AudioConfigFoldout = serializedObject.FindProperty("m_AudioConfigFoldout");

            m_CameraSelection = serializedObject.FindProperty("m_TargetCamera");
            m_SDKDisplayMode = serializedObject.FindProperty("m_SDKDisplayMode");
            m_SameVideoModeAsInput = serializedObject.FindProperty("m_SameVideoModeAsInput");
            m_SameVideoModeAsInputDevice = serializedObject.FindProperty("m_SameVideoModeAsInputDevice");
            m_AudioSameAsInputDevice = serializedObject.FindProperty("m_AudioSameAsInputDevice");
            m_QueueLength = serializedObject.FindProperty("m_QueueLength");
            m_FilterMode = serializedObject.FindProperty("m_FilterMode");
            m_KeyingMode = serializedObject.FindProperty("m_KeyingMode");
            m_LinkMode = serializedObject.FindProperty("m_LinkMode");
            m_PrerollLength = serializedObject.FindProperty("m_PrerollLength");
            m_LowLatencyMode = serializedObject.FindProperty("m_LowLatencyMode");
            m_RequestedSyncMode = serializedObject.FindProperty("m_RequestedSyncMode");
            m_TimecodeMode = serializedObject.FindProperty("m_TimecodeMode");
            m_UseGPUDirect = serializedObject.FindProperty("m_RequestedUseGPUDirect");
#if LIVE_CAPTURE_4_0_0_OR_NEWER
            m_TimecodeSource = serializedObject.FindProperty("m_TimecodeSource");
            m_Synchronizer = serializedObject.FindProperty("m_Synchronizer");
#endif
            m_TargetRenderTexture = serializedObject.FindProperty("m_TargetRenderTexture");
            m_AudioOutputMode = serializedObject.FindProperty("m_AudioOutputMode");
            m_AudioListener = serializedObject.FindProperty("m_AudioListener");
            m_RequestedPixelFormat = serializedObject.FindProperty("m_RequestedPixelFormat");
            m_RequestedColorSpace = serializedObject.FindProperty("m_RequestedColorSpace");
            m_RequestedTransferFunction = serializedObject.FindProperty("m_RequestedTransferFunction");

            m_DeviceName = Contents.DeviceNamePrefix + " " + m_Target.name;
            m_ActualSDKDisplayMode = m_SDKDisplayMode.intValue;

            // Initialize UI selection, mappings needs to be done first
            CacheEnumsMappings();
            m_KeyMode = m_KeyingMode.intValue;
            m_RequestedPixelFormatInt =
                m_PixelFormatMappings.FirstOrDefault(
                    x => x.Value == (BMDPixelFormat)m_RequestedPixelFormat.intValue
                    ).Key;
            m_RequestedColorSpaceInt =
                m_ColorSpaceMappings.FirstOrDefault(
                    x => x.Value == (BMDColorSpace)m_RequestedColorSpace.intValue
                    ).Key;
            m_RequestedTransferFunctionInt =
                m_TransferFunctionMappings.FirstOrDefault(
                    x => x.Value == (BMDTransferFunction)m_RequestedTransferFunction.intValue
                    ).Key;
            m_LinkModeEnumIndex = m_LinkMode.enumValueIndex;
            m_AsyncModeInt = m_RequestedSyncMode.intValue;
            m_FilterModeInt = m_FilterMode.intValue;
            m_AudioOutputModeInt = m_AudioOutputMode.intValue;

            CacheMode();
            CacheSupportedModes(m_ActualDeviceIndex);
            CacheResolutions();
            CacheFrameRates();
            CacheScanModes();

            m_InlineFoldoutStyle = new GUIStyle(EditorStyles.foldout);
            m_InlineFoldoutStyle.fontSize = EditorStyles.label.fontSize;

            if (TryGetManager())
            {
                m_DeckLinkManager.OnMappingProfilesChanged += CacheLinkModes;
                CacheLinkModes(m_DeckLinkManager);
            }

            m_DeviceSelectionCached = m_Target.DeviceSelection;

            // Keying and LinkMode are now cached and bound to the current Logical Device (SDI Port)
            // and not to the Connector Mapping anymore (because of the new Multi-cards support).
            m_IsKeyingAndLinkModeSupported = m_DeckLinkManager.IsKeyingAndLinkModeSupported(m_Target.DeviceSelection);
        }

        void OnDisable()
        {
            if (m_DeckLinkManager != null)
            {
                m_DeckLinkManager.OnMappingProfilesChanged -= CacheLinkModes;
            }
        }

        void DisplayUpdateInEditor()
        {
            if (!Application.isPlaying)
            {
                var noLogicalDeviceSet = m_Target.DeviceSelection < 0;
                var noInputToMatch = m_SameVideoModeAsInput.boolValue && !GetInputVideoMode().HasValue;

                using (new EditorGUI.DisabledScope((noLogicalDeviceSet || noInputToMatch) && !m_Target.UpdateInEditor))
                {
                    using (var change = new EditorGUI.ChangeCheckScope())
                    {
                        m_Target.UpdateInEditor = EditorGUILayout.Toggle(Contents.UpdateInEditorLabel, m_Target.UpdateInEditor);

                        if (change.changed)
                            EditorApplication.QueuePlayerLoopUpdate();
                    }
                }
            }
        }

        void PrepareAndDisplayVideoMode()
        {
            // On device change
            if (m_ActualDeviceIndex != m_Target.DeviceSelection)
            {
                m_ActualDeviceIndex = m_Target.DeviceSelection;

                CacheMode();
                CacheSupportedModes(m_ActualDeviceIndex);
                CacheResolutions();
                CacheFrameRates();
                CacheScanModes();
                CacheLinkModes();
            }

            // On device display mode changed by another object
            if (m_ActualSDKDisplayMode != m_SDKDisplayMode.intValue)
            {
                m_ActualSDKDisplayMode = m_SDKDisplayMode.intValue;

                CacheMode();
                CacheFrameRates();
                CacheScanModes();
            }

            var videoModeName = m_VideoModeName;
            if (m_SameVideoModeAsInput.boolValue)
            {
                var inputVideoMode = GetInputVideoMode();
                if (inputVideoMode.HasValue)
                    videoModeName = new GUIContent(inputVideoMode.Value.VideoModeName());
                else
                    videoModeName = Contents.InputDeviceNotApplicableLabel;
            }
            EditorGUILayout.LabelField(Contents.VideoModeLabel, videoModeName);
        }

        void TimecodeDropDown()
        {
            EditorGUILayout.PropertyField(m_TimecodeMode, Contents.TimecodeModeLabel);

            switch ((OutputTimecodeMode)m_TimecodeMode.intValue)
            {
                case OutputTimecodeMode.SameAsInput:
                {
                    if (!m_SameVideoModeAsInput.boolValue)
                    {
                        EditorGUILayout.HelpBox(Contents.FollowInputNeedsDeviceLabel.text, MessageType.Warning, true);
                    }
                    break;
                }
#if LIVE_CAPTURE_4_0_0_OR_NEWER
                case OutputTimecodeMode.TimecodeSource:
                {
                    EditorGUILayout.PropertyField(m_TimecodeSource, Contents.TimecodeSourceLabel);
                    break;
                }
                case OutputTimecodeMode.TimecodeSynchronizer:
                {
                    EditorGUILayout.PropertyField(m_Synchronizer, Contents.SynchronizerLabel);
                    break;
                }
#endif
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField(Contents.DeviceNameLabel, m_DeviceName);

            // We should never reach this condition, except if we encounter a bug.
            if (!TryGetManager())
            {
                EditorGUILayout.HelpBox(Contents.DeckLinkManagerErrorLabel.text, MessageType.Error, true);
                return;
            }

            PrepareAndDisplayVideoMode();

            EditorGUILayout.LabelField(Contents.TimecodeLabel, m_Target.Timestamp.ToString());

            EditorGUI.BeginDisabledGroup(m_Target.IsActive);
            EditorGUILayout.PropertyField(m_CameraSelection, Contents.CameraSelectionLabel);
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(m_TargetRenderTexture, Contents.TargetTextureLabel);
            EditorGUI.EndDisabledGroup();

            DisplayUpdateInEditor();
            DisplayVideoModeFoldout();
            DisplayDeviceSettings();
            DisplayAudioOptions();

            serializedObject.ApplyModifiedProperties();
        }

        void DisplayDeviceSettings()
        {
            m_DeviceSettingsFoldout.boolValue = EditorGUILayout.Foldout(m_DeviceSettingsFoldout.boolValue, Contents.DeviceSettingsFoldoutLabel, true, m_InlineFoldoutStyle);
            if (m_DeviceSettingsFoldout.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    TimecodeDropDown();

                    DisplayGenericTrench(
                        Contents.SyncModeLabel,
                        ref m_AsyncModeInt,
                        m_AsyncModeLabels,
                        (x) =>
                        {
                            // Mapping is assumed to be Internal ID == GUID
                            m_RequestedSyncMode.intValue = x;
                            return true;
                        });

                    if (m_Target.RequestedSyncMode == DeckLinkOutputDevice.SyncMode.ManualMode)
                    {
                        EditorGUILayout.PropertyField(m_QueueLength, Contents.QueueLengthLabel);
                    }

                    DisplayGenericTrench(
                        Contents.FilterModeLabel,
                        ref m_FilterModeInt,
                        m_FilterModeLabels,
                        (x) =>
                        {
                            // Mapping is assumed to be Internal ID == GUID
                            m_FilterMode.intValue = x;
                            return true;
                        }
                    );

                    EditorGUILayout.PropertyField(m_PrerollLength, Contents.PrerollLengthLabel);
                    EditorGUILayout.PropertyField(m_LowLatencyMode, Contents.LowLatencyModeLabel);

                    var isGenlocked = ((DeckLinkOutputDevice)target).IsGenlocked;
                    EditorGUILayout.LabelField(Contents.ReferenceStatusLabel,
                        isGenlocked ? Contents.GenlockEnabledLabel : Contents.GenlockDisabledLabel);
                }
            }
        }

        void DisplayAudioOptions()
        {
            m_AudioConfigFoldout.boolValue = EditorGUILayout.Foldout(m_AudioConfigFoldout.boolValue, Contents.AudioConfigFoldoutLabel, true, m_InlineFoldoutStyle);
            if (m_AudioConfigFoldout.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    var previousIndex = m_AudioOutputModeInt;

                    EditorGUI.BeginDisabledGroup(m_Target.IsActive);

                    DisplayGenericTrench(
                        Contents.AudioOutputModeLabel,
                        ref m_AudioOutputModeInt,
                        m_AudioOutputModeLabels,
                        (x) =>
                        {
                            // Mapping is assumed to be Internal ID == GUID
                            m_AudioOutputMode.intValue = x;
                            return true;
                        }
                    );

                    if (m_AudioOutputMode.intValue == (int)AudioOutputMode.AudioListener)
                    {
                        EditorGUILayout.PropertyField(m_AudioListener, Contents.AudioListenerLabel);
                    }
                    else if (m_AudioOutputMode.intValue == (int)AudioOutputMode.SameAsInput)
                    {
                        DisplayAudioSameAsInputField(previousIndex != m_AudioOutputModeInt);
                    }

                    EditorGUI.EndDisabledGroup();
                }
            }
        }

        void DisplayGenericTrench(
            GUIContent label,
            ref int currentSelectedGUID,
            GUIContent[] optionsLabels,
            Func<int, bool> propagateGUIDChange)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                var selectionGUID = GUILayout.Toolbar(currentSelectedGUID, optionsLabels, GUILayout.ExpandWidth(false));
                if (selectionGUID != currentSelectedGUID)
                {
                    currentSelectedGUID = selectionGUID;
                    if (propagateGUIDChange(currentSelectedGUID))
                        EditorUtility.SetDirty(m_Target);
                }

                GUILayout.FlexibleSpace();
            }
        }

        void DisplayKeying()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(Contents.KeyingModeLabel);

                // Note: explanations in the "DisplayOptionsContent" comment.
                m_KeyMode = m_KeyingMode.intValue >> 1;

                var newKeyMode = GUILayout.Toolbar(m_KeyMode, m_KeyingModeLabels, GUILayout.ExpandWidth(false));
                if (newKeyMode != m_KeyMode)
                {
                    m_KeyMode = newKeyMode;
                    var realKeyMode = Mathf.Max(1, newKeyMode << 1);
                    if (m_Target.ChangeKeyingMode((KeyingMode)realKeyMode))
                    {
                        EditorUtility.SetDirty(m_Target);
                    }
                }

                GUILayout.FlexibleSpace();
            }
        }

        void DisplayLinkMode()
        {
            DisplayGenericTrench(
                Contents.LinkModeLabel,
                ref m_LinkModeEnumIndex,
                m_LinkModeLabels,
                (x) => m_Target.ChangeLinkMode(m_CompatibleLinkModes[x].Item1)
            );
        }

        void DisplayVideoModeFoldout()
        {
            var newVideoModeFoldout = EditorGUILayout.Foldout(m_VideoModeFoldout.boolValue, Contents.VideoModeFoldoutLabel, true, m_InlineFoldoutStyle);
            if (newVideoModeFoldout != m_VideoModeFoldout.boolValue)
            {
                // On toggle, reset the UI to match the device's display mode

                CacheMode();
                CacheFrameRates();
                CacheScanModes();
            }
            m_VideoModeFoldout.boolValue = newVideoModeFoldout;

            if (m_VideoModeFoldout.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DisplayVideoModeComponents();
                }
            }

            // Display warnings if selected mode not supported on device
            if (!m_SupportedModes.IsEmpty())
            {
                var resolution = m_Resolution.AsVideoModeResolution();
                var frameRate = m_FrameRate.AsVideoModeFrameRate();
                var scanMode = m_ScanMode.AsVideoModeScanMode();

                if (!m_SupportedModes.IsSupported(resolution))
                    EditorGUILayout.HelpBox(Contents.ResolutionSupportWarningLabel.text, MessageType.Warning);
                else if (!m_SupportedModes.IsSupported(resolution, frameRate))
                    EditorGUILayout.HelpBox(Contents.FrameRateSupportWarningLabel.text, MessageType.Warning);
                else if (!m_SupportedModes.IsSupported(resolution, frameRate, scanMode))
                    EditorGUILayout.HelpBox(Contents.ScanModeSupportWarningLabel.text, MessageType.Warning);
            }
        }

        bool TryGetManager()
        {
            return (m_DeckLinkManager == null)
                ? DeckLinkManager.TryGetInstance(out m_DeckLinkManager)
                : true;
        }

        void DisplayVideoModeComponents()
        {
            DisplayConfigMode();

            var selectionGUID = EditorGUILayout.Popup(Contents.RequestedPixelFormatLabel, m_RequestedPixelFormatInt, m_PixelFormatLabels);
            if (selectionGUID != m_RequestedPixelFormatInt)
            {
                m_RequestedPixelFormatInt = selectionGUID;
                m_RequestedPixelFormat.intValue = (int)m_PixelFormatMappings[selectionGUID];
                EditorUtility.SetDirty(m_Target);
            }

            EditorGUILayout.LabelField(Contents.PixelFormatReceived, (m_Target.PixelFormat.ToString()));

            DisplayGenericTrench(
                Contents.RequestedColorSpaceLabel,
                ref m_RequestedColorSpaceInt,
                m_ColorSpaceLabels,
                x =>
                {
                    Assert.IsTrue(m_ColorSpaceMappings.ContainsKey(x));
                    m_RequestedColorSpace.intValue = (int)m_ColorSpaceMappings[x];
                    return true;
                }
            );

            if ((BMDColorSpace)m_RequestedColorSpace.intValue == BMDColorSpace.BT2020)
            {
                DisplayGenericTrench(
                    Contents.RequestedTransferFunctionLabel,
                    ref m_RequestedTransferFunctionInt,
                    m_TransferFunctionLabels,
                    x =>
                    {
                        Assert.IsTrue(m_TransferFunctionMappings.ContainsKey(x));
                        m_RequestedTransferFunction.intValue = (int)m_TransferFunctionMappings[x];
                        return true;
                    }
                );
            }

            if ((BMDColorSpace)m_RequestedColorSpace.intValue != BMDColorSpace.BT709)
            {
                var workingSpaceConversion = m_Target.m_WorkingSpaceConversion;
                m_Target.m_WorkingSpaceConversion = EditorGUILayout.Toggle(Contents.WorkingSpaceConversionLabel, m_Target.m_WorkingSpaceConversion);

                if (workingSpaceConversion != m_Target.m_WorkingSpaceConversion)
                {
                    EditorUtility.SetDirty(m_Target);
                }
            }

            // If the Logical Device has changed, we are re-caching the Keying and Link mode compatibilities
            // (as they might be available now).
            if (m_DeviceSelectionCached != m_Target.DeviceSelection)
            {
                m_DeviceSelectionCached = m_Target.DeviceSelection;
                m_IsKeyingAndLinkModeSupported = m_DeckLinkManager.IsKeyingAndLinkModeSupported(m_Target.DeviceSelection);
            }

            // Note: because we are targeting a range of specific DeckLink cards, we do know that
            // internal and external keying is always supported for the compatible Connector Mapping profile(s).
            // We might need to change the Keying UI the day we'll add the support of card(s) that doesn't support both
            // external and internal keying at the same time.
            if (m_IsKeyingAndLinkModeSupported.Item1 && m_Target.RequestedColorSpace != BMDColorSpace.BT2020)
            {
                DisplayKeying();

                if (m_Target.OutputKeyingMode != KeyingMode.None && !m_Target.IsKeyingAvailable())
                {
                    EditorGUILayout.HelpBox(Contents.KeyingSupportWarningLabel.text, MessageType.Warning);
                }
            }

            // Check if the SDI port is bound twice (on one input device and one output device).
            // It's not possible to have it bound twice with the Two Sub Device Full Duplex.
            if (m_Target.OutputKeyingMode != KeyingMode.None && m_DeckLinkManager.IsLogicalDeviceBoundTwice(m_Target.DeviceSelection))
            {
                EditorGUILayout.HelpBox(Contents.DeviceBoundTwiceWarningLabel.text, MessageType.Warning);
            }

            if (m_IsKeyingAndLinkModeSupported.Item2)
            {
                DisplayLinkMode();

                if (!m_CompatibleLinkModes[m_LinkModeEnumIndex].Item2)
                {
                    EditorGUILayout.HelpBox(Contents.LinkModeSupportWarningLabel.text, MessageType.Warning);
                }
            }

            if (m_Target.IsWindowsPlatform())
            {
                EditorGUI.BeginDisabledGroup(m_Target.IsActive);
                using (var change = new EditorGUI.ChangeCheckScope())
                {
                    EditorGUILayout.PropertyField(m_UseGPUDirect, Contents.UseGPUDirectLabel);

                    if (change.changed)
                    {
                        OutputGPUDirect.CacheIfGPUDirectIsAvailable("Is GPUDirect Compatible");
                    }
                }
                EditorGUI.EndDisabledGroup();

                if (m_Target.IsGPUDirectNotAvailable())
                {
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Direct3D11)
                        EditorGUILayout.HelpBox(Contents.GPUDirectNotCompatibleGraphicsAPIWarningLabel.text, MessageType.Warning);
                    else
                        EditorGUILayout.HelpBox(Contents.GPUDirectNotCompatibleWarningLabel.text, MessageType.Warning);
                }
            }
        }

        void DisplayConfigMode()
        {
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(Contents.MediaModeLabel);

                var newSameVideoModeAsInput = GUILayout.Toolbar(
                    m_SameVideoModeAsInput.boolValue ? 1 : 0,
                    m_ConfigModeLabels,
                    GUILayout.ExpandWidth(false)) == 1 ? true : false;

                // Auto-set input device
                if (newSameVideoModeAsInput &&
                    !m_SameVideoModeAsInput.boolValue &&
                    m_DeckLinkManager != null &&
                    m_DeckLinkManager.m_InputDevices.Count == 1)
                {
                    m_SameVideoModeAsInputDevice.objectReferenceValue = m_DeckLinkManager.m_InputDevices[0].VideoDevice;
                }

                m_SameVideoModeAsInput.boolValue = newSameVideoModeAsInput;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                if (m_SameVideoModeAsInput.boolValue && m_DeckLinkManager != null)
                {
                    var inputDeviceLabels = new string[m_DeckLinkManager.m_InputDevices.Count + 1];
                    inputDeviceLabels[0] = Contents.DeviceNone;
                    var inputDeviceIndex = 0;

                    for (int i = 0; i < m_DeckLinkManager.m_InputDevices.Count; i++)
                    {
                        var device = m_DeckLinkManager.m_InputDevices[i];

                        if (m_SameVideoModeAsInputDevice.objectReferenceValue == device.VideoDevice)
                            inputDeviceIndex = i + 1;

                        var name = device.Name;
                        var hardwareName = device.CurrentDeviceIndex >= 0 ? m_DeckLinkManager.m_InputDeviceNames[device.CurrentDeviceIndex] : Contents.DeviceNone;
                        inputDeviceLabels[i + 1] = name + ": " + hardwareName;
                    }

                    if (inputDeviceLabels != null && inputDeviceLabels.Length > 0)
                    {
                        inputDeviceIndex = EditorGUILayout.Popup(Contents.InputDeviceLabel, inputDeviceIndex, inputDeviceLabels) - 1;

                        if (inputDeviceIndex >= 0)
                        {
                            var inputDevice = m_DeckLinkManager.m_InputDevices[inputDeviceIndex].VideoDevice as DeckLinkInputDevice;
                            m_SameVideoModeAsInputDevice.objectReferenceValue = inputDevice;

                            if (inputDevice.DeviceSelection < 0)
                                EditorGUILayout.HelpBox(Contents.InputNoLogicalDeviceWarningLabel.text, MessageType.Warning, true);
                            else if (!inputDevice.IsActive)
                                EditorGUILayout.HelpBox(Contents.InputNotActiveWarningLabel.text, MessageType.Warning, true);
                            else if (!inputDevice.VideoMode.HasValue)
                                EditorGUILayout.HelpBox(Contents.InputUnknownFormatWarningLabel.text, MessageType.Warning, true);
                        }
                        else
                        {
                            m_SameVideoModeAsInputDevice.objectReferenceValue = null;
                            EditorGUILayout.HelpBox(Contents.InputNoDeviceSelectedWarningLabel.text, MessageType.Warning, true);
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(Contents.InputNoDevicesWarningLabel.text, MessageType.Warning, true);
                    }

                    var resolutionLabel = Contents.InputDeviceNotApplicableLabel;
                    var frameRateLabel = Contents.InputDeviceNotApplicableLabel;
                    var scanModeLabel = Contents.InputDeviceNotApplicableLabel;

                    if (m_SameVideoModeAsInputDevice.objectReferenceValue != null)
                    {
                        var inputDevice = m_SameVideoModeAsInputDevice.objectReferenceValue as DeckLinkInputDevice;
                        var videoMode = inputDevice.VideoMode;
                        if (videoMode.HasValue)
                        {
                            resolutionLabel = new GUIContent(videoMode.Value.resolution.ResolutionName());
                            frameRateLabel = new GUIContent(videoMode.Value.frameRate.FrameRateName());
                            scanModeLabel = new GUIContent(videoMode.Value.scanMode.ScanModeName());
                        }
                    }

                    EditorGUILayout.LabelField(Contents.ResolutionSelectionLabel, resolutionLabel);
                    EditorGUILayout.LabelField(Contents.FrameRateSelectionLabel, frameRateLabel);
                    EditorGUILayout.LabelField(Contents.ScanModeLabel, scanModeLabel);
                }


                if (!m_SameVideoModeAsInput.boolValue)
                {
                    // On new user resolution selection
                    var newResolution = EditorGUILayout.IntPopup(Contents.ResolutionSelectionLabel, m_Resolution, m_AllResolutionLabels, m_AllResolutionValues);
                    if (newResolution != m_Resolution)
                    {
                        m_Resolution = newResolution;

                        CacheFrameRates();
                        CacheScanModes();
                        ApplyMode();
                    }

                    // On new user framerate selection
                    var newFrameRate = EditorGUILayout.IntPopup(Contents.FrameRateSelectionLabel, m_FrameRate, m_FrameRateLabels, m_FrameRateValues);
                    if (newFrameRate != m_FrameRate)
                    {
                        m_FrameRate = newFrameRate;

                        CacheScanModes();
                        ApplyMode();
                    }

                    using (new EditorGUI.DisabledScope(!m_CanSetScanMode))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.PrefixLabel(Contents.ScanModeLabel);

                            // On new user scan mode selection
                            var newScanMode = GUILayout.Toolbar(m_ScanMode, m_ScanModeLabels, GUILayout.ExpandWidth(false));
                            if (newScanMode != m_ScanMode)
                            {
                                m_ScanMode = newScanMode;
                                ApplyMode();
                            }

                            GUILayout.FlexibleSpace();
                        }
                    }
                }
            }
        }

        void DisplayAudioSameAsInputField(bool indexChanged)
        {
            using (new GUILayout.HorizontalScope())
            {
                if (indexChanged && m_DeckLinkManager.m_InputDevices.Count == 1)
                {
                    m_AudioSameAsInputDevice.objectReferenceValue = m_DeckLinkManager.m_InputDevices[0].VideoDevice;
                }
            }

            using (new EditorGUI.IndentLevelScope())
            {
                var inputDeviceLabels = new string[m_DeckLinkManager.m_InputDevices.Count + 1];
                inputDeviceLabels[0] = Contents.DeviceNone;
                var inputDeviceIndex = 0;

                for (int i = 0; i < m_DeckLinkManager.m_InputDevices.Count; i++)
                {
                    var device = m_DeckLinkManager.m_InputDevices[i];

                    if (m_AudioSameAsInputDevice.objectReferenceValue == device.VideoDevice)
                        inputDeviceIndex = i + 1;

                    var name = device.Name;
                    var hardwareName = device.CurrentDeviceIndex >= 0 ? m_DeckLinkManager.m_InputDeviceNames[device.CurrentDeviceIndex] : Contents.DeviceNone;
                    inputDeviceLabels[i + 1] = name + ": " + hardwareName;
                }

                if (inputDeviceLabels != null && inputDeviceLabels.Length > 0)
                {
                    inputDeviceIndex = EditorGUILayout.Popup(Contents.InputDeviceLabel, inputDeviceIndex, inputDeviceLabels) - 1;

                    if (inputDeviceIndex >= 0)
                    {
                        var inputDevice = m_DeckLinkManager.m_InputDevices[inputDeviceIndex].VideoDevice as DeckLinkInputDevice;
                        m_AudioSameAsInputDevice.objectReferenceValue = inputDevice;

                        if (inputDevice.DeviceSelection < 0)
                            EditorGUILayout.HelpBox(Contents.AudioInputNoLogicalDeviceWarningLabel.text, MessageType.Warning, true);
                        else if (!inputDevice.IsActive)
                            EditorGUILayout.HelpBox(Contents.AudioInputNotActiveWarningLabel.text, MessageType.Warning, true);
                        else if (!inputDevice.VideoMode.HasValue)
                            EditorGUILayout.HelpBox(Contents.AudioInputUnknownFormatWarningLabel.text, MessageType.Warning, true);
                    }
                    else
                    {
                        m_AudioSameAsInputDevice.objectReferenceValue = null;
                        EditorGUILayout.HelpBox(Contents.InputNoDeviceSelectedWarningLabel.text, MessageType.Warning, true);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(Contents.AudioInputNoDevicesWarningLabel.text, MessageType.Warning, true);
                }
            }
        }

        void CacheMode()
        {
            var mode = m_ActualSDKDisplayMode.SDKValueToVideoMode();

            // Set components from the device's video mode
            if (mode.HasValue)
            {
                m_Resolution = mode.Value.resolution.AsInt();
                m_FrameRate = mode.Value.frameRate.AsInt();
                m_ScanMode = mode.Value.scanMode.AsInt();

                m_VideoModeName.text = mode.Value.VideoModeName();
            }
            // Set device to default video mode and components
            else
            {
                var defaultMode = DeckLinkOutputDevice.DefaultVideoMode;

                Debug.LogWarning("Output device video mode was invalid, reset to default value: " + defaultMode.VideoModeName());

                m_Resolution = defaultMode.resolution.AsInt();
                m_FrameRate = defaultMode.frameRate.AsInt();
                m_ScanMode = defaultMode.scanMode.AsInt();

                ApplyMode();
            }
        }

        void CacheEnumsMappings()
        {
            m_KeyingModeLabels = new GUIContent[]
            {
                Contents.NoneKeyingLabel,
                Contents.ExternalKeyingLabel,
                Contents.InternalKeyingLabel
            };

            var prefix = "Standard: ";
            m_PixelFormatLabels = new GUIContent[]
            {
                new GUIContent("8-bit YUV", prefix + "UYVY"),
                new GUIContent("10-bit YUV", prefix + "V210"),
                new GUIContent("8-bit ARGB", prefix + "ARGB"),
                new GUIContent("8-bit BGRA", prefix + "BGRA"),
                new GUIContent("10-bit RGB", prefix + "R210"),
                new GUIContent("10-bit RGBXLE", prefix + "R10L"),
                new GUIContent("10-bit RGBX", prefix + "R10B"),
                new GUIContent("12-bit RGB", prefix + "R12B"),
                new GUIContent("12-bit RGBLE", prefix + "R12L")
            };
            m_PixelFormatMappings[0] = BMDPixelFormat.YUV8Bit;
            m_PixelFormatMappings[1] = BMDPixelFormat.YUV10Bit;
            m_PixelFormatMappings[2] = BMDPixelFormat.ARGB8Bit;
            m_PixelFormatMappings[3] = BMDPixelFormat.BGRA8Bit;
            m_PixelFormatMappings[4] = BMDPixelFormat.RGB10Bit;
            m_PixelFormatMappings[5] = BMDPixelFormat.RGBXLE10Bit;
            m_PixelFormatMappings[6] = BMDPixelFormat.RGBX10Bit;
            m_PixelFormatMappings[7] = BMDPixelFormat.RGB12Bit;
            m_PixelFormatMappings[8] = BMDPixelFormat.RGBLE12Bit;

            m_ColorSpaceLabels = new GUIContent[]
            {
                new GUIContent(ColorTransform.ColorSpaceToString(BMDColorSpace.BT601), "ITU-R BT.601."),
                new GUIContent(ColorTransform.ColorSpaceToString(BMDColorSpace.BT709), "ITU-R BT.709."),
                new GUIContent(ColorTransform.ColorSpaceToString(BMDColorSpace.BT2020), "ITU-R BT.2020/2100.")
            };
            m_ColorSpaceMappings[0] = BMDColorSpace.BT601;
            m_ColorSpaceMappings[1] = BMDColorSpace.BT709;
            m_ColorSpaceMappings[2] = BMDColorSpace.BT2020;

            m_TransferFunctionLabels = new GUIContent[]
            {
                new GUIContent(BMDTransferFunction.HDR.ToString(), "ITU-R BT.2020."),
                new GUIContent(BMDTransferFunction.HLG.ToString(), "ITU-R BT.2100 HLG."),
                new GUIContent(BMDTransferFunction.PQ.ToString(), "ITU-R BT.2100 PQ.")
            };
            m_TransferFunctionMappings[0] = BMDTransferFunction.HDR;
            m_TransferFunctionMappings[1] = BMDTransferFunction.HLG;
            m_TransferFunctionMappings[2] = BMDTransferFunction.PQ;


            m_AudioOutputModeLabels = new GUIContent[]
            {
                new GUIContent(AudioOutputMode.Disabled.ToString(), "Audio is disabled."),
                new GUIContent(AudioOutputMode.AudioListener.ToString(), "Select an audio listener as source."),
                new GUIContent(AudioOutputMode.SameAsInput.ToString(), "Use the audio from an input device.")
                //new GUIContent(DeckLinkOutputDevice.AudioOutputMode.MainOutput.ToString(), "Use the main audio output.")
            };

            m_AsyncModeLabels = new GUIContent[]
            {
                new GUIContent("Manual", "Output frames are asynchronously scheduled by the completion callback."),
                new GUIContent("Async", "Output frames are directly scheduled by Unity.")
            };

            m_FilterModeLabels = new GUIContent[]
            {
                new GUIContent(FilterMode.Point.ToString(), "Point filtering - texture pixels become blocky up close."),
                new GUIContent(FilterMode.Bilinear.ToString(), "Bilinear filtering - texture samples are averaged."),
                new GUIContent(FilterMode.Trilinear.ToString(), "Trilinear filtering - texture samples are averaged and also blended between mipmap levels."),
            };
        }

        void CacheLinkModes()
        {
            if (m_DeckLinkManager != null)
                CacheLinkModes(m_DeckLinkManager);
        }

        void CacheLinkModes(DeckLinkManager manager)
        {
            m_CompatibleLinkModes.Clear();
            m_KeyModeIndex.Clear();

            var linkModeNames = System.Enum.GetNames(typeof(LinkMode));
            int index = 0;

            var deckLinkCardObject = manager.GetDeckLinkCardFromLogicalDevice(m_Target.DeviceSelection);
            if (deckLinkCardObject == null)
                return;

            var compatibleLinkModes = deckLinkCardObject.compatibleLinkModes;

            foreach (var linkMode in (LinkMode[])Enum.GetValues(typeof(LinkMode)))
            {
                if ((compatibleLinkModes & (int)linkMode) == 0)
                {
                    linkModeNames[index] += "*";
                    m_CompatibleLinkModes.Add(index, Tuple.Create(linkMode, false));
                }
                else
                {
                    m_CompatibleLinkModes.Add(index, Tuple.Create(linkMode, true));
                }

                m_KeyModeIndex.Add(linkMode, index);
                ++index;
            }

            m_LinkModeLabels = linkModeNames.Select(x => new GUIContent(x)).ToArray();
        }

        void CacheSupportedModes(int deviceIndex)
        {
            if (deviceIndex >= 0)
            {
                var SDKModeValues = DeckLinkDeviceEnumerator.GetOutputModes(deviceIndex);
                m_SupportedModes.LoadSDKModeValues(SDKModeValues);
            }
            else
            {
                m_SupportedModes.Clear();
            }
        }

        void CacheResolutions()
        {
            var resolutionNames = VideoModeRegistry.Instance.GetAllResolutionNames();
            if (!m_SupportedModes.IsEmpty())
            {
                var resolutionNamesTemp = new List<string>(resolutionNames);
                for (var i = 0; i < resolutionNamesTemp.Count; i++)
                {
                    var resolution = i.AsVideoModeResolution();
                    if (!m_SupportedModes.IsSupported(resolution))
                        resolutionNamesTemp[i] += " *";
                }
                resolutionNames = resolutionNamesTemp.AsReadOnly();
            }

            m_AllResolutionLabels = resolutionNames.Select(x => new GUIContent(x)).ToArray();
            m_AllResolutionValues = Enumerable.Range(0, resolutionNames.Count).ToArray();
        }

        void CacheFrameRates()
        {
            Debug.Assert(m_Resolution != Unset);

            var resolution = m_Resolution.AsVideoModeResolution();
            var frameRates = new List<VideoMode.FrameRate>();
            VideoModeRegistry.Instance.Support.GetFrameRates(resolution, frameRates);

            if (m_SupportedModes.IsEmpty())
            {
                m_FrameRateLabels = frameRates.Select(x => new GUIContent(x.FrameRateName())).ToArray();
            }
            else
            {
                m_FrameRateLabels = frameRates.Select(frameRate =>
                {
                    var frameRateName = frameRate.FrameRateName();
                    if (!m_SupportedModes.IsSupported(resolution, frameRate))
                        frameRateName += " *";
                    return new GUIContent(frameRateName);
                }).ToArray();
            }

            m_FrameRateValues = frameRates.Select(x => x.AsInt()).ToArray();

            // Select the first available framerate if it's not possible to retain the previous selection
            if (!frameRates.Contains(m_FrameRate.AsVideoModeFrameRate()))
                m_FrameRate = m_FrameRateValues.First();
        }

        void CacheScanModes()
        {
            Debug.Assert(m_Resolution != Unset && m_FrameRate != Unset);

            m_ScanModeLabels = new GUIContent[] { Contents.ProgressiveLabel, Contents.InterlacedLabel };

            var resolution = m_Resolution.AsVideoModeResolution();
            var frameRate = m_FrameRate.AsVideoModeFrameRate();
            var scanModes = new List<VideoMode.ScanMode>();
            VideoModeRegistry.Instance.Support.GetScanModes(resolution, frameRate, scanModes);

            // Select the first available scanmode if it's not possible to retain the previous selection
            if (!scanModes.Contains(m_ScanMode.AsVideoModeScanMode()))
                m_ScanMode = scanModes.First().AsInt();

            m_CanSetScanMode = scanModes.Count > 1;

            if (!m_SupportedModes.IsEmpty() && m_CanSetScanMode)
            {
                for (var i = 0; i < m_ScanModeLabels.Length; i++)
                {
                    if (!m_SupportedModes.IsSupported(resolution, frameRate, i.AsVideoModeScanMode()))
                        m_ScanModeLabels[i] = new GUIContent(m_ScanModeLabels[i].text + " *", m_ScanModeLabels[i].tooltip);
                }
            }
        }

        void ApplyMode()
        {
            Debug.Assert(m_Resolution != Unset && m_FrameRate != Unset && m_ScanMode != Unset);

            var resolution = m_Resolution.AsVideoModeResolution();
            var frameRate = m_FrameRate.AsVideoModeFrameRate();
            var scanMode = m_ScanMode.AsVideoModeScanMode();
            var mode = VideoModeRegistry.Instance.GetMode(resolution, frameRate, scanMode);

            if (mode.HasValue)
            {
                m_ActualSDKDisplayMode = mode.Value.sdkValue;
                m_SDKDisplayMode.intValue = m_ActualSDKDisplayMode;

                m_Target.FormatName = mode.Value.VideoModeName();
                m_VideoModeName.text = mode.Value.VideoModeName();
            }
        }

        VideoMode? GetInputVideoMode()
        {
            if (m_SameVideoModeAsInputDevice.objectReferenceValue != null)
            {
                var inputDevice = m_SameVideoModeAsInputDevice.objectReferenceValue as DeckLinkInputDevice;
                return inputDevice.VideoMode;
            }
            return null;
        }
    }
}
