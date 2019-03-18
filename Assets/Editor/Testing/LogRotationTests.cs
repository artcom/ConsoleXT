using System;
using System.IO;
using ArtCom.Logging;
using UnityEngine;

namespace LogTests {
    public class LogRotationTests : MonoBehaviour {
        void Start() {
            
            Logs.InitGlobalLogFile(Application.streamingAssetsPath, string.Format("log {0:" + LogFormat.TimeStampFormatISO8601 + "}.txt", DateTime.UtcNow), new TextLogOutputConfig {
                LogRotateSchedule = 1,
                LogRotate = true,
                LogRotateMaxSize = 1024,
                LogRotateFatalSize = 127*1024
            });
        }
        
        void Update() {
            Logs.Default.Write("Hi, I am an info test.");
        }
    }
}