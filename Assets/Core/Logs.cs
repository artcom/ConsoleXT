using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ArtCom.Logging
{
    public static class Logs
    {
        private static readonly string DefaultLogFileNameFormat = "log {0:" + LogFormat.TimeStampFormatISO8601 + "}.txt";
        private static readonly string DefaultLogFileDirectory = "Logs";

        private static StreamWriter        _textFileLogWriter = null;
        private static TextWriterLogOutput _textFileLogOutput = null;
        private static Log                 _systemLog         = null;
        private static Log                 _defaultLog        = null;
        private static Log                 _unityLog          = null;
        private static List<Log>           _customGlobalLogs  = new List<Log>();
        private static List<ILogOutput>    _globalLogOutput   = new List<ILogOutput>();
        
        public static Log System
        {
            get { return _systemLog; }
        }
        public static Log Default
        {
            get { return _defaultLog; }
        }
        public static Log Unity
        {
            get { return _unityLog; }
        }
        public static bool ForwardToUnity
        {
            get { return _globalLogOutput.OfType<UnityDebugLogOutput>().Any(); }
            set
            {
                UnityDebugLogOutput forwarder = _globalLogOutput.OfType<UnityDebugLogOutput>().FirstOrDefault();
                if (!value && forwarder != null) 
                    RemoveGlobalOutput(forwarder);
                else if (value && forwarder == null)
                    AddGlobalOutput(new UnityDebugLogOutput());
            }
        }
        public static IEnumerable<ILogOutput> GlobalLogOutput
        {
            get { return _globalLogOutput; }
        }
        public static IEnumerable<Log> All
        {
            get
            {
                yield return _systemLog;
                yield return _defaultLog;
                yield return _unityLog;
                foreach (Log custom in _customGlobalLogs)
                {
                    yield return custom;
                }
            }
        }

        public static Log Get<T>() where T : CustomLogInfo, new()
        {
            return StaticLogHolder<T>.Log;
        }

        public static void AddGlobalOutput(ILogOutput output)
        {
            if (_globalLogOutput.Contains(output))
                return;

            _globalLogOutput.Add(output);
            foreach (Log log in All)
            {
                log.AddOutput(output);
            }
        }
        public static void RemoveGlobalOutput(ILogOutput output)
        {
            _globalLogOutput.Remove(output);
            foreach (Log log in All)
            {
                log.RemoveOutput(output);
            }
        }


        static Logs()
        {
            AppDomain.CurrentDomain.ProcessExit += AppDomain_ProcessExit;
            AppDomain.CurrentDomain.DomainUnload += AppDomain_DomainUnload;
            AppDomain.CurrentDomain.AssemblyLoad += AppDomain_AssemblyLoad;

            // Normally, we'd use this hook to log all exceptions that end up uncaught, but
            // in testing, this never triggered. It is likely that Unity simply catches all
            // exceptions already, so this would be redundant with regular unity error log
            // forwarding, which is already in place.
            // AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;
            
            _systemLog  = new Log("System" );
            _defaultLog = new Log("Default");
            _unityLog   = new Log("Unity"  );

            // Install a forwarder from Unity to our custom logs
            UnityLogIntegration.Init();

            // Add a global log output that forwards to the regular Unity log
            try
            {
                UnityDebugLogOutput forwardToUnity = new UnityDebugLogOutput();
                Logs.AddGlobalOutput(forwardToUnity);
            }
            catch (Exception e)
            {
                Logs.System.WriteWarning("Rerouting Logs to Unity Debug Logs failed: {0}", LogFormat.Exception(e));
            }
        }
        private static void Shutdown()
        {
            // Note: Shutdown should NOT do anything besides shutting down.
            // No logs, no anything. Unity doesn't like to be bothered during
            // shutdown.

            AppDomain.CurrentDomain.AssemblyLoad -= AppDomain_AssemblyLoad;
            AppDomain.CurrentDomain.ProcessExit -= AppDomain_ProcessExit;
            
            UnityLogIntegration.Shutdown();
            ShutdownTextLog();
        }
        private static void AppDomain_DomainUnload(object sender, EventArgs e)
        {
            // The unity editor will unload the application domain when recompiling.
            // After compiling, the new code will run into the static initializers
            // again, since it's a new AppDomain.
            Shutdown();
        }
        private static void AppDomain_ProcessExit(object sender, EventArgs e)
        {
            Shutdown();
        }
        private static void AppDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            Logs.System.Write(
                "Assembly Loaded: {0} in AppDomain {1}", 
                LogFormat.Assembly(args.LoadedAssembly),
                LogFormat.AppDomain(AppDomain.CurrentDomain));
        }

        /// <summary>
        /// Initializes a global logfile using the specified target directory and file name
        /// using default settings. If not specified otherwise, a Logs directory is used and
        /// the log file's name is derived from the current <see cref="DateTime"/>.
        /// </summary>
        public static TextWriterLogOutput InitGlobalLogFile(string directory = null, string fileName = null, TextLogOutputConfig config = null)
        {
            // In case someone calls this multiple times, shut down the old one
            ShutdownTextLog();

            try
            {
                // Open a writable stream to the desired, default or fallback log file location
                string loggingPath;
                if (!TryCreateLogStream(directory, fileName, out _textFileLogWriter, out loggingPath))
                {
                    Logs.Default.WriteWarning("Text Logfile unavailable, because no logging location was accessible.");
                    return null;
                }

                config.LoggingPath = loggingPath;
                // Create, configure and register a log output using the log stream
                _textFileLogOutput = new TextWriterLogOutput(_textFileLogWriter, config);
                Logs.AddGlobalOutput(_textFileLogOutput);
                return _textFileLogOutput;
            }
            catch (Exception e)
            {
                Logs.Default.WriteWarning("Failed to create text logfile: {0}", LogFormat.Exception(e));
                return null;
            }
        }
        private static void ShutdownTextLog()
        {
            if (_textFileLogOutput != null) {
                Logs.RemoveGlobalOutput(_textFileLogOutput);
                _textFileLogOutput = null;
            }
            if (_textFileLogWriter != null) {
                _textFileLogWriter.Dispose();
                _textFileLogWriter = null;
            }
        }
        private static bool TryCreateLogStream(string preferredDir, string preferredName, out StreamWriter writer, out string loggingPath)
        {
            if (preferredDir == null) 
                preferredDir = DefaultLogFileDirectory;
            if (preferredName == null) 
                preferredName = string.Format(DefaultLogFileNameFormat, DateTime.UtcNow).Replace(':', '-');

            // Create a logfile at the desired path
            string logFilePath = Path.Combine(preferredDir, preferredName);
            if(TryCreateLogStream(logFilePath, out writer)) {
                loggingPath = logFilePath;
                return true;
            }

            // If that fails and it was a non-default path, try the default logfile path
            string defaultLogFilePath = Path.Combine(DefaultLogFileDirectory, preferredName);
            if(defaultLogFilePath != logFilePath &&
               TryCreateLogStream(defaultLogFilePath, out writer)) {
                loggingPath = defaultLogFilePath;
                return true;
            }

            // If that didn't work - for example due to security / permission issues - fall back
            // to a logfile path in Unity's persistent data path. This should be a writable location
            // in any case.
            string unityDataDir;
            try
            {
                // Unity paths are using forward slashes. We'll use GetFullPath to get a
                // normalized version so we can safely combine it without mixing path separators.
                unityDataDir = UnityEngine.Application.persistentDataPath;
                unityDataDir = Path.GetFullPath(unityDataDir);
            }
            catch (Exception e)
            {
                Logs.Default.WriteWarning("Unable to retrieve Unity persistent data path: {0}", LogFormat.Exception(e));
                loggingPath = null;
                return false;
            }

            string altLogFilePath = Path.Combine(Path.Combine(unityDataDir, DefaultLogFileDirectory), preferredName);
            if(TryCreateLogStream(altLogFilePath, out writer)) {
                loggingPath = altLogFilePath;
                return true;
            }

            // If all failed, we probably can't create any file at all for some reason.
            loggingPath = null;
            return false;
        }
        private static bool TryCreateLogStream(string path, out StreamWriter writer)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                writer = new StreamWriter(path);
                writer.AutoFlush = true;

                Logs.Default.Write("Created log stream at path '{0}'.", path);
                return true;
            }
            catch (Exception e)
            {
                Logs.Default.WriteWarning("Failed to create log stream at path '{0}': {1}", path, LogFormat.Exception(e));
                writer = null;
                return false;
            }
        }
        
        private static class StaticLogHolder<T> where T : CustomLogInfo, new()
        {
            public static Log Log;
            
            static StaticLogHolder() {
                try {
                    T initializer = new T();
                    Log = new Log(
                        initializer.Name, 
                        initializer.Prefix, 
                        Logs.GlobalLogOutput.ToArray());
                    initializer.InitLog(Log);
                    Logs._customGlobalLogs.Add(Log);
                }
                catch (Exception e) {
                    Log = new Log(string.Empty, string.Empty);
                    Logs.Default.WriteError(
                        "Error initializing custom Log '{0}': {1}", 
                        LogFormat.Type(typeof(T)), 
                        LogFormat.Exception(e));
                }
            }
        }
    }

}