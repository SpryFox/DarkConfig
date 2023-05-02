using System.IO;
using NUnit.Framework;
using DarkConfig;
using System.Collections.Generic;

[TestFixture]
class ListComposingTests {
    class Character {
        public int Height = 0;
        public string Item = "";
    }

    string tempDirPath;

    void CreateFile(string filename, string contents) {
        string fullPath = Path.Combine(tempDirPath, filename);
        using var sw = new StreamWriter(fullPath, false, new System.Text.UTF8Encoding());
        sw.Write(contents);
    }

    [SetUp]
    public void SetUp() {
        tempDirPath = Path.Combine(Path.GetTempPath(), "ListComposingTests");
        Directory.CreateDirectory(tempDirPath);

        Configs.Settings.EnableHotloading = true;
        Configs.Settings.HotloadCheckFrequencySeconds = 0.1f;
        Configs.AddConfigSource(new FileSource(tempDirPath, hotload: true));
    }

    [TearDown]
    public void TearDown() {
        Directory.Delete(tempDirPath, true);
        Configs.Clear();
    }

    [Test]
    public void MergeList() {
        CreateFile("aragorn.yaml", "Height: 12\nItem: Anduril");
        CreateFile("arathorn.yaml", "Height: 11\nItem: Son");
        CreateFile("beorn.yaml", "Height: 18\nItem: Bear");
        CreateFile("celeborn.yaml", "Height: 14\nItem: Silver");

        Configs.Preload();

        var CharactersEndingInOrn = new List<Character>();

        // load all files from the ListDir into one list
        Configs.LoadFilesAsList("*", d => {
            Assert.That(d.Count, Is.EqualTo(4));
            Configs.Reify(ref CharactersEndingInOrn, d);
            return true;
        });

        Assert.Multiple(() => {
            Assert.That(CharactersEndingInOrn, Has.Count.EqualTo(4));
            Assert.That(CharactersEndingInOrn[0].Height, Is.EqualTo(12));
            Assert.That(CharactersEndingInOrn[0].Item, Is.EqualTo("Anduril"));
        });

        // change file contents
        CreateFile("aragorn.yaml", "Height: 12\nItem: Throne");

        // Force hotload
        Configs.Update(1.0f);

        Assert.Multiple(() => {
            Assert.That(CharactersEndingInOrn, Has.Count.EqualTo(4));
            Assert.That(CharactersEndingInOrn[0].Height, Is.EqualTo(12));
            Assert.That(CharactersEndingInOrn[0].Item, Is.EqualTo("Throne"));
        });
    }
}
