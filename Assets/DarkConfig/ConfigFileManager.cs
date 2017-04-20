using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using YamlDotNet.RepresentationModel;

namespace DarkConfig {
    public class ConfigFileManager {
        delegate void SimpleLoadDelegate(DocNode d);

        /// <summary>
        /// If true (the default), DarkConfig will scan files for changes every 
        /// *HotloadCheckInterval* seconds.  Setting it to false stops hotloading;
        /// recommended for production games.
        /// </summary>
        public bool IsHotloadingFiles {
            get { return m_isHotloadingFiles; }
            set {
                m_isHotloadingFiles = value;
                if (m_isHotloadingFiles && m_watchFilesCoro == null) {
                    m_watchFilesCoro = WatchFilesCoro();
                    Platform.Instance.StartCoroutine(m_watchFilesCoro);
                } else if(!m_isHotloadingFiles && m_watchFilesCoro != null) {
                    Platform.Instance.StopCoroutine(m_watchFilesCoro);
                    m_watchFilesCoro = null;
                }
            }
        }

        /// <summary>
        /// How often, in seconds, to scan files for changes.
        /// </summary>
        public float HotloadCheckInterval = 2f;

        /// <summary>
        /// List of files in the index file.  This is all the files that DarkConfig can load at the time of access.
        /// Contents may change during preloading.  Do not modify list. (readonly lists are not supported by the 
        /// version of Mono that Unity bundles)
        /// </summary>
        public List<string> Files {
            get { return m_configFiles; }
        }

        /// <summary>
        /// Returns a dictionary of the files that are currently loaded.
        /// </summary>
        public Dictionary<string, ConfigFileInfo> FileInfos {
            get { return m_loadedFiles; }
        }

        /// <summary>
        /// This event is called for every file that gets hotloaded.
        /// </summary>
        public System.Action<string> OnHotloadFile;

        /// <summary>
        /// Loads index file and start loading all config files.  Must call
        /// this (via Config.Preload, not directly) before using anything else
        /// in DarkConfig.
        /// </summary>
        public void Preload(System.Action callback = null) {
            if(m_isPreloaded || m_isPreloading) return;
            m_isPreloading = true;

            m_loadedFiles.Clear();
            m_configFiles.Clear();

            Config.Log(LogVerbosity.Info, "Preloading", m_sources.Count, "sources");
            for(int i = 0; i < m_sources.Count; i++) {
                var source = m_sources[i];
                if(!source.CanLoadNow()) continue;
                
                Config.Log(LogVerbosity.Info, "Using source", source);

                source.Preload(()=> {
                    m_isPreloading = false;
                    m_isPreloaded = true;

                    var files = source.GetFiles();
                    foreach(var finfo in files) {
                        m_configFiles.Add(finfo.Name);
                        m_loadedFiles.Add(finfo.Name, finfo);
                    }

                    // put files in all other sources
                    for(int j = 0; j < m_sources.Count; j++) {
                        if(m_sources[j] != source) m_sources[j].ReceivePreloaded(files);
                    }

                    Config.Log(LogVerbosity.Info, "Done preloading, IsHotloadingFiles: ", IsHotloadingFiles);

                    if(IsHotloadingFiles) {
                        Platform.Instance.StartCoroutine(WatchFilesCoro());
                    }

                    if(callback != null) callback();
                });
                break;
            }
        }

        /// <summary>
        /// Adds a source of config files, to be consulted when loading or hotloading.
        /// </summary>
        public void AddSource(ConfigSource source) {
            m_sources.Add(source);
        }

        /// <summary>
        /// Returns the number of sources currently added.
        /// </summary>
        public int CountSources() {
            return m_sources.Count;
        }

        /// <summary>
        /// Load a config file into a DocNode and return it directly.
        /// </summary>
        public DocNode LoadConfig(string configName) {
            CheckPreload();
            if(!m_loadedFiles.ContainsKey(configName)) {
                throw new ConfigFileNotFoundException(configName);
            }
            return m_loadedFiles[configName].Parsed;
        }

        /// <summary>
        /// Load a config file and call *cb* immediately with the contents.  This also registers *cb* to
        /// be called every time the file is hotloaded.
        /// </summary>
        public void LoadConfig(string configName, ReloadDelegate cb) {
            CheckPreload();
            if(!m_loadedFiles.ContainsKey(configName)) {
                throw new ConfigFileNotFoundException(configName);
            }
            bool save = cb(m_loadedFiles[configName].Parsed);
            if(save) {
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
        public void RegisterCombinedFile(List<string> sourceFilenames, string newFilename, System.Func<List<DocNode>, DocNode> combiner) {
            CheckPreload();

            // clobber any existing setup for this filename
            if (m_combiners.ContainsKey(newFilename)) UnregisterCombinedFile(newFilename);

            var listener = new CombinerData {
                Filenames = sourceFilenames.ToArray(),
                Combiner = combiner,
                DestinationFilename = newFilename
            };
            m_combiners[newFilename] = listener;
            for (int i = 0; i < sourceFilenames.Count; i++) {
                var filename = sourceFilenames[i];
                if (!m_combinersBySubfile.ContainsKey(filename)) {
                    m_combinersBySubfile[filename] = new List<CombinerData>();
                }
                var list = m_combinersBySubfile[sourceFilenames[i]];
                if (!list.Contains(listener)) {
                    list.Add(listener);
                }
            }

            if (m_isPreloaded) {
                m_loadedFiles[newFilename] = new ConfigFileInfo{
                    Name = newFilename,
                    Parsed = BuildCombinedConfig(newFilename)
                };
            }
        }

        /// <summary>
        /// Stops producing a combined file, specified by name.
        /// </summary>
        public void UnregisterCombinedFile(string combinedConfigName) {
            CheckPreload();

            var combinedFilename = combinedConfigName;

            if (!m_combiners.ContainsKey(combinedFilename)) return;

            var mc = m_combiners[combinedFilename];

            for (int i = 0; i < mc.Filenames.Length; i++) {
                var list = m_combinersBySubfile[mc.Filenames[i]];
                list.Remove(mc);
            }
            m_combiners.Remove(combinedFilename);
        }

        /// <summary>
        /// Returns a list of files in the index that match a glob pattern.  Glob patterns work in a Unix-esque fashion:
        ///   '*' matches any sequence of characters, but stops at slashes
        ///   '?' matches a single character, except a slash
        ///   '**' matches any sequence of characters, including slashes
        /// </summary>
        public List<string> GetFilesByGlob(string glob) {
            CheckPreload();
            return GetFilesByGlobImpl(glob, m_configFiles);
        }

        /// <summary>
        /// Returns a list of files in the index that match a regular expression.
        /// </summary>
        public List<string> GetFilesByRegex(System.Text.RegularExpressions.Regex pattern) {
            CheckPreload();
            return GetFilesByRegexImpl(pattern, m_configFiles);
        }

        /// <summary>
        /// Don't call directly, used for tests.
        /// </summary>
        public List<string> GetFilesByGlobImpl(string glob, List<string> files) {
            var restring = System.Text.RegularExpressions.Regex.Escape(glob)
                .Replace(@"\*\*", @".*")
                .Replace(@"\*", @"[^/]*")
                .Replace(@"\?", @"[^/]");
            var re = new System.Text.RegularExpressions.Regex(
                "^" + restring + "$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
            return GetFilesByRegexImpl(re, files);
        }

        /// <summary>
        /// Don't call directly, used for tests.
        /// </summary>
        public List<string> GetFilesByRegexImpl(System.Text.RegularExpressions.Regex pattern, List<string> files) {
            var result = new List<string>();
            for (int i = 0; i < files.Count; i++) {
                if (pattern.IsMatch(files[i])) {
                    result.Add(files[i]);
                }
            }
            return result;
        }

        internal void RegisterReload(string filename, ReloadDelegate cb) {
            List<ReloadDelegate> delegates;
            if(!m_reloadCallbacks.TryGetValue(filename, out delegates)) {
                delegates = new List<ReloadDelegate>();
                m_reloadCallbacks[filename] = delegates;
            }
            if(!delegates.Contains(cb)) {
                delegates.Add(cb);
            }
        }

        internal int GetReloadDelegateCount() {
            int count = 0;
            var iter = m_reloadCallbacks.GetEnumerator();
            while(iter.MoveNext()) {
                count += iter.Current.Value.Count;
            }
            return count;
        }

        internal void CallAllDelegates() {
            List<string> modified = new List<string>();
            foreach(var kv in m_reloadCallbacks) {
                modified.Add(kv.Key);
            }
            CallCallbacks(modified);
        }

        void CheckPreload() {
#if UNITY_EDITOR
            UnityPlatform.Setup();
#endif

            if(!Platform.Instance.CanDoImmediatePreload) {
                Config.Assert(m_isPreloaded, "Can't use configs in any way in a built game, before preloading is complete");
                return;
            }

            // we can preload immediately; this means that the developer doesn't have to go through a loading screen for every scene; just hit play
            if(m_isPreloaded || m_isPreloading) return;

            if(m_sources.Count == 0) {
                LoadFromSourceImmediately(Platform.Instance.GetDefaultSource());
            } else {
                bool preloadWasImmediate = false;
                Preload(() => { preloadWasImmediate = true; }); // note: all preloading is immediate
                Config.Log(LogVerbosity.Info, "Done immediate-loading, IsHotloadingFiles: ", IsHotloadingFiles);
                Config.Assert(preloadWasImmediate, "Did not preload immediately");
            }

            Config.PreloadComplete();
        }

        /// <summary>
        /// Loads all files from the source immediately.  For editor tooling.
        /// </summary>
        public void LoadFromSourceImmediately(ConfigSource source) {
            Config.Assert(Platform.Instance.CanDoImmediatePreload, "Trying to load immediately on a platform that doesn't support it");
            m_isPreloading = true;
            Config.Log(LogVerbosity.Info, "Immediate-loading " + source);
            
            source.Preload(() => {});  // assume that this is immediate
            var files = source.GetFiles();
            foreach(var finfo in files) {
                m_configFiles.Add(finfo.Name);
                m_loadedFiles.Add(finfo.Name, finfo);
            }

            m_isPreloading = false;
            m_isPreloaded = true;
        }

        DocNode ParseAndStoreFile(string configName, string contents, int checksum) {
            var doc = Config.LoadDocFromString(contents, configName);
            m_configFiles.Add(configName);
            m_loadedFiles.Add(configName, new ConfigFileInfo {
                Name = configName,
                Size = contents.Length,
                Parsed = doc,
                Checksum = checksum
                });
            return doc;
        }

        /// <summary>
        /// Utility function that returns an integer checksum of a string.
        /// Used to compare file bodies to know whether they've been hotloaded
        /// or not.
        /// </summary>
        public static int Checksum(string body) {
            byte[] input = System.Text.Encoding.UTF8.GetBytes(body);
            using (MemoryStream stream = new MemoryStream(input)) {
                int hash = MurMurHash3.Hash(stream);
                return hash;
            }
        }

        /// <summary>
        /// Utility function that returns an integer checksum of a stream.
        /// Used to compare file bodies to know whether they've been hotloaded
        /// or not.
        /// </summary>
        public static int Checksum(Stream stream) {
            return MurMurHash3.Hash(stream);
        }

        /// <summary>
        /// Immediately checks all config files to see whether they can be
        /// hotloaded.  This may take tens or hundreds of milliseconds, but
        /// when it's complete every file will have been checked and hotloaded
        /// if necessary.  Calls callback when done.
        /// </summary>
        public void CheckHotloadImmediate(System.Action callback = null) {
            // deliberately ignore value of m_isCheckingHotloadNow
            var iter = CheckHotloadCoro(callback, 100);
            while(iter.MoveNext()) {
            }
        }

        /// <summary>
        /// Starts a coroutine to check every file to see whether it can be
        /// hotloaded, N files per frame.  It calls the callback when done
        /// with all files.  If one of the coroutines is already running when
        /// you call this, it will early exit (without calling the callback).
        /// </summary>
        public void CheckHotload(System.Action callback = null, int filesPerFrame = 1) {
            if(m_isCheckingHotloadNow) return;
            Platform.Instance.StartCoroutine(CheckHotloadCoro(callback, filesPerFrame));
        }

        IEnumerator CheckHotloadCoro(System.Action cb = null, int filesPerLoop = 1) {
            m_isCheckingHotloadNow = true;

            try {
                // kind of a brute-force implementation for now: look at each file and see whether it changed
                List<string> modifiedFiles = new List<string>();

                for (int k = 0; k < m_configFiles.Count; k++) {
                    if(!IsHotloadingFiles) yield break;
                    var configName = m_configFiles[k];
                    try {
                        var newInfo = CheckHotload(configName);
                        if(newInfo != null) {
                            modifiedFiles.Add(configName);
                            m_loadedFiles[configName] = newInfo;
                        }
                    } catch(Exception e) {
                        Config.Log(LogVerbosity.Error, "Exception loading file", configName, e);
                    }
                    if((k % filesPerLoop) == 0) {
                        // throttle how many files we check per frame to control the performance impact
                        yield return null;
                    }
                }
                CallCallbacks(modifiedFiles);
                if(cb != null) cb();
            }
            finally {
                m_isCheckingHotloadNow = false;
            }
        }

        internal ConfigFileInfo CheckHotload(string configName) {
            ConfigFileInfo finfo = null;
            lock(m_loadedFiles) {
                finfo = m_loadedFiles[configName];
            }

            for(int i = 0; i < m_sources.Count; i++) {
                var source = m_sources[i];
                if(!source.CanHotload()) continue;

                var newInfo = source.TryHotload(finfo);

                if(newInfo != null) {
                    Config.Log(LogVerbosity.Info, "Hotloaded file " + newInfo + " old: " + finfo);

                    if(newInfo.Name == "index") {
                        // make sure that we sync up our list of loaded files
                        HotloadIndex(source);
                    }
                    if(OnHotloadFile != null) OnHotloadFile(newInfo.Name);
                    return newInfo;
                }
            }
            return null;
        }

        void HotloadIndex(ConfigSource source) {
            var files = source.GetFiles();
            foreach(var finfo in files) {
                if(!m_configFiles.Contains(finfo.Name)) m_configFiles.Add(finfo.Name);
                bool isNewFile = !m_loadedFiles.ContainsKey(finfo.Name);
                m_loadedFiles[finfo.Name] = finfo;
                if(isNewFile && OnHotloadFile != null) OnHotloadFile(finfo.Name);
            }
        }

        IEnumerator WatchFilesCoro() {
            try {
                while (IsHotloadingFiles) {
                    while (!m_isPreloaded) yield return Platform.Instance.WaitForSeconds(0.1f);
                    yield return Platform.Instance.WaitForSeconds(HotloadCheckInterval);

                    yield return Platform.Instance.StartCoroutine(CheckHotloadCoro());
                }
            } finally {
                m_watchFilesCoro = null;
            }
        }

        DocNode BuildCombinedConfig(string filename) {
            if (m_combiners.ContainsKey(filename)) {
                var multifile = m_combiners[filename];
                var subdocs = new List<DocNode>(multifile.Filenames.Length);
                for (int i = 0; i < multifile.Filenames.Length; i++) {
                    var subfilename = multifile.Filenames[i];
                    if (subfilename == filename) continue; // prevent trivial infinite loops
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

                if (m_combinersBySubfile.ContainsKey(filename)) {
                    var multicallbacks = m_combinersBySubfile[filename];
                    for(int j = 0; j < multicallbacks.Count; j++) {
                        var mc = multicallbacks[j];
                        var shortName = mc.DestinationFilename;
                        m_loadedFiles[shortName] = new ConfigFileInfo {
                            Name = shortName,
                            Parsed = BuildCombinedConfig(mc.DestinationFilename)
                        };
                        modifiedFiles.Add(mc.DestinationFilename);
                    }
                }
            }

            // call callbacks for modified files
            for (int i = 0; i < modifiedFiles.Count; i++) {
                var filename = modifiedFiles[i];
                if (!m_reloadCallbacks.ContainsKey(filename)) continue;
                var callbacks = m_reloadCallbacks[filename];
                for(int j = 0; j < callbacks.Count; j++) {
                    var doc = LoadConfig(filename);
                    var save = callbacks[j](doc);
                    if (!save) {
                        callbacks.RemoveAt(j);
                        j--;
                    }
                }
            }
        }

        ////////////////////////////////////////////////////////////////

        bool m_isPreloading = false;
        bool m_isPreloaded = false;
        bool m_isHotloadingFiles = true;
        IEnumerator m_watchFilesCoro = null;

        bool m_isCheckingHotloadNow = false;

        internal bool IsPreloaded { get { return m_isPreloaded; } }

        List<string> m_configFiles = new List<string>();
        Dictionary<string, ConfigFileInfo> m_loadedFiles = new Dictionary<string, ConfigFileInfo>();
        Dictionary<string, List<ReloadDelegate>> m_reloadCallbacks = new Dictionary<string, List<ReloadDelegate>>();

        Dictionary<string, CombinerData> m_combiners = new Dictionary<string, CombinerData>();
        Dictionary<string, List<CombinerData>> m_combinersBySubfile = new Dictionary<string, List<CombinerData>>();

        class CombinerData {
            public string[] Filenames;
            public string DestinationFilename;
            public System.Func<List<DocNode>, DocNode> Combiner;
        }

        List<ConfigSource> m_sources = new List<ConfigSource>();
    }
}