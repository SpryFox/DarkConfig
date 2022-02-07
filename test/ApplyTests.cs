using System.IO;
using NUnit.Framework;
using DarkConfig;

[TestFixture]
class ApplyTests {
    string tempDirPath;
    FileSource fileSource;

    class Glass {
        public int Capacity;
        public float Height;
    }
    Glass drinkContainer;

    void CreateFile(string filename, string contents) {
        var fullPath = Path.Combine(tempDirPath, filename);
        using (var sw = new StreamWriter(fullPath, false, new System.Text.UTF8Encoding())) {
            sw.Write(contents);
        }
    }

    void DeleteFile(string filename) {
        File.Delete(Path.Combine(tempDirPath, filename));
    }

    [SetUp]
    public void Setup() {
        tempDirPath = Path.Combine(Path.GetTempPath(), "ApplyTests");
        Directory.CreateDirectory(tempDirPath);

        Config.Platform = new ConsolePlatform();
        Config.Settings.HotloadCheckFrequencySeconds = 0.1f;
        fileSource = new FileSource(tempDirPath, hotload:true);
        Config.FileManager.AddSource(fileSource);
    }

    [TearDown]
    public void TearDown() {
        Directory.Delete(tempDirPath, true);
        Config.Platform = null;
        //Config.FileManager = null;
    }

    [Test]
    [Ignore("(temp)Disabled for now...")]
    public void Apply_RemoveReloadCallbackOnGC() {
        CreateFile("playerGlass.yaml", "{\"Capacity\": 12, \"Height\": 0.25}");
        Config.Preload();
        
        {
            Glass glass = null;
            Config.Apply("playerGlass", ref glass);
            Assert.AreEqual(glass.Capacity, 12);
        }

        Assert.AreEqual(1, Config.FileManager.CountReloadCallbacks());

        // trigger garbage collection here so temporary Glass gets GC'd
        System.GC.Collect();

        // Trigger a hotload to clean up reload callbacks.
        Config.FileManager.DoHotload();

        Assert.AreEqual(0, Config.FileManager.CountReloadCallbacks());
    }
}