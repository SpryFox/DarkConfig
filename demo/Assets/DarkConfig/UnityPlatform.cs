using UnityEngine;

namespace DarkConfig {
    public static class UnityPlatform {
        public static void Setup() {
            UnityTypeReifiers.RegisterAll();
            Config.LogCallback = Log;
            Config.AssertCallback = (test, message) => Debug.Assert(test, message);
        }

        static void Log(LogVerbosity verbosity, string message) {
            switch (verbosity) {
                case LogVerbosity.Error: Debug.LogError(message); break;
                case LogVerbosity.Warn: Debug.LogWarning(message); break;
                case LogVerbosity.Info: Debug.Log(message); break;
            }
        }
    }
}