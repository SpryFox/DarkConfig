using UnityEngine;
using System.Collections;

namespace DarkConfig {
    public class UnityPlatform : Platform {
        public static void Setup() {
            Platform.Instance = new UnityPlatform();
            UnityFromDocs.RegisterAll();
        }

        public UnityPlatform() {
            if(Application.isEditor) {
                DefaultFilePath = Application.dataPath + "/Resources/Configs";
            } else {
                DefaultFilePath  = Application.dataPath;
            }

            CanDoImmediatePreload = Application.isEditor;
        }

        public override ConfigSource GetDefaultSource() {
            return new ResourcesSource("Configs");
        }

        public override void Log(string msg) {
            Debug.Log(msg);
        }

        public override void LogError(string msg) {
            Debug.LogError(msg);
        }

        public override void Clear() {
            if(Application.isEditor && !Application.isPlaying) {
                if (s_ownObject != null) GameObject.DestroyImmediate(s_ownObject);
            } else {
                if (s_ownObject != null) GameObject.Destroy(s_ownObject);
            }
        }

        public override object WaitForSeconds(float seconds) {
            return new WaitForSeconds(seconds);
        }

        public override object StartCoroutine(IEnumerator coro) {
            return TokenMonoBehaviour.StartCoroutine(coro);
        }

        public override void StopCoroutine(IEnumerator coro) {
            TokenMonoBehaviour.StopCoroutine(coro);
        }

        // instance of MonoBehaviour used only for its StartCoroutine functionality
        internal static MonoBehaviour TokenMonoBehaviour {
            get {
                if(s_tokenMonoBehaviour == null) {
                    s_ownObject = new GameObject("DarkConfigTemporary");
                    s_ownObject.hideFlags = HideFlags.HideAndDontSave;
					if (!Application.isEditor || Application.isPlaying) {
						UnityEngine.Object.DontDestroyOnLoad(s_ownObject);
					}
                    s_tokenMonoBehaviour = s_ownObject.AddComponent<MonoBehaviourSubclass>();
                }
                return s_tokenMonoBehaviour;
            }
        }
        static MonoBehaviour s_tokenMonoBehaviour;
        static GameObject s_ownObject = null;

    }

	[ExecuteInEditMode]
	public class MonoBehaviourSubclass : MonoBehaviour { }
}
