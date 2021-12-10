using System;
using System.Collections;

namespace DarkConfig {
    public delegate void AssertDelegate(bool test, params object[] messages);

    public enum LogVerbosity {
        Error,
        Warn,
        Info
    }
    
    public abstract class Platform {
        public static Platform Instance;

        public bool CanDoImmediatePreload = false;

        public abstract IConfigSource GetDefaultSource();

        public virtual void Clear() { }

        public abstract object WaitForSeconds(float seconds);
        public abstract object StartCoroutine(IEnumerator coro);
        public abstract void StopCoroutine(IEnumerator coro);
        
        protected virtual void Log(string msg) { Console.Out.WriteLine(msg); }
        protected virtual void LogError(string msg) { Console.Error.WriteLine(msg); }
        
        #region Logging
        const string LOG_GUARD = "DC_LOGGING_ENABLED";
        const string LogPrefix = "[DarkConfig] ";
        
        /// How aggressively DarkConfig logs to Debug.Log.
        public static LogVerbosity LogLevel = LogVerbosity.Info;
        
        [System.Diagnostics.Conditional(LOG_GUARD)]
        public static void Log(LogVerbosity level, string msg) {
            if (level <= LogLevel) {
                var message = LogPrefix + msg;
                if (level > LogVerbosity.Error) {
                    Instance.Log(message);
                } else {
                    Instance.LogError(message);
                }
            }
        }

        [System.Diagnostics.Conditional(LOG_GUARD)]
        public static void Log(LogVerbosity level, object msg1) {
            Log(level, msg1.ToString());
        }

        [System.Diagnostics.Conditional(LOG_GUARD)]
        public static void Log(LogVerbosity level, object msg1, object msg2) {
            Log(level, msg1 + " " + msg2);
        }

        [System.Diagnostics.Conditional(LOG_GUARD)]
        public static void Log(LogVerbosity level, object msg1, object msg2, object msg3) {
            Log(level, msg1 + " " + msg2 + " " + msg3);
        }

        [System.Diagnostics.Conditional(LOG_GUARD)]
        public static void Log(LogVerbosity level, object msg1, object msg2, object msg3, object msg4) {
            Log(level, msg1 + " " + msg2 + " " + msg3 + " " + msg4);
        }
        
        [System.Diagnostics.Conditional(LOG_GUARD)]
        public static void Log(LogVerbosity level, object msg1, object msg2, object msg3, object msg4, object msg5) {
            Log(level, msg1 + " " + msg2 + " " + msg3 + " " + msg4 + " " + msg5);
        }

        [System.Diagnostics.Conditional(LOG_GUARD)]
        public static void Log(LogVerbosity level, object msg1, object msg2, object msg3, object msg4, object msg5, object msg6) {
            Log(level, msg1 + " " + msg2 + " " + msg3 + " " + msg4 + " " + msg5 + " " + msg6);
        }
        
        [System.Diagnostics.Conditional(LOG_GUARD)]
        public static void Log(LogVerbosity level, params object[] msgs) {
            Log(level, string.Join(" ", msgs));
        }
        #endregion

        #region Asserts
        const string ASSERT_GUARD = "DC_ASSERTS_ENABLED";
        /// Hook for custom assert functions.  If set, DarkConfig calls this function for its assert checks.
        public static AssertDelegate AssertFailureCallback;
        
        [System.Diagnostics.Conditional(ASSERT_GUARD)]
        public static void Assert(bool test, string message) {
            if (AssertFailureCallback != null) {
                AssertFailureCallback(test, message);                    
            } else {
                System.Diagnostics.Debug.Assert(test, message);
            }
        }

        [System.Diagnostics.Conditional(ASSERT_GUARD)]
        public static void Assert(bool test, object msg1) {
            Assert(test, msg1.ToString());
        }

        [System.Diagnostics.Conditional(ASSERT_GUARD)]
        public static void Assert(bool test, object msg1, object msg2) {
            Assert(test, msg1 + " " + msg2);
        }

        [System.Diagnostics.Conditional(ASSERT_GUARD)]
        public static void Assert(bool test, object msg1, object msg2, object msg3) {
            Assert(test, msg1 + " " + msg2 + " " + msg3);
        }

        [System.Diagnostics.Conditional(ASSERT_GUARD)]
        public static void Assert(bool test, object msg1, object msg2, object msg3, object msg4) {
            Assert(test, msg1 + " " + msg2 + " " + msg3 + " " + msg4);
        }
        
        [System.Diagnostics.Conditional(ASSERT_GUARD)]
        public static void Assert(bool test, object msg1, object msg2, object msg3, object msg4, object msg5) {
            Assert(test, msg1 + " " + msg2 + " " + msg3 + " " + msg4 + " " + msg5);
        }

        [System.Diagnostics.Conditional(ASSERT_GUARD)]
        public static void Assert(bool test, object msg1, object msg2, object msg3, object msg4, object msg5, object msg6) {
            Assert(test, msg1 + " " + msg2 + " " + msg3 + " " + msg4 + " " + msg5 + " " + msg6);
        }

        [System.Diagnostics.Conditional(ASSERT_GUARD)]
        public static void Assert(bool test, object msg1, object msg2, object msg3, object msg4, object msg5, object msg6, object msg7) {
            Assert(test, msg1 + " " + msg2 + " " + msg3 + " " + msg4 + " " + msg5 + " " + msg6 + " " + msg7);
        }
        
        [System.Diagnostics.Conditional(ASSERT_GUARD)]
        public static void Assert(bool test, params object[] messages) {
            Assert(test, string.Join(" ", messages));
        }
        #endregion
    }
}