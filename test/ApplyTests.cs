using System.IO;
using NUnit.Framework;
using DarkConfig;

[TestFixture]
class ApplyTests {
    string tempDirPath;
    FileSource fileSource;

    class Glass {
        public int Capacity = 0;
        public float Height = 0.0f;
    }

    void CreateFile(string filename, string contents) {
        var fullPath = Path.Combine(tempDirPath, filename);
        using (var sw = new StreamWriter(fullPath, false, new System.Text.UTF8Encoding())) {
            sw.Write(contents);
        }
    }

    [SetUp]
    public void Setup() {
        tempDirPath = Path.Combine(Path.GetTempPath(), "ApplyTests");
        Directory.CreateDirectory(tempDirPath);
        
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
    [Ignore("(temp)Disabled for now...")]
    public void Apply_RemoveReloadCallbackOnGC() {
        CreateFile("playerGlass.yaml", "{\"Capacity\": 12, \"Height\": 0.25}");
        Configs.Preload();
        
        {
            Glass glass = null;
            Configs.Apply("playerGlass", ref glass);
            Assert.AreEqual(glass.Capacity, 12);
            Assert.AreEqual(glass.Height, 0.25f);
        }

        Assert.AreEqual(1, Configs.FileManager.CountReloadCallbacks());

        // trigger garbage collection here so temporary Glass gets GC'd
        System.GC.Collect();

        // Trigger a hotload to clean up reload callbacks.
        Configs.FileManager.DoHotload();

        Assert.AreEqual(0, Configs.FileManager.CountReloadCallbacks());
    }
}