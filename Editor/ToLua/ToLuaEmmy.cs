using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using LuaInterface;
using UnityEngine;
using EP.U3D.EDITOR.BASE;

namespace LuaInterface.Editor
{
    public class ToLuaEmmy
    {
        private static StringBuilder nsBuilder;

        private static void GenNameSpace(HashSet<string> nsDic, Dictionary<string, string> classAlias)
        {
            nsBuilder = new StringBuilder();
            nsBuilder.AppendFormat("--- using namespace");
            var nsList = new List<string>(nsDic);
            nsList.Sort();
            for (var index = 0; index < nsList.Count; index++)
            {
                var ns = nsList[index];
                nsBuilder.AppendFormat("\n---@class {0}", ns);
                nsBuilder.AppendFormat("\n{0} = {{}}", ns);
            }

            nsBuilder.AppendFormat("\n\n---using class");
            foreach (var kvp in classAlias)
            {
                nsBuilder.AppendFormat("\n{0} = {1}", kvp.Key, kvp.Value);
            }

            // [20211203]: 保存至a.lua文件，确保首先解析namespace内容
            File.WriteAllBytes(Constants.LUA_EMMYAPI_PATH + "a.lua", Encoding.GetEncoding("UTF-8").GetBytes(nsBuilder.ToString()));
        }

        public static void GenAPI()
        {
            Filter<Type> baseFilter = new GeneralFilter<Type>(ToLuaSetting.BaseType);
            Filter<Type> dropFilter = new GeneralFilter<Type>(ToLuaSetting.DropType);
            if (Helper.HasDirectory(Constants.LUA_EMMYAPI_PATH)) Helper.DeleteDirectory(Constants.LUA_EMMYAPI_PATH);
            Helper.CreateDirectory(Constants.LUA_EMMYAPI_PATH);
            var collection = new BindTypeCollection(ToLuaSetting.CustomTypeList);
            var bindTypes = collection.CollectBindType(baseFilter, dropFilter);
            var nsSet = new HashSet<string>();
            foreach (var bindType in bindTypes)
            {
                var generator = new LuaAPIGenerator();
                generator.Gen(bindType);
                if (bindType.type.ReflectedType == null && !string.IsNullOrEmpty(bindType.type.Namespace))
                {
                    var subNs = bindType.type.Namespace.Split('.');
                    for (var i = 0; i < subNs.Length; i++) nsSet.Add(string.Join(".", subNs, 0, i + 1));
                }
            }
            // proto
            List<string> files = new List<string>();
            Helper.CollectFiles(Constants.PROTO_SRC_PATH, files);
            foreach (var file in files)
            {
                if (file.EndsWith(".proto"))
                {
                    string name = Path.GetFileNameWithoutExtension(file).ToUpper();
                    nsSet.Add(name);
                    Directory.CreateDirectory(Constants.LUA_EMMYAPI_PATH + name);
                    EmmyProtoBufExport.Gen(file, name, Constants.LUA_EMMYAPI_PATH + name);
                }
            }
            GenNameSpace(nsSet, ToLuaSetting.ClassAlias);
            var zipName = Path.GetDirectoryName(Constants.LUA_EMMYAPI_PATH) + ".zip";
            if (File.Exists(zipName)) File.Delete(zipName);

            Helper.Zip(Helper.NormalizePath(Constants.LUA_EMMYAPI_PATH), Helper.NormalizePath(zipName));

            Debug.Log($"[FILE@{zipName}] Gen lua api success.");
            Helper.ShowToast("Gen lua api success.");
        }
    }

    internal abstract class Filter<T>
    {
        public delegate void EachProcessor(T value);

        public abstract bool Contains(T type);

        public Filter<T> Exclude(params Filter<T>[] others)
        {
            var v = this;
            for (var i = 0; i < others.Length; i++) v = new ExcludeFilter<T>(v, others[i]);

            return v;
        }

        public Filter<T> And(params Filter<T>[] others)
        {
            var v = this;
            for (var i = 0; i < others.Length; i++) v = new AndFilter<T>(v, others[i]);

            return v;
        }

        public Filter<T> Or(params Filter<T>[] others)
        {
            var v = this;
            for (var i = 0; i < others.Length; i++) v = new OrFilter<T>(v, others[i]);

            return v;
        }

        public virtual void Each(EachProcessor processor)
        {
        }
    }

    internal class ExcludeFilter<T> : Filter<T>
    {
        private readonly Filter<T> _baseFilter;
        private readonly Filter<T> _excludeFilter;

        public ExcludeFilter(Filter<T> baseFilter, Filter<T> excludeFilter)
        {
            _baseFilter = baseFilter;
            _excludeFilter = excludeFilter;
        }

        public override bool Contains(T type)
        {
            return _baseFilter.Contains(type) && !_excludeFilter.Contains(type);
        }

        public override void Each(EachProcessor processor)
        {
            _baseFilter.Each(v =>
            {
                if (!_excludeFilter.Contains(v)) processor(v);
            });
        }
    }

    internal class OrFilter<T> : Filter<T>
    {
        private readonly Filter<T> _baseFilter;
        private readonly Filter<T> _orFilter;

        public OrFilter(Filter<T> baseFilter, Filter<T> orFilter)
        {
            _baseFilter = baseFilter;
            _orFilter = orFilter;
        }

        public override bool Contains(T type)
        {
            return _baseFilter.Contains(type) || _orFilter.Contains(type);
        }

        public override void Each(EachProcessor processor)
        {
            _baseFilter.Each(processor);
            _orFilter.Each(processor);
        }
    }

    internal class AndFilter<T> : Filter<T>
    {
        private readonly Filter<T> _andFilter;
        private readonly Filter<T> _baseFilter;

        public AndFilter(Filter<T> baseFilter, Filter<T> andFilter)
        {
            _baseFilter = baseFilter;
            _andFilter = andFilter;
        }

        public override bool Contains(T type)
        {
            return _baseFilter.Contains(type) && _andFilter.Contains(type);
        }

        public override void Each(EachProcessor processor)
        {
            _baseFilter.Each(v =>
            {
                if (_andFilter.Contains(v)) processor(v);
            });
        }
    }

    internal class GeneralFilter<T> : Filter<T>
    {
        private readonly ICollection<T> _arr;

        public GeneralFilter(ICollection<T> arr)
        {
            _arr = arr;
        }

        public override bool Contains(T type)
        {
            return _arr.Contains(type);
        }

        public override void Each(EachProcessor processor)
        {
            foreach (var x1 in _arr) processor(x1);
        }
    }

    internal class BindTypeCollection : Filter<ToLuaMenu.BindType>
    {
        private readonly Queue<ToLuaMenu.BindType> _typeQueue;
        private List<ToLuaMenu.BindType> _typeList;

        public BindTypeCollection(ToLuaMenu.BindType[] typeArr)
        {
            _typeQueue = new Queue<ToLuaMenu.BindType>(typeArr);
        }

        public ToLuaMenu.BindType[] CollectBindType(Filter<Type> baseFilter, Filter<Type> excludeFilter)
        {
            var processed = new List<Type>();
            excludeFilter = excludeFilter.Or(new GeneralFilter<Type>(processed));
            _typeList = new List<ToLuaMenu.BindType>();

            baseFilter.Each(t => _typeQueue.Enqueue(new ToLuaMenu.BindType(t)));
            while (_typeQueue.Count > 0)
            {
                var bind = _typeQueue.Dequeue();
                if (!excludeFilter.Contains(bind.type))
                {
                    _typeList.Add(bind);
                    processed.Add(bind.type);
                    CreateBaseBindType(bind.baseType, excludeFilter);
                }
            }

            return _typeList.ToArray();
        }

        private void CreateBaseBindType(Type baseType, Filter<Type> excludeFilter)
        {
            if (baseType != null && !excludeFilter.Contains(baseType))
            {
                var bind = new ToLuaMenu.BindType(baseType);
                _typeQueue.Enqueue(bind);
                CreateBaseBindType(bind.baseType, excludeFilter);
            }
        }

        public override bool Contains(ToLuaMenu.BindType type)
        {
            return false;
        }

        public override void Each(EachProcessor processor)
        {
            foreach (var bindType in _typeList) processor(bindType);
        }
    }

    internal class OpMethodFilter : Filter<MethodInfo>
    {
        public override bool Contains(MethodInfo mi)
        {
            return mi.Name.StartsWith("Op_") || mi.Name.StartsWith("add_") || mi.Name.StartsWith("remove_");
        }
    }

    /// <summary>
    ///     Get/Set 方法过滤
    /// </summary>
    internal class GetSetMethodFilter : Filter<MethodInfo>
    {
        public override bool Contains(MethodInfo type)
        {
            return type.Name.StartsWith("get_") || type.Name.StartsWith("set_");
        }
    }

    /// <summary>
    ///     废弃过滤
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class ObsoleteFilter<T> : Filter<T> where T : MemberInfo
    {
        public override bool Contains(T mb)
        {
            var attrs = mb.GetCustomAttributes(true);

            for (var j = 0; j < attrs.Length; j++)
            {
                var t = attrs[j].GetType();

                if (t == typeof(ObsoleteAttribute) ||
                    t == typeof(NoToLuaAttribute) ||
                    t == typeof(MonoPInvokeCallbackAttribute) ||
                    t.Name == "MonoNotSupportedAttribute" ||
                    t.Name == "MonoTODOAttribute")
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    ///     黑名单过滤
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class BlackListMemberNameFilter<T> : Filter<T> where T : MemberInfo
    {
        public override bool Contains(T mi)
        {
            if (ToLuaSetting.MemberFilter.Contains(mi.Name))
                return true;
            var type = mi.ReflectedType;
            if (type != null)
                return ToLuaSetting.MemberFilter.Contains(type.Name + "." + mi.Name);
            return false;
        }
    }

    /// <summary>
    ///     泛型方法过滤
    /// </summary>
    internal class GenericMethodFilter : Filter<MethodInfo>
    {
        public override bool Contains(MethodInfo mi)
        {
            return mi.IsGenericMethod;
        }
    }

    /// <summary>
    ///     扩展方法过滤
    /// </summary>
    internal class ExtendMethodFilter : Filter<MethodInfo>
    {
        private readonly Type _type;

        public ExtendMethodFilter(Type type)
        {
            _type = type;
        }

        public override bool Contains(MethodInfo mi)
        {
            var infos = mi.GetParameters();
            if (infos.Length == 0) return false;

            var pi = infos[0];
            return pi.ParameterType == _type;
        }
    }

    internal class MethodData
    {
        public bool IsExtend;
        public MethodInfo Method;
    }

    internal class MethodDataSet
    {
        public List<MethodData> MethodList = new List<MethodData>();

        public void Add(MethodInfo mi, bool isExtend)
        {
            var md = new MethodData { IsExtend = isExtend, Method = mi };
            MethodList.Add(md);
        }
    }

    internal abstract class CodeGenerator
    {
        public static HashSet<string> KeyWords = new HashSet<string>
    {
        "end"
    };

        private readonly Filter<MethodInfo> methodExcludeFilter = new ObsoleteFilter<MethodInfo>()
            .Or(new OpMethodFilter())
            .Or(new BlackListMemberNameFilter<MethodInfo>())
            .Or(new GenericMethodFilter())
            .Or(new GetSetMethodFilter());

        protected ToLuaMenu.BindType _bindType;

        public virtual void Gen(ToLuaMenu.BindType bt)
        {
            _bindType = bt;

            GenMethods();
            GenProperties();
        }

        protected void GenMethods()
        {
            var flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Instance |
                        BindingFlags.DeclaredOnly;
            var allMethods = new Dictionary<string, MethodDataSet>();
            Action<MethodInfo, bool> methodCollector = (mi, isExtend) =>
            {
                MethodDataSet set;
                if (allMethods.TryGetValue(mi.Name, out set))
                {
                    set.Add(mi, isExtend);
                }
                else
                {
                    set = new MethodDataSet();
                    set.Add(mi, isExtend);
                    allMethods.Add(mi.Name, set);
                }
            };

            //extend
            if (_bindType.extendList != null)
                foreach (var type in _bindType.extendList)
                {
                    var methodInfos = type.GetMethods(flags);
                    var extFilter = new GeneralFilter<MethodInfo>(methodInfos)
                        .Exclude(methodExcludeFilter)
                        .And(new ExtendMethodFilter(_bindType.type));
                    extFilter.Each(mi => { methodCollector(mi, true); });
                }

            //base
            var methods = _bindType.type.GetMethods(flags);
            var filter = new GeneralFilter<MethodInfo>(methods);
            var methodFilter = filter.Exclude(methodExcludeFilter);
            methodFilter.Each(mi => { methodCollector(mi, false); });
            foreach (var pair in allMethods) GenMethod(_bindType.libName, pair.Value);
        }

        protected void GenProperties()
        {
            var type = _bindType.type;
            //props
            var propList = type.GetProperties(BindingFlags.GetProperty | BindingFlags.SetProperty |
                                              BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase |
                                              BindingFlags.DeclaredOnly | BindingFlags.Static);
            var propFilter = new GeneralFilter<PropertyInfo>(propList)
                .Exclude(new BlackListMemberNameFilter<PropertyInfo>())
                .Exclude(new ObsoleteFilter<PropertyInfo>());
            propFilter.Each(GenProperty);

            //fields
            var fields = type.GetFields(BindingFlags.GetField | BindingFlags.SetField | BindingFlags.Instance |
                                        BindingFlags.Public | BindingFlags.Static);
            var fieldFilter = new GeneralFilter<FieldInfo>(fields)
                .Exclude(new BlackListMemberNameFilter<FieldInfo>())
                .Exclude(new ObsoleteFilter<FieldInfo>());
            fieldFilter.Each(GenField);

            //events
            var events = type.GetEvents(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public |
                                        BindingFlags.Static);
            var evtFilter = new GeneralFilter<EventInfo>(events)
                .Exclude(new BlackListMemberNameFilter<EventInfo>())
                .Exclude(new ObsoleteFilter<EventInfo>());
            evtFilter.Each(GenEvent);
        }

        protected abstract void GenProperty(PropertyInfo pi);

        protected abstract void GenEvent(EventInfo ei);

        protected abstract void GenField(FieldInfo fi);

        protected abstract void GenMethod(string name, MethodDataSet methodDataSet);
    }

    internal static class TypeExtension
    {
        public static string GetTypeStr(this Type type)
        {
            if (typeof(ICollection).IsAssignableFrom(type)
                && !typeof(Array).IsAssignableFrom(type))
                return "table";

            if (type.IsGenericType)
            {
                var typeName = LuaMisc.GetTypeName(type);
                var pos = typeName.IndexOf("<", StringComparison.Ordinal);
                if (pos > 0)
                    return typeName.Substring(0, pos);
            }

            if (type == typeof(string))
            {
                return typeof(string).ToString();
            }
            if (type == typeof(object))
            {
                return typeof(object).ToString();
            }

            return LuaMisc.GetTypeName(type);
        }
    }

    internal class LuaAPIGenerator : CodeGenerator
    {
        private StringBuilder _baseSB;
        private StringBuilder _methodBuilder;
        private StringBuilder _propBuilder;

        public override void Gen(ToLuaMenu.BindType bt)
        {
            _baseSB = new StringBuilder();
            if (bt.baseType != null)
            {
                var baseType = bt.baseType.GetTypeStr();
                if (baseType == "object")
                {
                    baseType = "System.Object";
                }
                _baseSB.AppendFormat("---@class {0} : {1}\n", bt.name, baseType);
            }
            else
                _baseSB.AppendFormat("---@class {0}\n", bt.name);

            _propBuilder = new StringBuilder();
            _methodBuilder = new StringBuilder();
            base.Gen(bt);

            _baseSB.Append(_propBuilder);
            if (!string.IsNullOrEmpty(bt.type.Namespace) || bt.type.ReflectedType != null) _baseSB.AppendFormat("local ");
            _baseSB.AppendFormat("{0} = {{}}", bt.libName);
            _baseSB.Append(_methodBuilder);

            var folder = "Docs/Scripts/LuaAPI";

            if (!string.IsNullOrEmpty(bt.type.Namespace) || bt.type.ReflectedType != null)
            {
                if (!string.IsNullOrEmpty(bt.type.Namespace)) folder = string.Format("{0}/{1}", folder, bt.type.Namespace.Replace('.', '/'));
                // [20211203]: 修复泛型解析错误
                if (string.IsNullOrEmpty(bt.nameSpace))
                {
                    _baseSB.AppendFormat("\n\n{0} = {1}", bt.libName, bt.libName);
                }
                else
                {
                    _baseSB.AppendFormat("\n\n{0}.{1} = {2}", bt.nameSpace, bt.libName, bt.libName);
                }
            }

            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var fileType = bt.type;
            while (fileType.ReflectedType != null) fileType = fileType.ReflectedType;

            var fileName = string.Format("{0}/{1}.lua", folder, fileType.Name);
            FileStream fs;
            if (File.Exists(fileName))
            {
                fs = new FileStream(fileName, FileMode.Append, FileAccess.Write);
                _baseSB.Insert(0, "\n\n");
            }
            else
            {
                fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            }

            var sw = new StreamWriter(fs);
            sw.Write(_baseSB);
            sw.Flush();
            sw.Dispose();
        }

        protected override void GenProperty(PropertyInfo pi)
        {
            _propBuilder.AppendFormat("---@field {0} {1}\n", pi.Name, pi.PropertyType.GetTypeStr());
        }

        protected override void GenEvent(EventInfo ei)
        {
            _propBuilder.AppendFormat("---@field {0} {1}\n", ei.Name, ei.EventHandlerType.GetTypeStr());
        }

        protected override void GenField(FieldInfo fi)
        {
            _propBuilder.AppendFormat("---@field {0} {1}\n", fi.Name, fi.FieldType.GetTypeStr());
        }

        protected override void GenMethod(string name, MethodDataSet methodDataSet)
        {
            _methodBuilder.AppendFormat("\n\n");
            //overload
            if (methodDataSet.MethodList.Count > 1)
                for (var j = 1; j < methodDataSet.MethodList.Count; j++)
                {
                    var data = methodDataSet.MethodList[j];
                    var mi = data.Method;
                    var parameters = mi.GetParameters();
                    var startIdx = data.IsExtend ? 1 : 0;
                    var paramNames = new string[parameters.Length - startIdx];
                    for (var i = startIdx; i < parameters.Length; i++)
                    {
                        var pi = parameters[i];
                        paramNames[i - startIdx] = string.Format("{0}:{1}", pi.Name, pi.ParameterType.GetTypeStr());
                    }

                    _methodBuilder.AppendFormat("---@overload fun({0}):{1}\n", string.Join(", ", paramNames),
                        mi.ReturnType.GetTypeStr());
                }

            //main
            {
                var data = methodDataSet.MethodList[0];
                var mi = data.Method;
                var parameters = mi.GetParameters();
                var startIdx = data.IsExtend ? 1 : 0;
                var paramNames = new string[parameters.Length - startIdx];
                for (var i = startIdx; i < parameters.Length; i++)
                {
                    var pi = parameters[i];
                    var paramName = pi.Name;
                    if (KeyWords.Contains(paramName)) paramName = "_" + paramName;
                    _methodBuilder.AppendFormat("---@param {0} {1}\n", paramName, pi.ParameterType.GetTypeStr());
                    paramNames[i - startIdx] = paramName;
                }

                var returnType = mi.ReturnType;
                if (typeof(void) != returnType) _methodBuilder.AppendFormat("---@return {0}\n", returnType.GetTypeStr());

                var c = mi.IsStatic && !data.IsExtend ? "." : ":";
                _methodBuilder.AppendFormat("function {0}{1}{2}({3}) end", name, c, mi.Name, string.Join(", ", paramNames));
            }
        }
    }

    public class ProBufAPIFiled
    {
        public readonly string specify;
        public readonly string type;
        public readonly string name;
        public readonly string comment;

        public ProBufAPIFiled(string specify, string type, string name, string comment)
        {
            this.specify = specify;
            this.type = type;
            this.name = name;
            this.comment = comment;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(comment) ?
                string.Format("---@field {0} {1} {2}", name, type, specify) :
                string.Format("---@field {0} {1} {2} - {3}", name, type, specify, comment);
        }
    }

    public class ProBufAPI
    {
        public string ns;
        public string name;
        public string comment;
        public List<ProBufAPIFiled> fields = new List<ProBufAPIFiled>();
        public ProBufAPI(string ns) { this.ns = ns; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.IsNullOrEmpty(comment) ? string.Format("---@class {0}.{1}", ns, name) : string.Format("---@class {0}.{1} {2}", ns, name, comment));
            foreach (var item in fields) sb.AppendLine(item.ToString());
            sb.AppendLine(string.Format("local {0} = {1}", name, "{}"));
            sb.AppendLine(string.Format("{0}.{1} = {2}", ns, name, name));
            return sb.ToString();
        }
    }

    public class EmmyProtoBufExport
    {
        private readonly List<ProBufAPI> messages = new List<ProBufAPI>();

        private EmmyProtoBufExport(string ns, string pbstr)
        {
            messages.Clear();
            pbstr = pbstr.Replace("\t", " "); // 替换制表符
            pbstr = Regex.Replace(pbstr, @"\n[\s| ]*\r", "");  // 去空白行
            string[] lines = pbstr.Split('\n');
            bool parse = false;
            string comment = "";
            ProBufAPI message = new ProBufAPI(ns);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                line = line.Trim();
                if (line.StartsWith("message "))
                {
                    if (parse) message = new ProBufAPI(ns);
                    string name = line.Replace("message ", "");
                    name = name.Replace(" ", "");
                    name = name.Replace("{", "");
                    message.name = name;
                    message.comment = comment;
                    comment = "";
                    parse = true;
                    messages.Add(message);
                    continue;
                }
                if (line.StartsWith("//"))
                {
                    if (!string.IsNullOrEmpty(comment)) comment += " ";
                    comment += line.Replace("//", "").Trim();
                    continue;
                }
                if (line.StartsWith("{") || line.StartsWith("}") || parse == false)
                {
                    continue;
                }
                string[] strs = line.Trim().Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                if (strs.Length >= 3)
                {
                    string specify = strs[0];
                    string type = specify == "repeated" ? strs[1] + "[]" : strs[1];
                    string name = strs[2];
                    if (name.Contains("=")) name = name.Split('=')[0];
                    strs = line.Split(new string[] { "//" }, StringSplitOptions.RemoveEmptyEntries);
                    if (strs.Length >= 2)
                    {
                        comment = strs[1].Trim();
                    }
                    ProBufAPIFiled field = new ProBufAPIFiled(specify, type, name, comment);
                    message.fields.Add(field);
                    comment = "";
                }
            }
        }

        public static void Gen(string file, string name, string dst)
        {
            var proBufStr = File.ReadAllText(file, Encoding.UTF8);
            var export = new EmmyProtoBufExport(name, proBufStr);
            var fs = new FileStream(string.Format("{0}/{1}.lua", dst, name), FileMode.Create);
            var utf8WithoutBom = new UTF8Encoding(false);
            var sw = new StreamWriter(fs, utf8WithoutBom);
            sw.Write(export.ToString());
            sw.Flush();
            sw.Close();
            fs.Close();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int i = 0, imax = messages.Count; i < imax; i++) sb.AppendLine(messages[i].ToString());

            return sb.ToString();
        }

        private static string TrimUseless(string value)
        {
            //去注释
            value = Regex.Replace(value, "//.+", "");
            //替换制表符
            value = value.Replace("\t", " ");
            //去空白行
            value = Regex.Replace(value, @"\n[\s| ]*\r", "");
            return value;
        }
    }
}