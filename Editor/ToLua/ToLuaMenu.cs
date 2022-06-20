/*
Copyright (c) 2015-2017 topameng(topameng@qq.com)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
//打开开关没有写入导出列表的纯虚类自动跳过
//#define JUMP_NODEFINED_ABSTRACT         

using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.IO;
using System.Diagnostics;
using LuaInterface;

using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;
using Debugger = LuaInterface.Debugger;
using System.Threading;
using EP.U3D.EDITOR.BASE;

namespace LuaInterface.Editor
{
    public static class ToLuaMenu
    {
        static List<BindType> allTypes = new List<BindType>();

        static string RemoveNameSpace(string name, string space)
        {
            if (space != null)
            {
                name = name.Remove(0, space.Length + 1);
            }

            return name;
        }

        public class BindType
        {
            public string name;                 //类名称
            public Type type;
            public bool IsStatic;
            public string wrapName = "";        //产生的wrap文件名字
            public string libName = "";         //注册到lua的名字
            public Type baseType = null;
            public string nameSpace = null;     //注册到lua的table层级

            public List<Type> extendList = new List<Type>();

            public BindType(Type t)
            {
                if (typeof(System.MulticastDelegate).IsAssignableFrom(t))
                {
                    throw new NotSupportedException(string.Format("\nDon't export Delegate {0} as a class, register it in customDelegateList", LuaMisc.GetTypeName(t)));
                }

                //if (IsObsolete(t))
                //{
                //    throw new Exception(string.Format("\n{0} is obsolete, don't export it!", LuaMisc.GetTypeName(t)));
                //}

                type = t;
                nameSpace = ToLuaExport.GetNameSpace(t, out libName);
                name = ToLuaExport.CombineTypeStr(nameSpace, libName);
                libName = ToLuaExport.ConvertToLibSign(libName);

                if (name == "object")
                {
                    wrapName = "System_Object";
                    name = "System.Object";
                }
                else if (name == "string")
                {
                    wrapName = "System_String";
                    name = "System.String";
                }
                else
                {
                    wrapName = name.Replace('.', '_');
                    wrapName = ToLuaExport.ConvertToLibSign(wrapName);
                }

                int index = ToLuaSetting.StaticClassTypes.IndexOf(type);

                if (index >= 0 || (type.IsAbstract && type.IsSealed))
                {
                    IsStatic = true;
                }

                baseType = LuaMisc.GetExportBaseType(type);
            }

            public BindType SetBaseType(Type t)
            {
                baseType = t;
                return this;
            }

            public BindType AddExtendType(Type t)
            {
                if (!extendList.Contains(t))
                {
                    extendList.Add(t);
                }

                return this;
            }

            public BindType SetWrapName(string str)
            {
                wrapName = str;
                return this;
            }

            public BindType SetLibName(string str)
            {
                libName = str;
                return this;
            }

            public BindType SetNameSpace(string space)
            {
                nameSpace = space;
                return this;
            }

            public static bool IsObsolete(Type type)
            {
                object[] attrs = type.GetCustomAttributes(true);

                for (int j = 0; j < attrs.Length; j++)
                {
                    Type t = attrs[j].GetType();

                    if (t == typeof(System.ObsoleteAttribute) || t == typeof(NoToLuaAttribute) || t.Name == "MonoNotSupportedAttribute" || t.Name == "MonoTODOAttribute")
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        static void AutoAddBaseType(BindType bt, bool beDropBaseType)
        {
            Type t = bt.baseType;

            if (t == null)
            {
                return;
            }

            if (ToLuaSetting.SealedList.Contains(t))
            {
                ToLuaSetting.SealedList.Remove(t);
                Debugger.LogError("{0} not a sealed class, it is parent of {1}", LuaMisc.GetTypeName(t), bt.name);
            }

            if (t.IsInterface)
            {
                Debugger.LogWarning("{0} has a base type {1} is Interface, use SetBaseType to jump it", bt.name, t.FullName);
                bt.baseType = t.BaseType;
            }
            else if (ToLuaSetting.DropType.IndexOf(t) >= 0)
            {
                Debugger.LogWarning("{0} has a base type {1} is a drop type", bt.name, t.FullName);
                bt.baseType = t.BaseType;
            }
            else if (!beDropBaseType || ToLuaSetting.BaseType.IndexOf(t) < 0)
            {
                int index = allTypes.FindIndex((iter) => { return iter.type == t; });

                if (index < 0)
                {
#if JUMP_NODEFINED_ABSTRACT
                if (t.IsAbstract && !t.IsSealed)
                {
                    Debugger.LogWarning("not defined bindtype for {0}, it is abstract class, jump it, child class is {1}", LuaMisc.GetTypeName(t), bt.name);
                    bt.baseType = t.BaseType;
                }
                else
                {
                    Debugger.LogWarning("not defined bindtype for {0}, autogen it, child class is {1}", LuaMisc.GetTypeName(t), bt.name);
                    bt = new BindType(t);
                    allTypes.Add(bt);
                }
#else
                    Debugger.LogWarning("not defined bindtype for {0}, autogen it, child class is {1}", LuaMisc.GetTypeName(t), bt.name);
                    bt = new BindType(t);
                    allTypes.Add(bt);
#endif
                }
                else
                {
                    return;
                }
            }
            else
            {
                return;
            }

            AutoAddBaseType(bt, beDropBaseType);
        }

        static BindType[] GenBindTypes(BindType[] list, bool beDropBaseType = true)
        {
            allTypes = new List<BindType>(list);

            for (int i = 0; i < list.Length; i++)
            {
                for (int j = i + 1; j < list.Length; j++)
                {
                    if (list[i].type == list[j].type)
                        throw new NotSupportedException("Repeat BindType:" + list[i].type);
                }

                if (ToLuaSetting.DropType.IndexOf(list[i].type) >= 0)
                {
                    Debug.LogWarning(list[i].type.FullName + " in dropType table, not need to export");
                    allTypes.Remove(list[i]);
                    continue;
                }
                else if (beDropBaseType && ToLuaSetting.BaseType.IndexOf(list[i].type) >= 0)
                {
                    Debug.LogWarning(list[i].type.FullName + " is Base Type, not need to export");
                    allTypes.Remove(list[i]);
                    continue;
                }
                else if (list[i].type.IsEnum)
                {
                    continue;
                }

                AutoAddBaseType(list[i], beDropBaseType);
            }

            return allTypes.ToArray();
        }

        public static void GenerateClassWraps()
        {
            if (!File.Exists(Constants.LUA_ADAPTER_PATH))
            {
                Directory.CreateDirectory(Constants.LUA_ADAPTER_PATH);
            }

            allTypes.Clear();
            BindType[] typeList = ToLuaSetting.CustomTypeList;

            BindType[] list = GenBindTypes(typeList);
            ToLuaExport.allTypes.AddRange(ToLuaSetting.BaseType);

            for (int i = 0; i < list.Length; i++)
            {
                ToLuaExport.allTypes.Add(list[i].type);
            }

            for (int i = 0; i < list.Length; i++)
            {
                ToLuaExport.Clear();
                ToLuaExport.className = list[i].name;
                ToLuaExport.type = list[i].type;
                ToLuaExport.isStaticClass = list[i].IsStatic;
                ToLuaExport.baseType = list[i].baseType;
                ToLuaExport.wrapClassName = list[i].wrapName;
                ToLuaExport.libClassName = list[i].libName;
                ToLuaExport.extendList = list[i].extendList;
                ToLuaExport.Generate(Constants.LUA_ADAPTER_PATH);
            }

            Debug.Log("Generate lua binding files over");
            ToLuaExport.allTypes.Clear();
            allTypes.Clear();
            AssetDatabase.Refresh();
        }

        static HashSet<Type> GetCustomTypeDelegates()
        {
            BindType[] list = ToLuaSetting.CustomTypeList;
            HashSet<Type> set = new HashSet<Type>();
            BindingFlags binding = BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Instance;

            for (int i = 0; i < list.Length; i++)
            {
                Type type = list[i].type;
                FieldInfo[] fields = type.GetFields(BindingFlags.GetField | BindingFlags.SetField | binding);
                PropertyInfo[] props = type.GetProperties(BindingFlags.GetProperty | BindingFlags.SetProperty | binding);
                MethodInfo[] methods = null;

                if (type.IsInterface)
                {
                    methods = type.GetMethods();
                }
                else
                {
                    methods = type.GetMethods(BindingFlags.Instance | binding);
                }

                for (int j = 0; j < fields.Length; j++)
                {
                    Type t = fields[j].FieldType;

                    if (ToLuaExport.IsDelegateType(t))
                    {
                        set.Add(t);
                    }
                }

                for (int j = 0; j < props.Length; j++)
                {
                    Type t = props[j].PropertyType;

                    if (ToLuaExport.IsDelegateType(t))
                    {
                        set.Add(t);
                    }
                }

                for (int j = 0; j < methods.Length; j++)
                {
                    MethodInfo m = methods[j];

                    if (m.IsGenericMethod)
                    {
                        continue;
                    }

                    ParameterInfo[] pifs = m.GetParameters();

                    for (int k = 0; k < pifs.Length; k++)
                    {
                        Type t = pifs[k].ParameterType;
                        if (t.IsByRef) t = t.GetElementType();

                        if (ToLuaExport.IsDelegateType(t))
                        {
                            set.Add(t);
                        }
                    }
                }

            }

            return set;
        }

        static void GenLuaDelegates()
        {
            ToLuaExport.Clear();
            List<DelegateType> list = new List<DelegateType>();
            list.AddRange(ToLuaSetting.CustomDelegateList);
            HashSet<Type> set = GetCustomTypeDelegates();

            foreach (Type t in set)
            {
                if (null == list.Find((p) => { return p.type == t; }))
                {
                    list.Add(new DelegateType(t));
                }
            }

            ToLuaExport.GenDelegates(list.ToArray());
            set.Clear();
            ToLuaExport.Clear();
            AssetDatabase.Refresh();
            Debug.Log("Create lua delegate over");
        }

        static ToLuaTree<string> InitTree()
        {
            ToLuaTree<string> tree = new ToLuaTree<string>();
            ToLuaNode<string> root = tree.GetRoot();
            BindType[] list = GenBindTypes(ToLuaSetting.CustomTypeList);

            for (int i = 0; i < list.Length; i++)
            {
                string space = list[i].nameSpace;
                AddSpaceNameToTree(tree, root, space);
            }

            DelegateType[] dts = ToLuaSetting.CustomDelegateList;
            string str = null;

            for (int i = 0; i < dts.Length; i++)
            {
                string space = ToLuaExport.GetNameSpace(dts[i].type, out str);
                AddSpaceNameToTree(tree, root, space);
            }

            return tree;
        }

        static void AddSpaceNameToTree(ToLuaTree<string> tree, ToLuaNode<string> parent, string space)
        {
            if (space == null || space == string.Empty)
            {
                return;
            }

            string[] ns = space.Split(new char[] { '.' });

            for (int j = 0; j < ns.Length; j++)
            {
                List<ToLuaNode<string>> nodes = tree.Find((_t) => { return _t == ns[j]; }, j);

                if (nodes.Count == 0)
                {
                    ToLuaNode<string> node = new ToLuaNode<string>();
                    node.value = ns[j];
                    parent.childs.Add(node);
                    node.parent = parent;
                    node.layer = j;
                    parent = node;
                }
                else
                {
                    bool flag = false;
                    int index = 0;

                    for (int i = 0; i < nodes.Count; i++)
                    {
                        int count = j;
                        int size = j;
                        ToLuaNode<string> nodecopy = nodes[i];

                        while (nodecopy.parent != null)
                        {
                            nodecopy = nodecopy.parent;
                            if (nodecopy.value != null && nodecopy.value == ns[--count])
                            {
                                size--;
                            }
                        }

                        if (size == 0)
                        {
                            index = i;
                            flag = true;
                            break;
                        }
                    }

                    if (!flag)
                    {
                        ToLuaNode<string> nnode = new ToLuaNode<string>();
                        nnode.value = ns[j];
                        nnode.layer = j;
                        nnode.parent = parent;
                        parent.childs.Add(nnode);
                        parent = nnode;
                    }
                    else
                    {
                        parent = nodes[index];
                    }
                }
            }
        }

        static string GetSpaceNameFromTree(ToLuaNode<string> node)
        {
            string name = node.value;

            while (node.parent != null && node.parent.value != null)
            {
                node = node.parent;
                name = node.value + "." + name;
            }

            return name;
        }

        static void GenLuaBinder()
        {
            allTypes.Clear();
            ToLuaTree<string> tree = InitTree();
            StringBuilder sb = new StringBuilder();
            List<DelegateType> dtList = new List<DelegateType>();

            List<DelegateType> list = new List<DelegateType>();
            list.AddRange(ToLuaSetting.CustomDelegateList);
            HashSet<Type> set = GetCustomTypeDelegates();

            List<BindType> backupList = new List<BindType>();
            backupList.AddRange(allTypes);
            ToLuaNode<string> root = tree.GetRoot();
            string libname = null;

            foreach (Type t in set)
            {
                if (null == list.Find((p) => { return p.type == t; }))
                {
                    DelegateType dt = new DelegateType(t);
                    AddSpaceNameToTree(tree, root, ToLuaExport.GetNameSpace(t, out libname));
                    list.Add(dt);
                }
            }

            sb.AppendLineEx("//this source code was auto-generated by tolua#, do not modify it");
            sb.AppendLineEx("using System;");
            sb.AppendLineEx("using UnityEngine;");
            sb.AppendLineEx("using LuaInterface;");
            sb.AppendLineEx();
            sb.AppendLineEx("public static class LuaBinder");
            sb.AppendLineEx("{");
            sb.AppendLineEx("\tpublic static void Bind(LuaState L)");
            sb.AppendLineEx("\t{");
            sb.AppendLineEx("\t\tfloat t = Time.realtimeSinceStartup;");
            sb.AppendLineEx("\t\tL.BeginModule(null);");

            GenRegisterInfo(null, sb, list, dtList);

            Action<ToLuaNode<string>> begin = (node) =>
            {
                if (node.value == null)
                {
                    return;
                }

                sb.AppendFormat("\t\tL.BeginModule(\"{0}\");\r\n", node.value);
                string space = GetSpaceNameFromTree(node);

                GenRegisterInfo(space, sb, list, dtList);
            };

            Action<ToLuaNode<string>> end = (node) =>
            {
                if (node.value != null)
                {
                    sb.AppendLineEx("\t\tL.EndModule();");
                }
            };

            tree.DepthFirstTraversal(begin, end, tree.GetRoot());
            sb.AppendLineEx("\t\tL.EndModule();");

            if (ToLuaSetting.DynamicList.Count > 0)
            {
                sb.AppendLineEx("\t\tL.BeginPreLoad();");

                for (int i = 0; i < ToLuaSetting.DynamicList.Count; i++)
                {
                    Type t1 = ToLuaSetting.DynamicList[i];
                    BindType bt = backupList.Find((p) => { return p.type == t1; });
                    if (bt != null) sb.AppendFormat("\t\tL.AddPreLoad(\"{0}\", LuaOpen_{1}, typeof({0}));\r\n", bt.name, bt.wrapName);
                }

                sb.AppendLineEx("\t\tL.EndPreLoad();");
            }

            sb.AppendLineEx("\t\tDebugger.Log(\"Register lua type cost time: {0}\", Time.realtimeSinceStartup - t);");
            sb.AppendLineEx("\t}");

            for (int i = 0; i < dtList.Count; i++)
            {
                ToLuaExport.GenEventFunction(dtList[i].type, sb);
            }

            if (ToLuaSetting.DynamicList.Count > 0)
            {

                for (int i = 0; i < ToLuaSetting.DynamicList.Count; i++)
                {
                    Type t = ToLuaSetting.DynamicList[i];
                    BindType bt = backupList.Find((p) => { return p.type == t; });
                    if (bt != null) GenPreLoadFunction(bt, sb);
                }
            }

            sb.AppendLineEx("}\r\n");
            allTypes.Clear();
            string file = Constants.LUA_ADAPTER_PATH + "LuaBinder.cs";

            using (StreamWriter textWriter = new StreamWriter(file, false, Encoding.UTF8))
            {
                textWriter.Write(sb.ToString());
                textWriter.Flush();
                textWriter.Close();
            }

            AssetDatabase.Refresh();
            Debugger.Log("Generate LuaBinder over !");
        }

        static void GenRegisterInfo(string nameSpace, StringBuilder sb, List<DelegateType> delegateList, List<DelegateType> wrappedDelegatesCache)
        {
            for (int i = 0; i < allTypes.Count; i++)
            {
                Type dt = ToLuaSetting.DynamicList.Find((p) => { return allTypes[i].type == p; });

                if (dt == null && allTypes[i].nameSpace == nameSpace)
                {
                    string str = "\t\t" + allTypes[i].wrapName + "Wrap.Register(L);\r\n";
                    sb.Append(str);
                    allTypes.RemoveAt(i--);
                }
            }

            string funcName = null;

            for (int i = 0; i < delegateList.Count; i++)
            {
                DelegateType dt = delegateList[i];
                Type type = dt.type;
                string typeSpace = ToLuaExport.GetNameSpace(type, out funcName);

                if (typeSpace == nameSpace)
                {
                    funcName = ToLuaExport.ConvertToLibSign(funcName);
                    string abr = dt.abr;
                    abr = abr == null ? funcName : abr;
                    sb.AppendFormat("\t\tL.RegFunction(\"{0}\", {1});\r\n", abr, dt.name);
                    wrappedDelegatesCache.Add(dt);
                }
            }
        }

        static void GenPreLoadFunction(BindType bt, StringBuilder sb)
        {
            string funcName = "LuaOpen_" + bt.wrapName;

            sb.AppendLineEx("\r\n\t[MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]");
            sb.AppendFormat("\tstatic int {0}(IntPtr L)\r\n", funcName);
            sb.AppendLineEx("\t{");
            sb.AppendLineEx("\t\ttry");
            sb.AppendLineEx("\t\t{");
            sb.AppendLineEx("\t\t\tLuaState state = LuaState.Get(L);");
            sb.AppendFormat("\t\t\tstate.BeginPreModule(\"{0}\");\r\n", bt.nameSpace);
            sb.AppendFormat("\t\t\t{0}Wrap.Register(state);\r\n", bt.wrapName);
            sb.AppendFormat("\t\t\tint reference = state.GetMetaReference(typeof({0}));\r\n", bt.name);
            sb.AppendLineEx("\t\t\tstate.EndPreModule(L, reference);");
            sb.AppendLineEx("\t\t\treturn 1;");
            sb.AppendLineEx("\t\t}");
            sb.AppendLineEx("\t\tcatch(Exception e)");
            sb.AppendLineEx("\t\t{");
            sb.AppendLineEx("\t\t\treturn LuaDLL.toluaL_exception(L, e);");
            sb.AppendLineEx("\t\t}");
            sb.AppendLineEx("\t}");
        }

        static void GetAllDirs(string dir, List<string> list)
        {
            string[] dirs = Directory.GetDirectories(dir);
            list.AddRange(dirs);

            for (int i = 0; i < dirs.Length; i++)
            {
                GetAllDirs(dirs[i], list);
            }
        }

        static void CreateDefaultWrapFile(string path, string name)
        {
            StringBuilder sb = new StringBuilder();
            path = path + name + ".cs";
            sb.AppendLineEx("using System;");
            sb.AppendLineEx("using LuaInterface;");
            sb.AppendLineEx();
            sb.AppendLineEx("public static class " + name);
            sb.AppendLineEx("{");
            sb.AppendLineEx("\tpublic static void Register(LuaState L)");
            sb.AppendLineEx("\t{");
            sb.AppendLineEx("\t\tthrow new LuaException(\"Please click menu Lua/Gen BaseType Wrap first!\");");
            sb.AppendLineEx("\t}");
            sb.AppendLineEx("}");

            using (StreamWriter textWriter = new StreamWriter(path, false, Encoding.UTF8))
            {
                textWriter.Write(sb.ToString());
                textWriter.Flush();
                textWriter.Close();
            }
        }

        [MenuItem(Constants.MENU_SCRIPT_GEN_LUA_WRAPS)]
        static void GenLuaAll()
        {
            if (EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("Warning", "Please wait till compile done.", "OK");
                return;
            }
            GenLuaDelegates();
            AssetDatabase.Refresh();
            GenerateClassWraps();
            GenLuaBinder();
            AssetDatabase.Refresh();
            Debug.Log("Gen lua wraps success.");
            Helper.ShowToast("Gen lua wraps success.");
        }

        [MenuItem(Constants.MENU_SCRIPT_CLEAR_LUA_WRAPS)]
        static void ClearLuaWraps()
        {
            string[] files = Directory.GetFiles(Constants.LUA_ADAPTER_PATH, "*.cs", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                File.Delete(files[i]);
            }

            ToLuaExport.Clear();
            List<DelegateType> list = new List<DelegateType>();
            ToLuaExport.GenDelegates(list.ToArray());
            ToLuaExport.Clear();

            StringBuilder sb = new StringBuilder();
            sb.AppendLineEx("using System;");
            sb.AppendLineEx("using LuaInterface;");
            sb.AppendLineEx();
            sb.AppendLineEx("public static class LuaBinder");
            sb.AppendLineEx("{");
            sb.AppendLineEx("\tpublic static void Bind(LuaState L)");
            sb.AppendLineEx("\t{");
            sb.AppendLineEx("\t\tthrow new LuaException(\"Please generate LuaBinder files first!\");");
            sb.AppendLineEx("\t}");
            sb.AppendLineEx("}");

            string file = Constants.LUA_ADAPTER_PATH + "LuaBinder.cs";

            using (StreamWriter textWriter = new StreamWriter(file, false, Encoding.UTF8))
            {
                textWriter.Write(sb.ToString());
                textWriter.Flush();
                textWriter.Close();
            }

            AssetDatabase.Refresh();
            Debug.Log("Clear lua wraps success.");
            Helper.ShowToast("Clear lua wraps success.");
        }

        [MenuItem(Constants.MENU_SCRIPT_GEN_LUA_API)]
        static void GenAPI()
        {
            if (EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("Warning", "Please wait till compile done.", "OK");
                return;
            }
            ToLuaEmmy.GenAPI();
            AssetDatabase.Refresh();
        }
    }
}