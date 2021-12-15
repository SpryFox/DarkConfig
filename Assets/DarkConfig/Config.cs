using System.Collections.Generic;
using System;
using System.IO;
using YamlDotNet.RepresentationModel;

namespace DarkConfig {
    public delegate bool ReloadDelegate(DocNode d);

    public class ConfigFileNotFoundException : FileNotFoundException {
        public ConfigFileNotFoundException(string filename) : base("Couldn't find file " + filename + ". Perhaps it isn't in the index, or wasn't preloaded.", filename) { }
    }

    public class Config : ConfigReifier {
        public static ConfigFileManager FileManager => configFileManager = configFileManager ?? new ConfigFileManager();
        
        /// True if preloading is complete, false otherwise.
        public static bool IsPreloaded => FileManager.IsPreloaded;
        
        /////////////////////////////////////////////////

        /// Callback, gets called when preloading completes.
        public static event Action OnPreload {
            add {
                OnPreloadInvoker += value;
                if (IsPreloaded) {
                    value();
                }
            }
            remove => OnPreloadInvoker -= value;
        }

        /// Preloads the configuration files from the index into memory.  Must be completed
        /// before using any other DarkConfig functionality.
        /// 
        /// Optional callback argument is called once preloading is complete.
        public static void Preload(Action callback = null) {
            OnPreload += PreloadComplete;
            if (callback != null) {
                OnPreload += callback;
            }
            FileManager.Preload(OnPreloadInvoker);
        }

        /// Load the configuration from *filename* by calling a callback.
        /// 
        /// The callback *cb* is guaranteed to be called immediately when Load is called,
        /// and every time the file contents change.  The function should return false
        /// to unsubscribe itself from future calls, true otherwise.
        /// 
        /// Preloading must be complete before calling Load.
        public static void Load(string filename, ReloadDelegate cb) {
            FileManager.LoadConfig(filename, cb);
        }

        /// Load the configuration from *filename*.
        /// 
        /// Preloading must be complete before calling Load.
        public static DocNode Load(string filename) {
            return FileManager.LoadConfig(filename);
        }

        /// Use the configuration found in file *filename* to update *obj*.
        /// 
        /// Registers the object for updates whenever the config file changes in the future.
        /// To avoid leaking memory, updates cease when *obj* compares to null -- appropriate
        /// for MonoBehaviours.
        /// 
        /// Preloading must be complete before calling Apply
        public static void Apply<T>(string filename, ref T obj) {
            Reify(ref obj, FileManager.LoadConfig(filename));
            if (obj != null) {
                var wr = new WeakReference(obj);
                FileManager.RegisterReload(filename, d => {
                    var t = (T) wr.Target;
                    if (t == null) {
                        return false;
                    }
                    Reify(ref t, d);
                    return true;
                });
            }
        }

        /// Use the configuration found in file *filename* to update *obj*.
        /// It is not a ref parameter, so it's suitable for use with the 'this'
        /// keyword.
        /// Preloading must be complete before calling ApplyThis.
        public static void ApplyThis<T>(string filename, T obj) {
            Apply(filename, ref obj);
        }

        /// Use the configuration found in file *filename* to update the static fields of 
        /// class *T*.
        /// Preloading must be complete before calling ApplyStatic.
        public static void ApplyStatic<T>(string filename) {
            ReifyStatic<T>(FileManager.LoadConfig(filename));
            FileManager.RegisterReload(filename, d => {
                ReifyStatic<T>(d);
                return true;
            });
        }
        
        /// Utility function to find all subclasses of a class
        public static Dictionary<string, Type> FindSubclasses(Type t) {
            var subclasses = new Dictionary<string, Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
                foreach (var type in asm.GetTypes()) {
                    if (type.IsAbstract == false && t.IsAssignableFrom(type)) {
                        subclasses[type.Name.ToLower()] = type;
                    }
                }
            }

            return subclasses;
        }

        /// Cleans up DarkConfig's state, removing all listeners, loaded files, and so on, as if Preload had never been called.
        public static void Clear() {
            OnPreloadInvoker = null;
            CustomReifiers = new Dictionary<Type, FromDocDelegate>();
            Platform.Instance.Clear();
            configFileManager = null;
        }

        /// A function that loads multiple files and delivers it as a single
        /// list.  Each file's contents becomes an entry in the list, or if
        /// a file contains a list, it is flattened into the combined doc.  Ideal
        /// for having a directory of character documents.  The callback *cb*
        /// is called with the entire data structure immediately and also
        /// whenever any of the matching files changes.
        public static void LoadFilesAsList(string glob, ReloadDelegate cb) {
            var matchingFiles = FileManager.GetFilesByGlob(glob);
            var destFile = glob + "_file";
            FileManager.RegisterCombinedFile(matchingFiles, destFile, CombineList);
            FileManager.LoadConfig(destFile, cb);
        }

        public static DocNode CombineList(List<DocNode> docs) {
            string sourceInformation = null;
#if DARKCONFIG_ERROR_SOURCE_INFO
            var sb = new System.Text.StringBuilder("Combination of: [");
            for (int i = 0; i < docs.Count; i++) {
                if (i > 0) sb.Append(", ");
                sb.Append(docs[i].SourceInformation);
            }
            sb.Append("]");
            sourceInformation = sb.ToString();
#endif
            
            var result = new ComposedDocNode(DocNodeType.List, sourceInformation: sourceInformation);
            foreach (var t in docs) {
                if (t.Type == DocNodeType.List) { // flatten file containing list
                    for (int j = 0; j < t.Count; j++) {
                        result.Add(t[j]);
                    }
                } else {
                    result.Add(t);
                }
            }

            return result;
        }

        /// A function that loads multiple files and delivers it as a single
        /// dictionary.  Each file's contents should be a dictionary, and the
        /// resulting dictionary merges all the keys from all the
        /// dictionaries.  Duplicate keys are overridden by later files in the
        /// index, same as if they were later keys in the same file.
        public static void LoadFilesAsMergedDict(string glob, ReloadDelegate cb) {
            string destFile = glob + "_file";
            FileManager.RegisterCombinedFile(FileManager.GetFilesByGlob(glob), destFile, CombineDict);
            FileManager.LoadConfig(destFile, cb);
        }

        public static DocNode CombineDict(List<DocNode> docs) {
            string sourceInformation = null;
#if DARKCONFIG_ERROR_SOURCE_INFO
            var sb = new System.Text.StringBuilder("Combination of: [");
            for (int i = 0; i < docs.Count; i++) {
                if (i > 0) sb.Append(", ");
                sb.Append(docs[i].SourceInformation);
            }
            sb.Append("]");
            sourceInformation = sb.ToString();
#endif

            var result = new ComposedDocNode(DocNodeType.Dictionary, sourceInformation: sourceInformation);
            foreach (var doc in docs) {
                foreach (var kv in doc.Pairs) {
                    result[kv.Key] = kv.Value;
                }
            }

            return result;
        }

        /// Low-level function to read a YAML string into a DocNode.
        public static DocNode LoadDocFromString(string contents, string filename) {
            return LoadDocFromTextReader(new StringReader(contents), filename);
        }

        /// Low-level function to read a YAML stream into a DocNode.
        public static DocNode LoadDocFromStream(Stream stream, string filename) {
            return LoadDocFromTextReader(new StreamReader(stream), filename);
        }

        /////////////////////////////////////////////////
        
        static ConfigFileManager configFileManager;
        static Action OnPreloadInvoker;
        
        /////////////////////////////////////////////////

        internal static void PreloadComplete() {
            BuildInTypeRefiers.RegisterAll();
        }

        static DocNode LoadDocFromTextReader(TextReader reader, string filename) {
            var yaml = new YamlStream();
            yaml.Load(reader, filename);
            return yaml.Documents.Count <= 0 ? new YamlDocNode(null) : new YamlDocNode(yaml.Documents[0].RootNode);
        }
    }
}