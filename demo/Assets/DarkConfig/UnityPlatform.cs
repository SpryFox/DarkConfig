using UnityEngine;

namespace DarkConfig {
    public static class UnityPlatform {
        public static void Setup(bool hotloading = false) {
            UnityTypeReifiers.RegisterAll();
            Configs.LogCallback = Log;
            Configs.AssertCallback = (test, message) => Debug.Assert(test, message);

            if (hotloading) {
                SetupHotloadingManager();
            }
        }

        static void Log(LogVerbosity verbosity, string message) {
            switch (verbosity) {
                case LogVerbosity.Error: Debug.LogError(message); break;
                case LogVerbosity.Warn: Debug.LogWarning(message); break;
                case LogVerbosity.Info: Debug.Log(message); break;
            }
        }

        private static GameObject hotloadingManagerInstance = null;
        static void SetupHotloadingManager() {
            if (hotloadingManagerInstance != null) {
                return;
            }

            hotloadingManagerInstance = new GameObject("DarkConfigHotloadingManager");
            hotloadingManagerInstance.AddComponent<HotloadingManager>();
        }

        class HotloadingManager : MonoBehaviour {
            void Awake() {
                DontDestroyOnLoad(gameObject);
            }

            void Update() {
                Configs.Update(Time.deltaTime);
            }
        }
    }
}
