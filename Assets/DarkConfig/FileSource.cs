using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace DarkConfig {
#if !UNITY_WEBPLAYER
    
    /// Loads configs from loose files in a directory.  Uses timestamps to decide whether it should hotload or not.

    public class FileSource : ConfigSource {
        public FileSource(string baseDir, bool hotload = false) {
            if(string.IsNullOrEmpty(baseDir)) throw new ArgumentException("FileSource needs non-null baseDir");
            m_baseDir = baseDir;
            m_hotload = hotload;
        }

        public bool CanLoadNow() {
            return File.Exists(m_baseDir + "/index.bytes");
        }
        
        public bool CanHotload() {
            return m_hotload;
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
            Config.Log(LogVerbosity.Info, "ReceivePreloaded", files.Count);
            m_files = new List<ConfigFileInfo>(files);
            m_index = new List<string>();
            for(int i = 0; i < m_files.Count; i++) {
                m_index.Add(m_files[i].Name);
            }
        }

        public ConfigFileInfo ReadFile(string fname, string shortName) {
            try {
                var filename = fname + ".bytes";
                using(var fileStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    var checksum = ConfigFileManager.Checksum(fileStream);
                    fileStream.Seek(0, SeekOrigin.Begin);

                    var parsed = Config.LoadDocFromStream(fileStream, fname);
                    return new ConfigFileInfo {
                        Name = shortName,
                        Size = (int)fileStream.Length,
                        Modified = File.GetLastWriteTimeUtc(filename),
                        Checksum = checksum,
                        Parsed = parsed
                    };
                }
            } catch (Exception e) {
                Config.Log(LogVerbosity.Error, "Exception loading file", fname, e);
                throw e;
            }
        }

        public bool AreTimestampsEquivalent(DateTime a, DateTime b) {
            // https://blogs.msdn.microsoft.com/oldnewthing/20140903-00/?p=83
            // I'm using 3 seconds to be more generous in case I overlooked something
            return System.Math.Abs((a - b).TotalSeconds) < 3f;
        }

        public ConfigFileInfo TryHotload(ConfigFileInfo finfo) {
            var filename = m_baseDir + "/" + finfo.Name + ".bytes";
            if(!File.Exists(filename)) return null;
            var systemInfo = new System.IO.FileInfo(filename);
            var modifiedTime = File.GetLastWriteTimeUtc(filename);
            var fileLength = systemInfo.Length;

            if(fileLength == finfo.Size && AreTimestampsEquivalent(modifiedTime, finfo.Modified)) {
                return null;
            }

            // size and modified time differ; have to open the whole file to see if it's actually different
            using(var fileStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                var size = (int)fileStream.Length;
                var checksum = ConfigFileManager.Checksum(fileStream);
                if(checksum == finfo.Checksum) {
                    if(!AreTimestampsEquivalent(modifiedTime, finfo.Modified)) {
                        // set the mtime on the file so that we don't have to re-check it later
                        Config.Log(LogVerbosity.Info, "Setting mtime on file", finfo, "prev", modifiedTime, "new", finfo.Modified);
                        try {
                            File.SetLastWriteTimeUtc(filename, finfo.Modified);
                        } catch(Exception e) {
                            Config.Log(LogVerbosity.Info, "Error setting mtime on file", finfo, e.ToString());
                            // if we can't modify the file then let's at least store the mtime in memory for next time
                            finfo.Modified = modifiedTime;
                        }
                    }

                    if(fileLength != finfo.Size) {
                        // for some reason the file's length is different, but the checksum is the same, so let's remember the size so next time we won't have to reload
                        Config.Log(LogVerbosity.Info, "Saving size of file", finfo, "prev", finfo.Size, "new", fileLength);
                        finfo.Size = (int)fileLength;
                    }

                    return null; // checksum same, can skip parsing/hotloading this file
                }

                fileStream.Seek(0, SeekOrigin.Begin);

                var parsed = Config.LoadDocFromStream(fileStream, finfo.Name);

                var newInfo = new ConfigFileInfo {
                    Name = finfo.Name,
                    Size = size,
                    Modified = modifiedTime,
                    Checksum = checksum,
                    Parsed = parsed
                };
                if(newInfo.Name == "index") {
                    HotloadIndex(newInfo);  // index loading should trigger loading other files
                }
                return newInfo;
            }
        }

        
        public void HotloadIndex(ConfigFileInfo indexInfo) {
            if(m_index == null) {
                Config.Log(LogVerbosity.Warn, "Null m_index");
                return;
            }

            var indexNode = indexInfo.Parsed;

            var newFiles = new List<string>(10);
            var removedFiles = new List<string>(10);
            for(int i = 0; i < indexNode.Count; i++) {
                var fname = indexNode[i].StringValue;
                if(!m_index.Contains(fname)) {
                    newFiles.Add(fname);
                }
            }
            for(int i = 0; i < m_index.Count; i++) {
                if(!indexNode.Contains(m_index[i])) {
                    removedFiles.Add(m_index[i]);
                }
            }

            for(int i = 0; i < newFiles.Count; i++) {
                var filename = newFiles[i];
                m_index.Add(filename);
                if(filename == "index") continue;
                try {
                    var finfo = ReadFile(m_baseDir + "/" + filename, filename);
                    m_files.Add(finfo);
                } catch(Exception) {
                    continue;
                }
            }

            for(int i = 0; i < removedFiles.Count; i++) {
                m_index.Remove(removedFiles[i]);
                for(int j = 0; j < m_files.Count; j++) {
                    if(m_files[j].Name == removedFiles[i]) {
                        m_files.RemoveAt(j);
                    }
                }
            }
        }


        public List<ConfigFileInfo> GetFiles() {
            return m_files;
        }

        public override string ToString() {
            return string.Format("FileSource({0})", m_baseDir);
        }

        string m_baseDir;
        List<string> m_index;
        bool m_hotload;
        List<ConfigFileInfo> m_files;
    }
#endif
}