using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;

namespace ArtCom.Logging
{
    public static class UnityLogIntegration
    {
        private static bool            _initialized        = false;
        private static object          _unitySyncLock      = new object();
        private static Thread          _unityMainThread    = null;
        private static HashSet<string> _skipReadUnityLogs  = new HashSet<string>();
        private static HashSet<string> _skipWriteUnityLogs = new HashSet<string>();
        private static Log             _rerouteUnityLogTo  = null;

        public static bool IsUnityMainThread
        {
            get { return Thread.CurrentThread == _unityMainThread; }
        }
        public static int CurrentUnityFrame
        {
            get {
                if (IsUnityMainThread)
                    return UnityEngine.Time.frameCount;
                else
                    return -1;
            }
        }
        public static Log RerouteUnityLogTo
        {
            get { return _rerouteUnityLogTo; }
            set { _rerouteUnityLogTo = value; }
        }

        internal static void Init()
        {
            if (_initialized) return;
            _initialized = true;
            _unityMainThread = Thread.CurrentThread;
            _rerouteUnityLogTo = Logs.Unity;
            UnityEngine.Application.logMessageReceived += UnityApplication_LogMessageReceived;
        }
        internal static void Shutdown()
        {
            if (!_initialized) return;
            _initialized = false;
            UnityEngine.Application.logMessageReceived -= UnityApplication_LogMessageReceived;
        }
        private static void UnityApplication_LogMessageReceived(string condition, string stackTrace, UnityEngine.LogType type)
        {
            lock (_unitySyncLock) {
                if (_skipReadUnityLogs.Remove(condition)) return;
                if (_rerouteUnityLogTo == null) return;
                _skipWriteUnityLogs.Add(condition);
            }

            // Put additional information we get from unity into a context object, which we
            // forward through our custom logging. That way, we don't lose any information.
            UnityLogContext unityContext = new UnityLogContext
            {
                StackTrace = stackTrace
            };

            // Forward the Unity-log to our custom log and provide the stack trace as
            // a context object. We'll parse this on the editor side to provide code info.
            switch (type) {
            default:
            case UnityEngine.LogType.Log:
                _rerouteUnityLogTo.Write(condition, unityContext);
                break;
            case UnityEngine.LogType.Warning:
                _rerouteUnityLogTo.WriteWarning(condition, unityContext);
                break;
            case UnityEngine.LogType.Assert:
            case UnityEngine.LogType.Exception:
                _rerouteUnityLogTo.WriteError(
                    condition + Environment.NewLine + 
                    "Stacktrace:" + Environment.NewLine + 
                    "{1}", 
                    unityContext, stackTrace);
                break;
            case UnityEngine.LogType.Error:
                _rerouteUnityLogTo.WriteError(condition, unityContext);
                break;
            }
        }

        public static void ForwardToUnity(LogEntry entry, object context)
        {
            if (entry.Message == null) throw new ArgumentException("entry");

            lock (_unitySyncLock) {
                if (_skipWriteUnityLogs.Remove(entry.Message)) return;
                _skipReadUnityLogs.Add(entry.Message);
            }

            switch (entry.Type)
            {
            case LogMessageType.Debug:
            case LogMessageType.Message:
                UnityEngine.Debug.Log(entry.Message, context as UnityEngine.Object);
                break;
            case LogMessageType.Warning:
                UnityEngine.Debug.LogWarning(entry.Message, context as UnityEngine.Object);
                break;
            case LogMessageType.Error:
            case LogMessageType.Fatal:
                UnityEngine.Debug.LogError(entry.Message, context as UnityEngine.Object);
                break;
            }
        }
    }

}