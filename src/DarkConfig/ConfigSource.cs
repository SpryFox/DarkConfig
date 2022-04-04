using System;
using System.Collections.Generic;

namespace DarkConfig {
    /// A source of config files to load.
    /// e.g. a folder on disk, a binary file, a web server, etc.
    public abstract class ConfigSource {
        /// Does this config source support hotloading config files?
        public abstract bool CanHotload { get; }

        /// Find and load all configs that this source knows about
        public abstract void Preload();

        /// Try to hotload config files.  Adds names of changed files to the <paramref name="changedFiles"/> list.
        public virtual void Hotload(List<string> changedFiles) {}
        
        /////////////////////////////////////////////////
        
        /// All the currently loaded config file data.
        public Dictionary<string, ConfigFileInfo> AllFiles = new Dictionary<string, ConfigFileInfo>();
    }
}