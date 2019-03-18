using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

using UnityEngine;
using UnityEditor;

namespace ArtCom.Logging.Editor {

    [Serializable]
    public struct EditorWindowLogEntry
    {
        public LogEntry            Log;
        public string              SingleLineMessage;
        public int                 Indent;
        public string              SourceName;
        public LogEntryContext     Context;
        public LogEntryStrackTrace CallStack;
    }

}