using System;
using System.Collections.Generic;

namespace DarkConfig {
    public abstract class ConfigSource {
        public bool CanHotload { get; }

        public abstract bool CanLoadNow();

        public abstract void Preload(Action callback);

        public abstract void ReceivePreloaded(List<ConfigFileInfo> files);

        public abstract ConfigFileInfo TryHotload(ConfigFileInfo configFileInfo);
        
        /////////////////////////////////////////////////

        protected readonly List<string> index = new List<string>();
        public List<ConfigFileInfo> LoadedFiles = new List<ConfigFileInfo>();
        
        /////////////////////////////////////////////////

        protected ConfigSource(bool hotload) {
            CanHotload = hotload;
        }
    }
}