using System;
using System.Collections.Generic;

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

    public interface IConfigSource {
        bool CanLoadNow();

        bool CanHotload();

        List<ConfigFileInfo> GetFiles();

        void Preload(Action callback);

        void ReceivePreloaded(List<ConfigFileInfo> files);

        ConfigFileInfo TryHotload(ConfigFileInfo configFileInfo);
    }
}