using System.Collections.Generic;
using System;
using System.IO;
using YamlDotNet.RepresentationModel;

namespace DarkConfig {
    /// <summary>
    /// A custom method for a specific type that takes a parsed config doc
    /// and sets the fields on an instance of that type.
    /// 
    /// It should attempt to update the object in-place, 
    /// or if that's not possible, to return a new instance
    /// of the correct type.
    /// </summary>
    /// <param name="obj">the existing object (if any)</param>
    /// <param name="doc">the DocNode that is meant to update the object</param>
    /// <returns>The updated/created object</returns>
    public delegate object FromDocDelegate(object obj, DocNode doc);

    public delegate bool ReloadDelegate(DocNode d);

    public static class Config {
        public static ConfigFileManager FileManager => configFileManager = configFileManager ?? new ConfigFileManager();
        static ConfigFileManager configFileManager;
        
        /// True if preloading is complete, false otherwise.
        public static bool IsPreloaded => FileManager.IsPreloaded;

        public static Settings Settings = new Settings();
        
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

        /// <summary>
        /// Use the configuration found in file *filename* to update *obj*.
        ///
        /// Registers the object for updates whenever the config file changes in the future.
        /// To avoid leaking memory, updates cease when *obj* compares to null -- appropriate
        /// for MonoBehaviours.
        ///
        /// Preloading must be complete before calling Apply
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="obj"></param>
        /// <typeparam name="T"></typeparam>
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
        
        /// Cleans up DarkConfig's state, removing all listeners, loaded files, 
        /// and so on, as if Preload had never been called.
        public static void Clear() {
            OnPreloadInvoker = null;
            Internal.ConfigReifier.CustomReifiers.Clear();
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
            string destFile = glob + "_file";
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

        /// Sets up *obj* based on the contents of the parsed document *doc*
        /// So if obj is a Thing:
        ///   public class Thing {
        ///      float m1;
        ///      string m2;
        ///   }
        ///
        /// You can create a new instance, or set an existing instance's fields with this parsed document:
        ///  {"m1":1.0, "m2":"test"}
        ///
        /// *obj* can be null; if it is it gets assigned a new instance based on its type and the contents of *doc* (this is why the parameter is a ref)
        /// 
        /// Works on static and private variables, too.
        public static void Reify<T>(ref T obj, DocNode doc, ReificationOptions? options = null) {
            Reify(ref obj, typeof(T), doc, options);
        }

        /// Sets up *obj* based on the contents of the parsed document *doc* with a type override.
        /// Useful for (eg) instantiating concrete classes of an interface based on a keyword.
        /// So if obj is a Thing:
        ///   public class Thing {
        ///      float m1;
        ///      string m2;
        ///   }
        ///
        /// You can create a new instance, or set an existing instance's fields with this parsed document:
        ///  {"m1":1.0, "m2":"test"}
        ///
        /// *obj* can be null; if it is it gets assigned a new instance based on its type and the contents of *doc* (this is why the parameter is a ref)
        /// 
        /// Works on static and private variables, too.
        public static void Reify<T>(ref T obj, Type objType, DocNode doc, ReificationOptions? options = null) {
            obj = (T) Internal.ConfigReifier.ReadValueOfType(objType, obj, doc, options);
        }

        /// Sets up static variables (and only static variables) on type *T* based on the contents of the parsed document *doc*
        ///
        /// Ignores any fields in *doc* that are for non-static fields.
        public static void ReifyStatic<T>(DocNode doc, ReificationOptions? options = null) {
            ReifyStatic(typeof(T), doc, options);
        }

        /// Same as ReifyStatic<T>, but with a type argument instead of a generic.
        /// Static classes can't be used in generics, so use this version instead.
        public static void ReifyStatic(Type type, DocNode doc, ReificationOptions? options = null) {
            object dummyObj = null;
            Internal.ConfigReifier.SetFieldsOnObject(type, ref dummyObj, doc, options ?? Settings.DefaultReifierOptions);
        }

        /// Register a handler for loading a particular type.
        public static void RegisterFromDoc<T>(FromDocDelegate del) {
            RegisterFromDoc(typeof(T), del);
        }

        /// Register a handler for loading a particular type.
        public static void RegisterFromDoc(Type t, FromDocDelegate del) {
            Internal.ConfigReifier.CustomReifiers[t] = del;
        }

        /// Sets all members on the object *obj* (which must not be null) from *dict*.
        /// Expects *obj* to be a plain class, but if it's a boxed struct it will work as well.
        public static void SetFieldsOnObject<T>(ref T obj, DocNode dict, ReificationOptions? options = null) where T : class {
            Internal.ConfigReifier.SetFieldsOnObject<T>(ref obj, dict, options);
        }

        /// Sets all members on the struct *obj* (which must not be null) from *dict*.
        public static void SetFieldsOnStruct<T>(ref T obj, DocNode dict, ReificationOptions? options = null) where T : struct {
            Internal.ConfigReifier.SetFieldsOnStruct(ref obj, dict, options);
        }

        /////////////////////////////////////////////////

        static Action OnPreloadInvoker;

        /////////////////////////////////////////////////

        internal static void PreloadComplete() {
            Internal.BuiltInTypeRefiers.RegisterAll();
        }

        static DocNode LoadDocFromTextReader(TextReader reader, string filename) {
            var yaml = new YamlStream();
            yaml.Load(reader, filename);
            return yaml.Documents.Count <= 0 ? new YamlDocNode(null) : new YamlDocNode(yaml.Documents[0].RootNode);
        }
    }
}