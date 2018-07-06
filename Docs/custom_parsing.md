Custom Parsing
===============

Sometimes you'll want classes to have a different representation in the YAML than the default.

For example, you might want a Range datatype to to look like a list `[0, 1]` in addition to a dictionary `{"min": 0, "max": 1}`.

To do this, you need to define a custom parser.  A custom parser is a function that takes in a (possibly-existing) object of the target type, and a DocNode object (which represents the parsed YAML), and returns a parsed value.

Here's that Range example:

    public static object FromDoc(object obj, DocNode doc) {
        var existing = (Range)obj;
        if(existing == null) {
            existing = new Range(0, 0);
        }

        if(doc.Type == DocNodeType.Dictionary) {
            existing.min = doc['min'].As<Float>();
            existing.max = doc['max'].As<Float>();
        } else if(doc.Type == DocNodeType.List) {
            existing.min = doc[0].As<Float>();
            existing.max = doc[1].As<Float>();
        } else if(doc.Type == DocNodeType.Scalar) {
            existing.min = existing.max = doc.As<Float>();
        }
        return existing;
    }

See the DocNode documentation for more information on the interface of DocNodes.

Note that this parser supports the following three syntaxes for creating Range objects in the YAML file:
    
    range1:            # results in a range 10-20
        min: 10
        max: 20
    range2: [5, 7]     # results in a range 5-7
    range3: 4          # results in a range 4-4

Let's talk about that little business with the object named 'existing'.  DarkConfig hotloads config files when they change, so sometimes you'll have an object that was created by the old config file, and only one field on it has changed.  You might have references to that object instance throughout your codebase, it's not easy to know, even in principle.  It'd be a lot of effort to go around and find all the references and replace them with a new one, not to mention it'd allocate more memory which is bad for performance.  Instead, we simply modify the existing object in-place.  DarkConfig passes in an argument which might contain the existing object, or, if there is no object because this is our first time parsing the config, it will be null.

In almost all cases it's correct to do a simple null check at the beginning of the parser, as in the Range example.


Naming It "FromDoc"
---------------------

If you control the class, it might be easiest to simply name the custom parsing function "FromDoc".  When DarkConfig attempts to reify an object of a particular type, it checks whether that type has a `FromDoc` method, and calls it.  Example:

    public static Point FromDoc(Point existing, DocNode doc) {
        int p1 = doc[0].As<int>();
        int p2 = p1;
        if (doc.Count >= 2) {
            p2 = doc[1].As<int>();
        }
        return new Point(p1, p2);
    }


Calling Register
------------------

You can also explicitly register a FromDoc function by calling `Config.Register`.  There's a generic version:

    Config.Register<Vector2>(FromDoc_Vector2);

And an explicit-type version:

    Config.Register(typeof(Vector2), FromDoc_Vector2);

Multiple registrations for the same type will override each other, last one wins.
