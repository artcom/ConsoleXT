using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace ArtCom.Logging
{
    public static class LogFormat
    {
        /// <summary>
        /// DateTime format specifier using the ISO 8601 date/time format.
        /// </summary>
        public static readonly string TimeStampFormatISO8601 = @"yyyy-MM-ddTHH-mm-ss";
        /// <summary>
        /// The DateTime format specifier that is used as long as nothing else is specified.
        /// </summary>
        public static readonly string TimeStampFormatDefault = @"HH\:mm\:ss.fff";
        /// <summary>
        /// The frame stamp format specifier that is used as long as nothing else is specified.
        /// </summary>
        public static readonly string FrameStampFormatDefault = @"{0,7}";

        public static string LogPrefix(string prefix)
        {
            return string.Format("[{0}]", prefix).PadRight(7 + 2);
        }
        public static string LogMessageTypeShort(LogMessageType type)
        {
            switch (type)
            {
            case LogMessageType.Debug  : return "Dbg";
            default:
            case LogMessageType.Message: return "Msg";
            case LogMessageType.Warning: return "Wrn";
            case LogMessageType.Error  : return "ERR";
            case LogMessageType.Fatal  : return "FAT";
            }
        }
        public static string LogMessageTypeStandard(LogMessageType type)
        {
            switch (type)
            {
            case LogMessageType.Debug  : return "DEBUG";
            default:
            case LogMessageType.Message: return " INFO";
            case LogMessageType.Warning: return " WARN";
            case LogMessageType.Error  : return "ERROR";
            case LogMessageType.Fatal  : return "FATAL";
            }
        }

        public static string ToString<T>(this IEnumerable<T> enumerable, string separator)
        {
            StringBuilder sb = new StringBuilder();
            foreach (T item in enumerable)
            {
                sb.Append(item != null ? item.ToString() : "null");
                sb.Append(separator);
            }
            return sb.ToString(0, Math.Max(0, sb.Length - separator.Length));  // Remove at the end is faster
        }
        public static string ToString<T>(this IEnumerable<T> enumerable, Func<T, string> toString, string separator)
        {
            StringBuilder sb = new StringBuilder();
            foreach (T item in enumerable)
            {
                sb.Append(toString(item));
                sb.Append(separator);
            }
            return sb.ToString(0, Math.Max(0, sb.Length - separator.Length));  // Remove at the end is faster
        }

        public static bool TryFormat(object obj, out string formattedString)
        {
            if (obj is UnityEngine.Object)
                formattedString = LogFormat.UnityObject(obj as UnityEngine.Object);
            else if (obj is Exception)
                formattedString = LogFormat.Exception(obj as Exception);
            else if (obj is MemberInfo)
                formattedString = LogFormat.MemberInfo(obj as MemberInfo);
            else if (obj is Assembly)
                formattedString = LogFormat.Assembly(obj as Assembly);
            else if (obj is StackFrame)
                formattedString = LogFormat.StackFrame(obj as StackFrame);
            else
            {
                formattedString = null;
                return false;
            }

            return true;
        }

        public static string HumanFriendlyId(int unreadableId)
        {
            return string.Format("{0} | '{1}'", unreadableId, HumanFriendlyRandomString.Create(new Random(unreadableId)));
        }
        public static string AppDomain(AppDomain appDomain)
        {
            return string.Format("{0} ({1})", appDomain.FriendlyName, HumanFriendlyId(appDomain.GetHashCode()));
        }
        public static string Assembly(Assembly assembly, bool includePath = false)
        {
            string shortName = assembly.FullName.Split(',')[0];
            string fullUri;
            Version version;
            try
            {
                AssemblyName name = assembly.GetName();
                version = name.Version;
                fullUri = name.CodeBase;
            }
            catch (Exception)
            {
                version = null;
                fullUri = null;
            }
            if (includePath)
                return string.Format("{0} {1} at {2}", shortName, version, fullUri);
            else
                return string.Format("{0} {1}", shortName, version);
        }
        public static string Type(Type type)
        {
            return GetTypeCSCodeName(type, true);
        }
        public static string MethodInfo(MethodInfo info, bool includeDeclaringType = true)
        {
            string declTypeName = Type(info.DeclaringType);
            string returnTypeName = Type(info.ReturnType);
            string[] paramNames = info.GetParameters().Select(p => Type(p.ParameterType)).ToArray();
            string[] genArgNames = info.GetGenericArguments().Select(a => Type(a)).ToArray();
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture, 
                "{4} {0}{1}{3}({2})",
                includeDeclaringType ? declTypeName + "." : "",
                info.Name,
                paramNames.ToString(", "),
                genArgNames.Length > 0 ? "<" + genArgNames.ToString(", ") + ">" : "",
                returnTypeName);
        }
        public static string MethodBase(MethodBase info, bool includeDeclaringType = true)
        {
            if (info is MethodInfo)
                return MethodInfo(info as MethodInfo);
            else if (info is ConstructorInfo)
                return ConstructorInfo(info as ConstructorInfo);
            else if (info != null)
                return info.ToString();
            else
                return "null";
        }
        public static string ConstructorInfo(ConstructorInfo info, bool includeDeclaringType = true)
        {
            string declTypeName = Type(info.DeclaringType);
            string[] paramNames = info.GetParameters().Select(p => Type(p.ParameterType)).ToArray();
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture, 
                "{0}{1}({2})",
                includeDeclaringType ? declTypeName + "." : "",
                info.DeclaringType.Name,
                paramNames.ToString(", "));
        }
        public static string PropertyInfo(PropertyInfo info, bool includeDeclaringType = true)
        {
            string declTypeName = Type(info.DeclaringType);
            string propTypeName = Type(info.PropertyType);
            string[] paramNames = info.GetIndexParameters().Select(p => Type(p.ParameterType)).ToArray();
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture, 
                "{0} {1}{2}{3}",
                propTypeName,
                includeDeclaringType ? declTypeName + "." : "",
                info.Name,
                paramNames.Any() ? "[" + paramNames.ToString(", ") + "]" : "");
        }
        public static string FieldInfo(FieldInfo info, bool includeDeclaringType = true)
        {
            string declTypeName = Type(info.DeclaringType);
            string fieldTypeName = Type(info.FieldType);
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture, 
                "{0} {1}{2}",
                fieldTypeName,
                includeDeclaringType ? declTypeName + "." : "",
                info.Name);
        }
        public static string EventInfo(EventInfo info, bool includeDeclaringType = true)
        {
            string declTypeName = Type(info.DeclaringType);
            string fieldTypeName = Type(info.EventHandlerType);
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture, 
                "{0} {1}{2}",
                fieldTypeName,
                includeDeclaringType ? declTypeName + "." : "",
                info.Name);
        }
        public static string MemberInfo(MemberInfo info, bool includeDeclaringType = true)
        {
            if (info is MethodInfo)
                return MethodInfo(info as MethodInfo, includeDeclaringType);
            else if (info is ConstructorInfo)
                return ConstructorInfo(info as ConstructorInfo, includeDeclaringType);
            else if (info is PropertyInfo)
                return PropertyInfo(info as PropertyInfo, includeDeclaringType);
            else if (info is FieldInfo)
                return FieldInfo(info as FieldInfo, includeDeclaringType);
            else if (info is EventInfo)
                return EventInfo(info as EventInfo, includeDeclaringType);
            else if (info is Type)
                return Type(info as Type);
            else if (info != null)
                return info.ToString();
            else
                return "null";
        }
        public static string StackFrame(StackFrame frame)
        {
            return MethodBase(frame.GetMethod(), true);
        }

        public static string Exception(Exception e, bool callStack = true)
        {
            if (e == null) return null;
            
            string eName = Type(e.GetType());
            
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture, 
                "{0}: {1}{3}CallStack:{3}{2}",
                eName,
                e.Message,
                e.StackTrace,
                Environment.NewLine);
        }

        public static string GameObject(UnityEngine.GameObject obj, bool fullHierarchy = true)
        {
            // Unity overrides the equality operator of its objects, so
            // that they return true, if the object is destroyed. However,
            // the actual reference isn't null - it just reports to be
            // equal to null.
            //
            // We can use this to determine whether an object is destroyed,
            // and distinguish this from simply having a null reference.
            //

            bool isNull = object.ReferenceEquals(obj, null);
            bool isDestroyed = (!isNull && obj == null);

            if (isNull)
                return "null";
            else if (isDestroyed)
                return "destroyed";

            string objName;
            if (fullHierarchy) {
                // Determine the object's full name in the scene graph
                StringBuilder fullNameBuilder = new StringBuilder();
                UnityEngine.Transform current = obj.transform;
                while (current != null) {
                    if (fullNameBuilder.Length > 0)
                        fullNameBuilder.Insert(0, '/');
                    fullNameBuilder.Insert(0, current.name);
                    current = current.parent;
                }
                objName = fullNameBuilder.ToString();
            }
            else {
                objName = obj.name;
            }

            return objName;
        }
        public static string Component(UnityEngine.Component cmp)
        {
            // Unity overrides the equality operator of its objects, so
            // that they return true, if the object is destroyed. However,
            // the actual reference isn't null - it just reports to be
            // equal to null.
            //
            // We can use this to determine whether an object is destroyed,
            // and distinguish this from simply having a null reference.
            //
            
            bool isNull = object.ReferenceEquals(cmp, null);
            bool isDestroyed = (!isNull && cmp == null);
            
            if (isNull)
                return "null";
            else if (isDestroyed)
                return "destroyed";

            // Determine the index of this Component in the GameObject
            UnityEngine.Component[] components = cmp.gameObject.GetComponents<UnityEngine.Component>();
            int index = Array.IndexOf(components, cmp);
            bool multiple = Array.LastIndexOf(components, cmp) != index;

            // Determine the name of the parent object
            string objName = GameObject(cmp.gameObject);

            // Determine the name of the Component
            string cmpName;
            if (multiple)
                cmpName = Type(cmp.GetType()) + " #" + index + " in " + objName;
            else
                cmpName = Type(cmp.GetType()) + " in " + objName;
            
            return cmpName;
        }
        public static string UnityObject(UnityEngine.Object obj)
        {
            // First, try to use a more specific format, if one applies
            if (obj is UnityEngine.GameObject)
                return GameObject(obj as UnityEngine.GameObject);
            else if (obj is UnityEngine.Component)
                return Component(obj as UnityEngine.Component);

            // Unity overrides the equality operator of its objects, so
            // that they return true, if the object is destroyed. However,
            // the actual reference isn't null - it just reports to be
            // equal to null.
            //
            // We can use this to determine whether an object is destroyed,
            // and distinguish this from simply having a null reference.
            //
            
            bool isNull = object.ReferenceEquals(obj, null);
            bool isDestroyed = (!isNull && obj == null);
            
            if (isNull)
                return "null";
            else if (isDestroyed)
                return "destroyed";
            else
                return obj.ToString();
        }


        private static string GetTypeCSCodeName(Type type, bool shortName = false)
        {
            StringBuilder typeStr = new StringBuilder();
            
            if (type.IsGenericParameter)
            {
                return type.Name;
            }
            if (type.IsArray)
            {
                typeStr.Append(GetTypeCSCodeName(type.GetElementType(), shortName));
                typeStr.Append('[');
                typeStr.Append(',', type.GetArrayRank() - 1);
                typeStr.Append(']');
            }
            else
            {
                Type[] genArgs = type.IsGenericType ? type.GetGenericArguments() : null;
                
                if (type.IsNested)
                {
                    Type declType = type.DeclaringType;
                    
                    if (declType.IsGenericTypeDefinition)
                    {
                        Array.Resize(ref genArgs, declType.GetGenericArguments().Length);
                        declType = declType.MakeGenericType(genArgs);
                        genArgs = type.GetGenericArguments().Skip(genArgs.Length).ToArray();
                    }
                    string parentName = GetTypeCSCodeName(declType, shortName);
                    
                    string[] nestedNameToken = shortName ? type.Name.Split('+') : type.FullName.Split('+');
                    string nestedName = nestedNameToken[nestedNameToken.Length - 1];
                    
                    int genTypeSepIndex = nestedName.IndexOf("[[", StringComparison.Ordinal);
                    if (genTypeSepIndex != -1) nestedName = nestedName.Substring(0, genTypeSepIndex);
                    genTypeSepIndex = nestedName.IndexOf('`');
                    if (genTypeSepIndex != -1) nestedName = nestedName.Substring(0, genTypeSepIndex);
                    
                    typeStr.Append(parentName);
                    typeStr.Append('.');
                    typeStr.Append(nestedName);
                }
                else
                {
                    if (shortName)
                        typeStr.Append(type.Name.Split(new[] {'`'}, StringSplitOptions.RemoveEmptyEntries)[0].Replace('+', '.'));
                    else
                        typeStr.Append(type.FullName.Split(new[] {'`'}, StringSplitOptions.RemoveEmptyEntries)[0].Replace('+', '.'));
                }
                
                if (genArgs != null && genArgs.Length > 0)
                {
                    if (type.IsGenericTypeDefinition)
                    {
                        typeStr.Append('<');
                        typeStr.Append(',', genArgs.Length - 1);
                        typeStr.Append('>');
                    }
                    else if (type.IsGenericType)
                    {
                        typeStr.Append('<');
                        for (int i = 0; i < genArgs.Length; i++)
                        {
                            typeStr.Append(GetTypeCSCodeName(genArgs[i], shortName));
                            if (i < genArgs.Length - 1)
                                typeStr.Append(',');
                        }
                        typeStr.Append('>');
                    }
                }
            }
            
            return typeStr.Replace('+', '.').ToString();
        }
    }

}