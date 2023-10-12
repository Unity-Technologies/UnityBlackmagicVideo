using System;
using UnityEditor;
using UnityEngine;

namespace Unity.Media.Blackmagic
{
    [CustomPropertyDrawer(typeof(InputVideoDeviceHandle))]
    [CustomPropertyDrawer(typeof(OutputVideoDeviceHandle))]
    [CustomPropertyDrawer(typeof(LabelOverride))]
    class VideoDevicePropertyDrawer : PropertyDrawer
    {
        static class Contents
        {
            public const string ManagerNotEnabled = "The Blackmagic video manager is disabled.";
            public const string NoAvailableDevice = "No available devices.";
            public const string InputDevices = "Input devices";
            public const string OutputDevices = "Output devices";
        }

        SerializedProperty m_NameProperty;
        SerializedProperty m_DeviceType;
        SerializedProperty m_OldDeviceName;
        SerializedProperty m_UpdateDevice;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            position.height = EditorGUIUtility.singleLineHeight;

            m_NameProperty = property.FindPropertyRelative("Name");
            m_DeviceType = property.FindPropertyRelative("m_DeviceType");
            m_OldDeviceName = property.FindPropertyRelative("m_OldDeviceName"); ;
            m_UpdateDevice = property.FindPropertyRelative("m_UpdateDevice"); ;

            var deviceSelectedName = m_NameProperty.stringValue;
            var deviceType = (VideoDeviceType)m_DeviceType.intValue;

            var contentName = GetFinalePropertyName(deviceType);
            var applyProperties = DrawAvailableDevices(ref deviceSelectedName,
                position,
                deviceType,
                contentName,
                ref m_OldDeviceName,
                ref m_UpdateDevice);

            m_NameProperty.stringValue = deviceSelectedName;

            if (applyProperties)
            {
                property.serializedObject.ApplyModifiedProperties();
            }
        }

        static bool DrawAvailableDevices(ref string deviceSelectedName,
            Rect rect,
            VideoDeviceType deviceType,
            string contentName,
            ref SerializedProperty oldDeviceName,
            ref SerializedProperty updateDevice)
        {
            var applyProperties = false;

            if (DeckLinkManager.TryGetInstance(out var videoIOManager) && DeckLinkManager.EnableVideoManager)
            {
                var deviceNames = videoIOManager.GetAvailableDeviceNames(deviceType);
                if (deviceNames != null && deviceNames.Length > 0)
                {
                    if (updateDevice.boolValue)
                    {
                        deviceSelectedName = oldDeviceName.stringValue;
                        updateDevice.boolValue = false;
                    }

                    var index = Array.IndexOf(deviceNames, deviceSelectedName);

                    if (index == -1)
                    {
                        index = 0;
                        applyProperties = true;
                    }

                    index = EditorGUI.Popup(rect, contentName, index, deviceNames);

                    if (index != -1)
                    {
                        deviceSelectedName = deviceNames[index];
                        oldDeviceName.stringValue = deviceSelectedName;
                    }
                }
                else
                {
                    updateDevice.boolValue = true;
                    deviceSelectedName = null;
                    EditorGUI.LabelField(rect, Contents.NoAvailableDevice);
                }
            }
            else
            {
                EditorGUI.LabelField(rect, Contents.ManagerNotEnabled);
            }

            return applyProperties;
        }

        string GetFinalePropertyName(VideoDeviceType deviceType)
        {
            var propertyName = this.attribute as LabelOverride;
            if (propertyName != null)
            {
                return propertyName.Label;
            }
            return (deviceType == VideoDeviceType.Input) ? Contents.InputDevices : Contents.OutputDevices;
        }
    }
}
