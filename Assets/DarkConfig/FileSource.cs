using System;
using System.IO;
using System.Collections.Generic;

namespace DarkConfig {
    /// Loads configs from loose files in a directory.  Uses timestamps to decide whether it should hotload or not.
    public class FileSource : IConfigSource {
        /// Some platforms like Unity require that text files have a specific extension e.g. ".bytes"
        /// Also, some files use ".yml" instead of ".yaml"
        public static string ConfigFileExtension = ".yaml";
        
        public FileSource(string dir, bool hotload = false) {
            if (string.IsNullOrEmpty(dir)) {
                throw new ArgumentException("FileSource needs non-null dir");
            }
            baseDir = dir;
            hotloadingEnabled = hotload;
        }

        public bool CanLoadNow() {
            return File.Exists(baseDir + "/index" + ConfigFileExtension);
        }

        public bool CanHotload() {
            return hotloadingEnabled;
        }

        public void Preload(Action callback) {
            // load index file
            var indexInfo = ReadFile(baseDir + "/index", "index");

            files = new List<ConfigFileInfo> {
                indexInfo
            };
            var indexNode = indexInfo.Parsed;
            index = new List<string>(indexNode.Count);
            for (int i = 0; i < indexNode.Count; i++) {
                index.Add(indexNode[i].StringValue);
            }

            foreach (string filename in index) {
                if (filename == "index") {
                    continue;
                }

                string filePath = baseDir + "/" + filename;
                try {
                    files.Add(ReadFile(filePath, filename));
                } catch (Exception e) {
                    Platform.Log(LogVerbosity.Error, "Failed to load file at path", filePath, "with exception:", e);
                }
            }

            callback();
        }

        public void ReceivePreloaded(List<ConfigFileInfo> files) {
            Platform.Log(LogVerbosity.Info, "ReceivePreloaded", files.Count);
            
            // Copy the list
            this.files = new List<ConfigFileInfo>(files);
            
            index = new List<string>(files.Count);
            foreach (var file in this.files) {
                index.Add(file.Name);
            }
        }

        public ConfigFileInfo ReadFile(string filePath, string shortName) {
            string pathWithExtension = filePath + ConfigFileExtension;
            try {
                using (var fileStream = File.Open(pathWithExtension, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    int checksum = ConfigFileManager.Checksum(fileStream);
                    fileStream.Seek(0, SeekOrigin.Begin);

                    return new ConfigFileInfo {
                        Name = shortName,
                        Size = (int) fileStream.Length,
                        Modified = File.GetLastWriteTimeUtc(pathWithExtension),
                        Checksum = checksum,
                        Parsed = Config.LoadDocFromStream(fileStream, filePath)
                    };
                }
            } catch (Exception e) {
                Platform.Log(LogVerbosity.Error, "Exception loading file at path", pathWithExtension, "exception:", e);
                throw;
            }
        }

        public ConfigFileInfo TryHotload(ConfigFileInfo finfo) {
            var filename = baseDir + "/" + finfo.Name + ConfigFileExtension;
            if (!File.Exists(filename)) {
                return null;
            }
            var systemInfo = new FileInfo(filename);
            var modifiedTime = File.GetLastWriteTimeUtc(filename);
            var fileLength = systemInfo.Length;

            if (fileLength == finfo.Size && AreTimestampsEquivalent(modifiedTime, finfo.Modified)) {
                return null;
            }

            // size and modified time differ; have to open the whole file to see if it's actually different
            using (var fileStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                var size = (int) fileStream.Length;
                int checksum = ConfigFileManager.Checksum(fileStream);
                if (checksum == finfo.Checksum) {
                    if (!AreTimestampsEquivalent(modifiedTime, finfo.Modified)) {
                        // set the mtime on the file so that we don't have to re-check it later
                        Platform.Log(LogVerbosity.Info, "Setting mtime on file", finfo, "prev", modifiedTime, "new", finfo.Modified);
                        try {
                            File.SetLastWriteTimeUtc(filename, finfo.Modified);
                        } catch (Exception e) {
                            Platform.Log(LogVerbosity.Info, "Error setting mtime on file", finfo, e.ToString());
                            // if we can't modify the file then let's at least store the mtime in memory for next time
                            finfo.Modified = modifiedTime;
                        }
                    }

                    if (fileLength != finfo.Size) {
                        // for some reason the file's length is different, but the checksum is the same, so let's remember the size so next time we won't have to reload
                        Platform.Log(LogVerbosity.Info, "Saving size of file", finfo, "prev", finfo.Size, "new", fileLength);
                        finfo.Size = (int) fileLength;
                    }

                    return null; // checksum same, can skip parsing/hotloading this file
                }
                
                fileStream.Seek(0, SeekOrigin.Begin);

                var newInfo = new ConfigFileInfo {
                    Name = finfo.Name,
                    Size = size,
                    Modified = modifiedTime,
                    Checksum = checksum,
                    Parsed = Config.LoadDocFromStream(fileStream, finfo.Name)
                };
                
                if (newInfo.Name == "index") {
                    // index loading should trigger loading other files
                    HotloadIndex(newInfo);
                }

                return newInfo;
            }
        }

        public List<ConfigFileInfo> GetFiles() {
            return files;
        }

        public override string ToString() {
            return $"FileSource({baseDir})";
        }
        
        ////////////////////////////////////////////

        readonly bool hotloadingEnabled;
        readonly string baseDir;
        List<string> index;
        List<ConfigFileInfo> files;
        
        ////////////////////////////////////////////

        bool AreTimestampsEquivalent(DateTime a, DateTime b) {
            // https://blogs.msdn.microsoft.com/oldnewthing/20140903-00/?p=83
            // I'm using 3 seconds to be more generous in case I overlooked something
            return Math.Abs((a - b).TotalSeconds) < 3f;
        }

        void HotloadIndex(ConfigFileInfo indexInfo) {
            if (index == null) {
                Platform.Log(LogVerbosity.Warn, "Null m_index");
                return;
            }

            var indexNode = indexInfo.Parsed;

            var newFiles = new List<string>(10);
            var removedFiles = new List<string>(10);
            for (int i = 0; i < indexNode.Count; i++) {
                string filename = indexNode[i].StringValue;
                if (!index.Contains(filename)) {
                    newFiles.Add(filename);
                }
            }

            foreach (string filename in index) {
                if (!indexNode.Contains(filename)) {
                    removedFiles.Add(filename);
                }
            }

            foreach (string filename in newFiles) {
                index.Add(filename);
                if (filename == "index") continue;
                try {
                    var finfo = ReadFile(baseDir + "/" + filename, filename);
                    files.Add(finfo);
                } catch (Exception) {
                    continue;
                }
            }

            for (int i = 0; i < removedFiles.Count; i++) {
                index.Remove(removedFiles[i]);
                for (int j = 0; j < files.Count; j++) {
                    if (files[j].Name == removedFiles[i]) {
                        files.RemoveAt(j);
                    }
                }
            }
        }
    }
}