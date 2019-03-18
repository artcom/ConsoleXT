using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ArtCom.Logging {
    public class LogRotator {
        private TextLogOutputConfig _config;
        private Stack<FileInfo> _observedFiles;
        private bool _currentLogOverflow;
        private Stopwatch watch;
        private bool initialized;
        
        public bool CurrentLogOverflow {
            get { return _currentLogOverflow; }
            set {
                PerformLogSizeTrimming();
                _currentLogOverflow = value;
            }
        }

        public LogRotator(TextLogOutputConfig config) {
            _config = config;
	    _observedFiles = new Stack<FileInfo>();
            CreateLogScheduler();
        }

        public void PerformLogCheck() {
            if(watch.Elapsed.TotalSeconds > _config.LogRotateSchedule) {
                if(!initialized) {
                    initialized = true;
                    RefreshObservedFiles();
                    PerformLogCountTrimming();
                    PerformLogTimeSpanTrimming();
                    PerformLogSizeTrimming();
                }
                watch.Reset();
                watch.Start();
                PerformLogSizeTrimming();
            }
        }

        private void CreateLogScheduler() {
            watch = new Stopwatch();
            watch.Start();
        }

        public void PerformLogSizeTrimming() {
            if(_observedFiles.Count <= 0) {
                return;
            }

            long size = 0L;
            foreach(var fileInfo in _observedFiles) {
                size += fileInfo.Length;
            }

            if(size <= _config.LogRotateMaxFiles) {
                return;
            }


            long originalSize = size;
            int originalFileCount = _observedFiles.Count;
            while(size > _config.LogRotateMaxSize) {
                FileInfo candidate = _observedFiles.Peek();
                if(candidate.FullName == _config.LoggingPath) {
                    if(candidate.Length > _config.LogRotateFatalSize) {
                        // Crashes the engine on purpose.
                        Logs.Default.WriteFatal("PANIC: Logfile is much lager ({0:0.00}MB) than expected!",
                                                candidate.Length / (float) (1024 * 1024));
                        if(_config.LogRotateAllowSizeCrashes) {
                            Application.Quit();
                        }
                    }

                    Logs.Default.WriteWarning("Current Logfile exceeds maximum size!");
                    _currentLogOverflow = true;
                    return;
                }

                _observedFiles.Pop();
                size -= candidate.Length;
                File.Delete(candidate.FullName);
            }

            Logs.Default.WriteDebug("MAXIMUM FILE SIZE REACHED: Deleted [{0}] Log Files," +
                                    "saved [{1:0.00}]MB, then [{2:0.00}]MB, now [{2:0.00}]MB.",
                                    originalFileCount - _observedFiles.Count,
                                    originalSize / (float) (1024 * 1024),
                                    (originalSize - size) / (float) (1024 * 1024));
        }

        public void PerformLogCountTrimming() {
            if(_config.LogRotateMaxFiles < 0) {
                return;
            }

            if(_observedFiles.Count <= 0) {
                return;
            }

            var originalCount = _observedFiles.Count;
            while(_observedFiles.Count > _config.LogRotateMaxFiles) {
                var candidate = _observedFiles.Pop();
                File.Delete(candidate.FullName);
            }

            if(_observedFiles.Count < originalCount) {
                Logs.Default.WriteDebug("MAXIMUM LOG COUNT REACHED: Deleted [{0}] Log Files.",
                                        originalCount - _observedFiles.Count);
            }
        }

        public void PerformLogTimeSpanTrimming() {
            if(_config.LogRotateMaxAge == TimeSpan.MinValue) {
                return;
            }

            if(_observedFiles.Count <= 0) {
                return;
            }

            var originalCount = _observedFiles.Count;
            while(_observedFiles.Count > 0) {
                var candidate = _observedFiles.Peek();
                var creationTimeSpan = DateTime.UtcNow - candidate.CreationTimeUtc;
                if(creationTimeSpan < _config.LogRotateMaxAge) {
                    break;
                }

                _observedFiles.Pop();
                File.Delete(candidate.FullName);
            }

            if(_observedFiles.Count < originalCount) {
                Logs.Default.WriteDebug("MAXIMUM TIME SPAN REACHED: Deleted [{0}] Log Files",
                                        originalCount - _observedFiles.Count);
            }
        }

        public void RefreshObservedFiles() {
            var files = ListLogFiles();
            _observedFiles = new Stack<FileInfo>();
            files
                .Select(x => new FileInfo(x))
                .OrderByDescending(x => x.CreationTimeUtc)
                .ToList()
                .ForEach(x => _observedFiles.Push(x));
        }

        private string[] ListLogFiles() {
            var logPath = Path.GetDirectoryName(_config.LoggingPath);
            if(!Directory.Exists(logPath)) {
                return new string[] { };
            }

            var list = new List<string>();
            foreach(var file in Directory.GetFiles(logPath)) {
                if(_config.LogRotateMatcher.IsMatch(file)) {
                    list.Add(file);
                }
            }

            return list.ToArray();
        }
    }
}
