using DarkConfig;
using NUnit.Framework;
using System;
using System.Linq;
using System.Collections.Generic;

[TestFixture]
class DocNodeMergeTests {
    [Test]
    public void DeepMerge_EmptyListDocs_ReturnsEmptyListDoc() {
        var emptyDoc = new ComposedDocNode(DocNodeType.List, sourceInformation: "e1");
        var otherEmptyDoc = new ComposedDocNode(DocNodeType.List, sourceInformation: "e2");

        var merged = DocNode.DeepMerge(emptyDoc, otherEmptyDoc);

        var idealEmpty = new ComposedDocNode(DocNodeType.List);
        Assert.AreEqual(idealEmpty, merged);
        Assert.AreEqual("Combination of: [e1, e2]", merged.SourceInformation);
    }

    [Test]
    public void DeepMerge_EmptyDictDocs_ReturnsEmptyDictDoc() {
        var emptyDoc = new ComposedDocNode(DocNodeType.Dictionary);
        var otherEmptyDoc = new ComposedDocNode(DocNodeType.Dictionary);

        var merged = DocNode.DeepMerge(emptyDoc, otherEmptyDoc);

        var idealEmpty = new ComposedDocNode(DocNodeType.Dictionary);
        Assert.AreEqual(idealEmpty, merged);
        Assert.AreEqual(merged.SourceInformation,
            "Merging of: [ComposedDocNode Dictionary, ComposedDocNode Dictionary]");
    }

    [Test]
    public void DeepMerge_ScalarDocs_ReturnsSecondScalar() {
        var emptyDoc = CreateScalarNode("foo");
        var otherEmptyDoc = CreateScalarNode("bar");

        var merged = DocNode.DeepMerge(emptyDoc, otherEmptyDoc);

        var ideal = CreateScalarNode("bar");
        Assert.AreEqual(ideal, merged);
    }

    [Test]
    public void DeepMerge_DifferentDocTypes_ThrowsException() {
        var emptyDoc = new ComposedDocNode(DocNodeType.List);
        var otherEmptyDoc = new ComposedDocNode(DocNodeType.Dictionary);

        Assert.That(() => DocNode.DeepMerge(emptyDoc, otherEmptyDoc),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void DeepMerge_Lists_ListsMerged() {
        var doc = CreateListNode("foo", "bar");
        var otherDoc = CreateListNode("wiggle", "waggle");

        var merged = DocNode.DeepMerge(doc, otherDoc);

        var ideal = CreateListNode("foo", "bar", "wiggle", "waggle");
        Assert.AreEqual(ideal, merged);
    }

    [Test]
    public void DeepMerge_DictsWithDifferentKeys_KeysCombined() {
        var doc = CreateDictNode(Pair("A", "1"), Pair("B", "2"));
        var otherDoc = CreateDictNode(Pair("X", "9"), Pair("Y", "10"));

        var merged = DocNode.DeepMerge(doc, otherDoc);

        var ideal = CreateDictNode(Pair("A", "1"), Pair("B", "2"), Pair("X", "9"), Pair("Y", "10"));
        Assert.AreEqual(ideal, merged);
    }

    [Test]
    public void DeepMerge_DictsWithSameKeys_OtherKeysPreferred() {
        var doc = CreateDictNode(Pair("A", "1"), Pair("B", "2"));
        var otherDoc = CreateDictNode(Pair("A", "9"), Pair("Y", "10"));

        var merged = DocNode.DeepMerge(doc, otherDoc);

        var ideal = CreateDictNode(Pair("A", "9"), Pair("B", "2"), Pair("Y", "10"));
        Assert.AreEqual(ideal, merged);
    }

    [Test]
    public void DeepMerge_DictsWithIdenticallyKeyedLists_ListsCombined() {
        var doc = CreateDictNode(Pair("A", CreateListNode("foo", "bar")));
        var otherDoc = CreateDictNode(Pair("A", CreateListNode("wiggle", "waggle")));

        var merged = DocNode.DeepMerge(doc, otherDoc);

        var ideal = CreateDictNode(Pair("A", CreateListNode("foo", "bar", "wiggle", "waggle")));
        Assert.AreEqual(ideal, merged);
    }

    [Test]
    public void DeepMerge_DictsWithIdenticallyKeyedDicts_DictsCombined() {
        var doc = CreateDictNode(Pair("favourite", CreateDictNode(Pair("films", CreateListNode("die hard")))));
        var otherDoc =
            CreateDictNode(Pair("favourite", CreateDictNode(Pair("films", CreateListNode("fury road")))));

        var merged = DocNode.DeepMerge(doc, otherDoc);

        var ideal = CreateDictNode(Pair("favourite",
            CreateDictNode(Pair("films", CreateListNode("die hard",
                "fury road")))));
        Assert.AreEqual(ideal, merged);
    }

    //////////////////////////////////////////////////

    DocNode CreateListNode(params string[] items) {
        var stringNodes = items.Select(CreateScalarNode).ToArray();
        return CreateListNode(stringNodes);
    }

    DocNode CreateListNode(params DocNode[] items) {
        var list = new ComposedDocNode(DocNodeType.List);
        foreach (var item in items) {
            list.Add(item);
        }

        return list;
    }

    DocNode CreateDictNode(params KeyValuePair<string, DocNode>[] pairs) {
        var dict = new ComposedDocNode(DocNodeType.Dictionary);
        foreach (var pair in pairs) {
            dict.Add(pair.Key, pair.Value);
        }

        return dict;
    }

    DocNode CreateScalarNode(string value) {
        var valueNode = new ComposedDocNode(DocNodeType.Scalar);
        valueNode.StringValue = value;
        return valueNode;
    }

    KeyValuePair<string, DocNode> Pair(string key, string value) {
        return Pair(key, CreateScalarNode(value));
    }

    KeyValuePair<string, DocNode> Pair(string key, DocNode value) {
        return new KeyValuePair<string, DocNode>(key, value);
    }
}