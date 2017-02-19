using UnityEditor;
using UnityEngine;
using System.Collections;


// some of the integration tests want to trigger an asset refresh because they've modified a file;
// this class exists because you can't have a MonoBehaviour that calls UnityEditor stuff
[InitializeOnLoad]
public class TestReloadWatcher {
    static TestReloadWatcher() {
        EditorApplication.update += Update;
    }

    static void Update() {
        if (IntegrationTestBase.WantsAssetRefresh) {
            IntegrationTestBase.WantsAssetRefresh = false;
            Debug.Log("Refreshing asset database");
            AssetDatabase.Refresh();
        }
    }
}
