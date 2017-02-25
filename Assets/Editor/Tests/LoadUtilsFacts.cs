using System.Collections.Generic;
using DarkConfig;
using NUnit.Framework;

[TestFixture]
class LoadUtilsFacts {
    class TestClass {
        public int? intKey = null;
        public string stringKey = null;

        public string basedOn = null;

        public static string getBasedOn(TestClass c) {
            return c.basedOn;
        }
    }

    const string c_filename = "LoadUtilsFacts_TestFileName";

    DocNode FromString(string s) {
        return Config.LoadDocFromString(s, c_filename);
    }

    [Test]
    public void LoadUtils_SetParentDefaults_LoadSimple() {
        var doc = FromString(@"
            basic:
                intKey: 1
                stringKey: 2
            ");
        
        Dictionary<string, TestClass> d = null;
        LoadUtils.SetParentDefaults(ref d, doc, TestClass.getBasedOn);
        Assert.AreEqual(1, d.Count);
        Assert.AreEqual(1, d["basic"].intKey.Value);
        Assert.AreEqual("2", d["basic"].stringKey);
    }

    [Test]
    public void LoadUtils_SetParentDefaults_LoadParented() {
        var doc = FromString(@"
            default:
                intKey: 0
                stringKey: a
            basic:
                basedOn: default
                intKey: 1
            ");
        
        Dictionary<string, TestClass> d = null;
        LoadUtils.SetParentDefaults(ref d, doc, TestClass.getBasedOn);
        Assert.AreEqual(2, d.Count);
        Assert.AreEqual(1, d["basic"].intKey.Value);
        Assert.AreEqual("a", d["basic"].stringKey);
    }

    [Test]
    public void LoadUtils_SetParentDefaults_LoadParentedRecursive() {
        var doc = FromString(@"
            default:
                intKey: 0
                stringKey: a
            basic:
                basedOn: default
                intKey: 1
            deeper:
                basedOn: basic
                intKey: 2
            ");
        
        Dictionary<string, TestClass> d = null;
        LoadUtils.SetParentDefaults(ref d, doc, TestClass.getBasedOn);
        Assert.AreEqual(3, d.Count);
        Assert.AreEqual(2, d["deeper"].intKey.Value);
        Assert.AreEqual("a", d["deeper"].stringKey);
    }


    [Test]
    public void LoadUtils_SetParentDefaults_LoadNullableRecursive() {
        var doc = FromString(@"
            default:
                intKey: 0
                stringKey: a
            basic:
                basedOn: default
                stringKey: basicString
            deeper:
                basedOn: basic
                stringKey: deeperString
            ");
        
        Dictionary<string, TestClass> d = null;
        LoadUtils.SetParentDefaults(ref d, doc, TestClass.getBasedOn);
        Assert.AreEqual(3, d.Count);
        Assert.AreEqual(0, d["deeper"].intKey.Value);
        Assert.AreEqual("deeperString", d["deeper"].stringKey);
    }

    [Test]
    public void LoadUtils_SetParentDefaults_LoadParentedTwice() {
        var doc = FromString(@"
            default:
                intKey: 0
                stringKey: a
            basic:
                basedOn: default
                intKey: 1
            ");
        
        Dictionary<string, TestClass> d = null;
        LoadUtils.SetParentDefaults(ref d, doc, TestClass.getBasedOn);
        var doc2 = FromString(@"
            default:
                intKey: 1
                stringKey: b
            basic:
                basedOn: default
            ");
        LoadUtils.SetParentDefaults(ref d, doc2, TestClass.getBasedOn);
        Assert.AreEqual(2, d.Count);
        Assert.AreEqual(1, d["basic"].intKey.Value);
        Assert.AreEqual("b", d["basic"].stringKey);
    }


    [Test]
    public void LoadUtils_SetParentDefaults_IgnoreFields() {
        var doc = FromString(@"
            default:
                intKey: 0
                stringKey: a
            basic:
                basedOn: default
            ");
        
        Dictionary<string, TestClass> d = null;
        LoadUtils.SetParentDefaults(ref d, doc, TestClass.getBasedOn, 
            new string[] {"basedOn", "intKey"});
        Assert.AreEqual(null, d["basic"].intKey);
        Assert.AreEqual("a", d["basic"].stringKey);
    }
}