using UnityEngine;
using System;
using System.Collections.Generic;

namespace DarkConfig {
    /// loads configs from a Unity Resources directory
    /// 
    /// since we can't check the timestamp on the files, it has to read them in in their
    /// entirety to see whether to hotload them
    public class ResourcesSource : ConfigSource {
        public ResourcesSource(string baseDir = "Configs", bool hotload = false) : base(hotload && Application.isEditor) {
            this.baseDir = baseDir;
        }

        public override void Preload(Action callback) {
            // load index file
            var indexInfo = ReadFile(baseDir + "/index", "index");
            
            LoadedFiles.Clear();
            LoadedFiles.Add(indexInfo);
            
            var indexNode = indexInfo.Parsed;
            
            index.Clear();
            index.Capacity = indexNode.Count;

            for (int i = 0; i < indexNode.Count; i++) {
                index.Add(indexNode[i].StringValue);
            }

            foreach (string filename in index) {
                if (filename == "index") {
                    continue;
                }
                try {
                    LoadedFiles.Add(ReadFile(baseDir + "/" + filename, filename));
                } catch (Exception) {
                    // ignored
                }
            }

            callback();
        }

        public override void ReceivePreloaded(List<ConfigFileInfo> files) {
            LoadedFiles.Clear();
            LoadedFiles.AddRange(files);

            index.Clear();
            foreach (var file in LoadedFiles) {
                index.Add(file.Name);
            }
        }

        public override ConfigFileInfo TryHotloadFile(ConfigFileInfo finfo) {
            var filename = baseDir + "/" + finfo.Name;
            filename = System.IO.Path.ChangeExtension(filename, null);
            var asset = Resources.Load<TextAsset>(filename);
            if (asset == null) {
                Platform.Log(LogVerbosity.Error, "Null when loading file", filename);
                return null;
            }

            var contents = asset.text;
            var checksum = Internal.ChecksumUtils.Checksum(contents);
            if (checksum == finfo.Checksum) {
                // early-out with a false result
                return null;
            }

            var parsed = Config.LoadDocFromString(contents, finfo.Name);
            return new ConfigFileInfo {
                Name = finfo.Name,
                Size = contents.Length,
                Checksum = checksum,
                Parsed = parsed
            };
        }

        public override string ToString() {
            return $"ResourcesSource({baseDir})";
        }
        
        /////////////////////////////////////////////////

        readonly string baseDir;
        
        /////////////////////////////////////////////////
        
        ConfigFileInfo ReadFile(string fileName, string shortName) {
            try {
                // for some reason Unity prefers resource names without extensions
                var filename = System.IO.Path.ChangeExtension(fileName, null);
                var asset = Resources.Load<TextAsset>(filename);
                if (asset == null) {
                    Platform.Log(LogVerbosity.Error, "Null loading file", fileName);
                    return null;
                }

                var contents = asset.text;

                var parsed = Config.LoadDocFromString(contents, fileName);
                return new ConfigFileInfo {
                    Name = shortName,
                    Size = contents.Length,
                    Checksum = Internal.ChecksumUtils.Checksum(contents),
                    Parsed = parsed
                };
            } catch (Exception e) {
                Platform.Log(LogVerbosity.Error, "Exception loading file", fileName, e);
                throw;
            }
        }
    }
}