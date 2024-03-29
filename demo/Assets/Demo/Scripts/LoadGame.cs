using System.Collections;
using UnityEngine;
using DarkConfig;
using System.Diagnostics;
using UnityEngine.SceneManagement;

// This is the main loading class for the game.  It's expected to run inside
// its own scene, which then could display a loading indicator.
public class LoadGame : MonoBehaviour {
    Stopwatch stopwatch;
    
    /////////////////////////////////////////////////

    void Awake() {
#if DEBUG
        // Be strict in debug mode so that content creators will be quickly
        // notified of any mistakes.  It will warn for any missing fields
        // (which haven't been annotated with ConfigAllowMissing) and for any
        // extra fields.
        Configs.Settings.DefaultReifierOptions = ReificationOptions.None;
#else
        // In production mode, ignore missing/extra checks.  This makes 
        // loading faster.  ConfigMandatory fields are still checked.
        Configs.Settings.DefaultReifierOptions = ConfigOptions.AllowMissingExtraFields;
#endif

        UnityPlatform.Setup();
        Configs.AddConfigSource(new FileSource(Application.dataPath + "/Demo/Resources/Configs", ".bytes", hotload: true));
        StartCoroutine(StartGame());
    }

    IEnumerator StartGame() {
        stopwatch = Stopwatch.StartNew();

        // comment to disable periodic hotloading of files, it'll have to be manual
        Configs.Settings.EnableHotloading = true;

        // Preload will call StartGame when it's finished
        foreach (object _ in Configs.StepPreload()) {
            yield return null;
        }
        
        stopwatch.Stop();
        UnityEngine.Debug.Log("Config parsing ms: " + stopwatch.ElapsedMilliseconds);

        // PlaneCards are loaded on first access so this call to LoadConfigs is
        // functionally unnecessary, but since we're taking a framerate hit with
        // LoadLevel, might as well make it a tiny bit longer and load the cards
        // at the same time
        PlaneCard.LoadConfigs();
        SceneManager.LoadScene("PlaneDemo");
    }
}