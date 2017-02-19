Sources
========

DarkConfig can find the files it loads in a variety of places.  Supporting hotloading and the index is complex enough that we have an abstraction, the ConfigSource class, to help implement them.

We have three built-in sources:
    - `ResourcesSource` loads files from Unity's Resources directories.
    - `FileSource` loads loose files from any directory on the filesystem.

You pick which source you're using before you call preload.  Examples:

    Config.FileManager.AddSource(new ResourcesSource());  // loads the default, Resources/Configs
    Config.FileManager.AddSource(new ResourcesSource("Demo"));  // loads files from Resources/Demo

    Config.FileManager.AddSource(new FileSource(Application.dataPath + "/Configs")); // loads files from Assets/

It's also possible to author your own ConfigSource for your own custom needs; take a look at the existing ones for inspiration.
