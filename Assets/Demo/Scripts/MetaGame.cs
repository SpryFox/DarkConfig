using System;
using UnityEngine;
using DarkConfig;

public class MetaGame : MonoBehaviour {
    [Header("Prefabs")]
    public GameObject PlayerPrefab;
    public GameObject TitlePrefab;
    
    [Header("UI")]
    public TextMesh Score;
    
    [Header("References")]
    public GameObject Background;
    
    // Singleton.
    public static MetaGame Instance;

    /////////////////////////////////////////////////

    public void PlayerKilled() {
        SetState(GameState.Postgame);
    }

    public void AIKilled() {
        score++;
        Score.text = string.Format("Score: {0}", score);
    }

    public PlayerController GetPlayer() {
        if (player == null) return null;
        return player;
    }
    
    /////////////////////////////////////////////////

    enum GameState {
        Title,
        Playing,
        Postgame
    }
    
    int score;
    float currentStateStartTime;
    Transform title;
    PlayerController player;
    GameState currentState;

    /////////////////////////////////////////////////

    void Awake() {
        Instance = this;
    }

    void Start() {
        SetState(GameState.Title);
    }

    void Update() {
        switch (currentState) {
            case GameState.Title:
                if (Input.GetKeyDown(KeyCode.Space)) {
                    SetState(GameState.Playing);
                }
                break;
            case GameState.Playing:
                break;
            case GameState.Postgame:
                if (Time.time - currentStateStartTime > 10 || Input.GetKeyDown(KeyCode.Space)) {
                    SetState(GameState.Title);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        // Shift+H to hotload
        if (Input.GetKeyDown(KeyCode.H) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) {
            Debug.Log("Hotloading configs");
            Config.FileManager.CheckHotloadAsync();
        }

        // Q toggles hotloading
        if (Input.GetKeyDown(KeyCode.Q)) {
            Config.FileManager.IsHotloadingFiles = !Config.FileManager.IsHotloadingFiles;
            Debug.Log("Setting auto hotloading to: " + Config.FileManager.IsHotloadingFiles);
        }
    }

    void SetState(GameState newState) {
        if (currentState != newState) {
            ExitState(currentState);
            currentState = newState;
            currentStateStartTime = Time.time;
        }

        switch (newState) {
            case GameState.Title:
                Camera.main.transform.position = new Vector3(0, 0, -10);
                title = Instantiate(TitlePrefab).transform;
                break;
            case GameState.Playing:
                Score.gameObject.SetActive(true);
                score = 0;
                Score.text = "Score: 0";

                var playerTrf = Instantiate(PlayerPrefab).transform;
                player = playerTrf.GetComponent<PlayerController>();
                FindObjectOfType<CameraFollow>().Target = playerTrf;

                Background.BroadcastMessage("Init", playerTrf);

                break;
            case GameState.Postgame:
                break;
        }
    }

    void ExitState(GameState state) {
        switch (state) {
            case GameState.Title:
                Destroy(title.gameObject);
                break;
            case GameState.Playing:
                break;
            case GameState.Postgame:
                Score.gameObject.SetActive(false);
                break;
        }
    }
}