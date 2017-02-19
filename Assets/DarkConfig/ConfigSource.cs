using System;
using System.IO;
using System.Collections.Generic;

namespace DarkConfig {

    public class ConfigFileInfo {
        public string Name;          // short filename
        public int Checksum;         // checksum of YAML source
        public int Size;             // number of byes in YAML source
        public DateTime Modified;    // date YAML source was last modified
        public DocNode Parsed;       // parsed contents, may be null

        public override string ToString() {
            return string.Format("[{0} {1:X16} {2} {3}]", Name, Checksum, Size, Parsed == null ? "unparsed" : "parsed");
        }
    }

    public class NotPreloadedException : System.Exception { }

    public interface ConfigSource {
        bool CanLoadNow();

        bool CanHotload();

        List<ConfigFileInfo> GetFiles();

        void Preload(Action callback);

        void ReceivePreloaded(List<ConfigFileInfo> files);

        ConfigFileInfo TryHotload(ConfigFileInfo finfo);
    }
}