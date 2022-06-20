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
using System;
using System.Diagnostics;
using EP.U3D.EDITOR.BASE;
using System.Reflection;

namespace EP.U3D.EDITOR.LUA
{
    public class MenuIDE : Editor
    {
        [MenuItem(Constants.MENU_PATCH_BUILD_LUA)]
        public static void BuildLUA()
        {
            Helper.OnlyWindows(() =>
             {
                 if (EditorApplication.isCompiling == false)
                 {
                     BuildLua.Execute();
                 }
                 else
                 {
                     EditorUtility.DisplayDialog("Warning", "Please wait till compile done.", "OK");
                 }
             });
        }

        [MenuItem(Constants.ASSETS_OPEN_LUA_PROJECT)]
        public static void OpenLuaProj()
        {
            string ide = EditorIDE.EnsureLuaIDE();
            if (string.IsNullOrEmpty(ide) == false)
            {
                if (EditorIDE.GetIDEType(ide) == EditorIDE.IDEType.VSCode)
                {
                    Process proc = new Process();
                    proc.StartInfo.FileName = ide;
                    proc.StartInfo.Arguments = string.Format("--new-window {0}", Constants.LUA_SCRIPT_WORKSPACE);
                    proc.Start();
                }
                else if (EditorIDE.GetIDEType(ide) == EditorIDE.IDEType.IDEA)
                {
                    if (!File.Exists(Constants.LUA_SCRIPT_WORKSPACE + ".idea/.name"))
                    {
                        File.WriteAllText(Constants.LUA_SCRIPT_WORKSPACE + ".idea/.name", Path.GetFileName(Path.GetDirectoryName(Application.dataPath)));
                    }
                    Process proc = new Process();
                    proc.StartInfo.FileName = ide;
                    proc.StartInfo.Arguments = Constants.LUA_SCRIPT_WORKSPACE;
                    proc.Start();
                }
                else
                {
                    EditorUtility.DisplayDialog("Warning", "Only support for vscode and idea.", "OK");
                }
            }
        }

        [MenuItem(Constants.ASSETS_CREATE_LUA_CLASS, false, 1)]
        public static void CreateLuaClass()
        {
            var pkg = Helper.FindPackage(Assembly.GetExecutingAssembly());
            string path = string.IsNullOrEmpty(Constants.LUA_CLASS_TEMPLATE) ? pkg.resolvedPath + "/Editor/Libs/Template~/Class.txt" : Constants.LUA_CLASS_TEMPLATE;
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, CreateInstance<ProcCreateLua>(), GetSelectedPathOrFallback() + "/NewLuaClass.lua", null, path);
        }

        [MenuItem(Constants.ASSETS_CREATE_LUA_COMPONENT, false, 2)]
        public static void CreateLuaComponent()
        {
            var pkg = Helper.FindPackage(Assembly.GetExecutingAssembly());
            string path = string.IsNullOrEmpty(Constants.LUA_CLASS_TEMPLATE) ? pkg.resolvedPath + "/Editor/Libs/Template~/Component.txt" : Constants.LUA_CLASS_TEMPLATE;
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, CreateInstance<ProcCreateLua>(), GetSelectedPathOrFallback() + "/NewLuaComponent.lua", null, path);
        }

        public static string GetSelectedPathOrFallback()
        {
            string path = "Assets";
            foreach (UnityEngine.Object obj in Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets))
            {
                path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    path = Path.GetDirectoryName(path);
                    break;
                }
            }
            return path;
        }
    }
}
