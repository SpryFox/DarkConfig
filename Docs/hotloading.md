=== Hotloading ===

Hotloading is getting values from config files into the game while the game 
is running.

Hotloading is becoming the lifeblood of modern development.  Shorter iteration
times mean faster and better content creation. From React to Loom, lots of
engineering teams are now building tools around hotloading.

No matter how fast your game loads, it still takes some time to get into a
particular gameplay situation, or looking at a particular piece of content.
Hotloading allows you to change values without going through that whole 
process each time.  On a recent real project, one content creator estimated
that hotloading saved him 75% of the time it took to create items.

Unity supports some forms of hotloading; DarkConfig complements it with more.

== How ==

Hotloading is great, and DarkConfig does as much as it can to facilitate it,
but it isn't omniscient and it's possible for code to "break" hotloading.    
If changing values in the config doesn't result in those values being 
reflected in the game while it's running, that's broken. This section goes
over some concepts that will help keep hotloading running well.

DarkConfig works by loading the config files into a typed in-memory object
tree.  When you load a file, you also pass in an object that the contents of
the file will be converted to.  The object is generally a container, a list or
dictionary, which contains objects of a particular type.  These objects then
have sub-objects, sub-containers, and/or fields.  DarkConfig does the work of
converting the YAML config files to the in-memory C# object structure you've
specified.  Your gameplay code can then refer to that object structure
directly, without having to know how it's been loaded.

Every time that a hotload is triggered, DarkConfig re-applies the config 
values to that same object tree.  Therefore, if you refer to values via the 
object tree, you will always have the hotloaded value.

Here's a basic example: using global configuration values on a static object.
There's a singleton GlobalConfig object with a bunch of fields.  Once it's
wired up in this way, all code which does stuff like
`GlobalConfig.Instance.xpRadius` will always hotload correctly.

```  // GlobalConfig.cs
public class GlobalConfig {
    public GlobalConfig Instance;

    public float xpRadius = 10;
    public bool doesDebugDraw;
    // ... other fields ...

    public void Init() {
        DarkConfig.Config.Apply(ref Instance, "global");
    }
}
```

_Don't_ write code that copies the value of `xpRadius` anywhere else. That 
would break hotloading.  When a hotload happens, DarkConfig would modify
GlobalConfig.Instance but _not_ be able to modify the place where its value
was copied to.  Always refer to `xpRadius` on an instance of GlobalConfig.

*Do* be sure to use hotloading as you develop your game.

See `PlayerController.cs` in the Demo for another example of a similar design
pattern.  There, the global config is the Keyboard mapping, and it's subtly
different because it's using `ApplyStatic` to populate a static Dictionary.
Whether you go the singleton route or the static route is a matter of code
style preference.

There is one way DarkConfig currently works which is a little 
counterintuitive: deleting fields from config files means the hotloading 
leaves the previous value unchanged.  As an example, imagine that we run the 
game with the contents of global.bytes looking like this:

``` # global.bytes
xpRadius: 50
doesDebugDraw: false```

While the game is running, we change global.bytes to look like this, and
trigger a hotload:

``` # global.bytes
doesDebugDraw: false```

What's the value of `xpRadius` after the hotload? If you understand that
DarkConfig simply sets the fields on the object based on what's in the file,
you'll understand why it remains `50`.  However, many people would expect it 
to "return" to the default value of `10`.  That difference is the source of 
some confusion in using DarkConfig for now.

== Object Identity ==

Because DarkConfig modifies objects in-place, you can simply take a reference
to an object anywhere in the object tree, and DarkConfig will update it for 
you when the file changes.  Note that this only works for objects, not 
structs! Structs are value types which means that they are copied, and 
copying _breaks hotloading_.

Building on the previous `GlobalConfig` example, you could safely store
references to the `GlobalConfig.Instance` object in your code, like this:

``` // ServerManager.cs
public class ServerManager {
    GlobalConfig globals;
    public void Start() {
        globals = GlobalConfig.Instance;
    }
}
```

Note that you do have to worry about order of operations; it wouldn't be a 
good idea to run ServerManager.Start before GlobalConfig.Init, because then
ServerManager.globals would be null.

For a more realistic example, take a look at usage of `PlaneCard` objects in
the Demo.  In particular, note how in `EnemySpawner.cs`, the code selects a
card from the collection, and then  through calling 
`controller.Setup(chosenCard)`, passes that object along  where it's assigned
to a field on `PlaneController`.  Because `PlaneCard` is  an object, this means that anytime DarkConfig hotloads an appropriate config  file, it'll update the fields on the objects in `PlaneCard.m_cards` directly.  Any code that refers to fields on a PlaneCard will therefore hotload correctly.

_Don't_ copy objects unnecessarily, or assign structs outside of the config object tree; this breaks hotloading.

There are some limitations, which crop up when the quantity or ordering of
objects in a container are changed.  DarkConfig does its best to retain object
identity in containers.  For lists, if there's an object at the same index as
in the config, it'll get updated in-place and references will not be broken;
and similarly for dictionaries.  However, if you remove an item from the 
middle of a list, DarkConfig will set the fields on the list items after that 
shifted one off from their original position, so they might take on different
identities than the original.  Also, any object which used to be contained in
the list but is removed in the config, becomes unattached to the list and
unupdatable. In practice, it's rare to modify the order of collections and 
also pass out references to the contents of those collections, so this 
limitation is not encountered often.

== Hotload Notifications ==

Sometimes, you have to perform a side-effect that effectively copies a field
from the config object tree.  For example, if you set the position of an 
object based on the config, the position gets copied to the Transform; if you 
set a color on a Material from a config, that color gets copied to the
Material. There's sometimes no way around it.

In these cases, to support hotloading, what you want to do is to trigger a
_refresh_ operation when the relevant config is hotloaded.  This _refresh_ 
then re-copies the values from the object tree to wherever it needs to be 
copied to. This should be the same exact code path as the code that loads it 
for the first time.

There are a number of different ways DarkConfig can notify your code about
hotloads.  Here they are.

= Load =

When you call `DarkConfig.Config.Load` to load the contents of a file with a
function argument, that function gets called every time the file is hotloaded
as well.  This can be useful to implement any sort of self-consistency logic.
For example, if you have objects with identifiers which you also keep in
dictionaries, you can use this to hook up an ID field on the object as a
convenience.

```// Obj.cs
class EnemyConfig {
    public string ID;
    // ... other fields ...
}


public class ExampleUse {
    public Dictionary<string, EnemyConfig> dict;

    public void Awake() {
        Config.Load("enemies", (doc) => {
            Config.Reify(dict, doc);
            foreach(var kv in dict) {
                kv.Value.ID = kv.Key;
            }
            return true;  // note that returning false causes the function to never be called again
        });
    }
}
```

``` # enemies.bytes
enemy_unit_a:
    # note that we don't set ID here, because it's set in Load
    # ... other fields ...
boss_b:
    # ... other fields ...
```

= Custom Parsers =

See the `custom_parsing` for more specifics on how to implement these.  When
you're implementing the custom parsing logic, it will get called during
hotloading.

It's important that you don't allocate a new object each time the custom 
parser is called.  Always reuse the existing object; that way any references 
to the object in your codebase continue to work.  It's always possible to 
successfully do so.

Make sure that the fromdoc always sets the same fields every time, no matter
what the docnode contains.

Here's a FromDoc anti-pattern that can shoot hotloading in the foot: assuming
that the object always comes in fresh.  Example:

```
public class EnemyConfig {
    string lootTableId;
    LootTableConfig lootTable;

    public static EnemyConfig FromDoc(EnemyConfig existing, DocNode doc) {
        //  ... other code ...
        
        // Bad Pattern
        if(existing.lootTable == null) {
            existing.lootTable = LookupLootTable(existing.lootTableId);
        }
    }
```

This bad pattern will ignore any hotloaded changes to the `lootTableId` field.
When the FromDoc function is called a second time on the same object, the
lootTable value will already be non-null and therefore won't be re-looked up.
There's a simple fix for this, though:

```
public class EnemyConfig {
    string lootTableId;
    LootTableConfig lootTable;
    [ConfigIgnore]
    LootTableConfig _derivedLootTable;

    public static EnemyConfig FromDoc(EnemyConfig existing, DocNode doc) {
        //  ... other code ...
        
        // Correct Pattern
        if(existing.lootTable == null) {
            existing._derivedLootTable = LookupLootTable(
                                                       existing.lootTableId);
        } else {
            existing._derivedLootTable = existing.lootTable;
        }
    }
}
```

With this new version, any changes to either `lootTableId` or `lootTable` will
hotload successfully (downstream code needs to be changed so that it only uses
`_derivedLootTable`), It obeys the principle of always setting the same fields
regardless of what's in the docnode.  The fix also improves code clarity; it's
more explicit that the loot table on the enemy is derived from either the
`lootTableId` field or the `lootTable` field, but not both.

Note also that the new field `_derivedLootTable` gets the `ConfigIgnore`
attribute; this guarantees that it can't be inadvertently overridden in the
config file.

= PostDoc =

Sometimes you don't want to implement a custom parser just to get notification
that an object was hotloaded.  For that, we have PostDoc.

If you name a static function `PostDoc`, whenever DarkConfig succesfully
reifies that class, it will call `PostDoc` on that class.  Here's an example
signature:

```
public static PlaneCard PostDoc(PlaneCard existing) {
    // ... code here ...
}
```

You can see an example of this in the demo project, in `PlaneCard.cs`, where it's used to trigger updates to the Unity structures that display the graphics of the planes.


== When Files Are Hotloaded ==

DarkConfig decides that a file should be hotloaded by seeing whether its
modified time has changed or its size has changed.  If either has changed,
DarkConfig sees whether the checksum of the file is different to the checksum
of the previously-loaded file.  If there's a difference, the file gets
hotloaded.

Because it takes a non-zero amount of time to run these checks, it can't check
all the files every frame.  DarkConfig has a few options for controlling when
to look at files for hotloading purposes.

= Continuous Polling =

When enabled, continuous checking will periodically initiate a background
coroutine.  This coroutine will check one file per iteration to see whether
it's hotloaded (see `CheckHotloadCoro` in `ConfigFileManager.cs`).

To turn on continuous checking, do this:

```
Config.ConfigFileManager.IsHotloadingFiles = true;
```

Set `IsHotloadingFiles` to `false` at any time to cancel the coroutine.

This slow rate is unlikely to generate performance troubles in a 60 FPS game.
However, if there a hundreds of files, it will take many seconds for 
continuous polling to check them all, which means potentially unacceptably 
long waits.

= Manual =

You can trigger a manual check by calling:

```
Config.ConfigFileManager.CheckHotloadImmediate()
```

This function will block until all config files have been examined for
differences and hotloaded.  This can take dozens or hundreds of milliseconds,
very likely a dropped frame, so don't call this wantonly.  Hook things up so
that it's triggered by a key combination, and it will be a surprisingly
ergonomic workflow.
