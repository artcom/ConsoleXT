using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

namespace ArtCom.Logging
{
    public class Log
    {
        private List<ILogOutput> _outputs         = null;
        private string           _name            = null;
        private string           _prefix          = null;
        private string           _formattedPrefix = null;
        private object           _outputLock      = new object();


        public string Name
        {
            get { return _name; }
        }
        public string Prefix
        {
            get { return _prefix; }
        }
        public string FormattedPrefix
        {
            get { return _formattedPrefix; }
        }
        public IEnumerable<ILogOutput> Outputs
        {
            get { return _outputs; }
        }

        
        public Log(string name, params ILogOutput[] outputs) : this(name, name, outputs) {}
        public Log(string name, string prefix, params ILogOutput[] outputs)
        {
            _name = name;
            _prefix = prefix;
            _formattedPrefix = LogFormat.LogPrefix(prefix);
            _outputs = new List<ILogOutput>(outputs);
        }

        public void AddOutput(ILogOutput writer)
        {
            lock (_outputLock) {
                _outputs.Add(writer);
            }
        }
        public void RemoveOutput(ILogOutput writer)
        {
            lock (_outputLock) {
                _outputs.Remove(writer);
            }
        }

        public void PushIndent()
        {
            lock (_outputLock) {
                for (int i = 0; i < _outputs.Count; i++)
                {
                    ILogOutput log = _outputs[i];
                    try { log.PushIndent(); }
                    catch (Exception) {}
                }
            }
        }
        public void PopIndent()
        {
            lock (_outputLock) {
                for (int i = 0; i < _outputs.Count; i++)
                {
                    ILogOutput log = _outputs[i];
                    try { log.PopIndent(); }
                    catch (Exception) {}
                }
            }
        }

        /// <summary>
        /// Writes a debug message for development-only purposes.
        /// </summary>
        public void WriteDebug(string format, params object[] obj)
        {
            this.Write(LogMessageType.Debug, format, obj);
        }
        /// <summary>
        /// Writes a regular log message.
        /// </summary>
        public void Write(string format, params object[] obj)
        {
            this.Write(LogMessageType.Message, format, obj);
        }
        /// <summary>
        /// Writes a log warning that indicates something might be wrong.
        /// </summary>
        public void WriteWarning(string format, params object[] obj)
        {
            this.Write(LogMessageType.Warning, format, obj);
        }
        /// <summary>
        /// Writes a log error that indicates a recoverable failure.
        /// </summary>
        public void WriteError(string format, params object[] obj)
        {
            this.Write(LogMessageType.Error, format, obj);
        }
        /// <summary>
        /// Writes a fatal log error that indicates a non-recoverable failure.
        /// </summary>
        public void WriteFatal(string format, params object[] obj)
        {
            this.Write(LogMessageType.Fatal, format, obj);
        }
        private void Write(LogMessageType type, string format, object[] obj)
        {
            this.AddEntry(
                new LogEntry(type, FormatMessage(format ?? string.Empty, obj)),
                FindContext(obj));
        }

        private void AddEntry(LogEntry entry, object context)
        {
            // Check whether the message contains null characters. If it does, crop it, because it's probably broken.
            int nullCharIndex = entry.Message.IndexOf('\0');
            if (nullCharIndex != -1)
            {
                entry.Message = entry.Message
                    .Substring(0, Math.Min(nullCharIndex, 50)) + 
                    " | Contains '\0' and is likely broken.";
            }
            
            // Forward the message to all outputs
            lock (_outputLock) {
                for (int i = 0; i < _outputs.Count; i++)
                {
                    ILogOutput log = _outputs[i];
                    try
                    {
                        log.Write(this, entry, context);
                    }
                    catch (Exception)
                    {
                        // Don't allow log outputs to throw unhandled exceptions,
                        // because they would result in another log - and more exceptions.
                    }
                }
            }
        }

        private static string FormatMessage(string format, object[] obj)
        {
            if (obj == null || obj.Length == 0) return format;
            string msg;
            try
            {
                // Format unity objects, because their .ToString implementation isn't great
                // and if users do it manually, we lose the context object (because then it's a string).
                //
                // While we're at it, let's auto-transform some other objects with less-than-ideal
                // .ToString() implementation as well.
                object[] preFormatObj = new object[obj.Length];
                for (int i = 0; i < preFormatObj.Length; i++)
                {
                    string formattedObj;
                    if (LogFormat.TryFormat(obj[i], out formattedObj))
                        preFormatObj[i] = formattedObj;
                    else
                        preFormatObj[i] = obj[i];
                }

                // Format the actual message
                msg = string.Format(System.Globalization.CultureInfo.InvariantCulture, format, preFormatObj);
            }
            catch (Exception e)
            {
                // Don't allow log message formatting to throw unhandled exceptions,
                // because they would result in another log - and probably more exceptions.
                
                // Instead, embed format, arguments and the exception in the resulting
                // log message, so the user can retrieve all necessary information for
                // fixing his log call.
                msg = format + Environment.NewLine;
                if (obj != null)
                {
                    try
                    {
                        msg += obj.ToString(", ") + Environment.NewLine;
                    }
                    catch (Exception)
                    {
                        msg += "(Error in ToString call)" + Environment.NewLine;
                    }
                }
                msg += LogFormat.Exception(e);
            }
            return msg;
        }
        private static object FindContext(object[] obj)
        {
            if (obj != null)
            {
                // Check if there is a unity object that can serve as context
                // mentioned explicitly in the formatting arguments.
                for (int i = 0; i < obj.Length; i++)
                {
                    if (obj[i] is UnityEngine.Object)
                        return obj[i];
                }

                // Otherwise, just pick the first argument as context.
                // Note that forwarding and parsing unity stack traces
                // to the editor module relies on this to pass through
                // non-unity objects, or at least UnityLogContext objects.
                if (obj.Length > 0)
                    return obj[0];
            }

            // No context available
            return null;
        }
    }

}