using UnityEngine;
using System.Collections;
using DarkConfig;
using System.Diagnostics;

// This is the main loading class for the game.  It's expected to run inside
// its own scene, which then could display a loading indicator.
public class LoadGame : MonoBehaviour {
    void Awake() {
#if(DEBUG)
        // Be strict in debug mode so that content creators will be quickly
        // notified of any mistakes.  It will warn for any missing fields
        // (which haven't been annotated with ConfigAllowMissing) and for any
        // extra fields.
        Config.Settings.DefaultReifierOptions = ReificationOptions.None;
#else
        // In production mode, ignore missing/extra checks.  This makes the 
        // runtime faster.  ConfigMandatory fields are still checked.
        Config.Settings.DefaultReifierOptions = ConfigOptions.AllowMissingExtraFields;
#endif

        StartCoroutine(WaitAndStartGame());
    }

    IEnumerator WaitAndStartGame() {
        // this delay is purely because Unity's profiler doesn't capture the first frame a game is running
        yield return new WaitForSeconds(0.1f);

        Config.Platform = new UnityPlatform();
        Config.FileManager.AddSource(new FileSource(Application.dataPath + "/Demo/Resources/Configs", hotload: true));
        m_sw = Stopwatch.StartNew();

        // uncomment to disable periodic hotloading of files, it'll have to be manual
        //Config.FileManager.IsHotloadingFiles = false;

        // preload will call StartGame when it's finished
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

    Stopwatch m_sw;
}