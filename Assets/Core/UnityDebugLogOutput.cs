using System;
using System.IO;
using System.Threading;

namespace ArtCom.Logging {

    public class UnityDebugLogOutput : ILogOutput
    {
        private int _indent = 0;
        private bool _allowColors = true;
        private bool _isEditor = false;
        private bool _isEditorProSkin = false;

        public bool AllowColors {
            get { return _allowColors; }
            set { _allowColors = value; }
        }

        public void Write(Log source, LogEntry entry, object context)
        {
            // Note that now that we've transitioned towards a Managed Plugin, we can't
            // rely on preprocessor directives anymore. We're only compiling once, here.
            // All else has to be decided at runtime. The old preprocessor-driven editor
            // enhancements are only here for reference. As soon as we found out how to
            // safely access editor classes only in an editor context, they can be replaced
            // with runtime checks.

            // Are we in the editor?
            if (UnityLogIntegration.IsUnityMainThread)
            {
                _isEditor = UnityEngine.Application.isEditor;

                // Determine if we're using the pro skin, so we can adapt our rich 
                // text colors to fit the color scheme.
                #if UNITY_EDITOR
                _isEditorProSkin = UnityEditor.EditorGUIUtility.isProSkin;
                #endif
            }

            // If we're in the editor, don't clutter the regular console with system logs.
            if (_isEditor && source == Logs.System) return;

            string indentString;
            string[] lines = FormatMultiLineText(
                entry, 
                (source.FormattedPrefix ?? ""),
                _indent,
                _isEditorProSkin,
                _allowColors,
                out indentString);
            string fullText = string.Join(Environment.NewLine, lines);

#if UNITY_EDITOR
            // If we only have a single line, use the remaining line for displaying
            // the local stack frame or context object, if it's a unity object.
            if (lines.Length == 1) 
                fullText = AppendEditorContextInfo(fullText, indentString, context);

            // Add a separator to avoid confusion when viewing the full log
            // in the Unity Log Console and provide a nicer overview.
            fullText += Environment.NewLine;
#endif

            entry.Message = fullText;
            UnityLogIntegration.ForwardToUnity(entry, context);
        }
        public void PushIndent()
        {
            Interlocked.Increment(ref _indent);
        }
        public void PopIndent()
        {
            Interlocked.Decrement(ref _indent);
        }

        private static string[] FormatMultiLineText(LogEntry entry, string prefix, int indent, bool proSkin, bool allowColors, out string indentString)
        {
            // If we're in the editor, determine the consoles background color
            // so we can adapt to its color scheme
            bool darkBackground = true;
            bool useColors = false;
            bool useRichText = false;
            int indentSize = 2;
            #if UNITY_EDITOR
            darkBackground = proSkin;
            useColors = allowColors;
            useRichText = true;
            indentSize = 4;
            #endif

            indentString = (indentSize > 0) ? 
                new string(' ', indent * indentSize) : 
                    string.Empty;
            
            string textColor = darkBackground ? "#FFFFFFCC" : "#000000CC";
            string headColor;
            switch (entry.Type) {
            default:
            case LogMessageType.Debug:
                headColor = darkBackground ? "#ADE6C9FF" : "#008080FF";
                textColor = darkBackground ? "#FFFFFFAA" : "#000000AA";
                break;
            case LogMessageType.Message:
                headColor = darkBackground ? "#ADD8E6FF" : "#000080FF";
                break;
            case LogMessageType.Warning:
                headColor = darkBackground ? "#FFCC66FF" : "#804000FF";
                break;
            case LogMessageType.Error:
                headColor = darkBackground ? "#FF6666FF" : "#800000FF";
                break;
            case LogMessageType.Fatal:
                headColor = darkBackground ? "#FF3333FF" : "#CC0000FF";
                textColor = darkBackground ? "#FFFFFFFF" : "#000000FF";
                break;
            }
            
            // Apply indentation and coloring to individual lines
            string[] lines = entry.Message.Split(
                new[] { '\n', '\r', '\0' }, 
            StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                if (i == 0)
                {
                    if (!useRichText) {
                        lines[i] = 
                            indentString + 
                                prefix + " " + lines[i];
                    }
                    else if (useColors) {
                        lines[i] = 
                            indentString + 
                                "<b><color=" + headColor + ">" + prefix + "</color></b>" + " " + 
                                "<color=" + textColor + ">" + lines[i] + "</color>";
                    }
                    else {
                        lines[i] = 
                            indentString + 
                                "<b>" + prefix + "</b>" + " " + lines[i];
                    }
                }
                else
                {
                    lines[i] = 
                        indentString + 
                            lines[i];
                }
            }
            return lines;
        }
        private static string AppendEditorContextInfo(string message, string indentString, object context)
        {
            string contextInfo = RetrieveEditorContextInfo(context);
            
            // Only display the first line, should there be more than one
            if (!string.IsNullOrEmpty(contextInfo)) {
                string[] contextLines = contextInfo.Split(
                    new[] { '\n', '\r', '\0' }, 
                StringSplitOptions.RemoveEmptyEntries);
                if (contextLines.Length > 0) {
                    message += Environment.NewLine + indentString + contextLines[0];
                }
            }

            return message;
        }
        private static string RetrieveEditorContextInfo(object context)
        {
            // Do a stack trace in order to find one. Don't do this outside
            // the editor. It's too expensive and might not be supported on
            // some platforms
            //
            // We can skip two frames, since one is this one and the next
            // is definitely a Log method, since FindContext is private.
            System.Diagnostics.StackFrame stackFrame = null;
            try {
                System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(2);
                System.Diagnostics.StackFrame[] frames = trace.GetFrames();
                for (int i = 0; i < frames.Length; i++) {
                    System.Reflection.MethodBase method = frames[i].GetMethod();
                    Type type = method.DeclaringType;
                    bool isLoggingType = 
                        !string.IsNullOrEmpty(type.Namespace) &&
                            type.Namespace.StartsWith(typeof(Log).Namespace);
                    
                    // Select the first stack frame that is not part of the
                    // logging code, which is defined as everything in the
                    // same namespace as the Log class
                    if (!isLoggingType)
                    {
                        stackFrame = frames[i];
                        break;
                    }
                }
            }
            catch (Exception) {}
            
            // Select what to display based on the kind of context provided
            if (stackFrame != null)
                return LogFormat.StackFrame(stackFrame);
            else if (context is UnityEngine.Object)
                return LogFormat.UnityObject(context as UnityEngine.Object);
            else
                return null;
        }
    }

}