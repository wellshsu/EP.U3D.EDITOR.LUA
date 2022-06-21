using System;
using System.Collections.Generic;

namespace LuaInterface.Editor
{
    public static class ToLuaSetting
    {
        public static ToLuaMenu.BindType BT(Type t)
        {
            return new ToLuaMenu.BindType(t);
        }

        public static DelegateType DT(Type t)
        {
            return new DelegateType(t);
        }

        //导出时强制做为静态类的类型(注意customTypeList 还要添加这个类型才能导出)
        //unity 有些类作为sealed class, 其实完全等价于静态类
        public static List<Type> StaticClassTypes = new List<Type>();

        //附加导出委托类型(在导出委托时, customTypeList 中牵扯的委托类型都会导出， 无需写在这里)
        public static DelegateType[] CustomDelegateList = { };

        //在这里添加你要导出注册到lua的类型列表
        public static ToLuaMenu.BindType[] CustomTypeList = { };

        public static List<Type> DynamicList = new List<Type>();

        //重载函数，相同参数个数，相同位置out参数匹配出问题时, 需要强制匹配解决
        //使用方法参见例子14
        public static List<Type> OutList = new List<Type>() { };

        //ngui优化，下面的类没有派生类，可以作为sealed class
        public static List<Type> SealedList = new List<Type>();

        //不需要导出或者无法导出的类型
        public static List<Type> DropType = new List<Type>();

        //可以导出的内部支持类型
        public static List<Type> BaseType = new List<Type>();

        public static List<string> MemberFilter = new List<string>();

        //类型别名，用于生成api快速访问
        public static Dictionary<string, string> ClassAlias = new Dictionary<string, string>();
    }
}