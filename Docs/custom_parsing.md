# Custom Parsing

It's often useful to map more than one YAML syntax to the same C# type. This enables alternative or shorthand methods of authoring config yaml, which can make your data easier to write, read, and reason about.

Custom parsers enable this kind of flexibility and control over the interpretation of config data. Fundamentally, a custom parser is a static method delegate that converts YAML into an instance of a specific type.

Dark Config refers to a custom parsing function for a given type as it's **FromDoc method**, or more simply put it's **FromDoc**.

## Example: Range

You might have a data type that specifies a simple numerical range with a min and max value like so.

```C#
class Range {
    float min;
    float max;
}
```

By default, DarkConfig parses `Range` instances from YAML dictionaries that contain keys matching the fields of `Range`.

```YAML
# By default, DarkConfig expects this yaml structure when reading a `Range` value.
{
    "min": 0
    "max": 1
}
```

This is great, but the YAML can feel a bit verbose if we plan to use and read this type in config data often.  To streamline our configs, we might want to optionally allow for a more terse list-style YAML syntax.

``` YAML
# We want to allow this syntax as a shorthand for
# {"min": 0, "max": 1}
[0, 1]
```
We'll use a custom parsing function, or "FromDoc", to facilitate reading both of these yaml structures into `Range` values.

## Creating a FromDoc

We first create a static method in the `Range` class called `FromDoc` with the following signature.

```C#
/// <summary>
/// Reads YAML data and creates an instance of a specific object type
/// Can be public or private
/// </summary>
/// <param name="existing">Either an existing instance of the type to update, or null if a new instance of the type should be generated.</param>
/// <param name="doc">The parsed YAML config data</param>
/// <returns>The newly generated object, or <paramref name="existing"/></returns>
public static object FromDoc(object existing, DocNode doc) {
    // ...
}
```
Following this signature and naming the function `FromDoc` is not only convention.  It also allows DarkConfig to automatically find and use the function in the config reading process.

Because Dark Config supports hotloading item hierarchies from configs, FromDoc's need to support both creating a new instance of the target type and populating the values in existing instances.

## Implementing Range's FromDoc method

The fist thing we'll need to do in our FromDoc is get a reference to the object we're going to read data into.  We either cast `existing` to a `Range` value if it's not null, or create a new `Range` instance.

Then we'll read either a yaml dictionary or list, and update the values of the `Range` instance.  Check out the DocNode documentation for more information on the `DocNode` type and how Dark Config reads YAML data.

If the YAML is not in a format we can read, we'll throw a `ParseException` to indicate that the YAML is malformed.  DarkConfig will automatically add file and line number information to this exception before it's displayed to the yaml content author.

Finally, we'll return a reference to the populated object, regardless of whether we updated an existing object or created a new one.

```C#
public static object FromDoc(object existing, DocNode doc) {
    // If we aren't given a valid Range object to populate, create a new one.
    var result = existing as Range ?? new Range();

    if (doc.Type == DocNodeType.Dictionary) {
        // Dictionary syntax: {min: <min>, max: <max>}
        result.min = doc['min'].As<Float>();
        result.max = doc['max'].As<Float>();
    } else if(doc.Type == DocNodeType.List) {
        // List syntax: [<min>, <max>]
        result.min = doc[0].As<Float>();
        result.max = doc[1].As<Float>();
    } else {
        // If the YAML is some other type, indicate a parsing error.
        throw new ParseException("Expected YAML list or dictionary for Range values");
    }

    return result;
}
```

# Explicit FromDoc Registration

Naming a static method FromDoc in a class will enable Dark Config to automatically register it as a custom parser for that type.  To add a custom parser for a type that we don't have control over, we can instead register a FromDoc for that type manually by calling `Configs.RegisterFromDoc`.

``` C#

static class DateTimeYAMLParsing {
    public static object DateTime_FromDoc(object existing, DocNode doc) {
        var result = existing as DateTime ?? new DateTime();

        if (doc.Type != DocNodeType.Scalar) {
            throw new ParseException("Expected YAML string for DateTime value")
        }
        result = DateTime.Parse(doc.As<string>());

        return result;
    }
}

// ...

// Register our FromDoc for the System.DateTime type
Configs.RegisterFromDoc<System.DateTime>(DateTimeYAMLParsing.DateTime_FromDoc);

// Non-generic equivalent to above
Configs.RegisterFromDoc(typeof(System.DateTime), DateTimeYAMLParsing.DateTime_FromDoc);
```
