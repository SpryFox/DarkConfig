using System.IO;
using NUnit.Framework;
using DarkConfig;
using System.Text.RegularExpressions;

[TestFixture]
class MissingFilesTests {
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
        Configs.AddConfigSource(new FileSource(tempDirPath, hotload: true));
    }

    [TearDown]
    public void TearDown() {
        Directory.Delete(tempDirPath, true);
        Configs.Clear();
    }

    [Test]
    public void MissingFiles() {
        CreateFile("spinner.yaml", "key: ok");

        Configs.Preload();

        // check the index after preload
        var filenames = Configs.GetFilenamesMatchingRegex(new Regex(".*"));
        Assert.Greater(filenames.Count, 0);

        Assert.IsTrue(filenames.Contains("spinner"));

        // check that we can load existing files
        var spinnerDoc = Configs.ParseFile("spinner");

        // this file should be present so this should pass
        Assert.IsTrue(spinnerDoc.ContainsKey("key"));

        Assert.Throws<ConfigFileNotFoundException>(() => {
            Configs.ParseFile("nonexistent", (d) => {
                Assert.Fail("Callback shouldn't be called");
                return false;
            });
        });
    }
}
