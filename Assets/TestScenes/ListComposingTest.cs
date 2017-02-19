using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DarkConfig;
using System.IO;

public class ListComposingTest : IntegrationTestBase {
    struct Character {
        public int Height;
        public string Item;
    }
    List<Character> CharactersEndingInOrn;

	void Start () {
        UnityPlatform.Setup();
        Config.FileManager.AddSource(new ResourcesSource("TestComposedConfigs"));
        Config.Verbosity = LogVerbosity.Info;
        Config.FileManager.HotloadCheckInterval = 0.1f; // increase rate of checking so we don't have to wait as long for the test
        Config.Preload(RunTests);
	}

    void RunTests() {
        StartCoroutine(SimpleListTestsCoro());
    }

    IEnumerator SimpleListTestsCoro() {
        yield return null;

        bool loaded = true;

        // load all files from the ListDir into one list
        Config.LoadFilesAsList("ListDir/*", (d) => {
                Assert(d.Count == 4, "Should load all 4 characters into the composed docnode", d.Count);
                Config.Reify(ref CharactersEndingInOrn, d);
                loaded = true;
                return true;
        });
    
        Assert(CharactersEndingInOrn.Count == 4, "Reify all 4 chracters");
        Assert(CharactersEndingInOrn[0].Height == 12, "Aragorn should be the first character " + CharactersEndingInOrn[0].Item);
        Assert(CharactersEndingInOrn[0].Item == "Anduril", "Aragorn should have Anduril");

        yield return new WaitForSeconds(0.5f);

        // change file contents to ensure hotloading works
        var aragornPath = Application.dataPath + "/TestScenes/Resources/TestComposedConfigs/ListDir/aragorn.bytes";
        var writer = new StreamWriter(aragornPath, false);
        writer.Write(@"Item: Throne");
        writer.Flush();
        writer.Close();
        RefreshAssetDatabase();  // needed so that when the file is next loaded it has the new contents

        // wait for that reload to happen
        Debug.Log("ComposingTest waiting for reload");
        loaded = false;
        while(!loaded) {
            yield return new WaitForSeconds(0.1f);
        }

        Assert(CharactersEndingInOrn.Count == 4, "Didn't reify all 4 chracters");
        Assert(CharactersEndingInOrn[0].Height == 12, "Aragorn was not the first character");
        Assert(CharactersEndingInOrn[0].Item == "Throne", "Aragorn does not have the throne");

        // restore the old content
        writer = new StreamWriter(aragornPath, false);
        writer.Write(@"Height: 12
Item: Anduril");
        writer.Flush();
        writer.Close();
        RefreshAssetDatabase();

        Finish();
    }
}
