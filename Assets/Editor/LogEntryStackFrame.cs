using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;

using UnityEngine;
using UnityEditor;

namespace ArtCom.Logging.Editor {

    [Serializable]
    public struct LogEntryStrackFrame
    {
        private static readonly Regex regexUnityStackFrameWithFile = new Regex(
            @"([\w\.\+\<\>]+)" + // Group 0: Full class name
            @"[\.\:]" + 
            @"(\w+)" +       // Group 1: Method name
            @"\s*" + 
            @"\((.*)\)" +    // Group 2: Method Parameters
            @"\s\(at\s" +
            @"(.+)" +        // Group 3: File path
            @"\:" + 
            @"([0-9]+)" +    // Group 4: Line number
            @"\)");
        private static readonly Regex regexUnityStackFrame = new Regex(
            @"([\w\.\+\<\>]+)" + // Group 0: Full class name
            @"[\.\:]" + 
            @"(\w+)" +       // Group 1: Method name
            @"\s*" + 
            @"\((.*)\)"      // Group 2: Method Parameters
            );
        private static readonly Regex regexUnityBuildMessage = new Regex(
            @"(.+)\s*\(([0-9]+),\s*[0-9]+\)\s*:.+");

        public static readonly LogEntryStrackFrame Empty = new LogEntryStrackFrame();
        public static readonly LogEntryStrackFrame Error = new LogEntryStrackFrame { _rawStackFrame = "Error" };

        [SerializeField] private string _rawStackFrame;
        [SerializeField] private string _declaringTypeName;
        [SerializeField] private string _methodName;
        [SerializeField] private int _methodSignatureHash;

        [SerializeField] private string _fileName;
        [SerializeField] private int _lineNumber;

        private MethodBase _method;
        private bool _methodResolveFailed;

        public MethodBase Method
        {
            get
            {
                if (_method == null && !_methodResolveFailed)
                {
                    _method = ResolveMethod();
                    _methodResolveFailed = (_method == null);
                }
                return _method;
            }
        }
        public string FileName
        {
            get { return _fileName; }
        }
        public int LineNumber
        {
            get { return _lineNumber; }
        }

        private MethodBase ResolveMethod()
        {
            if (string.IsNullOrEmpty(_declaringTypeName)) return null;
            if (string.IsNullOrEmpty(_methodName)) return null;

            try
            {
                Type declaringType = null;
                Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly assembly in loadedAssemblies)
                {
                    declaringType = assembly.GetType(_declaringTypeName);
                    if (declaringType != null)
                        break;
                }
                if (declaringType == null)
                    return null;
                
                MethodInfo[] declaredMethods = declaringType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (MethodBase method in declaredMethods)
                {
                    if (method.Name != _methodName) continue;
                    
                    int methodHash = GetMethodSignatureHash(method);
                    if (methodHash != _methodSignatureHash) continue;
                    
                    return method;
                }
            }
            catch (Exception) {}

            return null;
        }

        public override string ToString()
        {
            MethodBase resolvedMethod = Method;
            if (resolvedMethod != null) return LogFormat.MethodBase(resolvedMethod);
            if (!string.IsNullOrEmpty(_methodName) && !string.IsNullOrEmpty(_declaringTypeName)) return "Unresolved: " + _rawStackFrame;
            return _rawStackFrame ?? "null";
        }
        
        public static LogEntryStrackFrame FromUnityBuildMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return Empty;
            try
            {
                // Parsing "Assets/Editor/LogEditorWindow.cs(36,15): error CS1519: ..."
                MatchCollection matches = regexUnityBuildMessage.Matches(message);
                if (matches.Count == 0) return Empty;
                
                LogEntryStrackFrame result = new LogEntryStrackFrame();
                result._rawStackFrame = message.Replace("{", "{{").Replace("}", "}}");
                result._fileName = matches[0].Groups[1].Value;
                result._lineNumber = Convert.ToInt32(matches[0].Groups[2].Value);
                return result;
            }
            catch (Exception e)
            {
                LogEntryStrackFrame result = new LogEntryStrackFrame();
                result._rawStackFrame = "Error: " + e.GetType().Name;
                return result;
            }
        }
        public static LogEntryStrackFrame FromUnityStackFrame(string stackFrame)
        {
            if (string.IsNullOrEmpty(stackFrame)) return Empty;
            try
            {
                // Parsing "Namespace.Foo+Nested.Bar () (at Assets/Code/Editor.cs:298)"
                MatchCollection matches = regexUnityStackFrameWithFile.Matches(stackFrame);
                if (matches.Count == 0)
                {
                    // Parsing "Namespace.Foo+Nested.Bar ()"
                    matches = regexUnityStackFrame.Matches(stackFrame);
                }
                if (matches.Count == 0) return Empty;
                
                string methodParams = matches[0].Groups[3].Value;
                string[] methodParamToken = methodParams.Split(',');
                for (int i = 0; i < methodParamToken.Length; i++)
                {
                    methodParamToken[i] = methodParamToken[i].Split('`')[0].Trim();
                }
                
                LogEntryStrackFrame result = new LogEntryStrackFrame();
                result._rawStackFrame = stackFrame;
                result._declaringTypeName = matches[0].Groups[1].Value;
                result._methodName = matches[0].Groups[2].Value;
                if (matches[0].Groups.Count > 5)
                {
                    result._fileName = matches[0].Groups[4].Value;
                    result._lineNumber = Convert.ToInt32(matches[0].Groups[5].Value);
                }
                result._methodSignatureHash = GetMethodSignatureHash(methodParamToken);
                return result;
            }
            catch (Exception e)
            {
                LogEntryStrackFrame result = new LogEntryStrackFrame();
                result._rawStackFrame = "Error: " + e.GetType().Name;
                return result;
            }
        }
        public static LogEntryStrackFrame FromStackFrame(StackFrame stackFrame)
        {
            if (stackFrame == null) return Empty;

            try
            {
                LogEntryStrackFrame result = new LogEntryStrackFrame();
                result._rawStackFrame = LogFormat.StackFrame(stackFrame);
                result._fileName = stackFrame.GetFileName();
                result._lineNumber = stackFrame.GetFileLineNumber();

                MethodBase method = stackFrame.GetMethod();
                if (method != null)
                {
                    result._declaringTypeName = method.DeclaringType.FullName;
                    result._methodName = method.Name;
                    result._methodSignatureHash = GetMethodSignatureHash(method);
                }

                return result;
            }
            catch (Exception e)
            {
                LogEntryStrackFrame result = new LogEntryStrackFrame();
                result._rawStackFrame = "Error: " + e.GetType().Name;
                return result;
            }
        }

        private static int GetMethodSignatureHash(MethodBase method)
        {
            ParameterInfo[] paramInfo = method.GetParameters();
            string[] parameters = new string[paramInfo.Length];
            for (int i = 0; i < paramInfo.Length; i++)
            {
                parameters[i] = paramInfo[i].ParameterType.ToString().Split('`')[0] + " " + paramInfo[i].Name;
            }
            return GetMethodSignatureHash(parameters);
        }
        private static int GetMethodSignatureHash(string[] parameters)
        {
            int result = 17;
            unchecked
            {
                for (int i = 1; i < parameters.Length; i++)
                {
                    result = result * 23 + parameters[i].GetHashCode();
                }
            }
            return result;
        }
    }

}