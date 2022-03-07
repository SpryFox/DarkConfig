using System.IO;
using NUnit.Framework;
using DarkConfig;

[TestFixture]
class DictComposingTests {
    string tempDirPath;
    FileSource fileSource;

    void CreateFile(string filename, string contents) {
        File.WriteAllText(Path.Combine(tempDirPath, filename), contents);
    }

    [SetUp]
    public void SetUp() {
        tempDirPath = Path.Combine(Path.GetTempPath(), "DictComposingTests");
        Directory.CreateDirectory(tempDirPath);
        
        Configs.Settings.EnableHotloading = true;
        Configs.Settings.HotloadCheckFrequencySeconds = 0.1f;
        fileSource = new FileSource(tempDirPath, hotload:true);
        Configs.FileManager.AddSource(fileSource);
    }

    [TearDown]
    public void TearDown() {
        Directory.Delete(tempDirPath, true);
        Configs.Clear();
    }

    [Test]
    public void MergedDict() {
        CreateFile("globals.yaml", "Beetles: 12\nBirdName: Shabazz\nVersion: 1.2");
        CreateFile("items.yaml", "Treehouse: true");
        CreateFile("rooms.yaml", "Version: 1.3\nrooms:\n  - Overthorax\n  - Chirpinghouse\n  - Antennagate\n  - Subchitin");

        Configs.Preload();

        DocNode MixedDict = null;

        // load all files from the DictDir into one dict
        Configs.LoadFilesAsMergedDict("*", d => {
            MixedDict = d;
            return true;
        });

        Assert.IsNotNull(MixedDict);

        Assert.AreEqual(5, MixedDict.Count);
        Assert.AreEqual(12, MixedDict["Beetles"].As<int>());
        Assert.AreEqual(1.3f, MixedDict["Version"].As<float>());
        Assert.True(MixedDict["Treehouse"].As<bool>());

        // Overwrite file contents
        CreateFile("items.yaml", "Chitin: 1000");
        
        // force a reload
        Configs.Update(1.0f);

        Assert.AreEqual(5, MixedDict.Count);
        Assert.False(MixedDict.ContainsKey("Treehouse"));
        Assert.AreEqual(1000, MixedDict["Chitin"].As<int>());
    }
}