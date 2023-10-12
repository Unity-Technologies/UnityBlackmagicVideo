using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// This class contains all the editor utilities functions.
    /// </summary>
    static class VirtualProductionEditorUtilities
    {
        /// <summary>
        /// Draws a splitter and a header.
        /// </summary>
        /// <param name="guiContent">The name of the header.</param>
        /// <param name="foldout">The header is foldout or not.</param>
        /// <param name="addSpace">Add spaces before the header.</param>
        /// <param name="drawSplitter">Add a splitter after the header.</param>
        public static void DrawGUIContentHeader(GUIContent guiContent, ref bool foldout, bool addSpace = true, bool drawSplitter = true)
        {
            if (addSpace)
            {
                EditorGUILayout.Space(2);
            }

            if (drawSplitter)
            {
                DrawSplitter(false);
            }

            foldout = DrawHeaderFoldout(guiContent, foldout);
        }

        /// <summary>
        /// Draws a header foldout.
        /// </summary>
        /// <param name="title">The name of the header.</param>
        /// <param name="state">The state of the header.</param>
        /// <param name="isBoxed">The header is boxed or not.</param>
        /// <param name="hasMoreOptions">Function pointer for more options.</param>
        /// <param name="toggleMoreOptions">Delegate toggle for more options.</param>
        /// <returns></returns>
        public static bool DrawHeaderFoldout(GUIContent title, bool state, bool isBoxed = false, Func<bool> hasMoreOptions = null, Action toggleMoreOptions = null)
        {
            const float height = 17f;
            var backgroundRect = GUILayoutUtility.GetRect(1f, height);
            float xMin = backgroundRect.xMin;

            var labelRect = backgroundRect;
            labelRect.xMin += 16f;
            labelRect.xMax -= 20f;

            var foldoutRect = backgroundRect;
            foldoutRect.y += 1f;
            foldoutRect.width = 13f;
            foldoutRect.height = 13f;
            foldoutRect.x = labelRect.xMin + 15 * (EditorGUI.indentLevel - 1);

            // More options 1/2
            var moreOptionsRect = new Rect();
            if (hasMoreOptions != null)
            {
                moreOptionsRect = backgroundRect;
                moreOptionsRect.x += moreOptionsRect.width - 16 - 1;
                moreOptionsRect.height = 15;
                moreOptionsRect.width = 16;
            }

            // Background rect should be full-width
            backgroundRect.xMin = 0f;
            backgroundRect.width += 4f;

            if (isBoxed)
            {
                labelRect.xMin += 5;
                foldoutRect.xMin += 5;
                backgroundRect.xMin = xMin == 7.0 ? 4.0f : EditorGUIUtility.singleLineHeight;
                backgroundRect.width -= 1;
            }

            // Background
            float backgroundTint = EditorGUIUtility.isProSkin ? 0.1f : 1f;
            EditorGUI.DrawRect(backgroundRect, new Color(backgroundTint, backgroundTint, backgroundTint, 0.2f));

            // More options 2/2
            if (hasMoreOptions != null)
            {
                EditorGUI.BeginChangeCheck();
                //Styles.DrawMoreOptions(moreOptionsRect, hasMoreOptions());
                if (EditorGUI.EndChangeCheck() && toggleMoreOptions != null)
                {
                    toggleMoreOptions();
                }
            }

            // Title
            EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);

            // Active checkbox
            state = GUI.Toggle(foldoutRect, state, GUIContent.none, EditorStyles.foldout);

            var e = Event.current;
            if (e.type == EventType.MouseDown && backgroundRect.Contains(e.mousePosition) && !moreOptionsRect.Contains(e.mousePosition) && e.button == 0)
            {
                state = !state;
                e.Use();
            }

            return state;
        }

        /// <summary>
        /// Draws a splitter.
        /// </summary>
        /// <param name="isBoxed">The line is boxed or not.</param>
        public static void DrawSplitter(bool isBoxed = false)
        {
            var rect = GUILayoutUtility.GetRect(1f, 1f);
            float xMin = rect.xMin;

            // Splitter rect should be full-width
            rect.xMin = 0f;
            rect.width += 4f;

            if (isBoxed)
            {
                rect.xMin = xMin == 7.0 ? 4.0f : EditorGUIUtility.singleLineHeight;
                rect.width -= 1;
            }

            if (Event.current.type != EventType.Repaint)
                return;

            EditorGUI.DrawRect(rect, !EditorGUIUtility.isProSkin
                ? new Color(0.6f, 0.6f, 0.6f, 1.333f)
                : new Color(0.12f, 0.12f, 0.12f, 1.333f));
        }
    }
}
