using System;
using System.Collections;
using System.Collections.Generic;

namespace DarkConfig {
    public class ConsolePlatform : Platform {
        public static void Setup() {
            Platform.Instance = new ConsolePlatform();
        }

        public ConsolePlatform() {
            CanDoImmediatePreload = false;
        }

        public override ConfigSource GetDefaultSource() {
            return new FileSource(AppDomain.CurrentDomain.BaseDirectory + "Configs");
        }

        public override void Log(string msg) {
            Console.WriteLine(msg);
        }

        public override void LogError(string msg) {
            Console.WriteLine("Error: " + msg);
        }

        /// <summary>
        /// The ConsolePlatform needs this to be called repeatedly in order to
        /// run its coroutines.  The currentTime argument should be the
        /// current time in seconds, measured relative to whatever epoch you
        /// like (except negative infinity).
        /// </summary>
        public void Update(float currentTime) {
            m_coroutines.AddRange(m_newCoroutines);
            m_newCoroutines.Clear();

            for(int i = 0; i < m_coroutines.Count; i++) {
                var coro = m_coroutines[i];
                if(coro.resumeAt > currentTime) continue;

                try {
                    if(!coro.iter.MoveNext()) {
                        // coroutine finished
                        if(coro.parent != null) {
                            // parent swaps in for the child
                            m_coroutines[i] = coro.parent;
                        } else {
                            // remove without disturbing the order
                            m_coroutines.RemoveAt(i);
                            i--;
                        }
                    } else {
                        // coroutine not finished, check the result
                        var stepResult = coro.iter.Current;
                        if(stepResult is float) {
                            // wait for some seconds
                            coro.resumeAt = currentTime + (float)stepResult;
                        } else if(stepResult is Coro) {
                            // child coro, need to wait for that to finish
                            var childCoro = stepResult as Coro;
                            childCoro.parent = coro;
                            // it replaces the parent in the updates; when it's done we'll swap the parent back
                            m_coroutines[i] = childCoro;
                            m_newCoroutines.Remove(childCoro);
                        } else {
                            // it's anything else, including null, so execute as soon as possible
                            coro.resumeAt = float.MinValue;
                        }
                    }
                } catch(Exception e) {
                    LogError("Caught coroutine error" + e);
                    m_coroutines.Remove(coro);
                    i--;
                }
            }
        }

        public override object WaitForSeconds(float seconds) {
            return seconds;
        }

        public override object StartCoroutine(IEnumerator coro) {
            if(coro == null) return null;

            var startedCoro = new Coro {
                iter = coro,
                resumeAt = float.MinValue,
                parent = null
            };
            m_newCoroutines.Add(startedCoro);
            return startedCoro;
        }

        class Coro {
            public IEnumerator iter;
            public float resumeAt;
            public Coro parent;
        }

        List<Coro> m_coroutines = new List<Coro>();
        List<Coro> m_newCoroutines = new List<Coro>();
    }
}
