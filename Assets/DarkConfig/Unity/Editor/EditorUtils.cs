using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace DarkConfig {
    public class EditorUtils {
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
            retval.Sort((a,b) => {
                var slashesA = CountCharacter('/', a);
                var slashesB = CountCharacter('/', b);
                if(slashesA != slashesB) {
                    return slashesA.CompareTo(slashesB);
                } else {
                    return a.CompareTo(b);
                }
            });

            return retval;
        }

        static int CountCharacter(char c, string s) {
            int count = 0;
            for(int i = 0; i < s.Length; i++) {
                if (s[i] == c) count++;
            }
            return count;
        }

        /// <summary>
        /// Writes the list of files to the index file.  
        /// The files are expected to be listed relative to a Resources directory.
        /// The indexFile is specified relative to the Assets directory.  It must also be in a resources directory.
        ///  E.g. "Assets/Resources/Configs/index.bytes"
        /// </summary>
        public static int WriteIndexFile(List<string> filesInIndex, string indexFile) {
            int resourcesIdx = indexFile.IndexOf("Resources/");
            Config.Assert(resourcesIdx >= 0, "Index file ", indexFile, " should have Resources directory in its path");

            string relToResources = indexFile.Substring(resourcesIdx + "Resources/".Length);

            string indexPath = Application.dataPath + "/" + indexFile;

            // create directory if necessary
            var indexDir = new FileInfo(indexPath).Directory;
            if(!indexDir.Exists) {
                indexDir.Create();
            }

            // write the header into the file
            var writer = new StreamWriter(indexPath, false);
            for(int i = 0; i < c_indexFileHeader.Length; i++) {
                writer.WriteLine(c_indexFileHeader[i]);
            }

            // write all the index entries into the file
            int totalWritten = 0;
            for(int i = 0; i < filesInIndex.Count; i++) {
                // skip over index file itself, it's likely to be in the list already
                if(filesInIndex[i] == relToResources) continue;
                writer.WriteLine("- " + filesInIndex[i]);
                totalWritten++;
            }
            writer.Flush();
            writer.Close();
            File.SetLastWriteTime(indexPath, System.DateTime.Now);
            return totalWritten;
        }

        static string[] c_indexFileHeader = new string[] {
            "# automatically generated DarkConfig index file",
            "#",
            "---"
        };

        static string GetShortName(string prefix, string filename) {
            if(filename.StartsWith(prefix)) {
                filename = filename.Replace(prefix + "/", "");
            }
            if(filename.EndsWith(".bytes")) {
                filename = filename.Replace(".bytes", "");
            }
            return filename;
        }

        public static void GenerateIndex(string baseDir) {
            var prefix = baseDir;
            var fullIndexFile = baseDir + "/index.bytes";
            Debug.Log("Generating Index at " + fullIndexFile + " using files in directory " + prefix);
            var configs = FindConfigFiles(prefix);
            // rename to short names
            for(int i = 0; i < configs.Count; i++) {
                configs[i] = GetShortName(prefix, configs[i]);
            }
            var total = WriteIndexFile(configs, fullIndexFile);
            Debug.Log("Wrote " + total + " configs to index");
        }
    }
}