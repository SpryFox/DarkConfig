#nullable enable

using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DarkConfig {
    /// Loads configs from loose files in a directory.
    /// Uses file modified timestamps to decide whether it should hotload or not.
    public class FileSource : ConfigSource {
        public override bool CanHotload { get; }
        /// If true, we till update the file modified time on disk with a checksum match, so hotloading won't check again next session
        public bool SetModifiedTimeOnChecksumMatch = false;

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
        /// <param name="ignorePattern">Ignore any paths that match this regex</param>
        /// <exception cref="ArgumentException">If <paramref name="dir"/> is null</exception>
        public FileSource(string dir, string fileExtension = ".yaml", bool hotload = false, Regex? ignorePattern = null) {
            baseDir = Path.GetFullPath(dir).Replace('\\', '/'); // Always use forward slashes in paths, even on windows.
            CanHotload = hotload;
            this.ignorePattern = ignorePattern;
            configFileExtensions = new[] {fileExtension};
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
        /// <param name="ignorePattern">Ignore any paths that match this regex</param>
        /// <exception cref="ArgumentException">If <paramref name="dir"/> is null</exception>
        public FileSource(string dir, string[] fileExtensions, bool hotload = false, Regex? ignorePattern = null) {
            baseDir = Path.GetFullPath(dir).Replace('\\', '/'); // Always use forward slashes in paths, even on windows.
            CanHotload = hotload;
            this.ignorePattern = ignorePattern;
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

                long fileSize = new FileInfo(file).Length;
                var modified = File.GetLastWriteTimeUtc(file);

                // Timestamp or size need to differ before we bother generating a checksum of the file.
                // Timestamps are considered different if there's at least 1 second between them.
                if (fileSize == fileInfo.Size && Math.Abs((modified - fileInfo.Modified).TotalSeconds) < 1f) {
                    continue;
                }

                using var fileStream = File.OpenRead(file);
                int checksum = Internal.ChecksumUtils.Checksum(fileStream);

                if (checksum == fileInfo.Checksum) {
                    // The files are identical (according to the checksum)
                    // Update the file size and modified timestamp
                    // We didn't early out a few lines above, so we know that at least one of these values is stale.
                    fileInfo.Size = fileSize;
                    if (SetModifiedTimeOnChecksumMatch) {
                        // Update the modified time on disk so we don't re-check this next session
                        try {
                            fileStream.Close();
                            File.SetLastWriteTimeUtc(file, fileInfo.Modified);
                            Configs.LogInfo($"Set mtime of file with matching checksum {fileInfo}");
                        } catch (Exception e) {
                            Configs.LogWarning($"Error setting mtime of file {fileInfo}: {e}"); // Warning cause execution can safely continue
                            fileInfo.Modified = modified; // At least we won't reload this session
                        }
                    } else {
                        fileInfo.Modified = modified;
                    }
                    continue;
                }

                // File has changed. Hotload it.
                fileStream.Seek(0, SeekOrigin.Begin);
                fileInfo.Parsed = Configs.ParseStream(fileStream, file);
                fileInfo.Checksum = checksum;
                fileInfo.Modified = modified;
                fileInfo.Size = fileSize;

                changedFiles.Add(fileName);
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
        readonly Regex? ignorePattern;

        ////////////////////////////////////////////

        /// Enumerate all files in the directory with the correct extensions.
        IEnumerable<string> FindConfigsInBaseDir() {
            foreach (string extension in configFileExtensions) {
                foreach (string file in Directory.GetFiles(baseDir, "*" + extension, SearchOption.AllDirectories)) {
                    if (ignorePattern != null && ignorePattern.IsMatch(GetFileNameFromPath(file))) {
                        continue;
                    }
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
            using var fileStream = File.OpenRead(filePath);

            int checksum = Internal.ChecksumUtils.Checksum(fileStream);
            fileStream.Seek(0, SeekOrigin.Begin);

            return new(
                name: GetFileNameFromPath(filePath),
                checksum: checksum,
                size: new FileInfo(filePath).Length,
                modified: File.GetLastWriteTimeUtc(filePath),
                parsed: Configs.ParseStream(fileStream, filePath));

        }
    }
}
