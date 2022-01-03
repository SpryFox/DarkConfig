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

        /// List of files in the index file.  This is all the files that DarkConfig can load at the time of access.
        /// Contents may change during preloading.  Do not modify list.
        public readonly List<string> Files = new List<string>();

        public readonly Dictionary<string, ConfigFileInfo> LoadedFiles = new Dictionary<string, ConfigFileInfo>();

        /// This event is called for every file that gets hotloaded.
        public event Action<string> OnHotloadFile;
        
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

            LoadedFiles.Clear();
            Files.Clear();

            Platform.Log(LogVerbosity.Info, "Preloading", sources.Count, "sources");
            foreach (var source in sources) {
                if (!source.CanLoadNow()) {
                    continue;
                }

                Platform.Log(LogVerbosity.Info, "Preloading source", source);

                var source1 = source;
                source.Preload(() => {
                    isPreloading = false;
                    IsPreloaded = true;

                    var files = source1.LoadedFiles;
                    foreach (var finfo in files) {
                        Files.Add(finfo.Name);
                        LoadedFiles.Add(finfo.Name, finfo);
                    }

                    // put files in all other sources
                    foreach (var s in sources) {
                        if (s != source1) {
                            s.ReceivePreloaded(files);
                        }
                    }

                    Platform.Log(LogVerbosity.Info, "Done preloading, IsHotloadingFiles: ", IsHotloadingFiles);

                    if (IsHotloadingFiles) {
                        Config.Platform.StartCoroutine(WatchFilesCoro());
                    }

                    callback?.Invoke();
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
            if (!LoadedFiles.ContainsKey(configName)) {
                throw new ConfigFileNotFoundException(configName);
            }

            return LoadedFiles[configName].Parsed;
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
            if (!LoadedFiles.ContainsKey(configName)) {
                throw new ConfigFileNotFoundException(configName);
            }

            bool save = callback(LoadedFiles[configName].Parsed);
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
                LoadedFiles[newFilename] = new ConfigFileInfo {
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
            return Internal.RegexUtils.FilterMatchingGlob(glob, Files);
        }

        /// <summary>
        /// Find all files in the index that match a regular expression.
        /// </summary>
        /// <param name="pattern">Regex to match file names with.</param>
        /// <returns>List of file names matching the given regex.</returns>
        public List<string> GetFilesByRegex(Regex pattern) {
            CheckPreload();
            return Internal.RegexUtils.FilterMatching(pattern, Files);
        }

        /// <summary>
        /// Loads all files from the source immediately.  For editor tooling.
        /// </summary>
        /// <param name="source"></param>
        public void LoadFromSourceImmediately(ConfigSource source) {
            Platform.Assert(Config.Platform.CanDoImmediatePreload, "Trying to load immediately on a platform that doesn't support it");
            isPreloading = true;
            Platform.Log(LogVerbosity.Info, "Immediate-loading " + source);

            source.Preload(() => { }); // assume that this is immediate
            var files = source.LoadedFiles;
            foreach (var finfo in files) {
                Files.Add(finfo.Name);
                LoadedFiles.Add(finfo.Name, finfo);
            }

            isPreloading = false;
            IsPreloaded = true;
        }

        /// Immediately checks all config files to see whether they can be
        /// hotloaded.  This may take tens or hundreds of milliseconds, but
        /// when it's complete every file will have been checked and hotloaded
        /// if necessary.  Calls callback when done.
        public void CheckHotloadImmediate(Action callback = null) {
            // deliberately ignore value of isCheckingHotloadNow
            var iter = CheckHotloadCoro(callback, 100);
            while (iter.MoveNext()) { }
        }

        /// Starts a coroutine to check every file to see whether it can be
        /// hotloaded, N files per frame.  It calls the callback when done
        /// with all files.  If one of the coroutines is already running when
        /// you call this, it will early exit (without calling the callback).
        public void CheckHotload(Action callback = null, int filesPerFrame = 1) {
            if (isCheckingHotloadNow) {
                return;
            }
            Config.Platform.StartCoroutine(CheckHotloadCoro(callback, filesPerFrame));
        }

        internal void RegisterReloadCallback(string filename, ReloadDelegate cb) {
            List<ReloadDelegate> delegates;
            if (!reloadCallbacks.TryGetValue(filename, out delegates)) {
                delegates = new List<ReloadDelegate>();
                reloadCallbacks[filename] = delegates;
            }

            if (!delegates.Contains(cb)) {
                delegates.Add(cb);
            }
        }

        internal int GetReloadDelegateCount() {
            int count = 0;
            foreach (var callback in reloadCallbacks) {
                count += callback.Value.Count;
            }
            return count;
        }

        internal void CallAllDelegates() {
            List<string> modified = new List<string>();
            foreach (var kv in reloadCallbacks) {
                modified.Add(kv.Key);
            }

            CallCallbacks(modified);
        }

        internal ConfigFileInfo CheckHotload(string configName) {
            ConfigFileInfo finfo;
            lock (LoadedFiles) {
                finfo = LoadedFiles[configName];
            }

            foreach (var source in sources) {
                if (!source.CanHotload) {
                    continue;
                }

                var newInfo = source.TryHotload(finfo);

                if (newInfo != null) {
                    Platform.Log(LogVerbosity.Info, "Hotloaded file " + newInfo + " old: " + finfo);

                    if (newInfo.Name == "index") {
                        // make sure that we sync up our list of loaded files
                        HotloadIndex(source);
                    }

                    OnHotloadFile?.Invoke(newInfo.Name);
                    return newInfo;
                }
            }

            return null;
        }

        /////////////////////////////////////////////////

        internal bool IsPreloaded { get; private set; }
        
        bool isPreloading;
        bool isHotloadingFiles = true;
        IEnumerator watchFilesCoro;
        bool isCheckingHotloadNow;
        readonly List<ConfigSource> sources = new List<ConfigSource>();
        readonly Dictionary<string, List<ReloadDelegate>> reloadCallbacks = new Dictionary<string, List<ReloadDelegate>>();
        readonly Dictionary<string, CombinerData> combiners = new Dictionary<string, CombinerData>();
        readonly Dictionary<string, List<CombinerData>> combinersBySubfile = new Dictionary<string, List<CombinerData>>();

        class CombinerData {
            public string[] Filenames;
            public string DestinationFilename;
            public Func<List<DocNode>, DocNode> Combiner;
        }
        
        /////////////////////////////////////////////////

        void CheckPreload() {
            if (!Config.Platform.CanDoImmediatePreload) {
                Platform.Assert(IsPreloaded, "Can't use configs in any way in a built game, before preloading is complete");
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
                Platform.Log(LogVerbosity.Info, "Done immediate-loading, IsHotloadingFiles: ", IsHotloadingFiles);
                Platform.Assert(preloadWasImmediate, "Did not preload immediately");
            }
        }

        void HotloadIndex(ConfigSource source) {
            var files = source.LoadedFiles;
            foreach (var finfo in files) {
                if (!Files.Contains(finfo.Name)) {
                    Files.Add(finfo.Name);
                }
                bool isNewFile = !LoadedFiles.ContainsKey(finfo.Name);
                LoadedFiles[finfo.Name] = finfo;
                if (isNewFile) {
                    OnHotloadFile?.Invoke(finfo.Name);
                }
            }
        }

        IEnumerator WatchFilesCoro() {
            try {
                while (IsHotloadingFiles) {
                    while (!IsPreloaded) {
                        yield return Config.Platform.WaitForSeconds(0.1f);
                    }
                    yield return Config.Platform.WaitForSeconds(Config.Settings.HotloadCheckFrequencySeconds);
                    yield return Config.Platform.StartCoroutine(CheckHotloadCoro());
                }
            } finally {
                watchFilesCoro = null;
            }
        }

        DocNode BuildCombinedConfig(string filename) {
            if (combiners.ContainsKey(filename)) {
                var multifile = combiners[filename];
                var subdocs = new List<DocNode>(multifile.Filenames.Length);
                foreach (string subfilename in multifile.Filenames) {
                    if (subfilename == filename) {
                        continue; // prevent trivial infinite loops
                    }
                    subdocs.Add(LoadConfig(subfilename));
                }

                return multifile.Combiner(subdocs);
            }

            return null;
        }

        void CallCallbacks(List<string> modifiedFiles) {
            // generate any combined files once, then mark that file changed
            for (int i = 0; i < modifiedFiles.Count; i++) {
                var filename = modifiedFiles[i];

                if (combinersBySubfile.ContainsKey(filename)) {
                    var multicallbacks = combinersBySubfile[filename];
                    foreach (var mc in multicallbacks) {
                        var shortName = mc.DestinationFilename;
                        LoadedFiles[shortName] = new ConfigFileInfo {
                            Name = shortName,
                            Parsed = BuildCombinedConfig(mc.DestinationFilename)
                        };
                        modifiedFiles.Add(mc.DestinationFilename);
                    }
                }
            }

            // call callbacks for modified files
            foreach (string filename in modifiedFiles) {
                if (!reloadCallbacks.ContainsKey(filename)) {
                    continue;
                }
                var callbacks = reloadCallbacks[filename];
                for (int j = 0; j < callbacks.Count; j++) {
                    var doc = LoadConfig(filename);
                    var save = callbacks[j](doc);
                    if (!save) {
                        callbacks.RemoveAt(j);
                        j--;
                    }
                }
            }
        }
        
        IEnumerator CheckHotloadCoro(Action cb = null, int filesPerLoop = 1) {
            isCheckingHotloadNow = true;

            try {
                // kind of a brute-force implementation for now: look at each file and see whether it changed
                List<string> modifiedFiles = new List<string>();

                for (int k = 0; k < Files.Count; k++) {
                    if (!IsHotloadingFiles) yield break;
                    var configName = Files[k];
                    try {
                        var newInfo = CheckHotload(configName);
                        if (newInfo != null) {
                            modifiedFiles.Add(configName);
                            LoadedFiles[configName] = newInfo;
                        }
                    } catch (Exception e) {
                        Platform.Log(LogVerbosity.Error, "Exception loading file", configName, e);
                    }

                    if ((k % filesPerLoop) == 0) {
                        // throttle how many files we check per frame to control the performance impact
                        yield return null;
                    }
                }

                CallCallbacks(modifiedFiles);
                cb?.Invoke();
            } finally {
                isCheckingHotloadNow = false;
            }
        }
    }
}