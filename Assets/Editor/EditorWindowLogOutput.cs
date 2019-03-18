using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

using UnityEngine;
using UnityEditor;

namespace ArtCom.Logging.Editor {

    [Serializable]
    public class EditorWindowLogOutput : ILogOutput
    {
        [SerializeField] private List<EditorWindowLogEntry> _logEntries = new List<EditorWindowLogEntry>();

        private object _lock = new object();
        private bool _logsReceivedSinceLastPoll = false;
        private List<EditorWindowLogEntry> _polledEntries = new List<EditorWindowLogEntry>();
        private int _indent = 0;
        private List<string> _knownChannels = null;
        private Dictionary<LogMessageType,int> _typeCount = null;

        public event EventHandler LogReceived = null;


        public IList<EditorWindowLogEntry> LogEntries
        {
            get
            {
                lock (_lock)
                {
                    return _polledEntries;
                }
            }
        }
        public int ErrorCount
        {
            get 
            {
                lock (_lock)
                {
                    EnsureTypeCount();
                    return _typeCount[LogMessageType.Error] + _typeCount[LogMessageType.Fatal];
                }
            }
        }
        public int WarningCount
        {
            get
            {
                lock (_lock)
                {
                    EnsureTypeCount();
                    return _typeCount[LogMessageType.Warning];
                }
            }
        }
        public int MessageCount
        {
            get
            {
                lock (_lock)
                {
                    EnsureTypeCount();
                    return _typeCount[LogMessageType.Message] + _typeCount[LogMessageType.Debug];
                }
            }
        }
        public IEnumerable<string> KnownChannels
        {
            get
            {
                lock (_lock)
                {
                    EnsureKnownChannels();
                    return _knownChannels;
                }
            }
        }


        public void Write(Log source, LogEntry entry, object context)
        {
            lock (_lock)
            {
                EnsureKnownChannels();
                EnsureTypeCount();

                if (!_knownChannels.Contains(source.Name))
                    _knownChannels.Add(source.Name);

                // If this log entry was actually written using Unity Debug.Log API
                // and merely forwarded to us for integration purposes, the context
                // will be a UnityLogContext structure. We can use this to extact
                // the callstack that was provided.
                UnityLogContext unityContext = (context is UnityLogContext) ? (UnityLogContext)context : default(UnityLogContext);

                // Store the log inside a wrapper struct, which contains all information
                // that can be used for diagnostic purposes, not just the actual log that
                // is written.
                EditorWindowLogEntry logEntry = new EditorWindowLogEntry {
                    Log = entry,
                    SingleLineMessage = entry.Message.Split('\n')[0],
                    Indent = _indent,
                    SourceName = source.Name,
                    Context = new LogEntryContext(context),
                    CallStack = (unityContext.StackTrace != null) ? 
                    LogEntryStrackTrace.FromUnityStackTrace(unityContext.StackTrace, entry.Message) : 
                    LogEntryStrackTrace.FromStackTrace(new StackTrace(2, true))
                };
                _logEntries.Add(logEntry);
                _typeCount[entry.Type]++;
                _logsReceivedSinceLastPoll = true;
            }
        }
        public void PushIndent()
        {
            lock (_lock)
            {
                _indent++;
            }
        }
        public void PopIndent()
        {
            lock (_lock)
            {
                _indent--;
            }
        }
        public void ResetIndent()
        {
            lock (_lock)
            {
                _indent = 0;
            }
        }
        public void Clear()
        {
            lock (_lock)
            {
                _polledEntries.Clear();
                _logEntries.Clear();

                // After clearing, re-initialize our by-messagetype-counter
                InitTypeCount();
            }
        }
        public bool PollMessages()
        {
            lock (_lock)
            {
                if (_logsReceivedSinceLastPoll)
                {
                    for (int i = _polledEntries.Count; i < _logEntries.Count; i++)
                        _polledEntries.Add(_logEntries[i]);
                    
                    if (LogReceived != null)
                        LogReceived(this, EventArgs.Empty);
                    
                    _logsReceivedSinceLastPoll = false;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private void EnsureKnownChannels()
        {
            if (_knownChannels == null)
                InitKnownChannels();
        }
        private void EnsureTypeCount()
        {
            if (_typeCount == null)
                InitTypeCount();
        }
        private void InitKnownChannels()
        {
            if (_knownChannels == null)
                _knownChannels = new List<string>();
            else
                _knownChannels.Clear();

            for (int i = 0; i < _logEntries.Count; i++)
            {
                if (!_knownChannels.Contains(_logEntries[i].SourceName))
                    _knownChannels.Add(_logEntries[i].SourceName);
            }
        }
        private void InitTypeCount()
        {
            if (_typeCount == null)
                _typeCount = new Dictionary<LogMessageType, int>();
            else
                _typeCount.Clear();

            foreach (LogMessageType messageType in Enum.GetValues(typeof(LogMessageType)))
            {
                _typeCount[messageType] = 0;
            }
            for (int i = 0; i < _logEntries.Count; i++)
            {
                _typeCount[_logEntries[i].Log.Type]++;
            }
        }
    }

}