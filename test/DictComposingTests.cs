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
        fileSource = new FileSource(tempDirPath, hotload: true);
        Configs.AddConfigSource(fileSource);
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
        Configs.ParseFilesAsMergedDict("*", d => {
            MixedDict = d;
            return true;
        });

        Assert.Multiple(() => {
            Assert.That(MixedDict, Is.Not.Null);
            Assert.That(MixedDict, Has.Count.EqualTo(5));
            Assert.That(MixedDict["Beetles"].As<int>(), Is.EqualTo(12));
            Assert.That(MixedDict["Version"].As<float>(), Is.EqualTo(1.3f));
            Assert.That(MixedDict["Treehouse"].As<bool>(), Is.True);
        });
        
        // Overwrite file contents
        CreateFile("items.yaml", "Chitin: 1000");

        // force a reload
        Configs.Update(1.0f);

        Assert.Multiple(() => {
            Assert.That(MixedDict, Has.Count.EqualTo(5));
            Assert.That(MixedDict.ContainsKey("Treehouse"), Is.False);
            Assert.That(MixedDict["Chitin"].As<int>(), Is.EqualTo(1000));
        });
    }
}
