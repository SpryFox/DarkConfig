using System;
using System.Collections;
using System.Collections.Generic;

namespace DarkConfig {
    public class ConsolePlatform : Platform {
        public ConsolePlatform() {
            CanDoImmediatePreload = false;
        }

        public override ConfigSource ConfigSource => new FileSource(AppDomain.CurrentDomain.BaseDirectory + "Configs");

        protected override void Log(string msg) {
            Console.WriteLine(msg);
        }

        protected override void LogError(string msg) {
            Console.WriteLine("Error: " + msg);
        }

        void FinishCoro(Coro coro, ref int index) {
            if (coro.parent != null) {
                // parent swaps in for the child
                coroutines[index] = coro.parent;
            } else {
                // remove without disturbing the order
                coroutines.RemoveAt(index);
                index--;
            }
        }

        /// The ConsolePlatform needs this to be called repeatedly in order to
        /// run its coroutines.  The currentTime argument should be the
        /// current time in seconds, measured relative to whatever epoch you
        /// like (except negative infinity).
        public void Update(float currentTime) {
            coroutines.AddRange(newCoroutines);
            newCoroutines.Clear();

            stoppedCoroutines.AddRange(newStoppedCoroutines);
            newStoppedCoroutines.Clear();

            for (int i = 0; i < coroutines.Count; i++) {
                var coro = coroutines[i];

                // check if the current coro is stopped
                Coro coroStopped = null;
                for (int s = 0; s < stoppedCoroutines.Count && coroStopped == null; s++) {
                    var stopped = stoppedCoroutines[s];
                    var tmpCoro = coro;
                    do {
                        if (tmpCoro.iter == stopped) {
                            coroStopped = tmpCoro;
                            break;
                        }

                        // also check parents, stopping parent stops the child!
                        tmpCoro = coro.parent;
                    } while (tmpCoro != null);
                }

                if (coroStopped != null) {
                    FinishCoro(coroStopped, ref i);
                    continue;
                }

                // don't run coroutines that are not yet ready to run
                if (coro.resumeAt > currentTime) continue;

                // now it's time to run it one step
                try {
                    if (!coro.iter.MoveNext()) {
                        FinishCoro(coro, ref i);
                        continue;
                    }

                    // coroutine not finished, check the result
                    var stepResult = coro.iter.Current;
                    if (stepResult is float) {
                        // wait for some seconds
                        coro.resumeAt = currentTime + (float) stepResult;
                    } else if (stepResult is Coro) {
                        // child coro, need to wait for that to finish
                        var childCoro = stepResult as Coro;
                        childCoro.parent = coro;
                        // it replaces the parent in the updates; when it's done we'll swap the parent back
                        coroutines[i] = childCoro;
                        newCoroutines.Remove(childCoro);
                    } else {
                        // it's anything else, including null, so execute as soon as possible
                        coro.resumeAt = float.MinValue;
                    }
                } catch (Exception e) {
                    LogError("Caught coroutine error" + e);
                    coroutines.Remove(coro);
                    i--;
                }
            }

            stoppedCoroutines.Clear();
        }

        public override object WaitForSeconds(float seconds) {
            return seconds;
        }

        public override object StartCoroutine(IEnumerator coro) {
            if (coro == null) return null;

            var startedCoro = new Coro {
                iter = coro,
                resumeAt = float.MinValue,
                parent = null
            };
            newCoroutines.Add(startedCoro);
            return startedCoro;
        }

        public override void StopCoroutine(IEnumerator coro) {
            if (coro == null) return;
            newStoppedCoroutines.Add(coro);
        }

        class Coro {
            public IEnumerator iter;
            public float resumeAt;
            public Coro parent;
        }

        List<Coro> coroutines = new List<Coro>();
        List<Coro> newCoroutines = new List<Coro>();

        List<IEnumerator> stoppedCoroutines = new List<IEnumerator>();
        List<IEnumerator> newStoppedCoroutines = new List<IEnumerator>();
    }
}