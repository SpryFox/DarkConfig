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
}