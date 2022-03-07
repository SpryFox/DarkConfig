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
        var fullPath = Path.Combine(tempDirPath, filename);
        using (var sw = new StreamWriter(fullPath, false, new System.Text.UTF8Encoding())) {
            sw.Write(contents);
        }
    }

    [SetUp]
    public void SetUp() {
        tempDirPath = Path.Combine(Path.GetTempPath(), "ListComposingTests");
        Directory.CreateDirectory(tempDirPath);

        Configs.Settings.EnableHotloading = true;
        Configs.Settings.HotloadCheckFrequencySeconds = 0.1f;
        Configs.FileManager.AddSource(new FileSource(tempDirPath, hotload:true));
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

        var CharactersEndingInOrn = new List<Character>();;

        // load all files from the ListDir into one list
        Configs.LoadFilesAsList("*", d => {
            Assert.AreEqual(4, d.Count);
            Configs.Reify(ref CharactersEndingInOrn, d);
            return true;
        });
    
        Assert.AreEqual(4, CharactersEndingInOrn.Count);
        Assert.AreEqual(12, CharactersEndingInOrn[0].Height);
        Assert.AreEqual("Anduril", CharactersEndingInOrn[0].Item);

        // change file contents
        CreateFile("aragorn.yaml", "Height: 12\nItem: Throne");

        // Force hotload
        Configs.Update(1.0f);
        
        Assert.AreEqual(4, CharactersEndingInOrn.Count);
        Assert.AreEqual(12, CharactersEndingInOrn[0].Height);
        Assert.AreEqual("Throne", CharactersEndingInOrn[0].Item);
    }
}