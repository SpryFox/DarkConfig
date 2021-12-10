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
}