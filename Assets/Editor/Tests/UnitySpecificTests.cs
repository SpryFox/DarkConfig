#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using DarkConfig;

public class UnityTestFixture {
    protected T ReifyString<T>(string str) where T : new() {
        var doc = Config.LoadDocFromString(str, "UnityTypesConfigReifierTests_ReifyString_TestFilename");
        var result = default(T);
        Config.Reify(ref result, doc);
        return result;
    }
}

[TestFixture]
public class UnityTypesConfigReifierTests : UnityTestFixture {
    class TestClass {
        // scalars
        public Vector2 vector2Key = Vector2.zero;
        public Vector3 vector3Key = Vector3.zero;
        public Color colorKey = Color.black;
    }
    
    [Test]
    public void ConfigReifier_SetsVector2() {
        var tc = ReifyString<TestClass>(@"---
            vector2Key: [2, 10]
            ");
        Assert.AreEqual(tc.vector2Key, new Vector2(2, 10));
    }

    [Test]
    public void ConfigReifier_SetsVector2_ScalarArgument() {
        var tc = ReifyString<TestClass>(@"---
            vector2Key: 1.1
            ");
        Assert.AreEqual(tc.vector2Key, new Vector2(1.1f, 1.1f));
    }

    [Test]
    public void ConfigReifier_SetsVector2_OneArgument() {
        var tc = ReifyString<TestClass>(@"---
            vector2Key: [5]
            ");
        Assert.AreEqual(tc.vector2Key, new Vector2(5, 5));
    }

    [Test]
    public void ConfigReifier_SetsVector3() {
        var tc = ReifyString<TestClass>(@"---
            vector3Key: [10, 4, 0.5]
            ");
        Assert.AreEqual(tc.vector3Key, new Vector3(10, 4, 0.5f));
    }

    [Test]
    public void ConfigReifier_SetsVector3_ScalarArgument() {
        var tc = ReifyString<TestClass>(@"---
            vector3Key: 2
            ");
        Assert.AreEqual(tc.vector3Key, new Vector3(2, 2, 2));
    }

    [Test]
    public void ConfigReifier_SetsVector3_OneArgument() {
        var tc = ReifyString<TestClass>(@"---
            vector3Key: [3]
            ");
        Assert.AreEqual(tc.vector3Key, new Vector3(3, 3, 3));
    }

    [Test]
    public void ConfigReifier_SetsVector3_TwoArguments() {
        var tc = ReifyString<TestClass>(@"---
            vector3Key: [6, 7]
            ");
        Assert.AreEqual(tc.vector3Key, new Vector3(6, 7, 0));
    }

    [Test]
    public void ConfigReifier_SetsColor() {
        var tc = ReifyString<TestClass>(@"---
            colorKey: [1, 1, 1, 1]
            ");
        Assert.AreEqual(tc.colorKey, Color.white);
    }

    [Test]
    public void ConfigReifier_SetsColor_ThreeArguments() {
        var tc = ReifyString<TestClass>(@"---
            colorKey: [1, 0, 0]
            ");
        Assert.AreEqual(tc.colorKey, Color.red);
    }
}

[TestFixture]
public class UnityMonoBehaviourReifyTests : UnityTestFixture {
    [ConfigMandatory]
    class MonoBehaviourSubclass : MonoBehaviour {
        public int field1;
    }

    [Test]
    public void ReifierAttributes_MonoBehaviour_ForcesAllowMissing() {
        var doc = Config.LoadDocFromString(@"---
            field1: 1
        ", "ConfigReifier_ReifierAttributes_TestFilename");

        var obj = new GameObject("Test_ReifierAttributes");
        var mb = obj.AddComponent<MonoBehaviourSubclass>();
        Config.Reify(ref mb, doc, ReificationOptions.None);
        Assert.AreEqual(mb.field1, 1);

        Object.DestroyImmediate(obj);
    }
}

[TestFixture]
public class UnityColorRefierTests : UnityTestFixture {
    [Test]
    public void Color_FourFloats() {
        var c = ReifyString<Color>("[0.1, 0.5, 0.5, 0.9]");
        Assert.AreEqual(c, new Color(0.1f, 0.5f, 0.5f, 0.9f));
    }

    [Test]
    public void Color_ThreeFloats() {
        var c = ReifyString<Color>("[0.1, 0.5, 0.5]");
        Assert.AreEqual(c, new Color(0.1f, 0.5f, 0.5f, 1f));
    }

    [Test]
    public void Color_FourBytes() {
        var c = ReifyString<Color>("[47, 83, 200, 240]");
        Assert.AreEqual(0.1843137f, c.r, 0.0001);
        Assert.AreEqual(0.3254901f, c.g, 0.0001);
        Assert.AreEqual(0.7843137f, c.b, 0.0001);
        Assert.AreEqual(0.9411764f, c.a, 0.0001);
    }

    [Test]
    public void Color_ThreeBytes() {
        var c = ReifyString<Color>("[47, 83, 200]");
        Assert.AreEqual(0.1843137f, c.r, 0.0001);
        Assert.AreEqual(0.3254901f, c.g, 0.0001);
        Assert.AreEqual(0.7843137f, c.b, 0.0001);
        Assert.AreEqual(1f, c.a, 0.0001);
    }

    [Test]
    public void Color_Scalar_HexFourBytes() {
        var c = ReifyString<Color>("2b313efe");
        Assert.AreEqual(0.1686274f, c.r, 0.0001);
        Assert.AreEqual(0.1921568f, c.g, 0.0001);
        Assert.AreEqual(0.2431372f, c.b, 0.0001);
        Assert.AreEqual(0.9960784f, c.a, 0.0001);
    }

    [Test]
    public void Color_Scalar_HexThreeBytes() {
        var c = ReifyString<Color>("2b313e");
        Assert.AreEqual(0.1686274f, c.r, 0.0001);
        Assert.AreEqual(0.1921568f, c.g, 0.0001);
        Assert.AreEqual(0.2431372f, c.b, 0.0001);
        Assert.AreEqual(1f, c.a, 0.0001);
    }

    [Test]
    public void Color_Scalar_FourBytes() {
        var c = ReifyString<Color>("128, 128, 128, 128");
        Assert.AreEqual(0.50196f, c.r, 0.0001);
        Assert.AreEqual(0.50196f, c.g, 0.0001);
        Assert.AreEqual(0.50196f, c.b, 0.0001);
        Assert.AreEqual(0.50196f, c.a, 0.0001);
    }

    [Test]
    public void Color_Scalar_ThreeBytes() {
        var c = ReifyString<Color>("128, 128, 128");
        Assert.AreEqual(0.50196f, c.r, 0.0001);
        Assert.AreEqual(0.50196f, c.g, 0.0001);
        Assert.AreEqual(0.50196f, c.b, 0.0001);
        Assert.AreEqual(1f, c.a, 0.0001);
    }
}

[TestFixture]
public class UnityEditorUtilsTests : UnityTestFixture {
    [TearDown]
    public void TearDown() {
        // clean up entire test directory
        var tempDir = new System.IO.DirectoryInfo(Application.dataPath + "/TestTemp");
        if (tempDir.Exists) {
            tempDir.Delete(true);
        }
    }

    [Test]
    public void FindConfigFiles() {
        var files = EditorUtils.FindConfigFiles("/TestScenes/Resources/FileSearchConfigs");
        files.Sort();
        Assert.AreEqual(3, files.Count);
        Assert.AreEqual("index.bytes", files[0]);
        Assert.AreEqual("test1.bytes", files[1]);
        Assert.AreEqual("test2.bytes", files[2]);
    }

    [Test]
    public void WriteIndexFile() {
        var files = new List<string>();
        files.Add("Derp/Derp.bytes");
        files.Add("Derp/index.bytes");
        var total = EditorUtils.WriteIndexFile(files, "TestTemp/Resources/Derp/index.bytes");
        Assert.AreEqual(1, total);

        var indexFilename = Application.dataPath + "/" + "TestTemp/Resources/Derp/index.bytes";
        System.IO.StreamReader newIndex = new System.IO.StreamReader(indexFilename);
        var contents = newIndex.ReadToEnd();
        newIndex.Close();
        Assert.AreEqual(@"# automatically generated DarkConfig index file
#
---
- Derp/Derp.bytes
", contents);

        // verify that we can actually read the index file
        var doc = Config.LoadDocFromString(contents, indexFilename);
        Assert.AreEqual(DocNodeType.List, doc.Type);
        Assert.AreEqual(1, doc.Count);
        Assert.AreEqual("Derp/Derp.bytes", doc[0].StringValue);
        // TODO: actually load this index file
    }
}

[TestFixture]
public class UnityDocNodeExtensionsTests : UnityTestFixture {
    DocNode GetDocNode(string str) {
        var doc = Config.LoadDocFromString(str, "ConfigReifierTests_ReifyString_TestFilename");
        return doc;
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

#endif