Builtin Types
=============

DarkConfig uses the types and structures defined in your code to interpret the meaning of the YAML configuration files.  When using it, you tend to build up structs, classes, lists and dictionaries, which are all completely unset and empty, then you apply a YAML file to fill them out.

DarkConfig has a pluggable parsing system that makes it easy to write your own parser for any type, see the Custom Parsing document.


Primitive types
----------------

Here's a class with a bunch of simple types.

    public class ShowAllPrimitives {
        bool boolVar;
        string stringVar;

        GameMode enumVar;

        Vector2 vector2Var1:
        Vector2 vector2Var2:

        Vector3 vector3Var1:
        Vector3 vector3Var2:

        Color colorVar1;
        Color colorVar2;
        Color colorVar3;

        float floatVar;
        double doubleVar;

        byte byteVar;
        sbyte sbyteVar;

        char charVar;

        short shortVar;
        ushort shortVar;

        int intVar;
        uint uintVar;

        long longVar;
        ulong ulongVar;
    }
    public enum GameMode { SinglePlayer, TwoPlayer, Networked }


Here's a file that would set every variable of this class.


    #  AllSimples.bytes
    boolVar: true
    stringVar: String contents

    enumVar: TwoPlayer

    vector2Var1: [2.5, -1]        # result is `new Vector2(2.5f, -1)`
    vector2Var2: 1                # result is `new Vector2(1, 1)`

    vector3Var1: [0, 1, 0]        # result is `new Vector3(0, 1, 0)`
    vector3Var2: 0.1              # result is `new Vector3(0.1f, 0.1f, 0.1f)`

    colorVar1: [1, 0.5, 0.5, 1]   # result is `new Color(1, 0.5f, 0.5f, 1)`
    colorVar2: [255, 127, 127, 1] # result is `new Color(1, 0.5f, 0.5f, 1)`
                                  # triggered whenever any of the fields is > 1
    colorVar3: [0, 1, 0.5]        # result is `new Color(0, 1, 0.5f, 1)`

    floatVar: 1.234
    doubleVar: 1.2345678

    byteVar: 127
    sbyteVar: 127

    charVar: 48

    shortVar: 32767
    ushortVar: 32767

    intVar: 2147483648
    uintVar: 2147483648

    longVar: 2147483649
    ulongVar: 2147483649


Notes:

* The order in the YAML file does not have to match the order in the class.  Variables are mapped by field name.
* No types are specified in the YAML file.  When parsing the document, DarkConfig looks at the type of the variable on the class, which expects a certain representation.  It will throw an exception if the value in the document doesn't parse.
* Observe that Vector2, Vector3, and Color have more than one possible representation.  This makes it a little bit more convenient to author.

Collection types
================

DarkConfig handles the two basic C# collections -- lists and dictionaries -- directly.  Here's a class that contains a bunch of collections as an example.

    public class ShowAllCollections {
        List<int> intListVar;

        Vector2[] vector2ArrayVar;

        Dictionary<string, KeyCode> dictionaryVar;
        Dictionary<string, int> dictionaryVar2;
    }

Here's the corresponding file.

    # AllCollections.bytes
    intListVar: [1, 1, 2, 3, 5, 7, 12, 19]

    vector2ArrayVar:
        - [1, 0]
        - [2.5, 0]
        - -1            # will show up as [-1, -1]

    dictionaryVar:
        up: UpArrow
        down: DownArrow
    dictionaryVar2: {"port": 8324, "retries": 5}

Notes:

* YAML has two syntaxes for lists/arrays.  The first is the JSON-like syntax, with square brackets on the outside and commas separating the values.  The second has an indented new line for each element, and a `-` at the beginning.
* Indentation matters in YAML!
* The collections are generic, meaning that you can use the same syntax for each list-element as you would use for a single variable of that type.  See how in the `vector2ArrayVar` we use the 2-element lists to specify Vector2s, and then at the end use a single number.
* YAML has two syntaxes for dictionaries.  The first is an indented key-colon-value list with newlines between each key/value pair.  The second is the JSON-like curly braces separated with commas.
* List and Dictionary values can be any type that DarkConfig supports (including custom parsed types).
* For the moment, DarkConfig only supports string keys for Dictionaries.
* DarkConfig will not know what to do with collections of `object` or Interfaces.  It looks at the specific type to know how to parse, so it won't know what to do when faced with a type that could represent many different classes.  DarkConfig does support mixed-type collections in several ways, look at the documentation on DocNodes and on registering handlers.


Classes
=======

When you apply a config file to a class, the keys of a YAML dictionary indicate the fields of the class to assign values to.  Order does not matter.  Here's a simple class and corresponding YAML file.

    public class NPC {
        bool isActive;
        public string name;

        public int numGrenades;
    }


    # NPC_example.bytes
    name: Alfred
    isActive: true


* You don't have to have a value for every field in the YAML file.  See Validation for more information on that.
* DarkConfig can care about capitalization, or ignore it, see Validation again for more information.
* The fields can have any accessibility (public, private, protected).  DarkConfig will set them.
* Properties are not supported at this time.

DarkConfig handles nested classes just fine (using the same key => fieldname association), and it conveniently ignores certain common fieldname prefixes.

    public class ShowClassParsing {
        public class State {
            string stateName;
            int probability;
            State[] sub_states;
        }

        State m_currentState;  # m_ prefix is dropped in YAML
        List<State> m_states;

        public class TerrainConfig {
            public string c_name;  # c_ prefix is dropped in YAML
            public float c_height; 
        }

        TerrainConfig terrain;
    }

Here's the corresponding YAML file:
    
    # nested_example.bytes
    currentState:
        stateName: "initial"
    states:
        - stateName: "attack"
          probability: 5
        - stateName: "flee"
          probability: 10
          sub_states:
            - stateName: "straight_flight"
              probability: 50
            - stateName: "circle_around"
              probability: 50
    terrain:
        name: water
        height: 0


* Both the `m_` and `c_` prefixes are ignored by DarkConfig when matching YAML keys to fieldnames.  In our codebase we find it convenient to use the `c_` prefix in the code to indicate a variable that is only set via configs.  You can use `m_`, `c_`, and unprefixed variables in the same class.
* DarkConfig does not support generic classes for now.
* Each class must have a zero-argument constructor in order to be instantiated.  (or no explicit constructors, as in the examples above)

### Single Field Classes ### 

As a special case, if you have a class that has only one field, you can parse it using a single YAML scalar.  E.g.:

        class SingleFieldClass {
            public int SingleField = 0;
        }

        SingleFieldClass Value;

        void Awake() {
            Config.Apply("Configs/properties.bytes", this);
        }

        # contents of properties.bytes:
        # ---
        # Value: 220
        #


Static Classes
--------------

Sometimes it's most convenient to have a static class that's configured.  DarkConfig can help you there.  Use Config.ApplyStatic.

    public class ServerList {
        public static string MasterIp;
    }

    // ... elsewhere ...
       Config.ApplyStatic<ServerList>("server_list");

And the corresponding config file:

    # server_list.bytes
    MasterIp: 10.0.0.1


Observers via PostDoc
--------------------------

Sometimes you want to take action when an object is updated from the configuration.  Maybe it's a post-processing step.  Maybe you just want to refresh rendering of something.  No matter the reason, you can define a delegate named PostDoc (it's a magic name just like FromDoc) on a class and it will get called when DarkConfig updates or creates such an object.

Here's an example from the PlaneCard class.

    public System.Func<PlaneCard, PlaneCard> PostDoc;

Elsewhere we can add an observer like so:

    m_card.PostDoc += Refresh;

The definition for a PostDoc delegate looks like this:

    public PlaneCard Refresh(PlaneCard card) {
        return card;
    }

Note that:
* It accepts an instance of its own class, and returns an instance of the same type.  This gives you an opportunity to modify or replace the instance.  If the instance is a value type, you have to replace it to modify it.

PostDoc will be called after all the fields have been set by the normal DarkConfig methods.  If is generally easier to implement listeners with PostDoc than implementing a custom FromDoc.