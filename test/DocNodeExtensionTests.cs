using NUnit.Framework;
using DarkConfig;
using System.Collections.Generic;

[TestFixture]
class DocNodeExtensionTests {
    DocNode GetDocNode(string str) {
        var doc = Configs.ParseString(str, "ConfigReifierTests_ReifyString_TestFilename");
        return doc;
    }

    [Test]
    public void AsInt_Parses() {
        var doc = GetDocNode(@"---
            10
            ");
        Assert.AreEqual(doc.As<int>(), 10);
    }

    [Test]
    public void AsInt_Fails() {
        var doc = GetDocNode(@"---
            not_an_int
            ");
        Assert.Throws<ParseException>(() => { doc.As<int>(); });
    }

    [Test]
    public void AsFloat_Parses() {
        var doc = GetDocNode(@"---
            1.45
            ");
        Assert.AreEqual(doc.As<float>(), 1.45f);
    }

    [Test]
    public void AsString_Parses() {
        var doc = GetDocNode(@"---
            derpy horse
            ");
        Assert.AreEqual(doc.As<string>(), "derpy horse");
    }

    [Test]
    public void AsBool_Parses() {
        var doc = GetDocNode(@"---
            true
            ");
        Assert.AreEqual(doc.As<bool>(), true);
    }

    [Test]
    public void NestedAccess_Dictionaries() {
        var doc = GetDocNode(@"---
            key:
                nested:
                    final: 123
            ");
        Assert.AreEqual(doc["key"]["nested"]["final"].As<int>(), 123);
    }

    [Test]
    public void NestedAccess_Lists() {
        var doc = GetDocNode(@"---
            key:
                - 9
                - 8.8
                - 7.1
            ");
        Assert.AreEqual(doc["key"][1].As<float>(), 8.8f);
        Assert.AreEqual(doc["key"][2].As<float>(), 7.1f);
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
}