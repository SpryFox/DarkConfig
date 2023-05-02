using NUnit.Framework;
using DarkConfig;
using System.Collections.Generic;

[TestFixture]
class DocNodeExtensionTests {
    [Test]
    public void AsInt_Parses() {
        var doc = Configs.ParseString("10", "TestFilename");
        Assert.That(doc.As<int>(), Is.EqualTo(10));
    }

    [Test]
    public void AsInt_Fails() {
        var doc = Configs.ParseString("not_an_int", "TestFilename");
        Assert.Throws<ParseException>(() => { doc.As<int>(); });
    }

    [Test]
    public void AsFloat_Parses() {
        var doc = Configs.ParseString("1.45", "TestFilename");
        Assert.That(doc.As<float>(), Is.EqualTo(1.45f));
    }

    [Test]
    public void AsString_Parses() {
        var doc = Configs.ParseString("derpy horse", "TestFilename");
        Assert.That(doc.As<string>(), Is.EqualTo("derpy horse"));
    }

    [Test]
    public void AsBool_Parses() {
        var doc = Configs.ParseString("true", "TestFilename");
        Assert.That(doc.As<bool>(), Is.True);
    }

    [Test]
    public void NestedAccess_Dictionaries() {
        const string yaml = @"---
            key:
                nested:
                    final: 123
            ";
        var doc = Configs.ParseString(yaml, "TestFilename");
        Assert.That(doc["key"]["nested"]["final"].As<int>(), Is.EqualTo(123));
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
        Assert.Multiple(() => {
            Assert.That(doc["key"][1].As<float>(), Is.EqualTo(8.8f));
            Assert.That(doc["key"][2].As<float>(), Is.EqualTo(7.1f));
        });
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
        Assert.Multiple(() => {
            Assert.That(list[0], Is.EqualTo("the"));
            Assert.That(list[1], Is.EqualTo("quick"));
            Assert.That(list[2], Is.EqualTo("brown"));
            Assert.That(list[3], Is.EqualTo("fox"));
        });
    }
}
