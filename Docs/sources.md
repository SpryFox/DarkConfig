# Config Sources

`ConfigSource` instances provide DarkConfig with a means to find and load config files.  DarkConfig provides two built-in source types out of the box that cover most cases, but the `ConfigSource` base class is designed to be extended to support custom data sources for your game.  Config sources can optionally support hotloading of configs

## Built-In Source Types

There are two built-in source types:
- `ResourcesSource` loads files from Unity's "Resources" directories.
- `FileSource` loads files from any directory on the filesystem.

Before working with DarkConfig, you need to provide it with one or more config sources.

```C#
// FileSource

var configsDir = Path.Combine(Directory.GetCurrentDirectory(), "Configs");

// load all files with the ".yaml" extension from the given path
Configs.AddSource(new FileSource(configsDir));

// load all files with the ".conf" extension from the given path.
Configs.AddSource(new FileSource(configsDir, "conf"))

// load all files with either the ".yml" or ".yaml" extension from the given path.
Configs.AddSource(new FileSource(configsDir, new[]{"yml", "yaml"}))

// same as above but also enable file hotloading (disabled by default)
Configs.AddSource(new FileSource(configsDir, new[]{"yml", "yaml"}, hotload:true))


// ResourcesSource (Unity only)

// load files in the default path: Assets/Resources/Configs
Configs.AddSource(new ResourcesSource());

// load files in the path: Assets/Resources/Demo
Configs.AddSource(new ResourcesSource("Demo"));

// load files in the path: Assets/Resources/Demo with hotloading (disabled by default) enabled.
Configs.AddSource(new ResourcesSource("Demo", hotload:true));
```

## Custom Sources

To provide an additional means of loading config files, you can create a custom `ConfigSource` subclass.  Refer to one of the built-in source types for an example of this process.

There are a few methods and properties you need to provide:

* `CanHotload`: (required) A bool indicating whether this config source supports hotloading files.
* `StepPreload`: (required) A generator function that loads one config file before yielding null.  This allows for both blocking and time-sliced loading.
* `Hotload`: (optional) Called by DarkConfig to detect changes to files that it should hotload.  Add the file names of changed configs to the `changedFiles` list parameter to indicate they should be hotloaded.
* `AllFiles`: field that provides the loaded config file data to DarkConfig.  `StepPreload` should populate this dictionary with instances of `ConfigFileInfo` mapped to by the config file name (without extension).
