using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace DarkConfig {
    public delegate bool ReloadDelegate(DocNode d);
    public delegate void AssertDelegate(bool test, params object[] messages);

    public class Config : ConfigReifier {
        /// <summary>
        /// True if preloading is complete, false otherwise.
        /// </summary>
        public static bool IsPreloaded {
            get { return FileManager.IsPreloaded; }
        }

        /// <summary>
        /// Callback, gets called when preloading completes.
        /// </summary>
        public static event System.Action OnPreload {
            add {
                OnPreloadInvoker += value; 
                if(IsPreloaded) value();
            }
            remove {
                OnPreloadInvoker -= value;
            }
        }
        // http://forum.unity3d.com/threads/executionengineexception-on-ios-only.113750/
        static System.Action OnPreloadInvoker;
        
        /// <summary>
        /// Preloads the configuration files from the index into memory.  Must be completed
        /// before using any other DarkConfig functionality.
        /// 
        /// Optional callback argument is called once preloading is complete.
        /// </summary>
        public static void Preload(System.Action callback = null) {
            OnPreload += PreloadComplete;
            if(callback != null) OnPreload += callback;
            FileManager.Preload(OnPreloadInvoker);
        }

        /// <summary>
        /// Load the configuration from *filename* by calling a callback.
        /// 
        /// The callback *cb* is guaranteed to be called immediately when Load is called,
        /// and every time the file contents change.  The function should return false
        /// to unsubscribe itself from future calls, true otherwise.
        /// 
        /// Preloading must be complete before calling Load.
        /// </summary>
        public static void Load(string filename, ReloadDelegate cb) {
            FileManager.LoadConfig(filename, cb);
        }

        /// <summary>
        /// Load the configuration from *filename*.
        /// 
        /// Preloading must be complete before calling Load.
        /// </summary>
        public static DocNode Load(string filename) {
            return FileManager.LoadConfig(filename);
        }

        /// <summary>
        /// Use the configuration found in file *filename* to update *obj*.
        /// 
        /// Registers the object for updates whenever the config file changes in the future.
        /// To avoid leaking memory, updates cease when *obj* compares to null -- appropriate
        /// for MonoBehaviours.
        /// 
        /// Preloading must be complete before calling Apply
        /// </summary>
        public static void Apply<T>(string filename, ref T obj) {
            Reify(ref obj, FileManager.LoadConfig(filename));
            if(obj != null) {
                var wr = new WeakReference(obj);
                FileManager.RegisterReload(filename, (d) => {
                    var t = (T)wr.Target;
                    if(t == null) return false;
                    Reify(ref t, d);
                    return true;
                });
            }
        }

        /// <summary>
        /// Use the configuration found in file *filename* to update *obj*.
        /// It is not a ref parameter, so it's suitable for use with the 'this'
        /// keyword.
        /// Preloading must be complete before calling ApplyThis.
        /// </summary>
        public static void ApplyThis<T>(string filename, T obj) {
            Apply<T>(filename, ref obj);
        }

        /// <summary>
        /// Use the configuration found in file *filename* to update the static fields of 
        /// class *T*.
        /// Preloading must be complete before calling ApplyStatic.
        /// </summary>
        public static void ApplyStatic<T>(string filename) {
            ReifyStatic<T>(FileManager.LoadConfig(filename));
            FileManager.RegisterReload(filename, (d) => {
                ReifyStatic<T>(d);
                return true;
            });
        }

        /// <summary>
        /// Hook for custom assert functions.  If set, DarkConfig calls this function for its assert checks.
        /// </summary>
        public static AssertDelegate AssertCallback;


        /// <summary>
        /// How agressively DarkConfig logs to Debug.Log.
        /// </summary>
        public static LogVerbosity Verbosity = LogVerbosity.Info;

        /// <summary>
        /// Utility function to find all subclasses of a class
        /// </summary>
        public static Dictionary<string, System.Type> FindSubclasses(System.Type t) {
            Dictionary<string, System.Type> subclasses = new Dictionary<string, System.Type>();
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) {
                foreach (var type in asm.GetTypes()) {
                    if (type.IsAbstract == false && t.IsAssignableFrom(type)) {
                        subclasses[type.Name.ToLower()] = type;
                    }
                }
            }
            return subclasses;
        }

        /// <summary>
        /// Cleans up DarkConfig's state, removing all listeners, loaded files, and so on, as if Preload had never been called.
        /// </summary>
        public static void Clear() {
            OnPreloadInvoker = null;
            s_fromDocs = new Dictionary<Type, FromDocDelegate>();
            Platform.Instance.Clear();
            s_files = null;
        }

        /// <summary>
        /// A function that loads multiple files and delivers it as a single
        /// list.  Each file's contents becomes an entry in the list, or if
        /// a file contains a list, it is flattened into the combined doc.  Ideal
        /// for having a directory of character documents.  The callback *cb*
        /// is called with the entire data structure immediately and also
        /// whenever any of the matching files changes.
        /// </summary>
        public static void LoadFilesAsList(string glob, ReloadDelegate cb) {
            var matchingFiles = FileManager.GetFilesByGlob(glob);
            var destFile = glob + "_file";
            FileManager.RegisterCombinedFile(matchingFiles, destFile, CombineList);
            FileManager.LoadConfig(destFile, cb);
        }

        public static DocNode CombineList(List<DocNode> docs) {
            var sb = new System.Text.StringBuilder("Combination of: [");
            for (int i = 0; i < docs.Count; i++) {
                if(i > 0) sb.Append(", ");
                sb.Append(docs[i].SourceInformation);
            }
            sb.Append("]");

            ComposedDocNode result = new ComposedDocNode(DocNodeType.List,
                sourceInformation: sb.ToString());
            for (int i = 0; i < docs.Count; i++) {
                if (docs[i].Type == DocNodeType.List) { // flatten file containing list
                    for (int j = 0; j < docs[i].Count; j++) {
                        result.Add(docs[i][j]);
                    }
                } else {
                    result.Add(docs[i]);
                }
            }
            return result;
        }


        /// <summary>
        /// A function that loads multiple files and delivers it as a single
        /// dictionary.  Each file's contents should be a dictionary, and the
        /// resulting dictionary merges all the keys from all the
        /// dictionaries.  Duplicate keys are overridden by later files in the
        /// index, same as if they were later keys in the same file.
        /// </summary>
        public static void LoadFilesAsMergedDict(string glob, ReloadDelegate cb) {
            var matchingFiles = FileManager.GetFilesByGlob(glob);
            var destFile = glob + "_file";
            FileManager.RegisterCombinedFile(matchingFiles, destFile, CombineDict);
            FileManager.LoadConfig(destFile, cb);
        }

        public static DocNode CombineDict(List<DocNode> docs) {
            var sb = new System.Text.StringBuilder("Combination of: [");
            for (int i = 0; i < docs.Count; i++) {
                if(i > 0) sb.Append(", ");
                sb.Append(docs[i].SourceInformation);
            }
            sb.Append("]");

            ComposedDocNode result = new ComposedDocNode(DocNodeType.Dictionary,
                sourceInformation: sb.ToString());
            for (int i = 0; i < docs.Count; i++) {
                var doc = docs[i];
                foreach (var kv in doc.Pairs) {
                    result[kv.Key] = kv.Value;
                }
            }
            return result;
        }

        /// <summary>
        /// Low-level function to read a YAML string into a DocNode.
        /// </summary>
        public static DocNode LoadDocFromString(string contents, string filename) {
            var input = new StringReader(contents);
            var yaml = new YamlStream();
            yaml.Load(input, filename);

            if(yaml.Documents.Count <= 0) {
                return new YamlDocNode(null);
            }
            return new YamlDocNode(yaml.Documents[0].RootNode);
        }

        /// <summary>
        /// Low-level function to read a YAML stream into a DocNode.
        /// </summary>
        public static DocNode LoadDocFromStream(Stream stream, string filename) {
            var input = new StreamReader(stream);
            var yaml = new YamlStream();
            yaml.Load(input, filename);

            if(yaml.Documents.Count <= 0) {
                return new YamlDocNode(null);
            }
            return new YamlDocNode(yaml.Documents[0].RootNode);
        }


        // variables //////////////////////////////////////////////

        public static ConfigFileManager FileManager {
            get {
                if (s_files == null) {
                    s_files = new ConfigFileManager();
                }
                return s_files;
            }
        }
        static ConfigFileManager s_files = null;

        // logging ////////////////////////////////////////////////
        public static void Log(LogVerbosity level, string msg) {
            if(level <= Verbosity) {
                var s = "DarkConfig: " + msg;
                if(level > LogVerbosity.Error) { Platform.Instance.Log(s); } else { Platform.Instance.LogError(s); }
            }
        }

        public static void Log(LogVerbosity level, object msg1) {
            if(level <= Verbosity) {
                var s = "DarkConfig: " + msg1.ToString();
                if(level > LogVerbosity.Error) { Platform.Instance.Log(s); } else { Platform.Instance.LogError(s); }
            }
        }

        public static void Log(LogVerbosity level, object msg1, object msg2) {
            if(level <= Verbosity) {
                var s = "DarkConfig: " + msg1.ToString() + " " + msg2.ToString();
                if(level > LogVerbosity.Error) { Platform.Instance.Log(s); } else { Platform.Instance.LogError(s); }
            }
        }

        public static void Log(LogVerbosity level, object msg1, object msg2, object msg3) {
            if(level <= Verbosity) {
                var s = "DarkConfig: " + msg1.ToString() + " " + msg2.ToString() + " " + msg3.ToString();
                if(level > LogVerbosity.Error) { Platform.Instance.Log(s); } else { Platform.Instance.LogError(s); }
            }
        }

        public static void Log(LogVerbosity level, object msg1, object msg2, object msg3, object msg4) {
            if(level <= Verbosity) {
                var s = "DarkConfig: " + msg1.ToString() + " " + msg2.ToString() + " " + msg3.ToString() + " " + msg4.ToString();
                if(level > LogVerbosity.Error) { Platform.Instance.Log(s); } else { Platform.Instance.LogError(s); }
            }
        }

        public static void Log(LogVerbosity level, object msg1, object msg2, object msg3, object msg4, object msg5) {
            if(level <= Verbosity) {
                var s = "DarkConfig: " + msg1.ToString() + " " + msg2.ToString() + " " + msg3.ToString() + " " + msg4.ToString() + " " + msg5.ToString();
                if(level > LogVerbosity.Error) { Platform.Instance.Log(s); } else { Platform.Instance.LogError(s); }
            }
        }

        public static void Log(LogVerbosity level, object msg1, object msg2, object msg3, object msg4, object msg5, object msg6) {
            if (level <= Verbosity) {
                var s = "DarkConfig: " + msg1.ToString() + " " + msg2.ToString() + " " + msg3.ToString() + " " + msg4.ToString() + " " + msg5.ToString() + " " + msg6.ToString();
                if(level > LogVerbosity.Error) { Platform.Instance.Log(s); } else { Platform.Instance.LogError(s); }
            }
        }

        // DarkConfig disables asserts when this flag is not set
        private const string s_assertGuard = "ASSERTS_ENABLED";

        [System.Diagnostics.Conditional(s_assertGuard)]
        public static void Assert(bool test, string message) {
            if (AssertCallback != null) {
                AssertCallback(test, message);
            } else {
                // here's a simple internal assertion implementation
                if(test == false) {
                    throw new AssertionException(message);
                }
            }
        }

        [System.Diagnostics.Conditional(s_assertGuard)]
        public static void Assert(bool test, object msg1) {
            if(!test) {
                Assert(false, msg1.ToString());
            }
        }

        [System.Diagnostics.Conditional(s_assertGuard)]
        public static void Assert(bool test, object msg1, object msg2) {
            if(!test) {
                Assert(false, msg1.ToString() + " " + msg2.ToString());
            }
        }

        [System.Diagnostics.Conditional(s_assertGuard)]
        public static void Assert(bool test, object msg1, object msg2, object msg3) {
            if(!test) {
                Assert(false, msg1.ToString() + " " + msg2.ToString() + " " + msg3.ToString());
            }
        }

        [System.Diagnostics.Conditional(s_assertGuard)]
        public static void Assert(bool test, object msg1, object msg2, object msg3, object msg4) {
            if(!test) {
                Assert(false, msg1.ToString() + " " + msg2.ToString() + " " + msg3.ToString() + " " + msg4.ToString());
            }
        }

        [System.Diagnostics.Conditional(s_assertGuard)]
        public static void Assert(bool test, object msg1, object msg2, object msg3, object msg4, object msg5) {
            if(!test) {
                Assert(false, msg1.ToString() + " " + msg2.ToString() + " " + msg3.ToString() + " " + msg4.ToString() + " " + msg5.ToString());
            }
        }
        
        [System.Diagnostics.Conditional(s_assertGuard)]
        public static void Assert(bool test, object msg1, object msg2, object msg3, object msg4, object msg5, object msg6) {
            if(!test) {
                Assert(false, msg1.ToString() + " " + msg2.ToString() + " " + msg3.ToString() + " " + msg4.ToString() + " " + msg5.ToString() + " " + msg6.ToString());
            }
        }
        
        [System.Diagnostics.Conditional(s_assertGuard)]
        public static void Assert(bool test, object msg1, object msg2, object msg3, object msg4, object msg5, object msg6, object msg7) {
            if(!test) {
                Assert(false, msg1.ToString() + " " + msg2.ToString() + " " + msg3.ToString() + " " + msg4.ToString() + " " + msg5.ToString() + " " + msg6.ToString() + " " + msg7.ToString());
            }
        }

        internal static void PreloadComplete() {
            DefaultFromDocs.RegisterAll();
        }
    }

    public class AssertionException : System.Exception {
        public AssertionException(string message) : base(message) {
        }
    }

    public class ConfigFileNotFoundException : System.IO.FileNotFoundException {
        public ConfigFileNotFoundException(string filename) : base("Could't find file " + filename + ". Perhaps it isn't in the index, or wasn't preloaded.", filename) {
        }
    }
}


public enum LogVerbosity {
    Error, Warn, Info
}