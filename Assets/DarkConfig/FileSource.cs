using System;
using System.IO;
using System.Collections.Generic;

namespace DarkConfig {
    /// Loads configs from loose files in a directory.
    /// Uses file modified timestamps to decide whether it should hotload or not.
    public class FileSource : ConfigSource {
        public override bool CanHotload { get; }
        
        /// <summary>
        /// Create a config source based on files in a directory.
        /// </summary>
        /// <param name="dir">Path containing config files</param>
        /// <param name="fileExtension">
        /// Some platforms like Unity require that text files have a specific extension e.g. ".bytes"
        /// Also, some files use ".yml" instead of ".yaml"
        /// </param>
        /// <param name="hotload">Allow file hotloading</param>
        /// <exception cref="ArgumentException">If <paramref name="dir"/> is null</exception>
        public FileSource(string dir, string fileExtension = ".yaml", bool hotload = false) {
            if (string.IsNullOrEmpty(dir)) {
                throw new ArgumentNullException(nameof(dir), "FileSource needs non-null base directory");
            }
            CanHotload = hotload;
            configFileExtension = fileExtension;
            baseDir = dir;
        }

        public override void Preload(Action callback) {
            foreach (string file in FindConfigsInBaseDir()) {
                var fileInfo = ReadFile(file);
                AllFiles.Add(fileInfo.Name, fileInfo);
            }
            callback();
        }

        string[] FindConfigsInBaseDir() {
            return Directory.GetFiles(baseDir, "*" + configFileExtension, SearchOption.AllDirectories);
        }

        public override void Hotload(List<string> changedFiles) {
            // TODO smarter hotloading.  Handle removed files.
            var loadedFileNames = new HashSet<string>(AllFiles.Keys);
            foreach (string filePath in FindConfigsInBaseDir()) {
                string fileName = GetFileNameFromPath(filePath);
                loadedFileNames.Remove(fileName);
                if (!AllFiles.TryGetValue(fileName, out var fileInfo)) {
                    // New file, add it.
                    var newFileInfo = ReadFile(filePath);
                    AllFiles.Add(newFileInfo.Name, newFileInfo);
                    changedFiles.Add(newFileInfo.Name);
                    continue;
                }
                
                var fileSize = new FileInfo(filePath).Length;
                var modified = File.GetLastWriteTimeUtc(filePath);

                // Timestamp or size need to differ before we bother generating a checksum of the file.
                // Timestamps are considered different if there's at least 1 second between them.
                if (fileSize == fileInfo.Size && Math.Abs((modified - fileInfo.Modified).TotalSeconds) < 1f) {
                    continue;
                }
                
                using (var fileStream = File.OpenRead(filePath)) {
                    int checksum = Internal.ChecksumUtils.Checksum(fileStream);
                    fileStream.Seek(0, SeekOrigin.Begin);
                    
                    // Update the modified timestamp and file size even if the checksum is the same.
                    // Because we didn't early out a few lines above, we know that at least one of these values
                    // is stale.
                    fileInfo.Modified = modified;
                    fileInfo.Size = fileSize;
                    
                    if (checksum == fileInfo.Checksum) {
                        // The files are identical.
                        continue;
                    }
                    
                    // File has changed. Hotload it.
                    fileInfo.Checksum = checksum;
                    fileInfo.Modified = modified;
                    fileInfo.Parsed = Config.LoadDocFromStream(fileStream, filePath);
                    
                    changedFiles.Add(fileName);
                }
            }

            foreach (string deletedFile in loadedFileNames) {
                AllFiles.Remove(deletedFile);
                changedFiles.Add(deletedFile);
            }
        }

        public override string ToString() {
            return $"FileSource({baseDir})";
        }
        
        ////////////////////////////////////////////

        readonly string baseDir;
        readonly string configFileExtension;
        
        ////////////////////////////////////////////

        string FullFilePath(string relativePath) {
            return Path.Combine(baseDir, relativePath + configFileExtension);
        }
        
        /// Get the relative path without the extension
        string GetFileNameFromPath(string filePath) {
            return Path.ChangeExtension(filePath, null)
                .Replace(baseDir + "/", "");
        }
        
        ConfigFileInfo ReadFile(string filePath) {
            using (var fileStream = File.OpenRead(filePath)) {
                int checksum = Internal.ChecksumUtils.Checksum(fileStream);
                fileStream.Seek(0, SeekOrigin.Begin);

                return new ConfigFileInfo {
                    Name = GetFileNameFromPath(filePath),
                    Checksum = checksum,
                    Size = new FileInfo(filePath).Length,
                    Modified = File.GetLastWriteTimeUtc(filePath),
                    Parsed = Config.LoadDocFromStream(fileStream, filePath)
                };
            }
        }
    }
}