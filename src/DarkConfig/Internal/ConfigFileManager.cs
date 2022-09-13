using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DarkConfig.Internal {
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

        /// True if all sources have been preloaded.
        internal bool IsPreloaded { get; private set; }
        internal readonly List<ConfigSource> sources = new List<ConfigSource>();
        
        /////////////////////////////////////////////////   

        /// <summary>
        /// Start parsing all config files.  Must call
        /// this via Configs.Preload before using anything else
        /// in DarkConfig.
        /// yield break's when all files are preloaded. 
        /// </summary>
        /// <returns></returns>
        public IEnumerable StepPreload() {
            if (IsPreloaded) { 
                yield break;
            }

            // Preload all sources.
            Configs.LogInfo($"Preloading {sources.Count} sources");
            foreach (var source in sources) {
                Configs.LogInfo($"Preloading source {source}");
                foreach (object _ in source.StepPreload()) {
                    yield return null;
                }
            }

            // Build combined files
            foreach (var combinerData in combiners.Values) {
                BuildCombinedConfig(combinerData);
                yield return null;
            }

            IsPreloaded = true;
            nextHotloadTime = Configs.Settings.HotloadCheckFrequencySeconds;

            Configs.LogInfo($"Done preloading, IsHotloadingFiles: {IsHotloadingFiles}");
        }

        /// <summary>
        /// Get the parsed contents of a preloaded file.
        /// </summary>
        /// <param name="filename">Name of the file to parse</param>
        /// <returns>The parsed yaml data</returns>
        /// <exception cref="ConfigFileNotFoundException">Thrown if a config can't be found with the given name.</exception>
        public DocNode ParseFile(string filename) {
            ThrowIfNotPreloaded();

            foreach (var source in sources) {
                if (source.AllFiles.TryGetValue(filename, out var configInfo)) {
                    return configInfo.Parsed;
                }
            }

            if (combiners.TryGetValue(filename, out var combinerData)) {
                return combinerData.Parsed;
            }

            throw new ConfigFileNotFoundException(filename);
        }

        /// <summary>
        /// Load a config file, parse the contents, and pass it to the given callback.
        /// Register the callback to be called every time the file is hotloaded.
        /// </summary>
        /// <param name="filename">Name of the config to load.</param>
        /// <param name="callback">Called whenever the file is loaded or changed.</param>
        /// <exception cref="ConfigFileNotFoundException">Thrown if a config can't be found with the given name.</exception>
        public void ParseFile(string filename, ReloadFunc callback) {
            ThrowIfNotPreloaded();
            
            foreach (var source in sources) {
                if (source.AllFiles.TryGetValue(filename, out var configInfo)) {
                    if (callback(configInfo.Parsed)) {
                        RegisterReloadCallback(filename, callback);
                    }
                    return;
                }
            }

            if (combiners.TryGetValue(filename, out var combinerData)) {
                if (callback(combinerData.Parsed)) {
                    RegisterReloadCallback(filename, callback);
                }
                return;
            }
            
            throw new ConfigFileNotFoundException(filename);
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
            ThrowIfNotPreloaded();

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
            ThrowIfNotPreloaded();

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
            return GetFilenamesMatchingRegex(RegexUtils.GlobToRegex(glob));
        }

        /// <summary>
        /// Find all files in the index that match a regular expression.
        /// </summary>
        /// <param name="pattern">Regex to match file names with.</param>
        /// <returns>List of file names matching the given regex.</returns>
        public List<string> GetFilenamesMatchingRegex(Regex pattern) {
            ThrowIfNotPreloaded();
            
            var results = new List<string>();
            
            foreach (var source in sources) {
                RegexUtils.FilterMatching(pattern, source.AllFiles.Keys, results);
            }
            
            return results;
        }

        public ConfigFileInfo GetFileInfo(string filename) {
            ThrowIfNotPreloaded();
            
            foreach (var source in sources) {
                if (source.AllFiles.TryGetValue(filename, out var info)) {
                    return info;
                }
            }
            return null;
        }

        /// If hotloading is enabled, triggers an immediate hotload.
        public void DoImmediateHotload() {
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
            
            // Log and call callbacks for modified files.
            foreach (string filename in modifiedFiles) {
                Configs.LogInfo($"Hotloading: {filename}");
                if (reloadCallbacks.TryGetValue(filename, out var callbacks)) {
                    for (int j = 0; j < callbacks.Count; j++) {
                        if (!callbacks[j](ParseFile(filename))) {
                            callbacks.RemoveAt(j);
                            j--;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Register a function to be called whenever a file is loaded.
        /// </summary>
        /// <param name="filename">Config file name.</param>
        /// <param name="callback">Called whenever the file is loaded.</param>
        public void RegisterReloadCallback(string filename, ReloadFunc callback) {
            if (!reloadCallbacks.TryGetValue(filename, out var callbacks)) {
                reloadCallbacks[filename] = new List<ReloadFunc> {callback};
                return;
            }

            if (!callbacks.Contains(callback)) {
                callbacks.Add(callback);
            }
        }

        public void Update(float dt) {
            if (IsHotloadingFiles) {
                nextHotloadTime -= dt;
                if (nextHotloadTime <= 0) {
                    DoImmediateHotload();
                }
            }
        }

        /////////////////////////////////////////////////
        
        float nextHotloadTime;
        readonly Dictionary<string, List<ReloadFunc>> reloadCallbacks = new Dictionary<string, List<ReloadFunc>>();
        
        class CombinerData {
            public string[] Filenames;
            public string CombinedFilename;
            public Func<List<DocNode>, DocNode> Combiner;
            public DocNode Parsed;
        }
        readonly Dictionary<string, CombinerData> combiners = new Dictionary<string, CombinerData>();
        readonly Dictionary<string, List<CombinerData>> combinersBySubfile = new Dictionary<string, List<CombinerData>>();
        
        /////////////////////////////////////////////////

        void ThrowIfNotPreloaded() {
            if (!IsPreloaded) {
                throw new NotPreloadedException("You must call Configs.Preload before using anything else in Dark Config");
            }
        }

        void BuildCombinedConfig(CombinerData combinerData) {
            var docs = new List<DocNode>(combinerData.Filenames.Length);
            foreach (string filename in combinerData.Filenames) {
                if (filename == combinerData.CombinedFilename) {
                    continue; // prevent trivial infinite loops
                }
                docs.Add(ParseFile(filename));
            }
            combinerData.Parsed = combinerData.Combiner(docs);
        }
    }
}
