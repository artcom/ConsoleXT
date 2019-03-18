using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;

using ArtCom.Logging;

namespace LogTests {

    public class EditorLogTest {

        [MenuItem("Custom Unity Logging/Throw Editor Exception")]
        public static void ThrowException() {
            throw new InvalidOperationException("Manually triggered test exception");
        }
        [MenuItem("Custom Unity Logging/Log Unity Message")]
        public static void LogUnityMessage() {
            Debug.LogFormat("I'm a regular old Unity message that was written by the editor");
        }
    }

}
