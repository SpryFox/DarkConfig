using UnityEngine;

namespace DarkConfig {
    public class UnityPlatform : Platform {
        public UnityPlatform() {
            UnityTypeReifiers.RegisterAll();
        }

        protected override void LogCallback(LogVerbosity verbosity, string message) {
            switch (verbosity) {
                case LogVerbosity.Error: Debug.LogError(message); break;
                case LogVerbosity.Warn: Debug.LogWarning(message); break;
                case LogVerbosity.Info: Debug.Log(message); break;
            }
        }

        public override void Assert(bool test, string message) {
            Debug.Assert(test, message);
        }
    }
}