using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ArtCom.Logging {

    public class TextWriterLogOutput : ILogOutput
    {
        private TextWriter          _target     = null;
        private TextLogOutputConfig _config     = null;
        private int                 _indent     = 0;
        private object              _writerLock = new object();
        private LogRotator          _logRotator = null;

        public TextWriter Target
        {
            get { return _target; }
        }
        public int Indent
        {
            get { return _indent; }
        }
        public TextLogOutputConfig Config
        {
            get { return _config; }
        }
        
        public TextWriterLogOutput(TextWriter target, TextLogOutputConfig config)
        {
            _target = target;
            _config = config ?? new TextLogOutputConfig();
            if(_config.LogRotate) {
                _logRotator = new LogRotator(config);
            }
        }

        public virtual void Write(Log source, LogEntry entry, object context)
        {
            string[] lines = entry.Message.Split(
                new[] { '\n', '\r', '\0' }, 
                StringSplitOptions.RemoveEmptyEntries);

            lock (_writerLock)
            {
                int totalPrefixLength = 0;
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < lines.Length; i++)
                {
                    builder.Length = 0;
                    if (i == 0)
                    {
                        if (_config.WriteTimeStamps)
                        {
                            builder.Append(entry.TimeStamp.ToString(_config.TimeStampFormat));
                            builder.Append(' ');
                        }
                        if (_config.WriteFrameStamps)
                        {
                            builder.AppendFormat(_config.FrameStampFormat, entry.FrameStamp);
                            builder.Append(' ');
                        }
                        if (source.FormattedPrefix != null)
                        {
                            builder.Append(source.FormattedPrefix);
                            builder.Append(' ');
                        }
                        builder.Append(_config.UseStandardTypeFormat ? 
                            LogFormat.LogMessageTypeStandard(entry.Type) : 
                            LogFormat.LogMessageTypeShort(entry.Type));
                        builder.Append(": ");
                        builder.Append(' ', _indent * 2);
                        totalPrefixLength = builder.Length;
                    }
                    else
                    {
                        builder.Append(' ', totalPrefixLength);
                    }
                    builder.Append(lines[i]);

                    lines[i] = builder.ToString();
                    WriteLine(entry, lines[i]);
                }
            }
        }
        public void PushIndent()
        {
            _indent++;
        }
        public void PopIndent()
        {
            _indent--;
        }

        protected virtual void WriteLine(LogEntry entry, string formattedLine) {
            _target.WriteLine(formattedLine);
            _logRotator.PerformLogCheck();
            if(_logRotator.CurrentLogOverflow) {
                
            } 
        }
    }

}
