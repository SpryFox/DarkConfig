using UnityEngine;
using System.Collections;

namespace DarkConfig {
    public class UnityPlatform : Platform {
        public UnityPlatform() {
            CanDoImmediatePreload = Application.isEditor;
            UnityTypeReifiers.RegisterAll();
        }

        protected override void LogCallback(LogVerbosity verbosity, string message) {
            switch (verbosity) {
                case LogVerbosity.Error: Debug.LogError(message); break;
                case LogVerbosity.Warn: Debug.LogWarning(message); break;
                case LogVerbosity.Info: Debug.Log(message); break;
            }
        }

        public override void Clear() {
            if (Application.isEditor && !Application.isPlaying) {
                if (tempGameObject != null) {
                    Object.DestroyImmediate(tempGameObject);
                }
            } else {
                if (tempGameObject != null) {
                    Object.Destroy(tempGameObject);
                }
            }
        }

        public override object WaitForSeconds(float seconds) {
            return new WaitForSeconds(seconds);
        }

        public override object StartCoroutine(IEnumerator coroutine) {
            return TokenMonoBehaviour.StartCoroutine(coroutine);
        }

        public override void StopCoroutine(IEnumerator coroutine) {
            TokenMonoBehaviour.StopCoroutine(coroutine);
        }
        
        public override void Assert(bool test, string message) {
            Debug.Assert(test, message);
        }

        /// instance of MonoBehaviour used only for its StartCoroutine functionality
        static MonoBehaviour TokenMonoBehaviour {
            get {
                if (tokenMonoBehaviour == null) {
                    tempGameObject = new GameObject("DarkConfigTemporary");
                    tokenMonoBehaviour = tempGameObject.AddComponent<CoroutineRunner>();
                }

                return tokenMonoBehaviour;
            }
        }
        
        /////////////////////////////////////////////////

        static MonoBehaviour tokenMonoBehaviour;
        static GameObject tempGameObject;
    }

    /// Creating an empty MonoBehaviour just to run coroutines on.
    [ExecuteInEditMode]
    public class CoroutineRunner : MonoBehaviour {
        void Start() {
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            if (!Application.isEditor || Application.isPlaying) {
                DontDestroyOnLoad(gameObject);
            }
        }
    }
}