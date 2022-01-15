using System;
using System.Collections.Generic;

namespace DarkConfig {
    /// A source of config files to load.
    /// e.g. a folder on disk, a binary file, a web server, etc.
    public abstract class ConfigSource {
        public abstract bool CanHotload { get; }

        public abstract void Preload();

        public virtual void Hotload(List<string> changedFiles) {}
        
        /////////////////////////////////////////////////
        
        public Dictionary<string, ConfigFileInfo> AllFiles = new Dictionary<string, ConfigFileInfo>();
    }
}