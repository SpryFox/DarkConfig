using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace DarkConfig {
    public class EditorUtils {
        static readonly string[] INDEX_FILE_HEADER = {
            "# automatically generated DarkConfig index file",
            "#",
            "---"
        };
        
        public static List<string> FindConfigFiles(string baseDir = "/Resources/Configs") {
            var retval = new List<string>();
            var absPath = new DirectoryInfo(Application.dataPath + baseDir);
            var absPathSlashed = absPath.FullName.Replace("\\", "/");

            var fileInfo = absPath.GetFiles("*.bytes", SearchOption.AllDirectories);
            foreach (var file in fileInfo) {
                var dirName = file.DirectoryName.Replace("\\", "/");

                var relativeToBase = dirName.Replace(absPathSlashed, "").Trim('/', '\\');
                var completePath = (relativeToBase + "/" + file.Name).Trim('/', '\\');
                retval.Add(completePath);
            }

            retval.Sort((a, b) => {
                var slashesA = CountCharacter('/', a);
                var slashesB = CountCharacter('/', b);
                if (slashesA != slashesB) {
                    return slashesA.CompareTo(slashesB);
                }

                return string.Compare(a, b, StringComparison.Ordinal);
            });

            return retval;
        }

        /// <summary>
        /// Counts the instances of a character in a string
        /// </summary>
        /// <param name="c">character to count</param>
        /// <param name="s">string to search within</param>
        /// <returns>count of </returns>
        static int CountCharacter(char c, string s) {
            int count = 0;
            foreach (char t in s) {
                if (t == c) {
                    count++;
                }
            }

            return count;
        }
       
        /// <summary>
        /// Writes the list of files to the index file.
        /// The files are expected to be listed relative to a Resources directory.
        /// The indexFile is specified relative to the Assets directory.  It must also be in a resources directory.
        /// E.g. "Assets/Resources/Configs/index.bytes"
        /// </summary>
        /// <param name="filesInIndex"></param>
        /// <param name="indexFile"></param>
        /// <returns></returns>
        public static int WriteIndexFile(List<string> filesInIndex, string indexFile) {
            int resourcesIdx = indexFile.IndexOf("Resources/", StringComparison.Ordinal);
            Platform.Assert(resourcesIdx >= 0, "Index file ", indexFile, " should have Resources directory in its path");

            string relToResources = indexFile.Substring(resourcesIdx + "Resources/".Length);

            string indexPath = Application.dataPath + "/" + indexFile;

            // create directory if necessary
            var indexDir = new FileInfo(indexPath).Directory;
            if (!indexDir.Exists) {
                indexDir.Create();
            }

            int totalWritten = 0;
            
            // write the header into the file
            using (var writer = new StreamWriter(indexPath, false)) {
                // Write file header
                foreach (string headerLine in INDEX_FILE_HEADER) {
                    writer.WriteLine(headerLine);
                }

                // write all the index entries into the file
                foreach (string file in filesInIndex) {
                    // skip over index file itself, it's likely to be in the list already
                    if (file == relToResources) {
                        continue;
                    }
                    writer.WriteLine("- " + file);
                    totalWritten++;
                }
            }

            File.SetLastWriteTime(indexPath, DateTime.Now);
            return totalWritten;
        }

        public static void GenerateIndex(string baseDir) {
            var indexFilePath = baseDir + "/index.bytes";
            Debug.Log("Generating Index at " + indexFilePath + " using files in directory " + baseDir);
            var configs = FindConfigFiles(baseDir);
            // rename to short names
            for (int configIndex = 0; configIndex < configs.Count; configIndex++) {
                configs[configIndex] = configs[configIndex]
                    .Replace(baseDir + "/", "")
                    .Replace(".bytes", "");
            }

            var total = WriteIndexFile(configs, indexFilePath);
            Debug.Log("Wrote " + total + " configs to index");
        }
    }
}