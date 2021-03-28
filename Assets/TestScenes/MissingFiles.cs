#if false
using UnityEngine;
using System.Collections;
using DarkConfig;

public class MissingFiles : IntegrationTestBase {
	void Start () {
        UnityPlatform.Setup();
        Config.FileManager.AddSource(new ResourcesSource("MissingFiles"));
        Config.Preload(RunTests);
	}

    void RunTests() {
        // check the index after preload
        Assert(Config.FileManager.Files.Count > 0, "Should load some files");
        Assert(Config.FileManager.Files.Contains("spinner"), "Should load spinner:", Config.FileManager.Files.Count);

        // check that we can load existing files
        var spinnerDoc = Config.Load("spinner");
        // this file should be present so this should pass
        Assert(spinnerDoc.ContainsKey("key"), "Should have key in existing config file");

        try {
            Config.Load("nonexistent", (d) => {
                Assert(false, "Callback shouldn't be called");
                return false;
            });
            Assert(false, "Didn't throw exception for nonexistent file");
        } catch (ConfigFileNotFoundException e) {
            Assert(e.Message.IndexOf("nonexistent") > 0,
                    "Exception message should contain filename: '", e.Message, "'");
        }
        Finish();
    }
}
#endif