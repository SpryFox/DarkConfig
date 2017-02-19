using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace DarkConfig {
    // loads configs from a Unity Resources directory; since we can't check the timestamp on the files, it has to read them in in their entirety to see whether to hotload them

    public class ResourcesSource : ConfigSource {
        public ResourcesSource(string baseDir = "Configs", bool hotload = false) {
            m_baseDir = baseDir;
            m_hotload = hotload;
        }

        public bool CanLoadNow() {
            return true;
        }
        
        public bool CanHotload() {
            return Application.isEditor && m_hotload;
        }

        public void Preload(System.Action callback) {
            // load index file
            var indexInfo = ReadFile(m_baseDir + "/index", "index");
            m_files = new List<ConfigFileInfo>();
            m_files.Add(indexInfo);
            var indexNode = indexInfo.Parsed;
            m_index = new List<string>(indexNode.Count);
            for(int i = 0; i < indexNode.Count; i++) {
                m_index.Add(indexNode[i].StringValue);
            }

            for(int i = 0; i < m_index.Count; i++) {
                var filename = m_index[i];
                if(filename == "index") continue;
                try {
                    var finfo = ReadFile(m_baseDir + "/" + filename, filename);
                    m_files.Add(finfo);
                } catch(Exception) {
                    continue;
                }
            }
            callback();
        }
        
        public void ReceivePreloaded(List<ConfigFileInfo> files) {
            m_files = new List<ConfigFileInfo>(files);
            m_index = new List<string>();
            for(int i = 0; i < m_files.Count; i++) {
                m_index.Add(m_files[i].Name);
            }
        }

        public ConfigFileInfo ReadFile(string fname, string shortName) {
            try {
                // for some reason Unity prefers resource names without extensions
                var filename = System.IO.Path.ChangeExtension(fname, null);
                TextAsset asset = (TextAsset)Resources.Load(filename);
                if(asset == null) {
                    Config.Log(LogVerbosity.Error, "Null loading file", fname);
                    return null;
                }
                var contents = asset.text;

                var parsed = Config.LoadDocFromString(contents, fname);
                return new ConfigFileInfo {
                    Name = shortName,
                    Size = contents.Length,
                    Checksum = ConfigFileManager.Checksum(contents),
                    Parsed = parsed
                };
            } catch (Exception e) {
                Config.Log(LogVerbosity.Error, "Exception loading file", fname, e);
                throw e;
            }
        }

        public ConfigFileInfo TryHotload(ConfigFileInfo finfo) {
            var filename = m_baseDir + "/" + finfo.Name;
            filename = System.IO.Path.ChangeExtension(filename, null);
            TextAsset asset = (TextAsset)Resources.Load(filename);
            if(asset == null) {
                Config.Log(LogVerbosity.Error, "Null when loading file", filename);
                return null;
            }
            var contents = asset.text;
            var checksum = ConfigFileManager.Checksum(contents);
            if(checksum == finfo.Checksum) {
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

        public List<ConfigFileInfo> GetFiles() {
            return m_files;
        }

        public override string ToString() {
            return string.Format("ResourcesSource({0})", m_baseDir);
        }

        string m_baseDir;
        List<string> m_index;
        bool m_hotload;
        List<ConfigFileInfo> m_files;
    }

}