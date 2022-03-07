using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DarkConfig {
    public class ConfigFileManager {
        /// If true, DarkConfig will periodically scan config files for changes and reload them as necessary.
        /// Setting it to false stops hotloading.  Enabling hotloading is only recommended during development, not in shipping builds.
        /// HotloadCheckFrequencySeconds in Settings controls the rate at which files are scanned.
        /// Defaults to false.
        public bool IsHotloadingFiles {
            get => _IsHotloadingFiles;
            set {
                _IsHotloadingFiles = value;
                if (_IsHotloadingFiles) {
                    // Don't immediately hotload.
                    nextHotloadTime = Configs.Settings.HotloadCheckFrequencySeconds;
                }
            }
        }
        bool _IsHotloadingFiles;

        /// This event is called for every file that gets hotloaded.
        public event Action<string> OnHotloadFile;
        
        /// True if all sources have been preloaded.
        internal bool IsPreloaded { get; private set; }
        
        /////////////////////////////////////////////////   

        /// <summary>
        /// Loads index file and start loading all config files.  Must call
        /// this (via Config.Preload, not directly) before using anything else
        /// in DarkConfig.
        /// </summary>
        public void Preload() {
            if (IsPreloaded) {
                return;
            }

            // Preload all sources.            
            Configs.LogInfo($"Preloading {sources.Count} sources");
            foreach (var source in sources) {
                Configs.LogInfo($"Preloading source {source}");
                source.Preload();
            }

            // Build combined files
            foreach (var combinerData in combiners.Values) {
                BuildCombinedConfig(combinerData);
            }

            IsPreloaded = true;
            nextHotloadTime = Configs.Settings.HotloadCheckFrequencySeconds;

            Configs.LogInfo($"Done preloading, IsHotloadingFiles: {IsHotloadingFiles}");
        }

        /// <summary>
        /// Add a config file source.
        /// Sources are used when loading or hotloading.
        /// Multiple sources of config files can be registered.
        /// </summary>
        /// <param name="source">The new source to register</param>
        public void AddSource(ConfigSource source) {
            sources.Add(source);
        }

        /// <summary>
        /// Remove a config file source.
        /// </summary>
        /// <param name="source">The source to </param>
        public void RemoveSource(ConfigSource source) {
            sources.Remove(source);
        }

        /// <summary>
        /// Get the number of sources currently registered.
        /// </summary>
        /// <returns>number of sources currently registered</returns>
        public int CountSources() {
            return sources.Count;
        }

        /// <summary>
        /// Get the parsed contents of a preloaded file.
        /// </summary>
        /// <param name="configName">Name of the config to load.</param>
        /// <returns>The parsed config file contents.</returns>
        /// <exception cref="ConfigFileNotFoundException">Thrown if a config can't be found with the given name.</exception>
        public DocNode LoadConfig(string configName) {
            CheckPreload();

            foreach (var source in sources) {
                if (source.AllFiles.TryGetValue(configName, out var configInfo)) {
                    return configInfo.Parsed;
                }
            }

            if (combiners.TryGetValue(configName, out var combinerData)) {
                return combinerData.Parsed;
            }

            throw new ConfigFileNotFoundException(configName);
        }

        /// <summary>
        /// Load a config file, parse the contents, and pass it to the given callback.
        /// Register the callback to be called every time the file is hotloaded.
        /// </summary>
        /// <param name="configName">Name of the config to load.</param>
        /// <param name="callback">Called whenever the file is loaded or changed.</param>
        /// <exception cref="ConfigFileNotFoundException">Thrown if a config can't be found with the given name.</exception>
        public void LoadConfig(string configName, ReloadDelegate callback) {
            CheckPreload();
            
            foreach (var source in sources) {
                if (source.AllFiles.TryGetValue(configName, out var configInfo)) {
                    if (callback(configInfo.Parsed)) {
                        RegisterReloadCallback(configName, callback);
                    }
                    return;
                }
            }

            if (combiners.TryGetValue(configName, out var combinerData)) {
                if (callback(combinerData.Parsed)) {
                    RegisterReloadCallback(configName, callback);
                }
                return;
            }
            
            throw new ConfigFileNotFoundException(configName);
        }

        /// <summary>
        /// Create a "combined file", which is made up of the contents of several other files.  
        /// It's not actually a real file, it's only in-memory, but you can load this combined
        /// file as though it was a real file in the index.  Useful as a technique to manage a
        /// directory of small config files as though it's one big file, or even more esoteric stuff.
        /// </summary>
        /// <param name="sourceFilenames">List of filenames that are to be combined</param>
        /// <param name="newFilename">Name of the new combined file.  Once it's registered, 
        /// you call LoadConfig with this name.  Should be unique -- naming a combined file
        /// the same as another will clobber it.</param>
        /// <param name="combiner">Combines multiple parsed fies into a single file.
        /// Called when any of the source files change with the DocNodes of all source files.</param>
        public void RegisterCombinedFile(List<string> sourceFilenames, string newFilename, Func<List<DocNode>, DocNode> combiner) {
            CheckPreload();

            // clobber any existing setup for this filename
            if (combiners.ContainsKey(newFilename)) {
                UnregisterCombinedFile(newFilename);
            }

            var combinerData = new CombinerData {
                Filenames = sourceFilenames.ToArray(),
                Combiner = combiner,
                CombinedFilename = newFilename
            };
            
            combiners[newFilename] = combinerData;

            foreach (string filename in sourceFilenames) {
                if (!combinersBySubfile.ContainsKey(filename)) {
                    combinersBySubfile[filename] = new List<CombinerData>();
                }

                var list = combinersBySubfile[filename];
                if (!list.Contains(combinerData)) {
                    list.Add(combinerData);
                }
            }

            if (IsPreloaded) {
                BuildCombinedConfig(combinerData);
            }
        }

        /// <summary>
        /// Stop producing a combined file.
        /// </summary>
        /// <param name="combinedFilename">Generated name of the combined file.</param>
        public void UnregisterCombinedFile(string combinedFilename) {
            CheckPreload();

            if (!combiners.ContainsKey(combinedFilename)) {
                return;
            }

            var combinerData = combiners[combinedFilename];

            foreach (string filename in combinerData.Filenames) {
                var list = combinersBySubfile[filename];
                if (list.Count == 1) {
                    // We're about to remove the configData reference from the list, which will result in an empty list,
                    // so just skip removing it and remove the entire list
                    combinersBySubfile.Remove(filename);
                } else {
                    list.Remove(combinerData);
                }
            }

            combiners.Remove(combinedFilename);
        }
        
        /// <summary>
        /// Find all files in the index that match a glob pattern.
        ///
        ///  Glob patterns work in a Unix-esque fashion:
        ///  '*' matches any sequence of characters, but stops at slashes
        ///  '?' matches a single character, except a slash
        ///  '**' matches any sequence of characters, including slashes
        /// </summary>
        /// <param name="glob">Glob to match file names with.</param>
        /// <returns>List of file names matching the given glob.</returns>
        public List<string> GetFilenamesMatchingGlob(string glob) {
            return GetFilenamesMatchingRegex(Internal.RegexUtils.GlobToRegex(glob));
        }

        /// <summary>
        /// Find all files in the index that match a regular expression.
        /// </summary>
        /// <param name="pattern">Regex to match file names with.</param>
        /// <returns>List of file names matching the given regex.</returns>
        public List<string> GetFilenamesMatchingRegex(Regex pattern) {
            CheckPreload();
            
            var results = new List<string>();
            
            foreach (var source in sources) {
                Internal.RegexUtils.FilterMatching(pattern, source.AllFiles.Keys, results);
            }
            
            return results;
        }

        /// If hotloading is enabled, triggers an immediate hotload.
        public void DoHotload() {
            if (!IsHotloadingFiles) {
                return;
            }
            nextHotloadTime = Configs.Settings.HotloadCheckFrequencySeconds;

            // Hotload from all sources.  Keep a list of the files that were changed.
            var modifiedFiles = new List<string>();
            foreach (var source in sources) {
                if (!source.CanHotload) {
                    continue;
                }                
                source.Hotload(modifiedFiles);
            }

            // Re-generate and mark as changed any combined files that depend on files that were modified.
            for (int modifiedFileIndex = 0; modifiedFileIndex < modifiedFiles.Count; modifiedFileIndex++) {
                string filename = modifiedFiles[modifiedFileIndex];

                if (combinersBySubfile.ContainsKey(filename)) {
                    foreach (var combinerData in combinersBySubfile[filename]) {
                        BuildCombinedConfig(combinerData);
                        modifiedFiles.Add(combinerData.CombinedFilename);
                    }
                }
            }
            
            // Call callbacks for modified files.
            foreach (string filename in modifiedFiles) {
                if (reloadCallbacks.TryGetValue(filename, out var callbacks)) {
                    for (int j = 0; j < callbacks.Count; j++) {
                        if (!callbacks[j](LoadConfig(filename))) {
                            callbacks.RemoveAt(j);
                            j--;
                        }
                    }
                }
                
                OnHotloadFile?.Invoke(filename);
            }
        }

        /// <summary>
        /// Register a function to be called whenever a file is loaded.
        /// </summary>
        /// <param name="filename">Config file name.</param>
        /// <param name="callback">Called whenever the file is loaded.</param>
        public void RegisterReloadCallback(string filename, ReloadDelegate callback) {
            if (!reloadCallbacks.TryGetValue(filename, out var delegates)) {
                reloadCallbacks[filename] = new List<ReloadDelegate> {callback};
                return;
            }

            if (!delegates.Contains(callback)) {
                delegates.Add(callback);
            }
        }

        public void Update(float dt) {
            if (IsHotloadingFiles) {
                nextHotloadTime -= dt;
                if (nextHotloadTime <= 0) {
                    DoHotload();
                }
            }
        }

        public int CountReloadCallbacks() {
            int callbackCount = 0;
            foreach (var kvp in reloadCallbacks) {
                callbackCount += kvp.Value.Count;
            }
            return callbackCount;
        }

        public bool HasFile(string filename) {
            foreach (var source in sources) {
                if (source.AllFiles.ContainsKey(filename)) {
                    return true;
                }
            }
            return false;
        }

        /////////////////////////////////////////////////
        
        float nextHotloadTime;
        readonly List<ConfigSource> sources = new List<ConfigSource>();
        readonly Dictionary<string, List<ReloadDelegate>> reloadCallbacks = new Dictionary<string, List<ReloadDelegate>>();
        
        class CombinerData {
            public string[] Filenames;
            public string CombinedFilename;
            public Func<List<DocNode>, DocNode> Combiner;
            public DocNode Parsed;
        }
        readonly Dictionary<string, CombinerData> combiners = new Dictionary<string, CombinerData>();
        readonly Dictionary<string, List<CombinerData>> combinersBySubfile = new Dictionary<string, List<CombinerData>>();
        
        /////////////////////////////////////////////////

        void CheckPreload() {
            if (IsPreloaded) {
                return;
            }

            Preload();
            Configs.LogInfo($"Done on-demand preloading, IsHotloadingFiles: {IsHotloadingFiles}");
        }

        void BuildCombinedConfig(CombinerData combinerData) {
            var docs = new List<DocNode>(combinerData.Filenames.Length);
            foreach (string filename in combinerData.Filenames) {
                if (filename == combinerData.CombinedFilename) {
                    continue; // prevent trivial infinite loops
                }
                docs.Add(LoadConfig(filename));
            }
            combinerData.Parsed = combinerData.Combiner(docs);
        }
    }
}