using System;
using System.Collections.Generic;
using DarkConfig;
using NUnit.Framework;
using System.IO;

[TestFixture]
class FileSourceTests {
    string tempDirPath;

    [SetUp]
    public void SetUp() {
        tempDirPath = Path.Combine(Path.GetTempPath(), "FileSourceTests");
        Directory.CreateDirectory(tempDirPath);
        Config.Platform = new ConsolePlatform();
    }

    [TearDown]
    public void TearDown() {
        Directory.Delete(tempDirPath, true);
        Config.Platform = null;
    }

    void CreateFile(string filename, string contents) {
        var fullPath = Path.Combine(tempDirPath, filename);
        using (var sw = new StreamWriter(fullPath, false, new System.Text.UTF8Encoding())) {
            sw.Write(contents);
        }
    }

    void DeleteFile(string filename) {
        File.Delete(Path.Combine(tempDirPath, filename));
    }

    [Test]
    public void OpenOneFile() {
        CreateFile("derp.yaml", "key: value");

        var fileSource = new FileSource(tempDirPath, hotload:true);
        fileSource.Preload();
        
        Assert.AreEqual(1, fileSource.AllFiles.Count);
        Assert.IsTrue(fileSource.AllFiles.ContainsKey("derp"));
        
        var fileInfo = fileSource.AllFiles["derp"];
        Assert.IsNotNull(fileInfo);
        Assert.AreEqual("derp", fileInfo.Name);
        Assert.AreEqual("value", fileInfo.Parsed["key"].StringValue);
    }

    [Test]
    public void Hotload_ExistingFile() {
        // Preload a single file.
        CreateFile("derp.yaml", "key: value");
        var fileSource = new FileSource(tempDirPath, hotload:true);
        fileSource.Preload();
        Assert.AreEqual(1, fileSource.AllFiles.Count);
        Assert.IsTrue(fileSource.AllFiles.ContainsKey("derp"));

        // Modify the file.
        CreateFile("derp.yaml", "key: value2");
        
        // Hotload the file.
        var changedFiles = new List<string>();
        fileSource.Hotload(changedFiles);

        // Make sure the contents were updated.
        Assert.AreEqual(1, changedFiles.Count);
        Assert.AreEqual("derp", changedFiles[0]);
        Assert.IsTrue(fileSource.AllFiles.ContainsKey("derp"));
        var fileInfo = fileSource.AllFiles["derp"];
        Assert.IsNotNull(fileInfo);
        Assert.AreEqual("derp", fileInfo.Name);
        Assert.AreEqual("value2", fileInfo.Parsed["key"].StringValue);
    }

    [Test]
    public void Hotload_DeletedFile() {
        CreateFile("derp.yaml", "key: value");
        CreateFile("durr.yaml", "a: b");

        var fileSource = new FileSource(tempDirPath, hotload:true);
        fileSource.Preload();
        Assert.AreEqual(2, fileSource.AllFiles.Count);

        DeleteFile("durr.yaml");

        var changedFiles = new List<string>();
        fileSource.Hotload(changedFiles);
        
        Assert.AreEqual(1, fileSource.AllFiles.Count);
        Assert.IsFalse(fileSource.AllFiles.ContainsKey("durr"));
    }

    [Test]
    public void Hotload_CreatedFile() {
        CreateFile("derp.yaml", "key: value");

        var fileSource = new FileSource(tempDirPath, hotload:true);
        fileSource.Preload();
        Assert.AreEqual(1, fileSource.AllFiles.Count);

        CreateFile("durr.yaml", "a: b");
        
        var changedFiles = new List<string>();
        fileSource.Hotload(changedFiles);

        Assert.AreEqual(2, fileSource.AllFiles.Count);
        
        Assert.IsTrue(fileSource.AllFiles.ContainsKey("derp"));
        Assert.IsTrue(fileSource.AllFiles.ContainsKey("durr"));
        
        Assert.AreEqual("b", fileSource.AllFiles["durr"].Parsed["a"].StringValue);
    }

    [Test]
    public void Hotload_CreatedThreeFiles() {
        CreateFile("derp.yaml", "key: value");

        var fileSource = new FileSource(tempDirPath, hotload:true);
        fileSource.Preload();

        CreateFile("durr.yaml", "a: b");
        CreateFile("hurr.yaml", "x: y");
        CreateFile("err.yaml", "u: v");
        
        var changedFiles = new List<string>();
        fileSource.Hotload(changedFiles);

        Assert.AreEqual(4, fileSource.AllFiles.Count);
        Assert.IsTrue(fileSource.AllFiles.ContainsKey("derp"));
        Assert.IsTrue(fileSource.AllFiles.ContainsKey("hurr"));
        Assert.IsTrue(fileSource.AllFiles.ContainsKey("err"));
        Assert.AreEqual("b", fileSource.AllFiles["durr"].Parsed["a"].StringValue);
        Assert.AreEqual("y", fileSource.AllFiles["hurr"].Parsed["x"].StringValue);
        Assert.AreEqual("v", fileSource.AllFiles["err"].Parsed["u"].StringValue);
    }

    [Test]
    public void Hotload_CreatedFileTwice() {
        CreateFile("derp.yaml", "key: value");

        var fileSource = new FileSource(tempDirPath, hotload:true);
        fileSource.Preload();

        CreateFile("durr.yaml", "a: b");
        CreateFile("hurr.yaml", "x: y");
        
        var changedFiles = new List<string>();
        fileSource.Hotload(changedFiles);

        Assert.AreEqual(3, fileSource.AllFiles.Count);
        Assert.IsTrue(fileSource.AllFiles.ContainsKey("derp"));
        Assert.IsTrue(fileSource.AllFiles.ContainsKey("durr"));
        Assert.IsTrue(fileSource.AllFiles.ContainsKey("hurr"));
        Assert.AreEqual("value", fileSource.AllFiles["derp"].Parsed["key"].StringValue);
        Assert.AreEqual("b", fileSource.AllFiles["durr"].Parsed["a"].StringValue);
        Assert.AreEqual("y", fileSource.AllFiles["hurr"].Parsed["x"].StringValue);
    }
}