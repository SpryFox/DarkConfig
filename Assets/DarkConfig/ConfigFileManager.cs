using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DarkConfig {
    public class ConfigFileManager {
        /// If true (the default), DarkConfig will scan files for changes every 
        /// HotloadCheckInterval seconds.  Setting it to false stops hotloading;
        /// recommended for production games.
        public bool IsHotloadingFiles {
            get => isHotloadingFiles;
            set {
                isHotloadingFiles = value;
                if (isHotloadingFiles && watchFilesCoro == null) {
                    watchFilesCoro = WatchFilesCoro();
                    Config.Platform.StartCoroutine(watchFilesCoro);
                } else if (!isHotloadingFiles && watchFilesCoro != null) {
                    Config.Platform.StopCoroutine(watchFilesCoro);
                    watchFilesCoro = null;
                }
            }
        }

        /// This event is called for every file that gets hotloaded.
        public event Action<string> OnHotloadFile;
        
        internal bool IsPreloaded { get; private set; }
        
        /////////////////////////////////////////////////   

        /// <summary>
        /// Loads index file and start loading all config files.  Must call
        /// this (via Config.Preload, not directly) before using anything else
        /// in DarkConfig.
        /// </summary>
        /// <param name="callback">Called when preloading is complete</param>
        public void Preload(Action callback = null) {
            if (IsPreloaded || isPreloading) {
                return;
            }
            
            isPreloading = true;

            loadedFiles.Clear();
            allFilenames.Clear();

            int preloadedSources = 0;
            
            Platform.LogInfo($"Preloading {sources.Count} sources");
            foreach (var source in sources) {
                Platform.LogInfo($"Preloading source {source}");

                var source1 = source;
                source.Preload(() => {
                    preloadedSources++;
                    
                    var files = source1.LoadedFiles;
                    foreach (var finfo in files) {
                        allFilenames.Add(finfo.Name);
                        loadedFiles.Add(finfo.Name, finfo);
                    }

                    // put files in all other sources
                    foreach (var s in sources) {
                        if (s != source1) {
                            s.ReceivePreloaded(files);
                        }
                    }

                    if (preloadedSources == sources.Count) {
                        isPreloading = false;
                        IsPreloaded = true;
                        
                        // We're done preloading all sources.
                        Platform.LogInfo($"Done preloading, IsHotloadingFiles: {IsHotloadingFiles}");

                        if (IsHotloadingFiles) {
                            Config.Platform.StartCoroutine(WatchFilesCoro());
                        }

                        callback?.Invoke();                        
                    }
                });
                break;
            }
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
            if (!loadedFiles.ContainsKey(configName)) {
                throw new ConfigFileNotFoundException(configName);
            }

            return loadedFiles[configName].Parsed;
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
            if (!loadedFiles.ContainsKey(configName)) {
                throw new ConfigFileNotFoundException(configName);
            }

            bool save = callback(loadedFiles[configName].Parsed);
            if (save) {
                RegisterReloadCallback(configName, callback);
            }
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

            var listener = new CombinerData {
                Filenames = sourceFilenames.ToArray(),
                Combiner = combiner,
                DestinationFilename = newFilename
            };
            combiners[newFilename] = listener;
            foreach (string filename in sourceFilenames) {
                if (!combinersBySubfile.ContainsKey(filename)) {
                    combinersBySubfile[filename] = new List<CombinerData>();
                }

                var list = combinersBySubfile[filename];
                if (!list.Contains(listener)) {
                    list.Add(listener);
                }
            }

            if (IsPreloaded) {
                loadedFiles[newFilename] = new ConfigFileInfo {
                    Name = newFilename,
                    Parsed = BuildCombinedConfig(newFilename)
                };
            }
        }

        /// <summary>
        /// Stop producing a combined file.
        /// </summary>
        /// <param name="combinedConfigName">Generated name of the combined file.</param>
        public void UnregisterCombinedFile(string combinedConfigName) {
            CheckPreload();

            var combinedFilename = combinedConfigName;

            if (!combiners.ContainsKey(combinedFilename)) {
                return;
            }

            var mc = combiners[combinedFilename];

            foreach (string filename in mc.Filenames) {
                var list = combinersBySubfile[filename];
                list.Remove(mc);
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
        public List<string> GetFilesByGlob(string glob) {
            CheckPreload();
            return Internal.RegexUtils.FilterMatchingGlob(glob, allFilenames);
        }

        /// <summary>
        /// Find all files in the index that match a regular expression.
        /// </summary>
        /// <param name="pattern">Regex to match file names with.</param>
        /// <returns>List of file names matching the given regex.</returns>
        public List<string> GetFilesByRegex(Regex pattern) {
            CheckPreload();
            return Internal.RegexUtils.FilterMatching(pattern, allFilenames);
        }

        /// <summary>
        /// Loads all files from the source immediately.  For editor tooling.
        /// </summary>
        /// <param name="source"></param>
        public void LoadFromSourceImmediately(ConfigSource source) {
            Config.Platform.Assert(Config.Platform.CanDoImmediatePreload, "Trying to load immediately on a platform that doesn't support it");
            isPreloading = true;
            Platform.LogInfo($"Immediate-loading {source}");

            source.Preload(() => { }); // assume that this is immediate
            foreach (var fileInfo in source.LoadedFiles) {
                allFilenames.Add(fileInfo.Name);
                loadedFiles.Add(fileInfo.Name, fileInfo);
            }

            isPreloading = false;
            IsPreloaded = true;
        }

        /// <summary>
        /// Immediately checks all config files for changes in a non-async way.
        /// Reloads any files that have changed since they were last loaded.
        /// </summary>
        public void CheckHotloadBlocking() {
            // deliberately ignore value of isCheckingHotloadNow
            var coroutine = CheckHotloadAsyncCoro(filesPerLoop:100);
            while (coroutine.MoveNext()) { }
        }

        /// True if there is an async hotload in-progress.
        public bool IsAsyncHotloading => isCheckingHotloadNow;
        
        /// <summary>
        /// Checks all config files for changes in a non-blocking way.
        /// Reloads any files that have changed since they were last loaded.
        /// Waits until all files are re-parsed and then calls all file on-reload callbacks at once.
        /// Calls the optional provided callback once all this is complete.
        ///
        /// If this process is already started, this function does nothing and does not call the callback.
        /// </summary>
        /// <param name="callback">(optional) Called when the process is complete.</param>
        /// <param name="filesPerFrame">(optional) Control how many files are checked per-frame.</param>
        public void CheckHotloadAsync(Action callback = null, int filesPerFrame = 1) {
            if (isCheckingHotloadNow) {
                return;
            }
            Config.Platform.StartCoroutine(CheckHotloadAsyncCoro(callback, filesPerFrame));
        }

        /////////////////////////////////////////////////

        /// <summary>
        /// Register a function to be called whenever a file is loaded.
        /// </summary>
        /// <param name="filename">Config file name.</param>
        /// <param name="callback">Called whenever the file is loaded.</param>
        internal void RegisterReloadCallback(string filename, ReloadDelegate callback) {
            if (!reloadCallbacks.TryGetValue(filename, out var delegates)) {
                reloadCallbacks[filename] = new List<ReloadDelegate> {callback};
                return;
            }

            if (!delegates.Contains(callback)) {
                delegates.Add(callback);
            }
        }

        /////////////////////////////////////////////////
        
        /// List of files in the index file.  This is all the files that DarkConfig can load at the time of access.
        /// Contents may change during preloading.  Do not modify list.
        readonly List<string> allFilenames = new List<string>();

        readonly Dictionary<string, ConfigFileInfo> loadedFiles = new Dictionary<string, ConfigFileInfo>();

        bool isPreloading;
        bool isHotloadingFiles = true;
        bool isCheckingHotloadNow;
        IEnumerator watchFilesCoro;
        
        readonly List<ConfigSource> sources = new List<ConfigSource>();
        readonly Dictionary<string, List<ReloadDelegate>> reloadCallbacks = new Dictionary<string, List<ReloadDelegate>>();
        
        class CombinerData {
            public string[] Filenames;
            public string DestinationFilename;
            public Func<List<DocNode>, DocNode> Combiner;
        }
        readonly Dictionary<string, CombinerData> combiners = new Dictionary<string, CombinerData>();
        readonly Dictionary<string, List<CombinerData>> combinersBySubfile = new Dictionary<string, List<CombinerData>>();
        
        /////////////////////////////////////////////////

        void CheckPreload() {
            if (!Config.Platform.CanDoImmediatePreload) {
                Config.Platform.Assert(IsPreloaded, "Can't use configs in any way in a built game, before preloading is complete");
                return;
            }

            // we can preload immediately; this means that the developer doesn't have to go through a loading screen for every scene; just hit play
            if (IsPreloaded || isPreloading) {
                return;
            }

            if (sources.Count == 0) {
                LoadFromSourceImmediately(Config.Platform.ConfigSource);
            } else {
                bool preloadWasImmediate = false;
                Preload(() => { preloadWasImmediate = true; }); // note: all preloading is immediate
                Platform.LogInfo($"Done immediate-loading, IsHotloadingFiles: {IsHotloadingFiles}");
                Config.Platform.Assert(preloadWasImmediate, "Did not preload immediately");
            }
        }

        IEnumerator WatchFilesCoro() {
            try {
                while (IsHotloadingFiles) {
                    while (!IsPreloaded) {
                        yield return Config.Platform.WaitForSeconds(0.1f);
                    }
                    yield return Config.Platform.WaitForSeconds(Config.Settings.HotloadCheckFrequencySeconds);
                    yield return Config.Platform.StartCoroutine(CheckHotloadAsyncCoro());
                }
            } finally {
                watchFilesCoro = null;
            }
        }

        DocNode BuildCombinedConfig(string combinedFilename) {
            if (combiners.TryGetValue(combinedFilename, out var combinerData)) {
                var docs = new List<DocNode>(combinerData.Filenames.Length);
                foreach (string filename in combinerData.Filenames) {
                    if (filename == combinedFilename) {
                        continue; // prevent trivial infinite loops
                    }
                    docs.Add(LoadConfig(filename));
                }

                return combinerData.Combiner(docs);
            }

            return null;
        }
        
        IEnumerator CheckHotloadAsyncCoro(Action callback = null, int filesPerLoop = 1) {
            isCheckingHotloadNow = true;

            // look at each file and see whether it changed
            var modifiedFiles = new List<string>();

            for (int fileIndex = 0; fileIndex < allFilenames.Count; fileIndex++) {
                string configName = allFilenames[fileIndex];
                
                if (!IsHotloadingFiles) {
                    yield break;
                }
                
                // throttle how many files we check per frame to control the performance impact
                if ((fileIndex % filesPerLoop) == 0) {
                    yield return null;
                }
                
                try {
                    var loadedFile = loadedFiles[configName];
                    foreach (var source in sources) {
                        if (!source.CanHotload) {
                            continue;
                        }

                        var newInfo = source.TryHotloadFile(loadedFile);
                
                        if (newInfo == null) {
                            continue;
                        }

                        Platform.LogInfo($"Re-parsed file {newInfo} old: {loadedFile}");

                        if (newInfo.Name == "index") {
                            // make sure that we sync up our list of loaded files
                            foreach (var finfo in source.LoadedFiles) {
                                if (!allFilenames.Contains(finfo.Name)) {
                                    allFilenames.Add(finfo.Name);
                                }
                                bool isNewFile = !loadedFiles.ContainsKey(finfo.Name);
                                loadedFiles[finfo.Name] = finfo;
                                if (isNewFile) {
                                    OnHotloadFile?.Invoke(finfo.Name);
                                }
                            }
                        }

                        loadedFiles[configName] = newInfo;
                        modifiedFiles.Add(configName);
                        break;
                    }
                } catch (Exception e) {
                    Platform.LogError($"Exception loading file {configName} {e}");
                }
            }

            // re-generate any combined files as necessary, and then mark the combined file as changed
            for (int modifiedFileIndex = 0; modifiedFileIndex < modifiedFiles.Count; modifiedFileIndex++) {
                string filename = modifiedFiles[modifiedFileIndex];
                if (combinersBySubfile.ContainsKey(filename)) {
                    var multicallbacks = combinersBySubfile[filename];
                    foreach (var combinerData in multicallbacks) {
                        string combinedName = combinerData.DestinationFilename;
                        loadedFiles[combinedName] = new ConfigFileInfo {
                            Name = combinedName,
                            Parsed = BuildCombinedConfig(combinedName)
                        };
                        modifiedFiles.Add(combinedName);
                    }
                }
            }
            
            // call callbacks for modified files
            foreach (string filename in modifiedFiles) {
                if (reloadCallbacks.TryGetValue(filename, out var callbacks)) {
                    for (int j = 0; j < callbacks.Count; j++) {
                        var doc = LoadConfig(filename);
                        bool save = callbacks[j](doc);
                        if (!save) {
                            callbacks.RemoveAt(j);
                            j--;
                        }
                    }
                }
                
                OnHotloadFile?.Invoke(filename);
            }
            
            callback?.Invoke();
            isCheckingHotloadNow = false;
        }
    }
}