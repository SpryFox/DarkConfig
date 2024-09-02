#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DarkConfig {
    /// A source of config files to load.
    /// e.g. a folder on disk, a binary file, a web server, etc.
    public abstract class ConfigSource {
        /// Does this config source support hotloading config files?
        public abstract bool CanHotload { get; }

        /// Generator function that finds and load all configs that this source knows about.
        /// Loads one file at a time, separated by a yield return null.
        /// Used for both blocking and time-sliced config loading.
        public abstract IEnumerable StepPreload();

        /// Try to hotload config files.  Adds names of changed files to the <paramref name="changedFiles"/> list.
        public virtual void Hotload(List<string> changedFiles) { }

        /// <summary>
        /// Enumerates all file lists (aka keys in <c>AllFiles</c>) in sorted order.
        /// </summary>
        /// <returns>An enumeration of all the filename keys in sorted order</returns>
        public IEnumerable<string> GetSortedFilenames() {
            foreach (string fileName in AllFiles.Keys.OrderBy(it => it)) {
                yield return fileName;
            }
        }

        /////////////////////////////////////////////////

        /// All the currently loaded config file data.
        public Dictionary<string, ConfigFileInfo> AllFiles = new();
    }
}
