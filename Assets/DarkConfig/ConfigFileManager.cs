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
        /// Contents may change during preloading.  Do not modify list. (readonly lists are not supported by the 
        /// version of Mono that Unity bundles)
        public readonly List<string> Files = new List<string>();

        /// Returns a dictionary of the files that are currently loaded.
        public readonly Dictionary<string, ConfigFileInfo> FileInfos = new Dictionary<string, ConfigFileInfo>();

        /// This event is called for every file that gets hotloaded.
        public event Action<string> OnHotloadFile;
        
        /////////////////////////////////////////////////   

        /// Loads index file and start loading all config files.  Must call
        /// this (via Config.Preload, not directly) before using anything else
        /// in DarkConfig.
        public void Preload(Action callback = null) {
            if (IsPreloaded || isPreloading) {
                return;
            }
            isPreloading = true;

            FileInfos.Clear();
            Files.Clear();

            Platform.Log(LogVerbosity.Info, "Preloading", sources.Count, "sources");
            foreach (var source in sources) {
                if (!source.CanLoadNow()) {
                    continue;
                }

                Platform.Log(LogVerbosity.Info, "Using source", source);

                var source1 = source;
                source.Preload(() => {
                    isPreloading = false;
                    IsPreloaded = true;

                    var files = source1.LoadedFiles;
                    foreach (var finfo in files) {
                        Files.Add(finfo.Name);
                        FileInfos.Add(finfo.Name, finfo);
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

                    if (callback != null) callback();
                });
                break;
            }
        }

        /// Adds a source of config files, to be consulted when loading or hotloading.
        public void AddSource(ConfigSource source) {
            sources.Add(source);
        }

        /// Returns the number of sources currently added.
        public int CountSources() {
            return sources.Count;
        }

        /// Load a config file into a DocNode and return it directly.
        public DocNode LoadConfig(string configName) {
            CheckPreload();
            if (!FileInfos.ContainsKey(configName)) {
                throw new ConfigFileNotFoundException(configName);
            }

            return FileInfos[configName].Parsed;
        }

        /// Load a config file and call *cb* immediately with the contents.  This also registers *cb* to
        /// be called every time the file is hotloaded.
        public void LoadConfig(string configName, ReloadDelegate cb) {
            CheckPreload();
            if (!FileInfos.ContainsKey(configName)) {
                throw new ConfigFileNotFoundException(configName);
            }

            bool save = cb(FileInfos[configName].Parsed);
            if (save) {
                RegisterReload(configName, cb);
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
        /// <param name="combiner">Function that takes in an array of DocNodes and returns a
        /// DocNode.  Implement whatever algorithm you want.  This function gets called every
        /// time any of the source files changes, with the DocNodes of all source files.</param>
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
                FileInfos[newFilename] = new ConfigFileInfo {
                    Name = newFilename,
                    Parsed = BuildCombinedConfig(newFilename)
                };
            }
        }

        /// Stops producing a combined file, specified by name.
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

        /// Returns a list of files in the index that match a glob pattern.  Glob patterns work in a Unix-esque fashion:
        ///   '*' matches any sequence of characters, but stops at slashes
        ///   '?' matches a single character, except a slash
        ///   '**' matches any sequence of characters, including slashes
        public List<string> GetFilesByGlob(string glob) {
            CheckPreload();
            return Internal.RegexUtils.FilterMatchingGlob(glob, Files);
        }

        /// Returns a list of files in the index that match a regular expression.
        public List<string> GetFilesByRegex(Regex pattern) {
            CheckPreload();
            return Internal.RegexUtils.FilterMatching(pattern, Files);
        }

        /// Loads all files from the source immediately.  For editor tooling.
        public void LoadFromSourceImmediately(ConfigSource source) {
            Platform.Assert(Config.Platform.CanDoImmediatePreload, "Trying to load immediately on a platform that doesn't support it");
            isPreloading = true;
            Platform.Log(LogVerbosity.Info, "Immediate-loading " + source);

            source.Preload(() => { }); // assume that this is immediate
            var files = source.LoadedFiles;
            foreach (var finfo in files) {
                Files.Add(finfo.Name);
                FileInfos.Add(finfo.Name, finfo);
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

        internal void RegisterReload(string filename, ReloadDelegate cb) {
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
            lock (FileInfos) {
                finfo = FileInfos[configName];
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
                bool isNewFile = !FileInfos.ContainsKey(finfo.Name);
                FileInfos[finfo.Name] = finfo;
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
                        FileInfos[shortName] = new ConfigFileInfo {
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
                            FileInfos[configName] = newInfo;
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