//---------------------------------------------------------------------//
//                    GNU GENERAL PUBLIC LICENSE                       //
//                       Version 2, June 1991                          //
//                                                                     //
// Copyright (C) Wells Hsu, wellshsu@outlook.com, All rights reserved. //
// Everyone is permitted to copy and distribute verbatim copies        //
// of this license document, but changing it is not allowed.           //
//                  SEE LICENSE.md FOR MORE DETAILS.                   //
//---------------------------------------------------------------------//
using UnityEngine;
using UnityEditor;
using System.IO;

namespace EP.U3D.EDITOR.LUA
{
    [CustomEditor(typeof(DefaultAsset))]
    public class InsLuaAsset : Editor
    {
        private const int MAX_LENGTH = 15000;
        private GUIStyle mTextStyle;

        public override void OnInspectorGUI()
        {
            if (mTextStyle == null)
            {
                mTextStyle = "ScriptText";
            }
            bool enabled = GUI.enabled;
            GUI.enabled = true;
            string assetPath = AssetDatabase.GetAssetPath(target);
            if (assetPath.EndsWith(".lua"))
            {
                string luaFile = File.ReadAllText(assetPath);
                string text;
                if (targets.Length > 1)
                {
                    text = Path.GetFileName(assetPath);
                }
                else
                {
                    text = luaFile;
                    if (text.Length > MAX_LENGTH)
                    {
                        text = text.Substring(0, MAX_LENGTH) + "...\n\n<...etc...>";
                    }
                }
                Rect rect = GUILayoutUtility.GetRect(new GUIContent(text), mTextStyle);
                rect.x = 0f;
                rect.y -= 3f;
                rect.width = EditorGUIUtility.currentViewWidth + 1f;
                GUI.Box(rect, text, mTextStyle);
            }
            GUI.enabled = enabled;
        }
    }
}