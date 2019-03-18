using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Reflection;

using UnityEngine;
using UnityEditor;

namespace ArtCom.Logging.Editor {

    [Serializable]
    public struct LogEntryStrackTrace
    {
        public static readonly LogEntryStrackTrace Empty = new LogEntryStrackTrace { _frames = new LogEntryStrackFrame[0] };
        public static readonly LogEntryStrackTrace Error = new LogEntryStrackTrace { _frames = new[] { LogEntryStrackFrame.Error } };

        [SerializeField] private LogEntryStrackFrame[] _frames;

        public bool IsAvailable
        {
            get { return _frames != null && _frames.Length > 0; }
        }
        public LogEntryStrackFrame[] Frames
        {
            get { return _frames ?? Empty.Frames; }
        }

        public static LogEntryStrackTrace FromUnityStackTrace(string stackTrace, string message)
        {
            try
            {
                // Do we have an actual stack trace from Unity? Parse it.
                if (!string.IsNullOrEmpty(stackTrace))
                {
                    string[] lines = stackTrace.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    // Skip stack frames within Unity's own Debug class
                    int skipLines = 0;
                    while (skipLines < lines.Length && lines[skipLines].StartsWith("UnityEngine.Debug:", StringComparison.InvariantCultureIgnoreCase))
                        skipLines++;
                    
                    LogEntryStrackTrace result = new LogEntryStrackTrace();
                    if (skipLines < lines.Length)
                    {
                        result._frames = new LogEntryStrackFrame[lines.Length - skipLines];
                        for (int i = skipLines; i < lines.Length; i++)
                        {
                            result._frames[i - skipLines] = LogEntryStrackFrame.FromUnityStackFrame(lines[i]);
                        }
                    }
                    
                    return result;
                }
                // Otherwise, try to parse the message in order to obtain a stack frame.
                // This will trigger for Unity reporting build errors.
                else if (!string.IsNullOrEmpty(message))
                {
                    LogEntryStrackTrace result = new LogEntryStrackTrace();
                    result._frames = new LogEntryStrackFrame[1];
                    result._frames[0] = LogEntryStrackFrame.FromUnityBuildMessage(message);
                    return result;
                }
                else
                {
                    return Empty;
                }
            }
            catch (Exception)
            {
                return Error;
            }
        }
        public static LogEntryStrackTrace FromStackTrace(StackTrace stackTrace)
        {
            if (stackTrace == null) return Empty;
            if (stackTrace.FrameCount == 0) return Empty;

            try
            {
                LogEntryStrackTrace result = new LogEntryStrackTrace();
                StackFrame[] frames = stackTrace.GetFrames();
                
                // Skip all frames within the logging framework itself
                int skipFrames = 0;
                for (int i = 0; i < frames.Length; i++)
                {
                    MethodBase method = frames[i].GetMethod();
                    Type type = method.DeclaringType;
                    bool isLoggingType = 
                        !string.IsNullOrEmpty(type.Namespace) &&
                            type.Namespace.StartsWith(typeof(Log).Namespace);
                    
                    // Select the first stack frame that is not part of the
                    // logging code, which is defined as everything in the
                    // same namespace as the Log class
                    if (isLoggingType)
                        skipFrames++;
                    else
                        break;
                }
                
                if (skipFrames < frames.Length)
                {
                    result._frames = new LogEntryStrackFrame[frames.Length - skipFrames];
                    for (int i = skipFrames; i < frames.Length; i++)
                    {
                        result._frames[i - skipFrames] = LogEntryStrackFrame.FromStackFrame(frames[i]);
                    }
                }

                return result;
            }
            catch (Exception)
            {
                return Error;
            }
        }
    }

}