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
    }

    [TearDown]
    public void TearDown() {
        Directory.Delete(tempDirPath, true);
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
        foreach (object _ in fileSource.StepPreload()) { }

        Assert.Multiple(() => {
            Assert.That(fileSource.AllFiles, Has.Count.EqualTo(1));
            Assert.That(fileSource.AllFiles.ContainsKey("derp"), Is.True);
        });

        var fileInfo = fileSource.AllFiles["derp"];
        Assert.Multiple(() => {
            Assert.That(fileInfo, Is.Not.Null);
            Assert.That(fileInfo.Name, Is.EqualTo("derp"));
            Assert.That(fileInfo.Parsed["key"].StringValue, Is.EqualTo("value"));
        });
    }

    [Test]
    public void Hotload_ExistingFile() {
        // Preload a single file.
        CreateFile("derp.yaml", "key: value");
        var fileSource = new FileSource(tempDirPath, hotload:true);
        foreach (object _ in fileSource.StepPreload()) { }
        Assert.Multiple(() => {
            Assert.That(fileSource.AllFiles.Count, Is.EqualTo(1));
            Assert.That(fileSource.AllFiles.ContainsKey("derp"), Is.True);
        });

        // Modify the file.
        CreateFile("derp.yaml", "key: value2");
        
        // Hotload the file.
        var changedFiles = new List<string>();
        fileSource.Hotload(changedFiles);

        // Make sure the contents were updated.
        Assert.Multiple(() => {
            Assert.That(changedFiles, Has.Count.EqualTo(1));
            Assert.That(changedFiles[0], Is.EqualTo("derp"));
            Assert.That(fileSource.AllFiles.ContainsKey("derp"), Is.True);
        });
        var fileInfo = fileSource.AllFiles["derp"];
        Assert.Multiple(() => {
            Assert.That(fileInfo, Is.Not.Null);
            Assert.That(fileInfo.Name, Is.EqualTo("derp"));
            Assert.That(fileInfo.Parsed["key"].StringValue, Is.EqualTo("value2"));
        });
    }

    [Test]
    public void Hotload_DeletedFile() {
        CreateFile("derp.yaml", "key: value");
        CreateFile("durr.yaml", "a: b");

        var fileSource = new FileSource(tempDirPath, hotload:true);
        foreach (object _ in fileSource.StepPreload()) { }
        Assert.That(fileSource.AllFiles, Has.Count.EqualTo(2));

        DeleteFile("durr.yaml");

        var changedFiles = new List<string>();
        fileSource.Hotload(changedFiles);

        Assert.Multiple(() => {
            Assert.That(fileSource.AllFiles, Has.Count.EqualTo(1));
            Assert.That(fileSource.AllFiles.ContainsKey("durr"), Is.False);
        });
    }

    [Test]
    public void Hotload_CreatedFile() {
        CreateFile("derp.yaml", "key: value");

        var fileSource = new FileSource(tempDirPath, hotload:true);
        foreach (object _ in fileSource.StepPreload()) { }
        Assert.That(fileSource.AllFiles, Has.Count.EqualTo(1));

        CreateFile("durr.yaml", "a: b");
        
        var changedFiles = new List<string>();
        fileSource.Hotload(changedFiles);

        Assert.Multiple(() => {
            Assert.That(fileSource.AllFiles, Has.Count.EqualTo(2));
            
            Assert.That(fileSource.AllFiles.ContainsKey("derp"), Is.True);
            Assert.That(fileSource.AllFiles.ContainsKey("durr"), Is.True);
            
            Assert.That(fileSource.AllFiles["durr"].Parsed["a"].StringValue, Is.EqualTo("b"));
        });
    }

    [Test]
    public void Hotload_CreatedThreeFiles() {
        CreateFile("derp.yaml", "key: value");

        var fileSource = new FileSource(tempDirPath, hotload:true);
        foreach (object _ in fileSource.StepPreload()) { }

        CreateFile("durr.yaml", "a: b");
        CreateFile("hurr.yaml", "x: y");
        CreateFile("err.yaml", "u: v");
        
        var changedFiles = new List<string>();
        fileSource.Hotload(changedFiles);

        Assert.Multiple(() => {
            Assert.That(fileSource.AllFiles, Has.Count.EqualTo(4));
            Assert.That(fileSource.AllFiles.ContainsKey("derp"), Is.True);
            Assert.That(fileSource.AllFiles.ContainsKey("hurr"), Is.True);
            Assert.That(fileSource.AllFiles.ContainsKey("err"), Is.True);
            Assert.That(fileSource.AllFiles["durr"].Parsed["a"].StringValue, Is.EqualTo("b"));
            Assert.That(fileSource.AllFiles["hurr"].Parsed["x"].StringValue, Is.EqualTo("y"));
            Assert.That(fileSource.AllFiles["err"].Parsed["u"].StringValue, Is.EqualTo("v"));
        });
    }

    [Test]
    public void Hotload_CreatedFileTwice() {
        CreateFile("derp.yaml", "key: value");

        var fileSource = new FileSource(tempDirPath, hotload:true);
        foreach (object _ in fileSource.StepPreload()) { }

        CreateFile("durr.yaml", "a: b");
        CreateFile("hurr.yaml", "x: y");
        
        var changedFiles = new List<string>();
        fileSource.Hotload(changedFiles);

        Assert.Multiple(() => {
            Assert.That(fileSource.AllFiles, Has.Count.EqualTo(3));
            Assert.That(fileSource.AllFiles.ContainsKey("derp"), Is.True);
            Assert.That(fileSource.AllFiles.ContainsKey("durr"), Is.True);
            Assert.That(fileSource.AllFiles.ContainsKey("hurr"), Is.True);
            Assert.That(fileSource.AllFiles["derp"].Parsed["key"].StringValue, Is.EqualTo("value"));
            Assert.That(fileSource.AllFiles["durr"].Parsed["a"].StringValue, Is.EqualTo("b"));
            Assert.That(fileSource.AllFiles["hurr"].Parsed["x"].StringValue, Is.EqualTo("y"));
        });
    }
}
