using NUnit.Framework;
using DarkConfig;
using System.Collections.Generic;
using System;

[TestFixture]
class FromDocTests {
    class TestClass {
        public int baseKey;

        public static TestClass FromDoc(TestClass existing, DocNode doc) {
            if (doc.Type != DocNodeType.List) {
                throw new System.ArgumentException("Not a list! " + doc.Type);
            }

            if (doc[0].StringValue == "Derived") {
                TestClassDerived derivedExisting;
                if (existing is TestClassDerived) {
                    derivedExisting = (TestClassDerived) existing;
                } else {
                    derivedExisting = new TestClassDerived();
                }

                derivedExisting.derivedKey =
                    Convert.ToInt32(doc[1].StringValue, System.Globalization.CultureInfo.InvariantCulture);
                return derivedExisting;
            } else {
                if (!(existing is TestClass)) {
                    existing = new TestClass();
                }

                existing.baseKey =
                    Convert.ToInt32(doc[1].StringValue, System.Globalization.CultureInfo.InvariantCulture);
                return existing;
            }
        }
    }

    class TestClassDerived : TestClass {
        public int derivedKey;
    }

    const string FILENAME = "FromDocTests_TestFileName";

    T ReifyString<T>(string str) where T : new() {
        var doc = Configs.ParseString(str, "FromDocTests_ReifyString_TestFileName");
        var instance = default(T);
        Configs.Reify(ref instance, doc);
        return instance;
    }

    [Test]
    public void FromDoc_CalledToReify() {
        var tc = ReifyString<TestClass>("[\"Base\", 12]");
        Assert.That(tc.baseKey, Is.EqualTo(12));
    }

    [Test]
    public void FromDoc_SpawnsDerivedClass() {
        var tc = ReifyString<TestClass>("[\"Derived\", 12]");
        Assert.Multiple(() => {
            Assert.That(tc.baseKey, Is.EqualTo(0));
            Assert.That(tc, Is.InstanceOf<TestClassDerived>());
        });
    }

    [Test]
    public void FromDoc_UpdatesTestClass() {
        var tc = new TestClass {baseKey = 15};
        var saved = tc;
        var doc = Configs.ParseString("[\"Base\", 99]", FILENAME);
        Configs.Reify(ref tc, doc);
        Assert.Multiple(() => {
            Assert.That(saved, Is.SameAs(tc));
            Assert.That(tc.baseKey, Is.EqualTo(99));
        });
    }

    [Test]
    public void FromDoc_UpdatesDerived() {
        TestClass tc = new TestClassDerived {baseKey = 1, derivedKey = 2};
        var saved = tc;
        var doc = Configs.ParseString("[\"Derived\", 66]", FILENAME);
        Configs.Reify(ref tc, doc);
        Assert.Multiple(() => {
            Assert.That(saved, Is.SameAs(tc));
            Assert.That(tc.baseKey, Is.EqualTo(1));
            Assert.That(((TestClassDerived) tc).derivedKey, Is.EqualTo(66));
        });
    }

    [Test]
    public void FromDoc_UpdatesDerived_AsBase() {
        TestClass tc = new TestClassDerived {baseKey = 4, derivedKey = 5};
        var saved = tc;
        var doc = Configs.ParseString("[\"Base\", 123]", FILENAME);
        Configs.Reify(ref tc, doc);
        Assert.Multiple(() => {
            Assert.That(saved, Is.SameAs(tc));
            Assert.That(tc.baseKey, Is.EqualTo(123));
            Assert.That(((TestClassDerived) tc).derivedKey, Is.EqualTo(5));
        });
    }

    [Test]
    public void FromDoc_OverwritesBase_WithDerived() {
        var tc = new TestClass {baseKey = 19};
        var saved = tc;
        var doc = Configs.ParseString("[\"Derived\", 321]", FILENAME);
        Configs.Reify(ref tc, doc);
        Assert.Multiple(() => {
            Assert.That(ReferenceEquals(tc, saved), Is.False);
            Assert.That(tc, Is.InstanceOf<TestClassDerived>());
            Assert.That(((TestClassDerived) tc).derivedKey, Is.EqualTo(321));
        });
    }

    [Test]
    [Ignore("It's not completely clear how we should call parent class FromDocs when reifying a derived object")]
    public void FromDoc_SpawnsDerivedClass_WhenCastAsDerived() {
        var tc = ReifyString<TestClassDerived>("[\"Derived\", 12]");
        Assert.That(tc.baseKey, Is.EqualTo(0));
    }

    [Test]
    public void FromDoc_WrapsExceptions() {
        Assert.Throws<ParseException>(() => { ReifyString<TestClass>("{\"wrong\": \"structure\"}"); });
    }

    [Test]
    public void FromDoc_CalledWhenReifyingNullClass() {
        TestClass tc = null;
        var doc = Configs.ParseString("[\"Base\", 451]", FILENAME);
        Configs.Reify(ref tc, doc);
        Assert.Multiple(() => {
            Assert.That(tc, Is.Not.Null);
            Assert.That(tc.baseKey, Is.EqualTo(451));
        });
    }

    [Test]
    public void FromDoc_CalledWhenReifyingEmptyList() {
        var lst = new List<TestClass>();
        var doc = Configs.ParseString("[[\"Base\", 451]]", FILENAME);
        Configs.Reify(ref lst, doc);
        Assert.Multiple(() => {
            Assert.That(lst, Has.Count.EqualTo(1));
            Assert.That(lst[0].baseKey, Is.EqualTo(451));
        });
    }
}
