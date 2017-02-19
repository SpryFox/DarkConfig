using UnityEngine;
using NUnit.Framework;
using DarkConfig;
using System.Collections.Generic;
using System;

[TestFixture]
class PostDocFacts {
    class PostDocClass {
        public int baseKey;
        public System.Func<PostDocClass, PostDocClass> PostDoc = (d) => {
                d.baseKey += 1;
                return d;
            };
    }

    class PostDocClass2 {
        public int baseKey = 0;
        System.Func<PostDocClass, PostDocClass> PostDoc;
    }


    const string c_filename = "PostDocFacts_TestFilename";

    T ReifyString<T>(string str) where T: new() {
        var doc = Config.LoadDocFromString(str, c_filename);
        T tc = default(T);
        ConfigReifier.Reify(ref tc, doc);
        return tc;
    }

    [Test]
    public void PostDoc_IsCalled() {
        var tc = ReifyString<PostDocClass>("baseKey: 10");
        Assert.True(tc.PostDoc != null);
        Assert.AreEqual(tc.baseKey, 11);
    }

    [Test]
    public void PostDoc_IsNull() {
        var doc = Config.LoadDocFromString("baseKey: 10", c_filename);
        PostDocClass2 tc = null;
        ConfigReifier.Reify(ref tc, doc);
        Assert.AreEqual(tc.baseKey, 10);
    }

    [Test]
    public void PostDoc_MultipleCalled() {
        bool secondCalled = false;
        var tc = ReifyString<PostDocClass>("baseKey: 10");
        tc.PostDoc += (x) => {
            secondCalled = true;
            return x;
        };
        var doc = Config.LoadDocFromString("baseKey: 10", c_filename);
        ConfigReifier.Reify(ref tc, doc);
        Assert.AreEqual(tc.PostDoc.GetInvocationList().Length, 2);
        Assert.True(secondCalled);
        Assert.AreEqual(tc.baseKey, 11);
    }
}