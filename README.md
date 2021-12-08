<p align="center">
    <a href="#concept">
      <img src="https://github.com/SpryFox/DarkConfig/raw/master/Docs/DarkConfig.png">
    </a>
</p>

DarkConfig
===========

DarkConfig is a configuration library for games which supports fast and expressive iteration.

Here's how DarkConfig supports fast iteration:

* It hotloads files into the running game when they're changed, allowing you to see the results of tweaks in moments.
* It requires very little code to get running, and supports arbitrary classes.
* It uses YAML, a stress-free format for authoring and editing.
* When there are syntax errors in the YAML files, it reports line and column numbers, so you can find the problem quickly.
* It works on iOS, with .Net 2.0 subset, stripped bytecode, and "fast but no exceptions" execution model.  On Android it supports the micro mscorlib.

DarkConfig supports .Net 3.5 or later, and has special support for Unity 5.2-5.5.

Concept
--------

DarkConfig converts YAML files into in-memory data structures, and updates them whenever the configuration files change.  This is all done using reflection, so very little glue code is needed.

Given this object:

    public class Spinner : MonoBehaviour {
        string Name;
        public Dictionary<string, KeyCode> Keyboard;

        void Start() {
            Config.ApplyThis("player", this);
        }

        void Update () {
            float rotation = 0;
            if(Input.GetKey(Keyboard["Left"])) {
                rotation += 10;
            }
            if(Input.GetKey(Keyboard["Right"])) {
                rotation -= 10;
            }

            transform.rotation *= Quaternion.AngleAxis(rotation * Time.deltaTime, Vector3.forward);
        }
    }

And this file:

    ---   # player.bytes
    Name: PlayerName
    Keyboard:
        Left: LeftArrow
        Right: RightArrow


You now have a rudimentary keyboard mapping object. Any changes that you make in player.bytes will take swift effect in-game.


Setup
------

The core of DarkConfig is a standalone managed DLL for ease of integration with many platforms, and to avoid lengthening compile times.  To integrate Dark Config into your own projects, you'll first need to build this DLL:

1. Open `Sln/DarkConfig.sln` in Visual Studio, or Monodevelop.
2. Clean and build the project.
3. Copy `DarkConfig.dll` from `Sln/bin/Debug` into your project (if you built for release, the dll will be in `Sln/bin/Release/`).

For integration with Unity, you'll also need a few additional files.  Copy over the `Assets/DarkConfig/Unity` folder into your project.

DarkConfig by default expects its config files in the `Assets/Resources/Configs` directory.  Create your first configuration file there.  Configuration files need to have a ".bytes" extension (this is due to a Unity limitation).  E.g. `Assets/Resources/Configs/player.bytes`.  You'd refer to this file in the code as simply `player`.

There's a little bit of coding to do the first time.  First let's create the initialization code.  Put this somewhere in your game loading code. Here's what an example setup would look like:
    
    UnityPlatform.Setup();
    Config.FileManager.AddSource(new ResourcesSource(hotload: true));
    Config.Preload();
    Config.OnPreload += () => {
        // load the rest of the game here
    }

You can't access any config files until preloading is complete, so it's best to delay any other code running until the callback is called. In our projects, we typically have a "Loader" scene that loads the config files before proceeding with the rest of the game.  See the demo project for an example.  As a special convenience for editing, DarkConfig will do a preload when you access the first config file.  So for editing your game you don't need to run your Loader scene every time; you can start from any scene and things will Just Work.  Try this out in the demo project as well!

Next, create a simple menu item script to autogenerate the index file.  If you're using a different location for your config files, specify it here.

    # Assets/Editor/DarkConfigEditor.cs
    using UnityEngine;
    using UnityEditor;
    using DarkConfig;

    public class DarkConfigEditorMenus {
        [MenuItem ("Assets/DarkConfig/Autogenerate Index")]
        static void MenuGenerateIndex() {
            EditorUtils.GenerateIndex("Resources/Configs");
            AssetDatabase.Refresh();
        }
    }

Finally, autogenerate index.bytes by using the menu item "_Assets -> Autogenerate DarkConfig Index_".  DarkConfig uses the index to locate its configuration files on platforms that don't support directory listing (such as Unity resources).


Building Objects From Configs
-----------------

There are two entry points for using configuration files to build up in-memory object structures.

### Apply:

The convenient Apply function accepts a filename and an object, and wires up the object immediately, and also sets up a listener to hot reload changes onto the object.  The code for this is simple:

    Config.Apply("spinner", ref spinnerInstance);

The hot reloading callback uses a weak reference in order to avoid leaking memory.  If the `spinnerInstance` object is garbage collected, or if it's a MonoBehaviour that gets destroyed, it won't continue to take up space.

It's absolutely fine to Apply the same file to different objects.

### DocNode:

You can save the DocNode to an instance variable and access it in a dynamically-typed way:

    DocNode SpinnerConfig;

    void Awake() {
        Config.Apply("spinner", ref SpinnerConfig);
    }

    void Update() {
        spinnerInstance.spinRate = SpinnerConfig["spin_rate"].AsFloat();
        spinnerInstance.complexVar = SpinnerConfig["positions"]["enemy"].As<List<Vector2>>();
    }

The DocNode will throw an exception if the typing is wrong at runtime (e.g. you're trying to access it as if it's a dictionary but in the YAML it's a list).  There's more documentation on how to use DocNodes in Docs/docnode.md


Hotloading in Standalone Unity Builds
------

[TODO: this is mostly correct but I haven't messed with this in a while so I don't remember if this works with ResourcesSource or if it requires FileSource]

DarkConfig supports hotloading configs for standalone builds.  This is a useful setup when you're making quick tweaks, or even as a full workflow when it's easier to share the build than the project.  By default, it expects these files to be in a "Configs" directory, which is placed at these locations:
    * Windows Standalone: within the _Data folder
    * Mac OS Standalone: within the .app/Contents folder

You have to manually copy the files to those locations when you make those types of builds.


Best Practices
---------------

Use the hot reloading to your advantage.  Being able to see the results of your changes quickly is absolutely essential for game design iteration.  While DarkConfig does a lot to help you do this, it can't do everything.

* Don't copy values off of objects that are managed by DarkConfig.  Once you've copied the value of a variable, the copy won't be updated by DarkConfig when the config file changes.  Instead, pass a reference to the object managed by DarkConfig and refer to the fields on it directly.  See the PlaneCard structure in the demo for an example of what that looks like.
* If you must copy values, set up listeners to take action when the config-driven values change.  One reason you might have to do this is that you're setting the position on a Transform.  Use the PostDoc technique to do this; see the documentation in Docs/types.md and the class PlaneCard in the demo.


Security
---------

Whenever you're reading files, there's a chance that they could be maliciously constructed.  In the past people have [devised exploits](https://www.sitepoint.com/anatomy-of-an-exploit-an-in-depth-look-at-the-rails-yaml-vulnerability/) that use a malicious YAML file to execute code within the process that is reading the file.  While we're not aware of any such exploits affecting DarkConfig, we also haven't done the work necessary to demonstrate that it's resistant to them.

Thus far, we've only deployed DarkConfig for loading configs we author, on player computers/phones.  In these situations, the worst case of file tampering means that the player gets control over a local process which they already had complete access to, so in our opinion the risk of a malicious YAML file does not increase the overall security risk.

We do not recommend using DarkConfig to seed a user-generated-content engine, where players load files created by others.  That introduces the risk that a malicious file gets distributed to many computers and does something bad to them.  For similar reasons, we also don't recommend loading files authored by untrusted sources on computers that you own.  It would not be wise to implement a modding engine using DarkConfig.


A Note From The Spry Foxes
------------

When we were first discussing what license to use for DarkConfig, we initially leaned towards writing one of our own that would allow people to use DarkConfig for free, as they desired and without any limitation, with the sole exception that DarkConfig could not be used in any game that was substantially similar to an existing game (i.e. a clone). This is an issue that we feel strongly about; making financially successful, original video games is extremely difficult, and we want to help other developers do it without simultaneously giving a speed boost to the many predatory companies that already rip off other devs with depressingly great alacrity. However, we ultimately decided that such a license would cause too much confusion and uncertainty and would stop legitimate devs from using DarkConfig, so we gave up on the idea. 

That is why we decided to use the simple 3 paragraph BSD open source license for the project; because it is short, easy to understand, and highly permissive. However, we also decided to add a single sentence to the license about crediting Spry Fox for our contribution to games that use DarkConfig. We had originally not planned to do this, but we can't resist making a statement (if not a statement in support of original game development, then perhaps another!) Our statement is simple: people deserve credit for their work, and that includes us. The game industry has a long and unfortunate history of individual contributors being excluded from the credits of games that they worked long and hard on. This issue is commonplace enough that the IGDA has for many years now maintained a "crediting standards" guide, in hopes of encouraging better behavior from studios and publishers. TLDR: we put a lot of work into DarkConfig and we don't think it's too much to ask for a polite nod in your credits page in return... and we hope you'll do the right thing and acknowledge every other contributor to your game there as well!

That's probably more than enough preaching from us. Thanks for reading this far! We just want to sign off with this: if you're using DarkConfig to make an original game that hopefully brings a little more happiness into the world, please know that everyone at Spry Fox thinks you are wonderful and we wish you all the success and joy you could ever hope for. :-)  

Love,
All the Spry Foxes
