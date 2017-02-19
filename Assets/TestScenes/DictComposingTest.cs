using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DarkConfig;
using System.IO;

public class DictComposingTest : IntegrationTestBase {
    void Start () {
        UnityPlatform.Setup();
        Config.FileManager.AddSource(new ResourcesSource("TestComposedConfigs"));
        Config.FileManager.HotloadCheckInterval = 0.1f; // increase rate of checking so we don't have to wait as long for the test
        Config.Preload(RunTests);
    }

    void RunTests() {
        StartCoroutine(MergedDictTestsCoro());
    }

    public DocNode MixedDict;

    IEnumerator MergedDictTestsCoro() {
        yield return null;

        bool loaded = true;

        // load all files from the DictDir into one dict
        Config.LoadFilesAsMergedDict("DictDir/*", (d) => {
                Assert(d.Count == 5, "Expecting 5 properties in merged Dict, was", d.Count);
                MixedDict = d;
                loaded = true;
                return true;
        });

        Assert(MixedDict.Count == 5, "Expecting 5 properties in merged Dict, was", MixedDict.Count);
        Assert(MixedDict["Beetles"].AsInt() == 12, "Expected 12 beetles, was", MixedDict["Beetles"].AsInt());
        Assert(MixedDict["Version"].AsFloat() == 1.3f, "Version should be overwritten to 1.3, was", MixedDict["Version"].AsFloat());
        Assert(MixedDict["Treehouse"].AsBool(), "Treehouse should be true, was", MixedDict["Treehouse"].AsBool());


        yield return new WaitForSeconds(0.25f);

        // force a reload
        var itemsPath = Application.dataPath + "/TestScenes/Resources/TestComposedConfigs/DictDir/items.bytes";
        var writer = new StreamWriter(itemsPath, false);
        writer.Write(@"Chitin: 1000");
        writer.Flush();
        writer.Close();
        RefreshAssetDatabase();  // needed so that when the file is next loaded it has the new contents

        // wait for that reload to happen
        loaded = false;
        Debug.Log("DictComposingTest waiting for reload");
        while (!loaded) {
            yield return new WaitForSeconds(0.1f);
        }

        Assert(MixedDict.Count == 5, "Should load all 5 items, got", MixedDict.Count);
        Assert(!MixedDict.ContainsKey("Treehouse"), "Treehouse should not be present");
        Assert(MixedDict["Chitin"].AsInt() == 1000, "Chitin should be 1000, is", MixedDict["Chitin"].AsInt());

        // restore the old content
        writer = new StreamWriter(itemsPath, false);
        writer.Write(@"Treehouse: true");
        writer.Flush();
        writer.Close();
        RefreshAssetDatabase();

        Finish();
    }
}
