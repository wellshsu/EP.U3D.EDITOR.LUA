//---------------------------------------------------------------------//
//                    GNU GENERAL PUBLIC LICENSE                       //
//                       Version 2, June 1991                          //
//                                                                     //
// Copyright (C) Wells Hsu, wellshsu@outlook.com, All rights reserved. //
// Everyone is permitted to copy and distribute verbatim copies        //
// of this license document, but changing it is not allowed.           //
//                  SEE LICENSE.md FOR MORE DETAILS.                   //
//---------------------------------------------------------------------//
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.ProjectWindowCallback;
using System.Text;
using System;
using EP.U3D.LIBRARY.BASE;

namespace EP.U3D.EDITOR.LUA
{
    public class ProcCreateLua : EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            UnityEngine.Object o = CreateScriptAssetFromTemplate(pathName, resourceFile);
            ProjectWindowUtil.ShowCreatedAsset(o);
        }

        internal static UnityEngine.Object CreateScriptAssetFromTemplate(string pathName, string resourceFile)
        {
            string fullPath = Path.GetFullPath(pathName);
            StreamReader streamReader = new StreamReader(resourceFile);
            string text = streamReader.ReadToEnd();
            streamReader.Close();

            // Replace #NAME#
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(pathName);
            text = Regex.Replace(text, "#NAME#", fileNameWithoutExtension);

            // Replace #DATETIME#
            string dataTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            text = Regex.Replace(text, "#DATETIME#", dataTime);

            // Replace #USER# Environment.UserName
            text = Regex.Replace(text, "#USER#", Preferences.Instance.Developer);

            UTF8Encoding encoding = new UTF8Encoding();
            bool append = false;
            StreamWriter streamWriter = new StreamWriter(fullPath, append, encoding);
            streamWriter.Write(text);
            streamWriter.Close();
            AssetDatabase.ImportAsset(pathName);
            return AssetDatabase.LoadAssetAtPath(pathName, typeof(UnityEngine.Object));
        }
    }
}