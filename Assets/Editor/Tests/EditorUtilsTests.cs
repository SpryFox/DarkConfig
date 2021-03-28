using UnityEngine;
using DarkConfig;
using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public class EditorUtilsFacts {
    [TearDown]
    public void TearDown() {
        // clean up entire test directory
        var tempDir = new System.IO.DirectoryInfo(Application.dataPath + "/TestTemp");
        if(tempDir.Exists) {
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