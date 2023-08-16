using System;

namespace DarkConfig {
    public class ConfigFileInfo {
        /// The identifier we use to refer to this file.
        /// Usually the relative config file path without the file extension.
        /// <example>"Weapons/Swords/Rapier" for the file "./Weapons/Swords/Rapier.yaml"</example>
        public string Name;

        // File size. Used as a quick, coarse-grained checksum for hotloading.
        public long Size;

        /// Checksum of file contents.  Used in hotloading.
        public int Checksum;

        /// Last modified time of the file when it was loaded. Used to detect changes when hotloading.
        public DateTime Modified;

        /// Parsed file contents.
        public DocNode Parsed;

        public override string ToString() {
            return $"[{Name} {Checksum:X16} {(Parsed == null ? "unparsed" : "parsed")}]";
        }
    }
}
