using System.IO;

namespace ArtCom.Logging
{
    public interface ILogOutput
    {
        void Write(Log source, LogEntry entry, object context);
        void PushIndent();
        void PopIndent();
    }
}