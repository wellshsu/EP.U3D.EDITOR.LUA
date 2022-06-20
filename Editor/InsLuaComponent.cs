//---------------------------------------------------------------------//
//                    GNU GENERAL PUBLIC LICENSE                       //
//                       Version 2, June 1991                          //
//                                                                     //
// Copyright (C) Wells Hsu, wellshsu@outlook.com, All rights reserved. //
// Everyone is permitted to copy and distribute verbatim copies        //
// of this license document, but changing it is not allowed.           //
//                  SEE LICENSE.md FOR MORE DETAILS.                   //
//---------------------------------------------------------------------//
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Reflection;
using System;
using System.Text;
using LuaInterface;
using EP.U3D.RUNTIME.LUA;
using EP.U3D.EDITOR.BASE;

namespace EP.U3D.EDITOR.LUA
{
    [CustomEditor(typeof(LuaComponent))]
    public class InsLuaComponent : Editor
    {
        private LuaComponent mInstance;
        public string[] mLuaScripts;
        private string mLuaProjRoot;
        private int mSelectedScript = -1;
        private Assembly mMainDLL;
        private Assembly mUnityDLL;
        private Assembly mUnityUIDLL;
        private readonly Dictionary<string, string> mReflects = new Dictionary<string, string>();
        private readonly string mHelpText =
            "USE @inspector to inspect property\n" +
            "[Type] int,long,float,double,bool/boolean,string\n" +
            "UnityEngine.Vector2/3/4,UnityEngine.Color\n" +
            "UnityEngine.Object,LuaComponent\n" +
            "[Example] ---@field col UnityEngine.Color @inspector\n" +
            "[Note] USE AssetManager.LoadAsset/LoadScene to load prefab.\n" +
            "USE UIHelper.AddComponent to add component dynamicly.\n";

        private void OnEnable()
        {
            mInstance = target as LuaComponent;
            mMainDLL = Assembly.GetAssembly(Constants.CSHAP_DLL);
            mUnityDLL = Assembly.GetAssembly(typeof(GameObject));
            mUnityUIDLL = Assembly.GetAssembly(typeof(UnityEngine.UI.Image));
            mLuaProjRoot = Constants.LUA_SCRIPT_WORKSPACE;
            List<string> scripts = new List<string>();
            CollectScripts(mLuaProjRoot, scripts);
            mLuaScripts = scripts.ToArray();

            mSelectedScript = -1;
            if (string.IsNullOrEmpty(mInstance.Script) == false)
            {
                string instanceScriptPath;
                if (string.IsNullOrEmpty(mInstance.Script))
                {
                    instanceScriptPath = mInstance.Script;
                }
                else
                {
                    instanceScriptPath = mInstance.Module + "." + mInstance.Script;
                }
                instanceScriptPath = instanceScriptPath.Replace(".", "/");
                instanceScriptPath += ".lua";
                for (int i = 0; i < mLuaScripts.Length; i++)
                {
                    string str = mLuaScripts[i];
                    if (instanceScriptPath.EndsWith(str))
                    {
                        mSelectedScript = i;
                        break;
                    }
                }
            }
        }

        public override void OnInspectorGUI()
        {
            //base.OnInspectorGUI();
            if (mInstance == null) return;
            GUILayout.Space(10f);
            //if (EditorHelper.DrawHeader("Help", false))
            //{
            //    EditorHelper.BeginContents();
            //    GUILayout.Label("Use @inspector to inspect property.");
            //    GUILayout.Label("Example: ---@field col UnityEngine.Color @inspector");
            //    EditorHelper.EndContents();

            //};
            EditorGUILayout.HelpBox(mHelpText, MessageType.Info);

            if (Application.isPlaying)
            {
                //GUILayout.BeginHorizontal();
                //GUILayout.Label("Instance", GUILayout.Width(60));
                //EditorGUILayout.TextField(mInstance.Table == null ? "null" : mInstance.Table.ToString());
                //GUILayout.EndHorizontal();
                if (mSelectedScript != -1) // TOFIX[20220612]: AddComponent的无法检视，无Module和Script
                {
                    string path = mLuaScripts[mSelectedScript];
                    path = mLuaProjRoot + path;

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.IntPopup(mSelectedScript, mLuaScripts, null, GUILayout.Height(15));
                    if (GUILayout.Button(new GUIContent("Edit"), GUILayout.Height(17), GUILayout.Width(50)))
                    {
                        EditorIDE.OpenScriptAtLine(path, 1);
                    }
                    GUILayout.EndHorizontal();
                    if (mInstance.Object == null)
                    {
                        EditorGUILayout.HelpBox($"Lua Object of {mInstance.Module}.{mInstance.Script} is nil", MessageType.Error);
                    }
                    else
                    {
                        mReflects.Clear();
                        string[] lines = File.ReadAllLines(path);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            string line = lines[i];
                            if (line.Contains("CLASS")) break;
                            if (line.Contains("@field") && line.Contains("@inspector"))
                            {
                                string[] strs = line.Split(' ');
                                if (strs.Length >= 4)
                                {
                                    string name = strs[1];
                                    string type = strs[2];
                                    if (mReflects.ContainsKey(name) == false) mReflects.Add(name, type);
                                }
                            }
                        }
                        foreach (var field in mReflects)
                        {
                            GUILayout.BeginHorizontal();
                            if (field.Value == "int")
                            {
                                int v = mInstance.Object.RawGet<string, int>(field.Key);
                                v = EditorGUILayout.IntField(field.Key, v);
                                mInstance.Object.RawSet(field.Key, v);
                            }
                            else if (field.Value == "long")
                            {
                                long v = mInstance.Object.RawGet<string, long>(field.Key);
                                v = EditorGUILayout.LongField(field.Key, v);
                                mInstance.Object.RawSet(field.Key, v);
                            }
                            else if (field.Value == "float")
                            {
                                float v = mInstance.Object.RawGet<string, float>(field.Key);
                                v = EditorGUILayout.FloatField(field.Key, v);
                                mInstance.Object.RawSet(field.Key, v);
                            }
                            else if (field.Value == "double")
                            {
                                double v = mInstance.Object.RawGet<string, double>(field.Key);
                                v = EditorGUILayout.DoubleField(field.Key, v);
                                mInstance.Object.RawSet(field.Key, v);
                            }
                            else if (field.Value == "bool" || field.Value == "boolean")
                            {
                                bool v = mInstance.Object.RawGet<string, bool>(field.Key);
                                v = EditorGUILayout.Toggle(field.Key, v);
                                mInstance.Object.RawSet(field.Key, v);
                            }
                            else if (field.Value == "UnityEngine.Vector2")
                            {
                                Vector2 v = mInstance.Object.RawGet<string, Vector2>(field.Key);
                                v = EditorGUILayout.Vector2Field(field.Key, v);
                                mInstance.Object.RawSet(field.Key, v);
                            }
                            else if (field.Value == "UnityEngine.Vector3")
                            {
                                Vector3 v = mInstance.Object.RawGet<string, Vector3>(field.Key);
                                v = EditorGUILayout.Vector3Field(field.Key, v);
                                mInstance.Object.RawSet(field.Key, v);
                            }
                            else if (field.Value == "UnityEngine.Vector4")
                            {
                                Vector4 v = mInstance.Object.RawGet<string, Vector4>(field.Key);
                                v = EditorGUILayout.Vector4Field(field.Key, v);
                                mInstance.Object.RawSet(field.Key, v);
                            }
                            else if (field.Value == "UnityEngine.Color")
                            {
                                Color v = mInstance.Object.RawGet<string, Color>(field.Key);
                                v = EditorGUILayout.ColorField(field.Key, v);
                                mInstance.Object.RawSet(field.Key, v);
                            }
                            else if (field.Value == "string")
                            {
                                string v = mInstance.Object.RawGet<string, string>(field.Key);
                                v = EditorGUILayout.TextField(field.Key, v);
                                mInstance.Object.RawSet(field.Key, v);
                            }
                            else
                            {
                                Type ftype = mUnityDLL.GetType(field.Value);
                                if (ftype == null) ftype = mUnityUIDLL.GetType(field.Value);
                                if (ftype == null) ftype = mMainDLL.GetType(field.Value);
                                if (ftype != null && ftype.IsSubclassOf(typeof(UnityEngine.Object)))
                                {
                                    UnityEngine.Object v = mInstance.Object.RawGet<string, UnityEngine.Object>(field.Key);
                                    v = EditorGUILayout.ObjectField(field.Key, v, ftype, true);
                                    mInstance.Object.RawSet(field.Key, v);
                                }
                                else
                                {
                                    LuaTable v = mInstance.Object.RawGet<string, LuaTable>(field.Key);
                                    LuaComponent lv = null;
                                    if (v != null)
                                    {
                                        GameObject o = v.RawGet<string, GameObject>("gameObject");
                                        if (o)
                                        {
                                            lv = o.GetComponent<LuaComponent>();
                                        }
                                    }
                                    lv = EditorGUILayout.ObjectField(field.Key, lv, typeof(LuaComponent), true) as LuaComponent;
                                    if (lv && lv.Script == field.Value)
                                    {
                                        mInstance.Object.RawSet(field.Key, lv.Object);
                                    }
                                }
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                }
            }
            else
            {
                GUILayout.BeginHorizontal();
                mSelectedScript = EditorGUILayout.IntPopup(mSelectedScript, mLuaScripts, null, GUILayout.Height(15));
                if (mSelectedScript != -1)
                {
                    string path = mLuaScripts[mSelectedScript];
                    path = path.Replace(".lua", "");
                    path = path.Replace("/", ".");
                    string module = path.Substring(0, path.LastIndexOf("."));
                    string script = path.Substring(path.LastIndexOf(".") + 1);
                    mInstance.Module = module;
                    mInstance.Script = script;
                }
                if (GUILayout.Button(new GUIContent("Edit"), GUILayout.Height(17), GUILayout.Width(50)))
                {
                    string path = mLuaScripts[mSelectedScript];
                    path = mLuaProjRoot + path;
                    EditorIDE.OpenScriptAtLine(path, 1);
                }
                GUILayout.EndHorizontal();
                if (mSelectedScript == -1)
                {
                    EditorGUILayout.HelpBox("Please select a script or remove this component.", MessageType.Error);
                }
                else
                {
                    mReflects.Clear();
                    string path = mLuaScripts[mSelectedScript];
                    path = mLuaProjRoot + path;
                    string[] lines = File.ReadAllLines(path);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        if (line.Contains("CLASS")) break;
                        if (line.Contains("@field") && line.Contains("@inspector"))
                        {
                            string[] strs = line.Split(' ');
                            if (strs.Length >= 4)
                            {
                                string name = strs[1];
                                string type = strs[2];
                                if (mReflects.ContainsKey(name) == false)
                                {
                                    mReflects.Add(name, type);
                                    var ret = mInstance.Fields.Find((ele) => { return ele.Key == name; });
                                    if (ret == null)
                                    {
                                        ret = new LuaComponent.Field();
                                        ret.Key = name;
                                        mInstance.Fields.Add(ret);
                                    }
                                    if (type != ret.Type) ret.Reset();
                                    ret.Type = type;
                                }
                            }
                        }
                    }
                    for (int i = 0; i < mInstance.Fields.Count;)
                    {
                        LuaComponent.Field field = mInstance.Fields[i];
                        if (mReflects.ContainsKey(field.Key) == false)
                        {
                            mInstance.Fields.Remove(field);
                        }
                        else
                        {
                            i++;
                            GUILayout.BeginHorizontal();
                            if (field.Type == "int")
                            {
                                int v = BitConverter.ToInt32(field.BValue, 0);
                                v = EditorGUILayout.IntField(field.Key, v);
                                field.BValue = BitConverter.GetBytes(v);
                            }
                            else if (field.Type == "long")
                            {
                                long v = BitConverter.ToInt64(field.BValue, 0);
                                v = EditorGUILayout.LongField(field.Key, v);
                                field.BValue = BitConverter.GetBytes(v);
                            }
                            else if (field.Type == "float")
                            {
                                float v = BitConverter.ToSingle(field.BValue, 0);
                                v = EditorGUILayout.FloatField(field.Key, v);
                                field.BValue = BitConverter.GetBytes(v);
                            }
                            else if (field.Type == "double")
                            {
                                double v = BitConverter.ToDouble(field.BValue, 0);
                                v = EditorGUILayout.DoubleField(field.Key, v);
                                field.BValue = BitConverter.GetBytes(v);
                            }
                            else if (field.Type == "bool" || field.Type == "boolean")
                            {
                                bool v = BitConverter.ToBoolean(field.BValue, 0);
                                v = EditorGUILayout.Toggle(field.Key, v);
                                field.BValue = BitConverter.GetBytes(v);
                            }
                            else if (field.Type == "UnityEngine.Vector2")
                            {
                                Vector2 v = Helper.ByteToStruct<Vector2>(field.BValue);
                                v = EditorGUILayout.Vector2Field(field.Key, v);
                                field.BValue = Helper.StructToByte(v);
                            }
                            else if (field.Type == "UnityEngine.Vector3")
                            {
                                Vector3 v = Helper.ByteToStruct<Vector3>(field.BValue);
                                v = EditorGUILayout.Vector3Field(field.Key, v);
                                field.BValue = Helper.StructToByte(v);
                            }
                            else if (field.Type == "UnityEngine.Vector4")
                            {
                                Vector4 v = Helper.ByteToStruct<Vector4>(field.BValue);
                                v = EditorGUILayout.Vector4Field(field.Key, v);
                                field.BValue = Helper.StructToByte(v);
                            }
                            else if (field.Type == "UnityEngine.Color")
                            {
                                Color v = Helper.ByteToStruct<Color>(field.BValue);
                                v = EditorGUILayout.ColorField(field.Key, v);
                                field.BValue = Helper.StructToByte(v);
                            }
                            else if (field.Type == "string")
                            {
                                string v = Encoding.UTF8.GetString(field.BValue);
                                v = EditorGUILayout.TextField(field.Key, v);
                                field.BValue = Encoding.UTF8.GetBytes(v);
                            }
                            else
                            {
                                Type ftype = mUnityDLL.GetType(field.Type);
                                if (ftype == null) ftype = mUnityUIDLL.GetType(field.Type);
                                if (ftype == null) ftype = mMainDLL.GetType(field.Type);
                                if (ftype != null && ftype.IsSubclassOf(typeof(UnityEngine.Object)))
                                {
                                    field.OValue = EditorGUILayout.ObjectField(field.Key, field.OValue, ftype, true);
                                }
                                else
                                {
                                    LuaComponent v = field.OValue as LuaComponent;
                                    v = EditorGUILayout.ObjectField(field.Key, v, typeof(LuaComponent), true) as LuaComponent;
                                    if (v && v.Script == field.Type)
                                    {
                                        field.OValue = v;
                                    }
                                    else
                                    {
                                        field.OValue = null;
                                    }
                                }
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                }
                if (GUI.changed) EditorUtility.SetDirty(target);
            }
        }

        private void CollectScripts(string directory, List<string> outfiles)
        {
            if (Directory.Exists(directory))
            {
                string[] files = Directory.GetFiles(directory);
                for (int i = 0; i < files.Length; i++)
                {
                    string file = files[i];
                    file = file.Replace("\\", "/");
                    if (file.EndsWith(".lua"))
                    {
                        file = file.Substring(mLuaProjRoot.Length);
                        outfiles.Add(file);
                    }
                }
                string[] dirs = Directory.GetDirectories(directory);
                for (int i = 0; i < dirs.Length; i++)
                {
                    CollectScripts(dirs[i], outfiles);
                }
            }
            else if (File.Exists(directory))
            {
                directory = directory.Substring(mLuaProjRoot.Length);
                outfiles.Add(directory);
            }
        }
    }
}