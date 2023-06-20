using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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
    public delegate object FromDocFunc(object obj, DocNode doc);
    
    public delegate object PostDocFunc(object obj);

    /// <summary>
    /// A callback to be called when a file is hotloaded.
    /// </summary>
    /// <param name="doc">The new file DocNode</param>
    /// <returns>False if the delegate should be un-registered for future reload callbacks.  True otherwise.</returns>
    public delegate bool HotloadCallbackFunc(DocNode doc);
    
    /// A callback when DarkConfig logs a message, warning or error.
    public delegate void LogFunc(LogVerbosity verbosity, string message);

    public static class Configs {
        const string LOG_GUARD = "DC_LOGGING_ENABLED";
        const string LogPrefix = "[DarkConfig] ";
        
        /////////////////////////////////////////////////
        
        /// Configuration settings for Dark Config itself.
        public static Settings Settings = new Settings();

        internal static Internal.ConfigFileManager FileManager { get; private set; } = new Internal.ConfigFileManager();
        
        public static LogFunc LogCallback;
        
        /// True if config file preloading is complete, false otherwise.
        public static bool IsPreloaded => FileManager.IsPreloaded;

        /////////////////////////////////////////////////
        
        #region ConfigSources
        /// <summary>
        /// Add a config file source.
        /// Sources are used when loading or hotloading.
        /// Multiple sources of config files can be registered.
        /// </summary>
        /// <param name="source">The new source to register</param>
        public static void AddConfigSource(ConfigSource source) {
            FileManager.sources.Add(source);
        }

        /// <summary>
        /// Remove a config file source.
        /// </summary>
        /// <param name="source">The source to </param>
        public static void RemoveConfigSource(ConfigSource source) {
            FileManager.sources.Remove(source);
        }

        /// Number of currently registered config sources
        public static int NumConfigSources => FileManager.sources.Count;

        /// Removes all config file sources.
        public static void ClearConfigSources() {
            FileManager.sources.Clear();
        }

        /// Enumerate all config file sources
        public static IEnumerable<ConfigSource> GetConfigSources() {
            return FileManager.sources;
        }
        #endregion
        
        #region Preloading
        /// <summary>
        /// Preloads all config files into memory.
        /// Preloading must be completed before using any other DarkConfig functionality.
        /// </summary>
        public static void Preload() {
            foreach (object _ in FileManager.StepPreload()) { }
        }

        /// <summary>
        /// Perform a single preloading step.
        /// Preloading must be completed before using any other DarkConfig functionality.
        /// Useful when you want to time-slice loading across multiple frames.
        /// Will yield break when preloading is complete.
        /// </summary>
        /// <returns>null while there's work left to do</returns>
        public static IEnumerable StepPreload() {
            foreach (object _ in FileManager.StepPreload()) {
                yield return null;
            }
        }

        /// If hotloading is enabled, triggers an immediate hotload.
        public static void DoImmediateHotload() {
            FileManager.DoImmediateHotload();
        }
        #endregion
        
        #region Container Utils
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
                if (doc.Type != DocNodeType.Dictionary) {
                    throw new ParseException("Expected all DocNodes to be dictionaries in CombineDict.");
                }
                foreach (var kv in doc.Pairs) {
                    result[kv.Key] = kv.Value;
                }
            }

            return result;
        }
        #endregion

        /// <summary>
        /// Cleans up DarkConfig's state, removing all listeners, loaded files, and so on.
        /// Does not reset Settings values.
        /// </summary>
        public static void Clear() {
            typeReifier = new Internal.TypeReifier();
            FileManager = new Internal.ConfigFileManager();
            processors = new List<ConfigProcessor>();
            LogCallback = null;
        }
        
        #region Files
        public static List<string> GetFilenamesMatchingGlob(string glob) {
            return FileManager.GetFilenamesMatchingGlob(glob);
        }
        
        public static List<string> GetFilenamesMatchingRegex(Regex pattern) {
            return FileManager.GetFilenamesMatchingRegex(pattern);
        }

        public static ConfigFileInfo GetFileInfo(string filename) {
            return FileManager.GetFileInfo(filename);
        }
        #endregion
        
        #region Parsing YAML
        /// <summary>
        /// Get the parsed contents of a preloaded file.
        /// 
        /// Preloading must be complete before calling this. 
        /// </summary>
        /// <param name="filename">The shortname of the file (filename without extension)</param>
        /// <returns>The parsed yaml data</returns>
        public static DocNode ParseFile(string filename) {
            return FileManager.ParseFile(filename);
        }

        /// <summary>
        /// Get the parsed contents of a preloaded file and register a callback to be called when the file is hotloaded.
        /// 
        /// The callback is called immediately with the parsed file contents, and again every time the file contents change.
        /// The callback function should return <c>false</c> to unsubscribe itself from future calls.  Otherwise it should return
        /// <c>true</c> to continue being called when the file is hotloaded.
        /// 
        /// Preloading must be complete before calling this.
        /// </summary>
        /// <param name="filename">The file to parse</param>
        /// <param name="callback">Reload callback to register. Called immediately with the parsed file data.</param>
        public static void ParseFile(string filename, HotloadCallbackFunc callback) {
            FileManager.ParseFile(filename, callback);
        }

        /// <summary>
        /// Parse a YAML string into a DocNode.
        /// </summary>
        /// <param name="yamlData">the YAML to parse</param>
        /// <param name="debugFilename">A filename used for error reporting</param>
        /// <returns>The parsed DocNode</returns>
        public static DocNode ParseString(string yamlData, string debugFilename) {
            return ParseYamlFromTextReader(new StringReader(yamlData), debugFilename);
        }

        /// <summary>
        /// Parse a YAML stream into a DocNode.
        /// </summary>
        /// <param name="stream">a stream of the YAML to read</param>
        /// <param name="filename">A filename used for error reporting</param>
        /// <param name="ignoreProcessors">Don't use config processors</param>
        /// <returns>The parsed DocNode</returns>
        public static DocNode ParseStream(Stream stream, string filename, bool ignoreProcessors = false) {
            return ParseYamlFromTextReader(new StreamReader(stream), filename, ignoreProcessors);
        }
        
        /// <summary>
        /// A function that parses multiple files and delivers it as a single list.
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
        public static void ParseFilesAsList(string glob, HotloadCallbackFunc callback) {
            string destFile = glob + "_file";
            FileManager.RegisterCombinedFile(FileManager.GetFilenamesMatchingGlob(glob), destFile, CombineList);
            FileManager.ParseFile(destFile, callback);
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
        public static void ParseFilesAsMergedDict(string glob, HotloadCallbackFunc callback) {
            string combinedFilename = glob + "_file";
            FileManager.RegisterCombinedFile(FileManager.GetFilenamesMatchingGlob(glob), combinedFilename, CombineDict);
            FileManager.ParseFile(combinedFilename, callback);
        }
        #endregion
        
        #region FromDocs
        /// <summary>
        /// Register a handler for loading a particular type.
        /// 
        /// Useful for types where you can't easily add a FromDoc static method.
        /// </summary>
        /// <param name="fromDoc">Custom config parsing function for the type</param>
        /// <typeparam name="T">Type to register the custom loader for</typeparam>
        public static void RegisterFromDoc<T>(FromDocFunc fromDoc) {
            RegisterFromDoc(typeof(T), fromDoc);
        }

        /// <summary>
        /// Register a handler for loading a particular type.
        /// Useful for types where you can't easily add a FromDoc static method.
        /// </summary>
        /// <param name="type">Type to register the custom loader for</param>
        /// <param name="fromDoc">Custom config parsing function for the type</param>
        public static void RegisterFromDoc(Type type, FromDocFunc fromDoc) {
            typeReifier.RegisteredFromDocs[type] = fromDoc;
        }
        #endregion
        
        #region PostDocs
        /// <summary>
        /// Register a post-serialization callback for a specific type.
        ///
        /// Useful for types where you can't easily add a PostDoc static method.
        /// </summary>
        /// <param name="postDoc">The post-serialization callback to invoke</param>
        /// <typeparam name="T">The type to register the callback for</typeparam>
        public static void RegisterPostDoc<T>(PostDocFunc postDoc) {
            RegisterPostDoc(typeof(T), postDoc);
        }

        /// <summary>
        /// Register a post-serialization callback for a specific type.
        ///
        /// Useful for types where you can't easily add a PostDoc static method.
        /// </summary>
        /// <param name="type">The type to register the callback for</param>
        /// <param name="postDoc">The post-serialization callback to invoke</param>
        public static void RegisterPostDoc(Type type, PostDocFunc postDoc) {
            typeReifier.RegisteredPostDocs[type] = postDoc;
        }
        #endregion

        #region Reify, Apply, SetFields
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
            Reify(ref obj, FileManager.ParseFile(filename));
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
            ReifyStatic<T>(FileManager.ParseFile(filename));
            FileManager.RegisterReloadCallback(filename, d => {
                ReifyStatic<T>(d);
                return true;
            });
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
            obj = (T) typeReifier.ReadValueOfType(objType, obj, doc, options);
        }

        /// <summary>
        /// Sets the static members of a type based on the contents of a parsed file.
        /// Ignores any non-static members specified in the parsed file.
        /// </summary>
        /// <param name="doc">The config doc to read.</param>
        /// <param name="options">(optional) Override default and type-defined reification behavior.</param>
        /// <typeparam name="T">The type who's static members should be updated.</typeparam>
        public static void ReifyStatic<T>(DocNode doc, ReificationOptions? options = null) {
            typeReifier.SetStaticMembers(typeof(T), doc, options ?? Settings.DefaultReifierOptions);
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
            typeReifier.SetStaticMembers(type, doc, options ?? Settings.DefaultReifierOptions);
        }

        /// <summary>
        /// Sets all public, private, and static members on a non-null object from a parsed config.
        /// </summary>
        /// <param name="obj">The object to update.  Must not be null</param>
        /// <param name="doc">The config doc to read.</param>
        /// <param name="options">(optional) Override default and type-defined reification behavior.</param>
        /// <typeparam name="T">The type of the object to update. Must be a class or a boxed struct type.</typeparam>
        public static void SetFieldsOnObject<T>(ref T obj, DocNode doc, ReificationOptions? options = null) where T : class {
            typeReifier.SetFieldsOnObject(ref obj, doc, options);
        }

        /// <summary>
        /// Sets all public, private, and static members on a struct from a parsed config.
        /// </summary>
        /// <param name="obj">The struct to update</param>
        /// <param name="doc">The config doc to read.</param>
        /// <param name="options">(optional) Override default and type-defined reification behavior.</param>
        /// <typeparam name="T">The type of the struct to update.</typeparam>
        public static void SetFieldsOnStruct<T>(ref T obj, DocNode doc, ReificationOptions? options = null) where T : struct {
            typeReifier.SetFieldsOnStruct(ref obj, doc, options);
        }

        /// <summary>
        /// Set a specific field on the given object from a parsed config.
        /// </summary>
        /// <param name="obj">The object to update.  Must not be null</param>
        /// <param name="fieldName">The name of the field to update.  Must not be null</param>
        /// <param name="doc">The config doc to read.</param>
        /// <param name="options">(optional) Override default and type-defined reification behavior.</param>
        /// <typeparam name="T">The type of <paramref name="obj"/></typeparam>
		/// <returns>true if we successfully set the field, false otherwise</returns>
        public static bool SetFieldOnObject<T>(ref T obj, string fieldName, DocNode doc, ReificationOptions? options = null) where T : class
        {
            return typeReifier.SetFieldOnObject(ref obj, fieldName, doc, options);
        }
        
        /// <summary>
        /// Set a specific field on the given struct from a parsed config.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="fieldName">The name of the field to update.  Must not be null</param>
        /// <param name="doc">The config doc to read.</param>
        /// <param name="options">(optional) Override default and type-defined reification behavior.</param>
        /// <typeparam name="T"></typeparam>
        public static void SetFieldOnStruct<T>(ref T obj, string fieldName, DocNode doc, ReificationOptions? options = null) where T : struct
        {
            typeReifier.SetFieldOnStruct(ref obj, fieldName, doc, options);
        }
        #endregion

        #region Processors
        /// <summary>
        /// Add a config processor
        /// </summary>
        /// <param name="processor">The config processor to register</param>
        public static void AddConfigProcessor(ConfigProcessor processor) {
            processors.Add(processor);
        }

        /// <summary>
        /// Remove a config processor
        /// </summary>
        /// <param name="processor">The config processor to unregister</param>
        public static void RemoveConfigProcessor(ConfigProcessor processor) {
            processors.Remove(processor);
        }

        /// Number of currently registered config processors
        public static int NumConfigProcessors => processors.Count;
        
        /// Unregisters all config processors
        public static void ClearConfigProcessors() {
            processors.Clear();
        }

        /// <summary>
        /// Enumerate all config processors
        /// </summary>
        /// <returns>An enumeration of the currently registered config processors</returns>
        public static IEnumerable<ConfigProcessor> GetConfigProcessors() {
            return processors;
        }

        /// <summary>
        /// Run all the config processors on this doc
        /// Useful for manual atypical flows
        /// </summary>
        /// <param name="filename">The filename associated with this data (used by some config processors)</param>
        /// <param name="doc">The parsed data to process</param>
        public static void ProcessWithConfigProcessors(string filename, ref DocNode doc) {
            if (doc.Type == DocNodeType.Invalid) {
                return;
            }

            // Run MakeMutable once if necessary
            foreach (var processor in processors) {
                if (processor.CanMutate) {
                    doc = ComposedDocNode.MakeMutable(doc);
                    break;
                }
            }

            foreach (var processor in processors) {
                processor.Process(filename, ref doc);
            }
        }
        #endregion
        
        #region Logging
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogInfo(string message) {
            Log(LogVerbosity.Info, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogWarning(string message) {
            Log(LogVerbosity.Warn, message);
        }
        #endregion
        
        public static void Update(float dt) {
            FileManager.Update(dt);
        }

        /////////////////////////////////////////////////

        static Internal.TypeReifier typeReifier = new Internal.TypeReifier();

        static List<ConfigProcessor> processors = new List<ConfigProcessor>();

        /////////////////////////////////////////////////

        static DocNode ParseYamlFromTextReader(TextReader reader, string filename, bool ignoreProcessors = false) {
            var yaml = new YamlStream();
            try {
                yaml.Load(reader);
            } catch (Exception e) {
                throw new Exception($"Error loading file '{filename}': {e.Message}");
            }
            DocNode docNode = yaml.Documents.Count <= 0 ? new YamlDocNode(null, filename)
                : new YamlDocNode(yaml.Documents[0].RootNode, filename);
            if (!ignoreProcessors) {
                ProcessWithConfigProcessors(filename, ref docNode);
            }
            return docNode;
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
