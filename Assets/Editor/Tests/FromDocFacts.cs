using UnityEngine;
using NUnit.Framework;
using DarkConfig;
using System.Collections.Generic;
using System;

[TestFixture]
class FromDocFacts {
    class TestClass {
        public int baseKey;
        public static TestClass FromDoc(TestClass existing, DocNode doc) {
            if(doc.Type != DocNodeType.List) {
                throw new System.ArgumentException("Not a list! " + doc.Type);
            }

            if(doc[0].StringValue == "Derived") {
                TestClassDerived derivedExisting;
                if(existing is TestClassDerived) {
                    derivedExisting = (TestClassDerived)existing;
                } else {
                    derivedExisting = new TestClassDerived();
                }
                derivedExisting.derivedKey = Convert.ToInt32(doc[1].StringValue, System.Globalization.CultureInfo.InvariantCulture);
                return derivedExisting;
            } else {
                if(!(existing is TestClass)) {
                    existing = new TestClass();
                }
                existing.baseKey = Convert.ToInt32(doc[1].StringValue, System.Globalization.CultureInfo.InvariantCulture);
                return existing;
            }
        }
    }

    class TestClassDerived : TestClass {
        public int derivedKey;
    }

    const string c_filename = "FromDocFacts_TestFileName";

    T ReifyString<T>(string str) where T: new() {
        var doc = Config.LoadDocFromString(str, "FromDocFacts_ReifyString_TestFileName");
        T tc = default(T);
        ConfigReifier.Reify(ref tc, doc);
        return tc;
    }

    [Test]
    public void FromDoc_CalledToReify() {
        var tc = ReifyString<TestClass>("[\"Base\", 12]");
        Assert.AreEqual(tc.baseKey, 12);
    }

    [Test]
    public void FromDoc_SpawnsDerivedClass() {
        var tc = ReifyString<TestClass>("[\"Derived\", 12]");
        Assert.AreEqual(tc.baseKey, 0);
        Assert.IsTrue(tc is TestClassDerived);
    }

    [Test]
    public void FromDoc_UpdatesTestClass() {
        var tc = new TestClass { baseKey = 15 };
        var saved = tc;
        var doc = Config.LoadDocFromString("[\"Base\", 99]", c_filename);
        ConfigReifier.Reify(ref tc, doc);
        Assert.AreSame(tc, saved);
        Assert.AreEqual(tc.baseKey, 99);
    }

    [Test]
    public void FromDoc_UpdatesDerived() {
        TestClass tc = new TestClassDerived { baseKey = 1, derivedKey = 2 };
        var saved = tc;
        var doc = Config.LoadDocFromString("[\"Derived\", 66]", c_filename);
        ConfigReifier.Reify(ref tc, doc);
        Assert.AreSame(tc, saved);
        Assert.AreEqual(tc.baseKey, 1);
        Assert.AreEqual(((TestClassDerived)tc).derivedKey, 66);
    }

    [Test]
    public void FromDoc_UpdatesDerived_AsBase() {
        TestClass tc = new TestClassDerived { baseKey = 4, derivedKey = 5 };
        var saved = tc;
        var doc = Config.LoadDocFromString("[\"Base\", 123]", c_filename);
        ConfigReifier.Reify(ref tc, doc);
        Assert.AreSame(tc, saved);
        Assert.AreEqual(tc.baseKey, 123);
        Assert.AreEqual(((TestClassDerived)tc).derivedKey, 5);
    }

    [Test]
    public void FromDoc_OverwritesBase_WithDerived() {
        TestClass tc = new TestClass { baseKey = 19 };
        var saved = tc;
        var doc = Config.LoadDocFromString("[\"Derived\", 321]", c_filename);
        ConfigReifier.Reify(ref tc, doc);
        Assert.IsFalse(object.ReferenceEquals(tc, saved));
        Assert.IsTrue(tc is TestClassDerived);
        Assert.AreEqual(((TestClassDerived)tc).derivedKey, 321);
    }

    [Test]
    [Ignore("It's not completely clear how we should call parent class FromDocs when reifying a derived object")]
    public void FromDoc_SpawnsDerivedClass_WhenCastAsDerived() {
        var tc = ReifyString<TestClassDerived>("[\"Derived\", 12]");
        Assert.AreEqual(tc.baseKey, 0);
        Assert.IsTrue(tc is TestClassDerived);
    }

#if UNITY_5_6
    [Test]
    public void FromDoc_WrapsExceptions() {
        Assert.That(() => {
            ReifyString<TestClass>("{\"wrong\": \"structure\"}");
        }, Throws.TypeOf<ParseException>());
    }
#else
    [Test]
    [ExpectedException(typeof(ParseException))]
    public void FromDoc_WrapsExceptions() {
        ReifyString<TestClass>("{\"wrong\": \"structure\"}");
    }
#endif

    [Test]
    public void FromDoc_CalledWhenReifyingNullClass() {
        TestClass tc = null;
        var doc = Config.LoadDocFromString("[\"Base\", 451]", c_filename);
        ConfigReifier.Reify(ref tc, doc);
        Assert.IsNotNull(tc);
        Assert.AreEqual(tc.baseKey, 451);
    }

    [Test]
    public void FromDoc_CalledWhenReifyingEmptyList() {
        List<TestClass> lst = new List<TestClass>();
        var doc = Config.LoadDocFromString("[[\"Base\", 451]]", c_filename);
        ConfigReifier.Reify(ref lst, doc);
        Assert.AreEqual(1, lst.Count);
        Assert.AreEqual(lst[0].baseKey, 451);
    }
}