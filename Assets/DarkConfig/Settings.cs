using System;

namespace DarkConfig {
    [Flags]
    public enum ReificationOptions {
        None = 0,

        /// fields present in the YAML document but not on the object are allowed
        AllowExtraFields = 1 << 0,

        /// fields present on the object but not in the YAML document are allowed
        AllowMissingFields = 1 << 1,

        /// both missing and extra fields are allowed
        AllowMissingExtraFields = AllowExtraFields | AllowMissingFields,

        /// properties care about case
        CaseSensitive = 1 << 2
    }

	public class Settings {
		/// Default options for refication.  Change this if you want to change
        /// DarkConfig behavior without passing in parameters to each call.
        public ReificationOptions DefaultReifierOptions = ReificationOptions.AllowMissingExtraFields | ReificationOptions.CaseSensitive;

		/// If enabled DarkConfig will scan files for changes every HotloadCheckInterval seconds.
		/// Setting it to false stops hotloading.  Useful during production when configs are under rapid iteration.
		public bool EnableHotloading {
			get => Config.FileManager.IsHotloadingFiles;
			set => Config.FileManager.IsHotloadingFiles = value;
		}
		
		/// How often, in seconds, to scan files for changes.
		public float HotloadCheckFrequencySeconds = 2f;
	}
}