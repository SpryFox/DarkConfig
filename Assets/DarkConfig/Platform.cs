using System.Collections;

namespace DarkConfig {
    public class Platform {
        public static Platform Instance = new Platform();

        public static string DefaultFilePath = null;

        public bool CanDoImmediatePreload = false;

        public virtual ConfigSource GetDefaultSource() {
            throw new System.NotSupportedException("There's not default source in empty Platform");
        }

        public virtual void Log(string msg) {
        }

        public virtual void LogError(string msg) {
        }

        public virtual void Clear() {
        }

        public virtual object WaitForSeconds(float seconds) {
            throw new System.NotSupportedException("Cannot call WaitForSeconds using empty Platform");
        }

        public virtual object StartCoroutine(IEnumerator coro) {
            throw new System.NotSupportedException("Cannot start coroutine using empty Platform");
        }

        public virtual void StopCoroutine(IEnumerator coro) {
            throw new System.NotSupportedException("Cannot start coroutine using empty Platform");
        }
    }

}