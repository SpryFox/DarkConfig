﻿using System;
using UnityEngine;

namespace DarkConfig {
    public static class UnityPlatform {
        public static void Setup(bool hotloading = false) {
            UnityTypeReifiers.RegisterAll();
            Configs.LogCallback = Log;

            if (hotloading) {
                SetupHotloadingManager();
            }
        }

        static void Log(LogVerbosity verbosity, string message) {
            switch (verbosity) {
                case LogVerbosity.Warn: Debug.LogWarning(message); break;
                case LogVerbosity.Info: Debug.Log(message); break;
                default: throw new ArgumentOutOfRangeException(nameof(verbosity), verbosity, null);
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
