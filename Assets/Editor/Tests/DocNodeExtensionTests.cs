using UnityEngine;
using NUnit.Framework;
using DarkConfig;
using System.Collections.Generic;

[TestFixture]
class DocNodeExtensionTests {
    [SetUp]
    public void DoSetup() {
        DefaultFromDocs.RegisterAll();
        UnityFromDocs.RegisterAll();
    }

    DocNode GetDocNode(string str) {
        var doc = Config.LoadDocFromString(str, "ConfigReifierTests_ReifyString_TestFilename");
        return doc;
    }

    [Test]
    public void AsInt_Parses() {
        var doc = GetDocNode(@"---
            10
            ");
        Assert.AreEqual(doc.AsInt(), 10);
    }

    [Test]
    public void AsInt_Fails() {
        var doc = GetDocNode(@"---
            not_an_int
            ");
        Assert.Throws<ParseException>(() => { doc.AsInt(); });
    }

    [Test]
    public void AsFloat_Parses() {
        var doc = GetDocNode(@"---
            1.45
            ");
        Assert.AreEqual(doc.AsFloat(), 1.45f);
    }

    [Test]
    public void AsString_Parses() {
        var doc = GetDocNode(@"---
            derpy horse
            ");
        Assert.AreEqual(doc.AsString(), "derpy horse");
    }

    [Test]
    public void AsBool_Parses() {
        var doc = GetDocNode(@"---
            true
            ");
        Assert.AreEqual(doc.AsBool(), true);
    }

    [Test]
    public void NestedAccess_Dictionaries() {
        var doc = GetDocNode(@"---
            key:
                nested:
                    final: 123
            ");
        Assert.AreEqual(doc["key"]["nested"]["final"].AsInt(), 123);
    }

    [Test]
    public void NestedAccess_Lists() {
        var doc = GetDocNode(@"---
            key:
                - 9
                - 8.8
                - 7.1
            ");
        Assert.AreEqual(doc["key"][1].AsFloat(), 8.8f);
        Assert.AreEqual(doc["key"][2].AsFloat(), 7.1f);
    }

    [Test]
    public void As_ListOfString() {
        var doc = GetDocNode(@"---
            - the
            - quick
            - brown
            - fox
            ");
        var list = doc.As<List<string>>();
        Assert.AreEqual(list[0], "the");
        Assert.AreEqual(list[1], "quick");
        Assert.AreEqual(list[2], "brown");
        Assert.AreEqual(list[3], "fox");
    }

    [Test]
    public void As_DictOfVector3() {
        var doc = GetDocNode(@"---
            parry: [1, 2, 0.5]
            swerve: [1]
            ");
        var dict = doc.As<Dictionary<string, Vector3>>();
        Assert.AreEqual(dict["parry"], new Vector3(1, 2, 0.5f));
        Assert.AreEqual(dict["swerve"], new Vector3(1, 1, 1));
    }
}