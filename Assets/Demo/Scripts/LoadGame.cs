using UnityEngine;
using System.Collections;
using DarkConfig;
using System.Diagnostics;

public class LoadGame : MonoBehaviour {
    void Awake() {
        #if(DEBUG)
        Config.DefaultOptions = ConfigOptions.None;
        #else
        Config.DefaultOptions = ConfigOptions.AllowMissingExtraFields;
        #endif

        StartCoroutine(WaitAndStartGame());
    }

    IEnumerator WaitAndStartGame() {
        // this latency is purely because Unity's profiler doesn't capture the first frame a game is running
        yield return new WaitForSeconds(0.1f);

        UnityPlatform.Setup();
        Config.FileManager.AddSource(new FileSource(Application.dataPath + "/Demo/Resources/Configs", hotload:true));
        m_sw = Stopwatch.StartNew();

        // uncomment to disable periodic hotloading of files, it'll have to be manual
        //Config.FileManager.IsHotloadingFiles = false;
        
        Config.Preload(StartGame);
    }

    void StartGame() {
        m_sw.Stop();
        UnityEngine.Debug.Log("Config parsing ms: " + m_sw.ElapsedMilliseconds);

        // PlaneCards are loaded on first access so this call to LoadConfigs is
        // functionally unncessary, but since we're taking a framerate hit with
        // LoadLevel, might as well make it a tiny bit longer and load the cards
        // at the same time
        PlaneCard.LoadConfigs();
        UnityEngine.SceneManagement.SceneManager.LoadScene("PlaneDemo");
    }

    public static byte[] GetPassword() {
        return new byte[] { 4, 5, 12, 34, 99, 81, 4, 6 };
    }

    Stopwatch m_sw;
}
