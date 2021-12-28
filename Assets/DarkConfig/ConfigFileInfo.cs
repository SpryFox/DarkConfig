using System;

namespace DarkConfig {
    public class ConfigFileInfo {
        /// short filename
        public string Name;
        /// checksum of source file
        public int Checksum;
        /// number of byes in source file
        public int Size;
        /// date source file was last modified
        public DateTime Modified;
        /// parsed contents, may be null
        public DocNode Parsed;

        public override string ToString() {
            return $"[{Name} {Checksum:X16} {Size} {(Parsed == null ? "unparsed" : "parsed")}]";
        }
    }
}
