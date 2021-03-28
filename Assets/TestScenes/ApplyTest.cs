#if false
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DarkConfig;
using System.IO;

public class ApplyTest : IntegrationTestBase {
    class Glass {
        #pragma warning disable 649
        public int Capacity;
        public float Height;
        public Vector3 Position;
        #pragma warning restore 649
    }
    Glass drinkContainer;

	void Start () {
        UnityPlatform.Setup();
        Config.FileManager.HotloadCheckInterval = 0.1f; // increase rate of checking so we don't have to wait as long for the test
        Config.FileManager.AddSource(new ResourcesSource("TestApply"));
        Config.Preload(RunTests);
	}

    void RunTests() {
        StartCoroutine(TestsCoro());
    }

    IEnumerator TestsCoro() {
        yield return null;

        TestTemporaryLoad();
        Assert(Config.FileManager.GetReloadDelegateCount() == 1, "Should have 1 delegate registered, is", Config.FileManager.GetReloadDelegateCount());

        // trigger garbage collection here so temporary Glass gets nulled
        System.GC.Collect();

        // force hotload to trigger cleanup of callbacks
        Config.FileManager.CallAllDelegates();

        Assert(Config.FileManager.GetReloadDelegateCount() == 0, "Should have 0 delegates registered, is", Config.FileManager.GetReloadDelegateCount());

        Finish();
    }

    void TestTemporaryLoad() {
        Glass g = null;
        Config.Apply("playerGlass", ref g);
        Assert(g.Capacity == 12, "Loaded playerGlass capacity", 12);
    }
}
#endif