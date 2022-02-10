using UnityEngine;
using System;
using System.Collections.Generic;

namespace DarkConfig {
    /// loads configs from a Unity Resources directory
    /// 
    /// since we can't check the timestamp on the files, it has to read them in in their
    /// entirety to see whether to hotload them
    public class ResourcesSource : ConfigSource {
        const string INDEX_FILENAME = "index";

        public override bool CanHotload { get; }

        public ResourcesSource(string baseDir = "Configs", bool hotload = false) {
            this.baseDir = baseDir;
            CanHotload = hotload && Application.isEditor;
        }

        public override void Preload() {
            AllFiles.Clear();
            filesList.Clear();
            
            // Load the index file.
            indexFile = ReadFile(INDEX_FILENAME);
            if (indexFile == null) {
                Config.LogError($"Index file is missing at Resources path {INDEX_FILENAME}.");
                return;
            }
            
            // Load all the files.
            foreach (var nameNode in indexFile.Parsed.Values) {
                string filename = nameNode.StringValue;
                filesList.Add(filename);
                if (filename != "index") {
                    AllFiles[filename] = ReadFile(filename);    
                }
            }
        }
        
        public override void Hotload(List<string> changedFiles) {
            // First try to load the index in case any files were added or removed.
            var newIndex = ReadFile(INDEX_FILENAME);
            if (newIndex == null) {
                Config.LogError($"Index file is missing at Resources path {INDEX_FILENAME}.");
                return;
            }
            
            if (newIndex.Checksum != indexFile.Checksum) {
                // Index has changed, possibly have added or removed files from the index.
                // TODO Smart update, don't just toss the whole list and start from scratch.
                Preload();
                changedFiles.AddRange(filesList);
            } else {
                // Index hasn't changed.  Check each file.
                foreach (string file in filesList) {
                    var newFile = ReadFile(file);
                    if (newFile.Checksum == AllFiles[file].Checksum) {
                        continue;
                    }
                    AllFiles[file] = newFile;
                    changedFiles.Add(file);
                }
            }
        }

        public override string ToString() {
            return $"ResourcesSource({baseDir})";
        }
        
        /////////////////////////////////////////////////

        ConfigFileInfo indexFile;
        readonly List<string> filesList = new List<string>();
        readonly string baseDir;

        /////////////////////////////////////////////////
        
        ConfigFileInfo ReadFile(string filename) {
            // Get the full resources path for the file.
            string path = baseDir + "/" + filename;
            
            // Remove extension if one is specified.  
            path = System.IO.Path.ChangeExtension(path, null);
            
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) {
                return null;
            }

            return new ConfigFileInfo {
                Name = filename,
                Checksum = Internal.ChecksumUtils.Checksum(asset.text),
                
                // It's not easy to get a modified timestamp on a resources file, so just set it to the
                // default DateTime value. We'll instead rely on checksums to detect differences that need hotloading.
                Modified = new DateTime(),
                Size = asset.text.Length,
                Parsed = Config.LoadDocFromString(asset.text, filename)
            };
        }
    }
}