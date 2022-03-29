using NUnit.Framework;
using DarkConfig;
using System.Collections.Generic;

[TestFixture]
class DocNodeExtensionTests {
    [Test]
    public void AsInt_Parses() {
        var doc = Configs.ParseString("10", "TestFilename");
        Assert.AreEqual(doc.As<int>(), 10);
    }

    [Test]
    public void AsInt_Fails() {
        var doc = Configs.ParseString("not_an_int", "TestFilename");
        Assert.Throws<ParseException>(() => { doc.As<int>(); });
    }

    [Test]
    public void AsFloat_Parses() {
        var doc = Configs.ParseString("1.45", "TestFilename");
        Assert.AreEqual(doc.As<float>(), 1.45f);
    }

    [Test]
    public void AsString_Parses() {
        var doc = Configs.ParseString("derpy horse", "TestFilename");
        Assert.AreEqual(doc.As<string>(), "derpy horse");
    }

    [Test]
    public void AsBool_Parses() {
        var doc = Configs.ParseString("true", "TestFilename");
        Assert.AreEqual(doc.As<bool>(), true);
    }

    [Test]
    public void NestedAccess_Dictionaries() {
        const string yaml = @"---
            key:
                nested:
                    final: 123
            ";
        var doc = Configs.ParseString(yaml, "TestFilename");
        Assert.AreEqual(doc["key"]["nested"]["final"].As<int>(), 123);
    }

    [Test]
    public void NestedAccess_Lists() {
        const string yaml = @"---
            key:
                - 9
                - 8.8
                - 7.1
            ";
        var doc = Configs.ParseString(yaml, "TestFilename");
        Assert.AreEqual(doc["key"][1].As<float>(), 8.8f);
        Assert.AreEqual(doc["key"][2].As<float>(), 7.1f);
    }

    [Test]
    public void As_ListOfString() {
        const string yaml = @"---
            - the
            - quick
            - brown
            - fox
            ";
        var doc = Configs.ParseString(yaml, "TestFilename");
        var list = doc.As<List<string>>();
        Assert.AreEqual(list[0], "the");
        Assert.AreEqual(list[1], "quick");
        Assert.AreEqual(list[2], "brown");
        Assert.AreEqual(list[3], "fox");
    }
}