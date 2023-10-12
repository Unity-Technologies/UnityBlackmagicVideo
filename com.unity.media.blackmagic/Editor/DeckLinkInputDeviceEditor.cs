using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Unity.Media.Blackmagic.Editor
{
    [CustomEditor(typeof(DeckLinkInputDevice))]
    class DeckLinkInputDeviceEditor : UnityEditor.Editor
    {
        static class Contents
        {
            public static GUIContent QueueLengthLabel = new GUIContent("Queue Length", "Maximum number of received frames in the queue. A bigger value can be useful to avoid potential dropped frames.");
            public static GUIContent TargetRenderTextureLabel = new GUIContent("Preview Texture", "The texture that contains the input video received from the configured device selection.");
            public static GUIContent UpdateInEditorLabel = new GUIContent("Update in Editor", "Keeps the video source updated in the Editor.");
            public static GUIContent FilterModeLabel = new GUIContent("Filter Mode", "The filtering used for the allocated RenderTexture. The different options have different performance costs and image quality.");
            public static GUIContent RequestedPixelFormatLabel = new GUIContent("Format", "The capture pixel format to be used by the device.");
            public static GUIContent RequestedColorSpaceLabel = new GUIContent("Color Space", "The capture color space to be used by the device.");
            public static GUIContent RequestedTransferFunctionLabel = new GUIContent("Transfer Function", "The capture transfer function to be used by the device.");
            public static GUIContent EnablePassThroughLabel = new GUIContent("Enable Passthrough", "The monitoring video output is directly electrically connected to the video input.");
            public static GUIContent VideoModeFoldoutLabel = new GUIContent("Video Mode Configuration", "Configure resolution, framerate, and scanning mode.");
            public static GUIContent SignalLabel = new GUIContent("Signal", "Detect or override input signal.");
            public static GUIContent SameSignalAsDeviceLabel = new GUIContent("Same as Device", "Use the same pixel format, color space and transfer function as the input device.");
            public static GUIContent OverrideSignalLabel = new GUIContent("Override Signal", "Force Unity to interpret the signal using a different pixel format, color space or transfer function.");
            public static GUIContent DeviceSettingsFoldoutLabel = new GUIContent("Device Settings", "Configure Input Device settings regarding timing and latency.");
            public static GUIContent WorkingSpaceConversionLabel = new GUIContent("Convert Color Space", "Convert the Input Colors into Unity working space (709).");

            public const string DeviceNameLabel = "Device";
            public const string TimecodeLabel = "Timecode";
            public const string DeviceNamePrefix = "Input";
            public const string VideoModeReceived = "Video Mode";
            public const string VideoFormatReceived = "Video Format";
            public const string PixelFormatReceived = "Pixel Format";
            public const string ColorSpaceReceived = "Color Space";
            public const string TransferFunctionLabel = "Transfer Function";
        }

        SerializedProperty m_QueueLength;
        SerializedProperty m_FilterMode;
        SerializedProperty m_EnablePassThrough;
        SerializedProperty m_RequestedInPixelFormat;
        SerializedProperty m_RequestedColorSpace;
        SerializedProperty m_RequestedTransferFunction;
        SerializedProperty m_TargetRenderTexture;
        DeckLinkInputDevice m_Target;

        // Remember foldout states
        SerializedProperty m_VideoModeFoldout;
        SerializedProperty m_DeviceSettingsFoldout;

        string m_DeviceName;

        const int Unset = -1;
        int m_FilterModeInt = Unset;
        int m_RequestedInPixelFormatInt = Unset;
        int m_RequestedColorSpaceInt = Unset;
        int m_RequestedTransferFunctionInt = Unset;

        GUIContent[] m_FilterModeLabels;
        GUIContent[] m_PixelFormatLabels;
        GUIContent[] m_ColorSpaceLabels;
        GUIContent[] m_TransferFunctionLabels;
        GUIContent[] m_SignalTrenchLabels;
        GUIStyle m_InlineFoldoutStyle;

        Dictionary<int, BMDPixelFormat> m_PixelFormatMappings = new Dictionary<int, BMDPixelFormat>();
        Dictionary<int, BMDColorSpace> m_ColorspaceMappings = new Dictionary<int, BMDColorSpace>();
        Dictionary<int, BMDTransferFunction> m_TransferFunctionMappings = new Dictionary<int, BMDTransferFunction>();

        public override bool RequiresConstantRepaint()
        {
            return true;
        }

        void OnEnable()
        {
            m_Target = (DeckLinkInputDevice)target;
            m_QueueLength = serializedObject.FindProperty("m_QueueLength");
            m_FilterMode = serializedObject.FindProperty("m_FilterMode");
            m_EnablePassThrough = serializedObject.FindProperty("m_EnablePassThrough");
            m_RequestedInPixelFormat = serializedObject.FindProperty("m_RequestedInPixelFormat");
            m_RequestedColorSpace = serializedObject.FindProperty("m_RequestedColorSpace");
            m_RequestedTransferFunction = serializedObject.FindProperty("m_RequestedTransferFunction");
            m_TargetRenderTexture = serializedObject.FindProperty("m_TargetRenderTexture");

            m_VideoModeFoldout = serializedObject.FindProperty("m_VideoModeFoldout");
            m_DeviceSettingsFoldout = serializedObject.FindProperty("m_DeviceSettingsFoldout");

            m_DeviceName = Contents.DeviceNamePrefix + " " + m_Target.name;
            m_InlineFoldoutStyle = new GUIStyle(EditorStyles.foldout);
            m_InlineFoldoutStyle.fontSize = EditorStyles.label.fontSize;

            // Cache labels and initialize selections
            CacheLabels();

            m_RequestedInPixelFormatInt =
                m_PixelFormatMappings.FirstOrDefault(
                    x => x.Value == (BMDPixelFormat)m_RequestedInPixelFormat.intValue
                    ).Key;

            m_RequestedColorSpaceInt =
                m_ColorspaceMappings.FirstOrDefault(
                    x => x.Value == (BMDColorSpace)m_RequestedColorSpace.intValue
                    ).Key;

            m_RequestedTransferFunctionInt =
                m_TransferFunctionMappings.FirstOrDefault(
                    x => x.Value == (BMDTransferFunction)m_RequestedTransferFunction.intValue
                    ).Key;

            m_FilterModeInt = m_FilterMode.intValue;
        }

        void CacheLabels()
        {
            m_FilterModeLabels = new GUIContent[]
            {
                new GUIContent(FilterMode.Point.ToString(), "Point filtering - texture pixels become blocky up close."),
                new GUIContent(FilterMode.Bilinear.ToString(), "Bilinear filtering - texture samples are averaged."),
                new GUIContent(FilterMode.Trilinear.ToString(), "Trilinear filtering - texture samples are averaged and also blended between mipmap levels."),
            };

            m_PixelFormatLabels = new GUIContent[]
            {
                new GUIContent("Use Best quality", "Use signal best quality"),
                new GUIContent("8-bit YUV", "UYVY"),
                new GUIContent("10-bit YUV", "V210"),
                new GUIContent("8-bit ARGB", "ARGB"),
                new GUIContent("8-bit BGRA", "BGRA"),
                new GUIContent("10-bit RGB", "R210"),
                new GUIContent("10-bit RGBXLE", "R10L"),
                new GUIContent("10-bit RGBX", "R10B"),
                new GUIContent("12-bit RGB", "R12B"),
                new GUIContent("12-bit RGBLE", "R12L")
            };

            m_PixelFormatMappings[0] = BMDPixelFormat.UseBestQuality;
            m_PixelFormatMappings[1] = BMDPixelFormat.YUV8Bit;
            m_PixelFormatMappings[2] = BMDPixelFormat.YUV10Bit;
            m_PixelFormatMappings[3] = BMDPixelFormat.ARGB8Bit;
            m_PixelFormatMappings[4] = BMDPixelFormat.BGRA8Bit;
            m_PixelFormatMappings[5] = BMDPixelFormat.RGB10Bit;
            m_PixelFormatMappings[6] = BMDPixelFormat.RGBXLE10Bit;
            m_PixelFormatMappings[7] = BMDPixelFormat.RGBX10Bit;
            m_PixelFormatMappings[8] = BMDPixelFormat.RGB12Bit;
            m_PixelFormatMappings[9] = BMDPixelFormat.RGBLE12Bit;

            m_ColorSpaceLabels = new GUIContent[]
            {
                new GUIContent("Device Signal", "Follow the signal received from the active device"),
                new GUIContent(ColorTransform.ColorSpaceToString(BMDColorSpace.BT601), "ITU-R BT.601."),
                new GUIContent(ColorTransform.ColorSpaceToString(BMDColorSpace.BT709), "ITU-R BT.709."),
                new GUIContent(ColorTransform.ColorSpaceToString(BMDColorSpace.BT2020), "ITU-R BT.2020/2100.")
            };
            m_ColorspaceMappings[0] = BMDColorSpace.UseDeviceSignal;
            m_ColorspaceMappings[1] = BMDColorSpace.BT601;
            m_ColorspaceMappings[2] = BMDColorSpace.BT709;
            m_ColorspaceMappings[3] = BMDColorSpace.BT2020;

            m_TransferFunctionLabels = new GUIContent[]
            {
                new GUIContent("Device Signal", "Follow the signal received from the active device"),
                new GUIContent(BMDTransferFunction.HDR.ToString(), "ITU-R BT.2020."),
                new GUIContent(BMDTransferFunction.HLG.ToString(), "ITU-R BT.2100 HLG."),
                new GUIContent(BMDTransferFunction.PQ.ToString(), "ITU-R BT.2100 PQ.")
            };
            m_TransferFunctionMappings[0] = BMDTransferFunction.UseDeviceSignal;
            m_TransferFunctionMappings[1] = BMDTransferFunction.HDR;
            m_TransferFunctionMappings[2] = BMDTransferFunction.HLG;
            m_TransferFunctionMappings[3] = BMDTransferFunction.PQ;

            m_SignalTrenchLabels = new GUIContent[]
            {
                Contents.SameSignalAsDeviceLabel,
                Contents.OverrideSignalLabel
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField(Contents.DeviceNameLabel, m_DeviceName);
            EditorGUILayout.LabelField(Contents.VideoModeReceived, m_Target.FormatName);
            EditorGUILayout.LabelField(Contents.TimecodeLabel, m_Target.Timestamp.ToString());

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(m_TargetRenderTexture, Contents.TargetRenderTextureLabel);
            EditorGUI.EndDisabledGroup();

            if (!Application.isPlaying)
            {
                using (new EditorGUI.DisabledScope(m_Target.DeviceSelection < 0 && !m_Target.UpdateInEditor))
                {
                    using (var change = new EditorGUI.ChangeCheckScope())
                    {
                        m_Target.UpdateInEditor = EditorGUILayout.Toggle(Contents.UpdateInEditorLabel, m_Target.UpdateInEditor);

                        if (change.changed)
                            EditorApplication.QueuePlayerLoopUpdate();
                    }
                }
            }

            m_VideoModeFoldout.boolValue = EditorGUILayout.Foldout(m_VideoModeFoldout.boolValue, Contents.VideoModeFoldoutLabel, true, m_InlineFoldoutStyle);
            if (m_VideoModeFoldout.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.LabelField(Contents.VideoFormatReceived, (m_Target.FormatName));

                    // Offer conversion path if the desired color space is not 709
                    if ((m_Target.m_SignalOverride == 0 && m_Target.InColorSpace != BMDColorSpace.BT709) ||
                        (m_Target.m_SignalOverride == 1 && m_RequestedColorSpace.intValue != (int)BMDColorSpace.BT709))
                    {
                        var workingSpaceConversion = m_Target.m_WorkingSpaceConversion;
                        m_Target.m_WorkingSpaceConversion = EditorGUILayout.Toggle(Contents.WorkingSpaceConversionLabel, workingSpaceConversion);

                        if (workingSpaceConversion != m_Target.m_WorkingSpaceConversion)
                        {
                            EditorUtility.SetDirty(m_Target);
                        }
                    }

                    // Signal override trench
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PrefixLabel(Contents.SignalLabel);

                        var signalOverride = m_Target.m_SignalOverride;
                        m_Target.m_SignalOverride = GUILayout.Toolbar(signalOverride, m_SignalTrenchLabels, GUILayout.ExpandWidth(false));

                        if (signalOverride != m_Target.m_SignalOverride)
                        {
                            EditorUtility.SetDirty(m_Target);
                        }

                        GUILayout.FlexibleSpace();
                    }

                    using (new EditorGUI.IndentLevelScope())
                    {
                        if (m_Target.m_SignalOverride == 0) // Else ; override
                        {
                            EditorGUILayout.LabelField(Contents.PixelFormatReceived, m_Target.PixelFormat.ToString());
                            EditorGUILayout.LabelField(Contents.ColorSpaceReceived, m_Target.InColorSpace.ToString());

                            if (m_Target.InColorSpace == BMDColorSpace.BT2020)
                            {
                                EditorGUILayout.LabelField(Contents.TransferFunctionLabel, m_Target.TransferFunction.ToString());
                            }
                        }
                        else
                        {
                            // We need to update the Selection first, from the device, since we can be pushed overrides through the API
                            // Then we capture the user selection if any, and push it to the device.

                            m_RequestedInPixelFormatInt = m_PixelFormatMappings.FirstOrDefault(x => x.Value == (BMDPixelFormat)m_Target.requestedInPixelFormat).Key;
                            var selectionGUID = EditorGUILayout.Popup(Contents.RequestedPixelFormatLabel, m_RequestedInPixelFormatInt, m_PixelFormatLabels);
                            if (selectionGUID != m_RequestedInPixelFormatInt)
                            {
                                m_RequestedInPixelFormatInt = selectionGUID;
                                m_RequestedInPixelFormat.intValue = (int)m_PixelFormatMappings[selectionGUID];
                                EditorUtility.SetDirty(m_Target);
                            }

                            m_RequestedColorSpaceInt = m_ColorspaceMappings.FirstOrDefault(x => x.Value == (BMDColorSpace)m_Target.requestedColorSpace).Key;
                            var selectionGUID2 = EditorGUILayout.Popup(Contents.RequestedColorSpaceLabel, m_RequestedColorSpaceInt, m_ColorSpaceLabels);
                            if (selectionGUID2 != m_RequestedColorSpaceInt)
                            {
                                m_RequestedColorSpaceInt = selectionGUID2;
                                m_RequestedColorSpace.intValue = (int)m_ColorspaceMappings[selectionGUID2];
                                EditorUtility.SetDirty(m_Target);
                            }

                            if (m_RequestedColorSpace.intValue == (int)BMDColorSpace.BT2020)
                            {
                                m_RequestedTransferFunctionInt = m_TransferFunctionMappings.FirstOrDefault(x => x.Value == (BMDTransferFunction)m_Target.m_RequestedTransferFunction).Key;
                                var selectionGUID3 = EditorGUILayout.Popup(Contents.RequestedTransferFunctionLabel, m_RequestedTransferFunctionInt, m_TransferFunctionLabels);
                                if (selectionGUID3 != m_RequestedTransferFunctionInt)
                                {
                                    m_RequestedTransferFunctionInt = selectionGUID3;
                                    m_RequestedTransferFunction.intValue = (int)m_TransferFunctionMappings[selectionGUID3];
                                    EditorUtility.SetDirty(m_Target);
                                }
                            }
                        }
                    }
                }
            }

            m_DeviceSettingsFoldout.boolValue = EditorGUILayout.Foldout(m_DeviceSettingsFoldout.boolValue, Contents.DeviceSettingsFoldoutLabel, true, m_InlineFoldoutStyle);
            if (m_DeviceSettingsFoldout.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(m_QueueLength, Contents.QueueLengthLabel);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PrefixLabel(Contents.FilterModeLabel);

                        m_FilterModeInt = m_FilterMode.intValue;
                        var selectionGUID = GUILayout.Toolbar(m_FilterModeInt, m_FilterModeLabels, GUILayout.ExpandWidth(false));
                        if (selectionGUID != m_FilterModeInt)
                        {
                            m_FilterMode.intValue = selectionGUID;
                            EditorUtility.SetDirty(m_Target);
                        }

                        GUILayout.FlexibleSpace();
                    }

                    using (new EditorGUI.DisabledScope(m_Target.UpdateInEditor || Application.isPlaying))
                    {
                        EditorGUILayout.PropertyField(m_EnablePassThrough, Contents.EnablePassThroughLabel);
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
