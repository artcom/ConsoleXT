using System;
using System.Collections.Generic;
using System.IO;

namespace ArtCom.Logging {

    [Serializable]
    public struct LogEntry {
        public LogMessageType Type;
        public string         Message;
        public long           TimeStampTicks; // Unity can't serialize DateTime, so we'll store ticks instead
        public int            FrameStamp;

        public DateTime TimeStamp
        {
            get { return new DateTime(TimeStampTicks, DateTimeKind.Utc); }
        }

        public LogEntry(LogMessageType type, string msg) {
            Type = type;
            Message = msg;
            TimeStampTicks = DateTime.UtcNow.Ticks;
            FrameStamp = UnityLogIntegration.CurrentUnityFrame;
        }
    }

}