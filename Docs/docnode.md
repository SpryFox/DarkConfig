DocNode
---------

DocNode is a union type.  This means that though as far as C# the language is concerned there is only the one type, DocNode, under the hood it _behaves_ as though it is one of a few types.  DocNodes can be Scalar, List, Dictionary, or (rarely) Invalid.  Every DocNode has methods that behave like List or Dictionary accessors, but those will only work if the underlying type is right.  So before you operate on a DocNode, it makes sense to check the type, using the `Type` property.

Here's a summary of the DocNode interface.

#### Type ####

    DocNodeType Type { get; }

Type of the DocNode: Scalar, List, Dictionary, or Invalid

#### As ####

    T As<T>();

Converts the DocNode to a type T, using the normal parsing rules.  For example: `var arr = dn.As<float[]>()`.  Be careful not to call `As<T>()` on the object passed in to the FromDoc function where T is also the registered type of the FromDoc; this will cause an infinite loop.

#### List Accessor ####

    DocNode this[int index] { get; }

Access the node as if it was a List<DocNode>.  Will throw a DocNodeAccessException if Type is not List.

#### Dictionary Accessor ####

    DocNode this[string key] { get; }

Access the node as if it was a Dictionary<string, DocNode>.  Will throw a DocNodeAccessException if Type is not Dictionary.

#### Count ####

    int Count { get; }

Number of items in the collection, for both List and Dictionary type.  Will throw a DocNodeAccessException if Type is not List or Dictionary.

#### StringValue ####
    
    string StringValue { get; }

Value as a string.  Will throw a DocNodeAccessException if Type is not Scalar.

#### ContainsKey ####

    bool ContainsKey(string key);

Returns true if the key is in the dictionary.  Will throw a DocNodeAccessException if Type is not Dictionary.

#### Values ####

    IEnumerable<DocNode> Values { get; }

Iterates over the values of a list.  Will throw a DocNodeAccessException if Type is not List.

#### Pairs ####

    IEnumerable<KeyValuePair<string, DocNode>> Pairs { get; }

Iterates over a the key/value pairs of a dictionary.  Will throw a DocNodeAccessException if Type is not Dictionary.

#### SourceInformation ####

    string SourceInformation { get; }

String describing the position and context in the source format (e.g. line number).  Used to print useful error messages when difficulties are encountered parsing.
