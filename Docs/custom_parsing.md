# Custom Parsing

It's often useful to map more than one YAML syntax to the same C# type. This enables alternative or shorthand methods of authoring config yaml which can make your data easier to write, read, and reason about.

Custom parsers enable this kind of flexibility and are the means to control the interpretation of config data. Fundamentally, a custom parser is a static method or delegate that consumes YAML and either updates or produces a new instance of a specific type.

Dark Config refers to a custom parsing function for a given type as it's **FromDoc method**, or more simply put it's **FromDoc**.

## Example: Range

You might have a data type that specifies a simple numerical range with a min and max value like so.

```C#
class Range {
    float min;
    float max;
}
```

By default, DarkConfig will parse `Range` instances from YAML dictionaries that contain keys matching the fields of `Range`.

```YAML
# By default, DarkConfig expects this yaml structure when reading a `Range` value.
{
    "min": 0
    "max": 1
}
```

This is great, but the YAML can feel a bit clunky and overly verbose if we plan to use this type in config data often.  To streamline our configs, we might want to optionally allow for a more terse list-style syntax.

``` YAML
# We want to allow this syntax as shorthand for
# {"min": 0, "max": 1}
[0, 1]
```
We'll use a custom parsing function (a "FromDoc") to facilitate reading both the normal dictionary structure and the shorthand list syntax into `Range` values.

## Creating a FromDoc

We first create a static method in the `Range` class called `FromDoc` with the following structure.

```C#
class Range {
    float min;
    float max;

    /// <summary>
    /// Reads YAML data and creates an instance of a specific object type
    /// Can be public or private
    /// </summary>
    /// <param name="existing">Either an existing instance of the type to update, or null if a new instance of the type should be generated.</param>
    /// <param name="doc">The parsed config data</param>
    /// <returns>The newly generated object, or <paramref name="existing"/> if it was not null</returns>
    public static object FromDoc(object existing, DocNode doc) {
        Range result = existing as Range ?? new Range();

        // TODO: custom parsing code here...

        return result;
    }
}
```

Following this structure and naming the function `FromDoc` allows DarkConfig to automatically find and use the function in both the config loading and hotloading processes.  When initially reading data from a config, `existing` will be null and `FromDoc` is expected to return a new instance of the read type.  When hotloading, `existing` will be a reference to the previously loaded instance which `FromDoc` is expected to update with the new config data.

## Implementing Range's FromDoc method

The fist thing we'll need to do in our FromDoc is get a reference to the object we're going to read data into.  We either cast `existing` to a `Range` value if it's not null, or create a new `Range` instance.

Then we'll check the node type of the DocNode parameter.  If it's a dictionary, we'll use the default DarkConfig loading mechanisms.  If it's a list, we'll instead read the list data and set the values of result directly.  Check out the `DocNode` documentation for more information on the type and how DarkConfig reads config data.

If the YAML is not in a format we recognize, we'll throw a `ParseException` to indicate that the config data is malformed.  DarkConfig will automatically add file and line number information to this exception before it's displayed to the config author.

Finally, we'll return a reference to the populated object, regardless of whether we updated an existing object or created a new one.

```C#
public static object FromDoc(object existing, DocNode doc) {
    // If we aren't given a valid Range object to populate, create a new one.
    var result = existing as Range ?? new Range();

    if (doc.Type == DocNodeType.Dictionary) {
        // Use DarkConfig's default behavior to read the normal dictionary syntax:
        // {min: <min>, max: <max>}
        Configs.SetFieldsOnObject(ref result, doc);
    } else if(doc.Type == DocNodeType.List) {
        // Manually read the abbreviated list syntax:
        // [<min>, <max>]
        result.min = doc[0].As<Float>();
        result.max = doc[1].As<Float>();
    } else {
        // If the config data is some other type, indicate a parsing error.
        throw new ParseException("Expected list or dictionary for Range values");
    }

    return result;
}
```

# Explicit FromDoc Registration

Naming a static method FromDoc in a class will enable Dark Config to automatically register it as a custom parser for that type.  Additionally, a custom parser can be registered for a sealed or non-class/struct type with `Configs.RegisterFromDoc`.

``` C#
static class MyDateTimeParser {
    /// Reads DateTime values from configs as strings following the 
    /// DateTime.Parse formatting conventions
    public static object DateTime_FromDoc(object existing, DocNode doc) {
        var result = existing as DateTime ?? new DateTime();

        if (doc.Type != DocNodeType.Scalar) {
            throw new ParseException("Expected string for DateTime value")
        }
        result = DateTime.Parse(doc.As<string>());

        return result;
    }
}

// Somewhere in your app startup...

// Register our FromDoc for the System.DateTime type
Configs.RegisterFromDoc<System.DateTime>(MyDateTimeParser.DateTime_FromDoc);
// or
Configs.RegisterFromDoc(typeof(System.DateTime), MyDateTimeParser.DateTime_FromDoc);
```
