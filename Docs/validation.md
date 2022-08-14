# Validation

So the designer you work with added a ton of new enemies, but sometimes accidentally forgot to set their color.  So now you're wondering why sometimes the map has all these black dots on it.  Whoops, might have been nice to have a big old error pointing this subtle bug out!  That's what validation is for.

DarkConfig does two very simple validations:  checking for *missing* fields, and *extra* fields.  A missing field is one that is in your class, but it's missing from the config file (as in our mainColor example).  An extra field is present in the config file but not on the object -- maybe it's a misspelling like "mainColour", or maybe it's left over from a previous generation.

```C#
// SampleObject.cs ----
public class SampleObject {
    string field1;
    string field2;
}
```
```yaml
# sample_config.bytes ----
field1: value1
# field2 is missing
field3: value3  # this field is extra
```

When you call Config.Apply, DarkConfig will run these validations and throw ParseExceptions for violations, showing the file and line number.  Then you can fix them!

You can turn on and off validation at many different levels in DarkConfig.

The precedence of validation is:  Field Attribute > Class Attribute > Reify > Global

## Global Validation

Control the default/global validation mode by setting the Config.ConfigOptions enum.  This enum has several flags:

* AllowExtraFields: If set, DarkConfig will not check whether extra fields are present in the config file.
* AllowMissingFields: If set, DarkConfig will not check whether any fields are missing from the config file.
* CaseSensitive: If set, DarkConfig will treat fields as distinct if they differ only in case.
* None: None of the above.  DarkConfig will be case-insensitive and will check for missing and extra fields.

Example:
    
    // tell DarkConfig to not complain about anything
    Config.ConfigOptions = ConfigOptions.AllowExtraFields | ConfigOptions.AllowMissingFields;

    // we can skip some fields but say something if there's an extra field
    Config.ConfigOptions = ConfigOptions.AllowMissingFields;

## Field Validation

Set per-field validation with attributes.

* The `ConfigMandatory` attribute means the field cannot go missing.  This overrides any global setting of `ConfigOptions.AllowMissingFields`.
* `ConfigAllowMissing` makes its field skippable, it will not give an exception if not present in the config file.  This overrides a global setting that lacks `ConfigOptions.AllowMissingFields`.
* ConfigIgnore is special: it causes DarkConfig to ignore any validation on the field and _also_ never set it.  It will be as though the field doesn't exist on the class.  This is useful when you want to have strict validation on all other fields but have a few fields that are not set from config files.


```C#
class AttributesClass {
    [ConfigMandatory]
    public int field1 = -1;    // will complain if this field is not in config

    [ConfigAllowMissing]
    public string field2 = "initial";   // will happily tolerate this field being missing

    [ConfigIgnore]
    public bool field3 = false;    // will not set this field
}
```

## Class Validation

Change DarkConfig's validation on a per-class basis with attributes.

You can apply ConfigMandatory or ConfigAllowMissing to any class, where they apply to all fields.

As a special case, classes derived from MonoBehaviour have an implicit ConfigAllowMissing.  This is because the MonoBehaviour class has a ton of fields that you don't control and don't want to set.


## Reify Validation

You can set ConfigOptions per call to Reify.  This overrides the global setting for the duration of the call.

    Config.Reify("Configs/testconfig.bytes", ref obj, ConfigOptions.CaseSensitive | ConfigOptions.AllowExtraFields);

