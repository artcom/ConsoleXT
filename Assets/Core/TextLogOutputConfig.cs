using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace ArtCom.Logging {

    public class TextLogOutputConfig
    {

        private bool     _useStandardTypeFormat = true;
        private bool     _writeTimeStamps       = true;
        private bool     _writeFrameStamps      = true;
        private string   _loggingPath           = null;
        private string   _timeStampFormat       = "(" + LogFormat.TimeStampFormatDefault + ")";
        private string   _frameStampFormat      = "(" + LogFormat.FrameStampFormatDefault + ")";
        private bool     _logRotate             = false;
        private Regex    _logRotateMatcher      = new Regex(@"log\W([\d-T]*)\.txt");
        private long     _logRotateMaxSize      = 50L * 1024L * 1024L; // in bytes
        private long     _logRotateFatalSize    = 1024L * 1024L * 1024L; // 1GB of log
        private bool _logRotateAllowSizeCrashes = true;
        private long     _logRotateMaxFiles     = 50L;
        private int      _logRotateSchedule     = 60; // in second
        private TimeSpan _logRotateMaxAge       = new TimeSpan(31, 0, 0, 0);
        

        /// <summary>
        /// Use DEBUG, INFO, WARN, ERROR, FATAL instead of abbreviations
        /// </summary>
        public bool UseStandardTypeFormat
        {
            get { return _useStandardTypeFormat; }
            set { _useStandardTypeFormat = value; }
        }
        /// <summary>
        /// Writes time stamp into the logging stream
        /// </summary>
        public bool WriteTimeStamps
        {
            get { return _writeTimeStamps; }
            set { _writeTimeStamps = value; }
        }
        /// <summary>
        /// Writes explicit frame counters
        /// </summary>
        public bool WriteFrameStamps
        {
            get { return _writeFrameStamps; }
            set { _writeFrameStamps = value; }
        }
        /// <summary>
        /// Storage of logging directory
        /// </summary>
        public string LoggingPath {
            get { return _loggingPath; }
            set { _loggingPath = value; }
        }
        /// <summary>
        /// Sets a specific timestamp format, default value is @"HH\:mm\:ss.fff"
        /// </summary>
        public string TimeStampFormat
        {
            get { return _timeStampFormat; }
            set { _timeStampFormat = value; }
        }
        /// <summary>
        /// Sets a specific timestamp format, default value is @"{0,7}"
        /// </summary>
        public string FrameStampFormat
        {
            get { return _frameStampFormat; }
            set { _frameStampFormat = value; }
        }
        /// <summary>
        /// Enable LogRotate?
        /// </summary>
        public bool LogRotate {
            get { return _logRotate; }
            set { _logRotate = value; }
        }
        /// <summary>
        /// A compiled regex that matches the logging format
        /// </summary>
        public Regex LogRotateMatcher {
            get { return _logRotateMatcher; }
            set { _logRotateMatcher = value; }
        }
        /// <summary>
        /// Maximum Log Size in Logging Folder, in byte - default: 50MB
        /// </summary>
        public long LogRotateMaxSize {
            get { return _logRotateMaxSize; }
            set { _logRotateMaxSize = value; }
        }
        /// <summary>
        /// Fatal Log Size in logging folder, this will creash Unity on purpose.
        /// </summary>
        public long LogRotateFatalSize {
            get { return _logRotateFatalSize; }
            set { _logRotateFatalSize = value; }
        }
        /// <summary>
        /// Is it allowed to crash the application if the log files exceed the
        /// absolute maximum in log file size.
        /// WARNING: This should be enabled to avoid system locks by an overflowing filesystem!
        /// </summary>
        public bool LogRotateAllowSizeCrashes {
            get { return _logRotateAllowSizeCrashes; }
            set { _logRotateAllowSizeCrashes = value; }
        }
        /// <summary>
        /// Maximum Log Files in Logging Folder - default: 50
        /// </summary>
        public long LogRotateMaxFiles {
            get { return _logRotateMaxFiles; }
            set { _logRotateMaxFiles = value; }
        }
        /// <summary>
        /// How often should the Logrotator check for log sizes (in seconds) 
        /// </summary>
        public int LogRotateSchedule {
            get { return _logRotateSchedule; }
            set { _logRotateSchedule = value; }
        }
        /// <summary>
        /// Maximum age in Rotation - default: Inifitely
        /// </summary>
        public TimeSpan LogRotateMaxAge {
            get { return _logRotateMaxAge; }
            set { _logRotateMaxAge = value; }
        }
    }
}
