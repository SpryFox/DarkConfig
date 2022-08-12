using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;

namespace DarkConfig {
    /// Loads configs from loose files in a directory.
    /// Uses file modified timestamps to decide whether it should hotload or not.
    public class FileSource : ConfigSource {
        public override bool CanHotload { get; }
                
        ////////////////////////////////////////////

        /// <summary>
        /// Create a config source based on files in a directory.
        /// </summary>
        /// <param name="dir">(optional) Path containing config files. Defaults to a folder named "Configs" in the current path.</param>
        /// <param name="fileExtension">
        /// File extension used for config files.  Specify with a leading dot.
        /// Some platforms like Unity require that text files have a specific extension e.g. ".bytes"
        /// Also, some files use ".yml" instead of ".yaml"
        /// </param>
        /// <param name="hotload">Allow file hotloading</param>
        /// <exception cref="ArgumentException">If <paramref name="dir"/> is null</exception>
        public FileSource(string dir, string fileExtension = ".yaml", bool hotload = false) {
            baseDir = Path.GetFullPath(dir).Replace('\\', '/'); // Always use forward slashes in paths, even on windows.
            CanHotload = hotload;
            configFileExtensions = new[]{fileExtension};
        }

        /// <summary>
        /// Create a config source based on files in a directory.
        /// </summary>
        /// <param name="dir">(optional) Path containing config files. Defaults to a folder named "Configs" in the current path.</param>
        /// <param name="fileExtensions">
        /// Array of file extension used for config files.  Specify each with a leading dot.
        /// Allows you to easily load a directory containing a mix of extensions like ".yml" and ".yaml".
        /// </param>
        /// <param name="hotload">Allow file hotloading</param>
        /// <exception cref="ArgumentException">If <paramref name="dir"/> is null</exception>
        public FileSource(string dir, string[] fileExtensions, bool hotload = false) {
            baseDir = Path.GetFullPath(dir).Replace('\\', '/'); // Always use forward slashes in paths, even on windows.
            CanHotload = hotload;
            configFileExtensions = fileExtensions;
        }

        public override IEnumerable StepPreload() {
            foreach (string file in FindConfigsInBaseDir()) {
                var fileInfo = ReadFile(file);
                AllFiles.Add(fileInfo.Name, fileInfo);
                yield return null;
            }
        }

        public override void Hotload(List<string> changedFiles) {
            // TODO smarter hotloading.  Handle removed files.
            var loadedFileNames = new HashSet<string>(AllFiles.Keys);
            foreach (string file in FindConfigsInBaseDir()) {
                string fileName = GetFileNameFromPath(file);
                loadedFileNames.Remove(fileName);
                if (!AllFiles.TryGetValue(fileName, out var fileInfo)) {
                    // New file, add it.
                    var newFileInfo = ReadFile(file);
                    AllFiles.Add(newFileInfo.Name, newFileInfo);
                    changedFiles.Add(newFileInfo.Name);
                    continue;
                }
                
                var fileSize = new FileInfo(file).Length;
                var modified = File.GetLastWriteTimeUtc(file);

                // Timestamp or size need to differ before we bother generating a checksum of the file.
                // Timestamps are considered different if there's at least 1 second between them.
                if (fileSize == fileInfo.Size && Math.Abs((modified - fileInfo.Modified).TotalSeconds) < 1f) {
                    continue;
                }
                
                using (var fileStream = File.OpenRead(file)) {
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
                    fileInfo.Parsed = Configs.LoadDocFromStream(fileStream, file);
                    
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
        readonly string[] configFileExtensions;
        
        ////////////////////////////////////////////
        
        /// Enumerate all files in the directory with the correct extensions.
        IEnumerable<string> FindConfigsInBaseDir() {
            foreach (var ext in configFileExtensions) {
                foreach (var file in Directory.GetFiles(baseDir, "*" + ext, SearchOption.AllDirectories)) {
                    yield return file;
                }
            }
        }

        /// Get the relative path without the extension
        string GetFileNameFromPath(string filePath) {
            // Normalize the path to fix any mixed forward and back slashes.
            filePath = Path.GetFullPath(filePath).Replace('\\', '/'); // Always use forward slashes in paths, even on windows.
            
            return Path.ChangeExtension(filePath, null)
                .Substring(baseDir.Length + 1); // remove the basedir from the path.
        }
        
        /// Reads and parses a file's contents.
        ConfigFileInfo ReadFile(string filePath) {
            using (var fileStream = File.OpenRead(filePath)) {
                int checksum = Internal.ChecksumUtils.Checksum(fileStream);
                fileStream.Seek(0, SeekOrigin.Begin);

                return new ConfigFileInfo {
                    Name = GetFileNameFromPath(filePath),
                    Checksum = checksum,
                    Size = new FileInfo(filePath).Length,
                    Modified = File.GetLastWriteTimeUtc(filePath),
                    Parsed = Configs.LoadDocFromStream(fileStream, filePath)
                };
            }
        }
    }
}