//---------------------------------------------------------------------//
//                    GNU GENERAL PUBLIC LICENSE                       //
//                       Version 2, June 1991                          //
//                                                                     //
// Copyright (C) Wells Hsu, wellshsu@outlook.com, All rights reserved. //
// Everyone is permitted to copy and distribute verbatim copies        //
// of this license document, but changing it is not allowed.           //
//                  SEE LICENSE.md FOR MORE DETAILS.                   //
//---------------------------------------------------------------------//
#pragma warning disable 0618

using EP.U3D.EDITOR.BASE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Preferences = EP.U3D.LIBRARY.BASE.Preferences;

namespace EP.U3D.EDITOR.LUA
{
    public class BuildLua
    {
        public static List<string> IgnoreList = new List<string>()
        {
            "Core/LuaDebug.lua"
        };
        public static bool x64 = false;
        public static void Execute()
        {
            x64 = false;
            int build = 2; // x86 + x64
            for (int i = 0; i < build; i++)
            {
                if (!PrepareDirectory()) return;
                List<string> byteFiles = new List<string>();
                LuaFileToBytes(byteFiles);
                ProcessBuild();
                DeleteLuaBytesFiles(byteFiles);
                GenerateManifest();
                x64 = true;
            }
            string toast = "Compile Lua done.";
            Helper.Log(toast);
            Helper.ShowToast(toast);
        }

        private static bool PrepareDirectory()
        {
            if (Directory.Exists(Constants.LUA_SCRIPT_WORKSPACE) == false)
            {
                Helper.LogError("Directory doesn't exist: {0}.", Constants.LUA_SCRIPT_WORKSPACE);
                return false;
            }
            string targetPath = x64 ?
                Constants.BUILD_LUA_BUNDLE_PATH + "x64/" :
                Constants.BUILD_LUA_BUNDLE_PATH + "x86/";
            if (Directory.Exists(targetPath) == false)
            {
                Directory.CreateDirectory(targetPath);
            }
            else
            {
                Directory.Delete(targetPath, true);
                Directory.CreateDirectory(targetPath);
            }
            return true;
        }

        private static void LuaFileToBytes(List<string> files)
        {
            if (files == null)
            {
                files = new List<string>();
            }
            CollectFiles(Constants.LUA_SCRIPT_WORKSPACE, files, "*" + Constants.LUA_BUNDLE_FILE_EXTENSION);
            if (files != null && files.Count >= 0)
            {
                for (int i = 0; i < files.Count; i++)
                {
                    string file = files[i];
                    if (string.IsNullOrEmpty(file)) continue;
                    bool skip = false;
                    for (int j = 0; j < IgnoreList.Count; j++)
                    {
                        string ignore = IgnoreList[j];
                        if (file.EndsWith(ignore))
                        {
                            skip = true;
                            break;
                        }
                    }
                    if (skip) { continue; }
                    if (Preferences.Instance.LiveMode)
                    {
                        Encrypt(file, file + ".bytes");
                    }
                    else
                    {
                        File.Copy(file, file + ".bytes");
                    }
                }
            }
            AssetDatabase.Refresh();
        }

        private static void ProcessBuild()
        {
            Caching.ClearCache();

            string[] subDirectories = Directory.GetDirectories(Constants.LUA_SCRIPT_WORKSPACE, "*", SearchOption.AllDirectories);

            if (subDirectories != null && subDirectories.Length > 0)
            {
                for (int i = 0; i < subDirectories.Length; i++)
                {
                    string directory = subDirectories[i];
                    BuildLuaScriptsToBundle(directory);
                }
            }
        }

        private static void BuildLuaScriptsToBundle(string directory)
        {
            directory = directory.Replace('\\', '/');
            directory = directory.Substring(directory.IndexOf("Assets/"));
            string[] files = Directory.GetFiles(directory, "*.lua.bytes");

            if (files != null && files.Length > 0)
            {
                string bundleName = directory.Substring(directory.IndexOf("LUA/") + 4);
                bundleName = bundleName.Replace("/", "_");
                bundleName = bundleName.ToLower();
                bundleName = bundleName + Constants.LUA_BUNDLE_FILE_EXTENSION;
                List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
                for (int i = 0; i < files.Length; i++)
                {
                    UnityEngine.Object obj = AssetDatabase.LoadMainAssetAtPath(files[i]);
                    assets.Add(obj);
                }

                BuildAssetBundleOptions options = BuildAssetBundleOptions.CollectDependencies | BuildAssetBundleOptions.CompleteAssets |
                                                BuildAssetBundleOptions.DeterministicAssetBundle | BuildAssetBundleOptions.UncompressedAssetBundle;

                string outputPath = x64 ?
                    Constants.BUILD_LUA_BUNDLE_PATH + "x64/" + bundleName :
                    Constants.BUILD_LUA_BUNDLE_PATH + "x86/" + bundleName;

                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                BuildPipeline.BuildAssetBundle(null, assets.ToArray(), outputPath, options, EditorUserBuildSettings.activeBuildTarget);
            }
        }

        private static void DeleteLuaBytesFiles(List<string> files)
        {
            if (files != null && files.Count >= 0)
            {
                for (int i = 0; i < files.Count; i++)
                {
                    string file = files[i];
                    if (string.IsNullOrEmpty(file)) continue;
                    if (File.Exists(file + ".bytes") == false) continue;
                    File.Delete(file + ".bytes");
                }
            }
            AssetDatabase.Refresh();
        }

        private static void GenerateManifest()
        {
            string targetPath = x64 ?
                            Constants.BUILD_LUA_BUNDLE_PATH + "x64/" :
                            Constants.BUILD_LUA_BUNDLE_PATH + "x86/";
            string filePath = targetPath + "/manifest.txt";
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            List<string> files = new List<string>();
            CollectFiles(targetPath, files, "*" + Constants.LUA_BUNDLE_FILE_EXTENSION);
            FileStream fs = new FileStream(filePath, FileMode.CreateNew);
            StreamWriter sw = new StreamWriter(fs);
            for (int i = 0; i < files.Count; i++)
            {
                string file = files[i];
                if (file.EndsWith(".meta")) continue;
                string md5 = Helper.FileMD5(file);
                string value = file.Replace(targetPath, string.Empty);
                int size = Helper.FileSize(file);
                sw.WriteLine(value + "|" + md5 + "|" + size);
            }
            sw.Close();
            fs.Close();
            AssetDatabase.Refresh();
        }

        public static List<string> CollectFiles(string directory, List<string> output, string extension)
        {
            if (output == null)
            {
                output = new List<string>();
            }
            if (Directory.Exists(directory))
            {
                string[] files = Directory.GetFiles(directory, extension);
                for (int i = 0; i < files.Length; i++)
                {
                    string file = NormallizePath(files[i]);
                    output.Add(file);
                }
                string[] dirs = Directory.GetDirectories(directory);
                for (int i = 0; i < dirs.Length; i++)
                {
                    CollectFiles(dirs[i], output, extension);
                }
            }
            return output;
        }

        public static string NormallizePath(string path)
        {
            return path.Replace("\\", "/");
        }

        public static string GetFileDirectory(string fullpath)
        {
            if (string.IsNullOrEmpty(fullpath))
            {
                return null;
            };
            string[] paths = fullpath.Split(new char[] { '/' });
            if (paths.Length == 0)
            {
                return null;
            }
            return paths[paths.Length - 1];
        }

        public static void Encrypt(string srcFile, string outFile)
        {
            if (!srcFile.ToLower().EndsWith(".lua"))
            {
                File.Copy(srcFile, outFile, true);
                return;
            }
            var pkg = Helper.FindPackage(Assembly.GetExecutingAssembly());
            bool isWin = true;
            string luaexe = string.Empty;
            string args = string.Empty;
            string exedir = string.Empty;
            string currDir = Directory.GetCurrentDirectory();
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                isWin = true;
                luaexe = "luajit.exe";
                args = "-b -g " + srcFile + " " + outFile;
                exedir = x64 ? pkg.resolvedPath + "/Editor/Libs/Encoder~/Win32/x64" : pkg.resolvedPath + "/Editor/Libs/Encoder~/Win32/x86";
            }
            else if (Application.platform == RuntimePlatform.OSXEditor)
            {
                isWin = false;
                luaexe = "./luajit";
                args = "-b -g " + srcFile + " " + outFile;
                exedir = x64 ? pkg.resolvedPath + "/Editor/Libs/Encoder~/Darwin/x64" : pkg.resolvedPath + "/Editor/Libs/Encoder~/Darwin/x86";
            }
            Directory.SetCurrentDirectory(exedir);
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = luaexe;
            info.Arguments = args;
            info.WindowStyle = ProcessWindowStyle.Hidden;
            info.UseShellExecute = isWin;
            info.ErrorDialog = true;

            Process pro = Process.Start(info);
            pro.WaitForExit();
            Directory.SetCurrentDirectory(currDir);
        }
    }
}
