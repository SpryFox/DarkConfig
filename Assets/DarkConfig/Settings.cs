using System;

namespace DarkConfig {
    [Flags]
    public enum ConfigOptions {
        None = 0,

        /// extra fields in the YAML document are allowed
        AllowExtraFields = 1 << 0,

        /// fields present on object but not in YAML document are allowed
        AllowMissingFields = 1 << 1,

        /// both missing and extra fields are allowed
        AllowMissingExtraFields = AllowExtraFields | AllowMissingFields,

        /// properties care about case
        CaseSensitive = 1 << 2
    }

	public static class Settings {
		/// Default options for refication.  Change this if you want to change
        /// DarkConfig behavior without passing in parameters to each call.
        public static ConfigOptions DefaultReifierOptions = ConfigOptions.AllowMissingExtraFields | ConfigOptions.CaseSensitive;
	}
}