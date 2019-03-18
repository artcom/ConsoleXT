using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEngine.Networking;

namespace ArtCom.Logging.Internal
{
    /// <summary>
    /// Internal hidden <see cref="MonoBehaviour"/> where log integrations can schedule web requests,
    /// which will be executed asynchronously, in order and disposed of afterwards.
    /// </summary>
    public class UnityWebRequestScheduler : MonoBehaviour
    {
        private Queue<UnityWebRequest> pending = new Queue<UnityWebRequest>();
        private UnityWebRequest active = null;
        private float activeTimer = 0.0f;
        private float unusedTimer = 0.0f;
        private object queueLock = new object();

        public void Schedule(UnityWebRequest request)
        {
            if (request == null)
            {
                Logs.Default.WriteDebug("Attempting to schedule a log-related null request.");
                return;
            }
            lock (queueLock)
            {
                pending.Enqueue(request);
            }
        }

        private void ProcessNext()
        {
            if (active != null) return;

            lock (queueLock)
            {
                if (pending.Count > 0)
                {
                    activeTimer = 0.0f;
                    active = pending.Dequeue();
                    active.Send();
                }
            }
        }
        private bool WaitForActiveDone()
        {
            if (active == null) return true;

            activeTimer += Time.unscaledDeltaTime;

            const float TimeoutSeconds = 30.0f;
            bool isTimeout = activeTimer >= TimeoutSeconds;
            
#if UNITY_5
            if (active.isDone || active.isError || isTimeout)
            {
                if (active.isError)
                {
#else
            if (active.isDone || active.isNetworkError || isTimeout)
            {
                if(active.isNetworkError)
                {
#endif
                    Logs.Default.WriteDebug(
                        "Error performing log-related web request to {0}: {1}", 
                        active.url,
                        active.error);
                }
                else if (isTimeout)
                {
                    Logs.Default.WriteDebug(
                        "Timeout while performing log-related web request t0 {0} after {1:F} seconds", 
                        active.url,
                        activeTimer);
                }

                active.Dispose();
                active = null;
            }

            return false;
        }
        private void SelfDestructWhenUnused()
        {
            if (active == null)
            {
                unusedTimer += Time.unscaledDeltaTime;
                if (unusedTimer > 30.0f)
                {
                    GameObject.Destroy(gameObject);
                }
            }
            else
            {
                unusedTimer = 0.0f;
            }
        }

        private void Update()
        {
            // Wait for the current web request to be processed, proceed
            // with the next one from the queue when done
            if (WaitForActiveDone())
                ProcessNext();

            // When unused for an extended period of time, the scheduler 
            // will destroy itself.
            SelfDestructWhenUnused();
        }
        private void OnDestroy()
        {
            if (active != null)
            {
                active.Dispose();
                active = null;
            }

            foreach (UnityWebRequest request in pending)
            {
                request.Dispose();
            }
            pending.Clear();
        }

        public static UnityWebRequestScheduler Create()
        {
            GameObject obj = new GameObject("UnityWebRequestScheduler", typeof(UnityWebRequestScheduler));
            obj.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(obj);
            return obj.GetComponent<UnityWebRequestScheduler>();
        }
    }
}