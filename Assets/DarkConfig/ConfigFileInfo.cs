using System;

namespace DarkConfig {
    public class ConfigFileInfo {
        public string Name; // short filename
        public int Checksum; // checksum of YAML source
        public int Size; // number of byes in YAML source
        public DateTime Modified; // date YAML source was last modified
        public DocNode Parsed; // parsed contents, may be null

        public override string ToString() {
            return $"[{Name} {Checksum:X16} {Size} {(Parsed == null ? "unparsed" : "parsed")}]";
        }
    }
}
