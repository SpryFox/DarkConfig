
public enum ConfigOptions {
    None = 0x0,
    // extra fields in the YAML document are allowed
    AllowExtraFields = 0x1,
    // fields present on object but not in YAML document are allowed
    AllowMissingFields = 0x2,
    // both missing and extra fields are allowed
    AllowMissingExtraFields = 0x3,
    // properties care about case
    CaseSensitive = 0x4
}