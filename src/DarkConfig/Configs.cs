using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

    /// <summary>
    /// A callback to be called when a file is hotloaded.
    /// </summary>
    /// <param name="doc">The new file DocNode</param>
    /// <returns>False if the delegate should be un-registered for future reload callbacks.  True otherwise.</returns>
    public delegate bool ReloadDelegate(DocNode doc);

    public static class Configs {
        const string LOG_GUARD = "DC_LOGGING_ENABLED";
        const string ASSERT_GUARD = "DC_ASSERTS_ENABLED";
        const string LogPrefix = "[DarkConfig] ";
        
        public delegate void AssertFunc(bool test, string message);
        public delegate void LogFunc(LogVerbosity verbosity, string message);

        /////////////////////////////////////////////////
        
        /// Configuration settings for Dark Config itself.
        public static Settings Settings = new Settings();

        public static ConfigFileManager FileManager { get; private set; } = new ConfigFileManager();
        
        public static AssertFunc AssertCallback = null;
        public static LogFunc LogCallback = null;
        
        /// True if config file preloading is complete, false otherwise.
        public static bool IsPreloaded => FileManager.IsPreloaded;

        /////////////////////////////////////////////////

        /// Event that's called once preloading is complete.
        /// Adding a delegate to this event after preloading has completed will call the new delegate immediately.
        public static event Action OnPreload {
            add {
                _OnPreload += value;
                if (IsPreloaded) {
                    value();
                }
            }
            remove => _OnPreload -= value;
        }

        /// <summary>
        /// Preloads all config files into memory.
        /// Must be completed before using any other DarkConfig functionality.
        /// </summary>
        /// <param name="callback">(optional) Called once preloading is complete</param>
        public static void Preload(Action callback = null) {
            if (callback != null) {
                OnPreload += callback;
            }
            FileManager.Preload();
            _OnPreload?.Invoke();
        }

        /// <summary>
        /// Load a file and register a reload callback.
        /// 
        /// The callback is called immediately when Load is called, and every time the file contents change.
        /// The callback function should return false to unsubscribe itself from future calls, true otherwise.
        /// 
        /// Preloading must be complete before calling Load.
        /// </summary>
        /// <param name="filename">The filename to load</param>
        /// <param name="callback">
        /// The reload callback to register.
        /// Called immediately with the initial file contents.
        /// </param>
        public static void Load(string filename, ReloadDelegate callback) {
            FileManager.LoadConfig(filename, callback);
        }

        /// Load the configuration from *filename*.
        /// 
        /// Preloading must be complete before calling Load.
        public static DocNode Load(string filename) {
            return FileManager.LoadConfig(filename);
        }

        /// <summary>
        /// Use a config file to update an object.
        ///
        /// Registers the object for updates whenever the config file changes in the future.
        /// To avoid leaking memory, updates cease when *obj* compares to null -- appropriate
        /// for MonoBehaviours.
        ///
        /// Preloading must be complete before calling Apply
        /// </summary>
        /// <param name="filename">Config filename</param>
        /// <param name="obj">Object to update</param>
        /// <typeparam name="T">Type of object to update</typeparam>
        public static void Apply<T>(string filename, ref T obj) {
            Reify(ref obj, FileManager.LoadConfig(filename));
            if (obj == null) {
                return;
            }
            
            var weakReference = new WeakReference(obj);
            FileManager.RegisterReloadCallback(filename, doc => {
                var t = (T) weakReference.Target;
                if (t == null) {
                    // The object was GC'd
                    return false;
                }
                Reify(ref t, doc);
                return true;
            });
        }
        
        /// <summary>
        /// Use a config file to update an object.
        /// 
        /// It is not a ref parameter, so it's suitable for use with the 'this' keyword.
        /// 
        /// Preloading must be complete before calling ApplyThis.
        /// </summary>
        /// <param name="filename">Config filename</param>
        /// <param name="obj">Object to update</param>
        /// <typeparam name="T">Type of object to update</typeparam>
        public static void ApplyThis<T>(string filename, T obj) {
            Apply(filename, ref obj);
        }
        
        /// <summary>
        /// Use a config file to update the static members of a type.
        /// 
        /// Preloading must be complete before calling ApplyStatic.
        /// </summary>
        /// <param name="filename">Config filename</param>
        /// <typeparam name="T">Type to set static members on</typeparam>
        public static void ApplyStatic<T>(string filename) {
            ReifyStatic<T>(FileManager.LoadConfig(filename));
            FileManager.RegisterReloadCallback(filename, d => {
                ReifyStatic<T>(d);
                return true;
            });
        }
        
        /// <summary>
        /// Cleans up DarkConfig's state, removing all listeners, loaded files, and so on.
        /// Does not reset Settings values.
        /// </summary>
        public static void Clear() {
            _OnPreload = null;
            configReifier = new Internal.ConfigReifier();
            FileManager = new ConfigFileManager();
            LogCallback = null;
            AssertCallback = null;
        }

        /// <summary>
        /// A function that loads multiple files and delivers it as a single list.
        /// Each file's contents becomes an entry in the list, or if a file contains a list,
        /// it is flattened into the combined doc.
        /// 
        /// The callback is called with the combined config data immediately and also
        /// whenever any of the matching files changes.
        /// </summary>
        /// <param name="glob">Glob describing the files to load</param>
        /// <param name="callback">
        /// Called when the files are loaded.
        /// Registered as a reload callback for the merged files.
        /// </param>
        public static void LoadFilesAsList(string glob, ReloadDelegate callback) {
            var matchingFiles = FileManager.GetFilenamesMatchingGlob(glob);
            string destFile = glob + "_file";
            FileManager.RegisterCombinedFile(matchingFiles, destFile, CombineList);
            FileManager.LoadConfig(destFile, callback);
        }

        /// <summary>
        /// A function that loads multiple files and delivers it as a single dictionary.
        /// Each file's contents should be a dictionary, and the resulting dictionary
        /// merges all the keys from all the dictionaries.
        /// Duplicate keys are overridden by later files in the index,
        /// same as if they were later keys in the same file.
        ///
        /// The callback is called with the combined config data immediately and also
        /// whenever any of the matching files changes. 
        /// </summary>
        /// <param name="glob">Glob describing the files to load</param>
        /// <param name="callback">
        /// Called when the files are loaded.
        /// Registered as a reload callback for the merged files.
        /// </param>
        public static void LoadFilesAsMergedDict(string glob, ReloadDelegate callback) {
            string combinedFilename = glob + "_file";
            FileManager.RegisterCombinedFile(FileManager.GetFilenamesMatchingGlob(glob), combinedFilename, CombineDict);
            FileManager.LoadConfig(combinedFilename, callback);
        }

        /// <summary>
        /// Low-level function for combining a list of DocNode lists into a single DocNode list.
        /// Useful for combining multiple files into a single DocNode.
        /// </summary>
        /// <param name="docs">A list of DocNodes to combine. All DocNodes must be lists</param>
        /// <returns>The combined DocNode</returns>
        public static DocNode CombineList(List<DocNode> docs) {
            string sourceInformation = "Combination of: [";
            for (int i = 0; i < docs.Count; i++) {
                if (i > 0) {
                    sourceInformation += ", ";
                }

                sourceInformation += docs[i].SourceInformation;
            }
            sourceInformation += "]";

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

        /// <summary>
        /// Low-level function to combine a list of DocNode dictionaries into a single DocNode dictionary.
        /// Useful for combining multiple files into a single DocNode.
        /// </summary>
        /// <param name="docs">A list of DocNodes to combine. All DocNodes must be dictionaries</param>
        /// <returns>The combined DocNode</returns>
        public static DocNode CombineDict(List<DocNode> docs) {
            string sourceInformation = "Combination of: [";
            for (int i = 0; i < docs.Count; i++) {
                if (i > 0) {
                    sourceInformation += ", ";
                }
                sourceInformation += docs[i].SourceInformation;
            }
            sourceInformation += "]";

            var result = new ComposedDocNode(DocNodeType.Dictionary, sourceInformation: sourceInformation);
            foreach (var doc in docs) {
                Assert(doc.Type == DocNodeType.Dictionary,
                    "Expected all DocNodes to be dictionaries in CombineDict.");
                foreach (var kv in doc.Pairs) {
                    result[kv.Key] = kv.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// Low-level function to read a YAML string into a DocNode.
        /// </summary>
        /// <param name="contents">the YAML to read</param>
        /// <param name="filename">A filename used for error reporting</param>
        /// <returns>The parsed DocNode</returns>
        public static DocNode LoadDocFromString(string contents, string filename) {
            return LoadDocFromTextReader(new StringReader(contents), filename);
        }

        /// <summary>
        /// Low-level function to read a YAML stream into a DocNode.
        /// </summary>
        /// <param name="stream">a stream of the YAML to read</param>
        /// <param name="filename">A filename used for error reporting</param>
        /// <returns>The parsed DocNode</returns>
        public static DocNode LoadDocFromStream(Stream stream, string filename) {
            return LoadDocFromTextReader(new StreamReader(stream), filename);
        }

        /// <summary>
        /// Register a handler for loading a particular type.
        /// 
        /// Useful for types where you can't easily add a FromDoc static method.
        /// </summary>
        /// <param name="fromDoc">Custom config parsing function for the type</param>
        /// <typeparam name="T">Type to register the custom loader for</typeparam>
        public static void RegisterFromDoc<T>(FromDocDelegate fromDoc) {
            RegisterFromDoc(typeof(T), fromDoc);
        }

        /// <summary>
        /// Register a handler for loading a particular type.
        /// Useful for types where you can't easily add a FromDoc static method.
        /// </summary>
        /// <param name="type">Type to register the custom loader for</param>
        /// <param name="fromDoc">Custom config parsing function for the type</param>
        public static void RegisterFromDoc(Type type, FromDocDelegate fromDoc) {
            configReifier.CustomReifiers[type] = fromDoc;
        }

        /// <summary>
        /// Sets an object's public, private and static members based on the contents of the parsed document.
        /// </summary>
        /// <param name="obj">The object to update.  If null, it will be set to a new instance of the type.</param>
        /// <param name="doc">The config doc to read.</param>
        /// <param name="options">(optional) Override default and type-defined reification behavior.</param>
        /// <typeparam name="T">The type of the object to reify.</typeparam>
        /// <example>
        /// If obj is a Thing:
        /// <code>
        /// public class Thing {
        ///     float m1;
        ///     string m2;
        /// }
        /// </code>
        /// 
        /// You can create a new instance, or set an existing instance's fields with the document:
        /// <code>
        /// {"m1":1.0, "m2":"test"}
        /// </code>
        /// </example>
        public static void Reify<T>(ref T obj, DocNode doc, ReificationOptions? options = null) {
            Reify(ref obj, typeof(T), doc, options);
        }
        
        /// <summary>
        /// Sets the public, private and static members of a given type on the given object
        /// based on the contents of the parsed document.
        ///
        /// This is mostly useful for reifying concrete instances into base-class references.
        /// </summary>
        /// <param name="obj">The object to update.  If null, it will be set to a new instance of the type.</param>
        /// <param name="objType">The concrete type to reify</param>
        /// <param name="doc">The config doc to read.</param>
        /// <param name="options">(optional) Override default and type-defined reification behavior.</param>
        /// <typeparam name="T">The type of the reference to set or update.</typeparam> 
        /// <example>
        /// Given this class hierarchy
        /// <code>
        /// public class Base {
        ///     float m1;
        /// }
        /// public class Derived : Base {
        ///     string m2;
        /// }
        /// </code>
        /// 
        /// You can reify an instance of Derived into a Base reference with the document:
        /// <code>
        ///     {"m1":1.0, "m2":"test"}
        /// </code>
        /// by calling
        /// <code>
        /// Base b;
        /// Config.Reify(ref b, typeof(Derived), doc);
        /// </code>
        /// </example>
        public static void Reify<T>(ref T obj, Type objType, DocNode doc, ReificationOptions? options = null) {
            obj = (T) configReifier.ReadValueOfType(objType, obj, doc, options);
        }

        /// <summary>
        /// Sets the static members of a type based on the contents of a parsed file.
        /// Ignores any non-static members specified in the parsed file.
        /// </summary>
        /// <param name="doc">The config doc to read.</param>
        /// <param name="options">(optional) Override default and type-defined reification behavior.</param>
        /// <typeparam name="T">The type who's static members should be updated.</typeparam>
        public static void ReifyStatic<T>(DocNode doc, ReificationOptions? options = null) {
            ReifyStatic(typeof(T), doc, options);
        }

        /// <summary>
        /// Sets the static members of a type based on the contents of a parsed file.
        /// Ignores any non-static members specified in the parsed file.
        ///
        /// This override is useful because static classes can't be used in generic calls.
        /// </summary>
        /// <param name="type">The type who's static members should be updated.</param>
        /// <param name="doc">The config doc to read.</param>
        /// <param name="options">(optional) Override default and type-defined reification behavior.</param>
        public static void ReifyStatic(Type type, DocNode doc, ReificationOptions? options = null) {
            object dummyObj = null;
            configReifier.SetFieldsOnObject(type, ref dummyObj, doc, options ?? Settings.DefaultReifierOptions);
        }

        /// <summary>
        /// Sets all public, private, and static members on a non-null object from a parsed config.
        /// </summary>
        /// <param name="obj">The object to update.  Must not be null</param>
        /// <param name="doc">The config doc to read.</param>
        /// <param name="options">(optional) Override default and type-defined reification behavior.</param>
        /// <typeparam name="T">The type of the object to update. Must be a class or a boxed struct type.</typeparam>
        public static void SetFieldsOnObject<T>(ref T obj, DocNode doc, ReificationOptions? options = null) where T : class {
            configReifier.SetFieldsOnObject(ref obj, doc, options);
        }

        /// <summary>
        /// Sets all public, private, and static members on a struct from a parsed config.
        /// </summary>
        /// <param name="obj">The struct to update</param>
        /// <param name="doc">The config doc to read.</param>
        /// <param name="options">(optional) Override default and type-defined reification behavior.</param>
        /// <typeparam name="T">The type of the struct to update.</typeparam>
        public static void SetFieldsOnStruct<T>(ref T obj, DocNode doc, ReificationOptions? options = null) where T : struct {
            configReifier.SetFieldsOnStruct(ref obj, doc, options);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogInfo(string message) {
            Log(LogVerbosity.Info, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogWarning(string message) {
            Log(LogVerbosity.Warn, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogError(string message) {
            Log(LogVerbosity.Error, message);
        }
        
        public static void Update(float dt) {
            FileManager.Update(dt);
        }

        /////////////////////////////////////////////////

        [System.Diagnostics.Conditional(ASSERT_GUARD)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Assert(bool test, string message) {
            if (AssertCallback != null) {
                AssertCallback(test, message);
            } else {
                DefaultAssertCallback(test, message);
            }
        }
        
        /////////////////////////////////////////////////

        static Action _OnPreload;
        static Internal.ConfigReifier configReifier = new Internal.ConfigReifier();

        /////////////////////////////////////////////////

        static DocNode LoadDocFromTextReader(TextReader reader, string filename) {
            var yaml = new YamlStream();
            yaml.Load(reader);
            return yaml.Documents.Count <= 0 ? new YamlDocNode(null, filename)
                : new YamlDocNode(yaml.Documents[0].RootNode, filename);
        }

        static void DefaultAssertCallback(bool test, string message) {
            System.Diagnostics.Debug.Assert(test, message);
        }
        
        [System.Diagnostics.Conditional(LOG_GUARD)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Log(LogVerbosity level, string msg) {
            if (level > Settings.LogLevel) {
                return;
            }
            if (LogCallback != null) {
                LogCallback(level, LogPrefix + msg);
            } else {
                DefaultLogCallback(level, LogPrefix + msg);
            }
        }
        
        static void DefaultLogCallback(LogVerbosity verbosity, string message) {
            if (verbosity == LogVerbosity.Info) {
                Console.Out.WriteLine(message);
            } else {
                Console.Error.WriteLine(message);
            }
        }
    }
}