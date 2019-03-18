using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ArtCom.Logging;

namespace LogTests {

    public class LogTest : MonoBehaviour, ISomeGenericInterface<DummyComponent>, ISomeInterface {

        private GameObject _destroyedObj;
        private Component _destroyedCmp;
        private bool _lateStart;

        private void Awake() {
            // Prepare a destroyed GameObject and Component for testing purposes
            _destroyedObj = new GameObject();
            _destroyedCmp = _destroyedObj.AddComponent<DummyComponent>();
            Destroy(_destroyedCmp);
            Destroy(_destroyedObj);
        }

        private void Start() {
            //
            // All Log Write commands follow the string formatting scheme, i.e.
            //
            // prefer .Write("Number: {0}", _yourNum);
            // over   .Write("Number: " + _yourNum.ToString());
            //

            // Log something before we have an actual log file should be fine.
            Logs.Default.Write("I'm logged before a log file exists");

            // Call this multiple times to check behaviour. Don't do this regularly.
            Logs.InitGlobalLogFile("Logs", "invalidlog.txt");
            Logs.InitGlobalLogFile();

            // Test-writing system specs
            LogUtility.WriteAllSpecs(Logs.System);

            // Let's customize the output of our global log file!
            // TextWriterLogOutput globalLogFileOutput = Logs.InitGlobalLogFile();
            // globalLogFileOutput.TimeStampFormat = xy;

            Logs.Default.WriteDebug("This is a debug-only log message.");
            Logs.Default.Write("This is a log message.");
            Logs.Default.WriteWarning("This is a log warning.");
            Logs.Default.WriteError("This is a log error.");
            Logs.Default.WriteFatal("This is a log fatal error.");
            
            Logs.Default.Write("This is a multi-line" + Environment.NewLine + "log message!");

            Logs.Default.Write("Writing some logs with indent...");
            Logs.Default.PushIndent();
            Logs.Default.Write("First");
            Logs.Default.Write("Second");
            Logs.Default.Write("Third");
            Logs.Default.PopIndent();
            Logs.Default.Write("Done with testing indent.");

            Logs.Get<CustomLog>().Write("This is written to a custom log.");
            Logs.Get<CustomLog>().WriteWarning("This too is written to a custom log.");

            Logs.Default.Write("This log has a context object.", this);
            Logs.Default.Write("Auto-Format Test GameObject: {0}", this.gameObject);
            Logs.Default.Write("Auto-Format Test Component: {0}", this);
            Logs.Default.Write("Auto-Format Test pass-through: {0}", 42);
            Logs.Default.Write("Auto-Format Test pass-through: {0}", "Hello World");
            Debug.Log("I'm a regular old Unity log, but I should show up in the custom log file anyway");

            // Check if multi-threaded logs are possible
            {
                var threadA = new System.Threading.Thread(() =>
                {
                    for (int i = 0; i < 10; i++) {
                        Logs.Default.Write("Written from thread A");
                        System.Threading.Thread.Sleep(500);
                    }
                });
                var threadB = new System.Threading.Thread(() =>
                {
                    for (int i = 0; i < 10; i++) {
                        Logs.Default.Write("Written from thread B");
                        System.Threading.Thread.Sleep(500);
                    }
                });
                threadA.Name = "Thread A";
                threadB.Name = "Thread B";
                threadA.Start();
                threadB.Start();
            }

            // Log something that's more difficult to unwrap based on unity text callstacks
            DummyComponent dummyVar = null;
            this.Foo<DummyComponent>(dummyVar);
            this.Foo(ref dummyVar);
            (this as ISomeInterface).Foo(dummyVar);
            (this as ISomeGenericInterface<DummyComponent>).Foo(ref dummyVar);

            // Misuse push / pop indent to see if it's properly reset when 
            // entering / leaving play mode.
            Logs.Default.PushIndent();

            // Throw an exception and see if it's logged properly
            ThrowException();
        }

        private void LateUpdate() {
            if (_lateStart) return;

            Logs.Default.Write("Destroyed GameObject: {0}", _destroyedObj);
            Logs.Default.Write("Destroyed Component: {0}", _destroyedCmp);

            _lateStart = true;
        }

        private static void ThrowException() {
            throw new NotImplementedException();
        }

        private void Foo<T>(T value) {
            Debug.Log("I'm a regular old Unity log, written in a generic method.");
        }
        private void Foo(ref DummyComponent value) {
            Debug.Log("I'm a regular old Unity log, written in a method with ref parameter.");
        }
        void ISomeInterface.Foo(DummyComponent value) {
            Debug.Log("I'm a regular old Unity log, written in an explicit implementation of an interface.");
        }
        void ISomeGenericInterface<DummyComponent>.Foo(ref DummyComponent value) {
            Debug.Log("I'm a regular old Unity log, written in an explicit implementation of a generic interface, which uses a ref parameter.");
        }
    }

    public class DummyComponent : MonoBehaviour {}

    public class CustomLog : CustomLogInfo {}

    public interface ISomeInterface {
        void Foo(DummyComponent value);
    }
    public interface ISomeGenericInterface<T> {
        void Foo(ref T value);
    }

}
