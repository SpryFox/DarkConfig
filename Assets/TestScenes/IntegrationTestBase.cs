#if false
using UnityEngine;
using System.Linq;
using DarkConfig;

public class IntegrationTestBase : MonoBehaviour {
    public bool IsFailed = false;

    public void Assert(bool statement, params object[] msgs) {
        if(IsFailed) return; // ignore subsequent failures for now
        if(!statement) {
            Debug.LogError("Assertion failed:" + string.Join(" ", msgs.Select(x => x == null ? "[null]" : x.ToString()).ToArray()));
            IsFailed = true;
            IntegrationTest.Fail(gameObject);
        } else {
            Debug.Log("Passed: " + string.Join(" ", msgs.Select(x => x == null ? "[null]" : x.ToString()).ToArray()));
        }
    }

    public void Finish() {
        if(!IsFailed) {
            IntegrationTest.Pass(gameObject);
        }
        Config.Clear();
    }

    public void RefreshAssetDatabase() {
        WantsAssetRefresh = true;
    }

    public static bool WantsAssetRefresh = false;
}
#endif