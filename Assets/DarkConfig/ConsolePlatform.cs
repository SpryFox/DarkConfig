using System;

namespace DarkConfig {
    public class ConsolePlatform : Platform {
        protected override void LogCallback(LogVerbosity verbosity, string message) {
            if (verbosity == LogVerbosity.Info) {
                Console.Out.WriteLine(message);
            } else {
                Console.Error.WriteLine(message);
            }
        }
    }
}