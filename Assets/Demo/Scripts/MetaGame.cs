using UnityEngine;
using System.Collections.Generic;
using SpryFox.Common;
using DarkConfig;

public class MetaGame : MonoBehaviour {
    public Transform PlayerPrefab;
    public Transform TitlePrefab;

    public TextMesh Score;

    public GameObject Background;

    public static MetaGame Instance;


    void Awake() {
        Instance = this;
    }

    void Start() {
        SetState(GameStateEnum.Title);
    }

    void SetState(GameStateEnum state) {
        if(m_gameState != state) {
            ExitState(m_gameState);
            m_gameState = state;
            m_stateStartTime = Time.time;
        }

        switch(state) {
            case GameStateEnum.Title:
                Camera.main.transform.position = new Vector3(0, 0, -10);
                m_title = Instantiate(TitlePrefab);
                break;
            case GameStateEnum.Playing:
                Score.gameObject.SetActive(true);
                m_score = 0;
                Score.text = "Score: 0";

                var playerTrf = (Transform)Instantiate(PlayerPrefab);
                m_player = playerTrf.GetComponent<PlayerController>();
                FindObjectOfType<CameraFollow>().Target = playerTrf;

                Background.BroadcastMessage("Init", playerTrf);

                break;
            case GameStateEnum.Postgame:
                break;
        }
    }

    void ExitState(GameStateEnum state) {
        switch(state) {
            case GameStateEnum.Title:
                Destroy(m_title.gameObject);
                break;
            case GameStateEnum.Playing:
                break;
            case GameStateEnum.Postgame:
                Score.gameObject.SetActive(false);
                break;
        }
    }

    void Update() {
        if(m_gameState == GameStateEnum.Title) {
            if(Input.GetKeyDown(KeyCode.Space)) {
                SetState(GameStateEnum.Playing);
            }
        }
        if(m_gameState == GameStateEnum.Postgame) {
            if(Time.time - m_stateStartTime > 10 || Input.GetKeyDown(KeyCode.Space)) {
                SetState(GameStateEnum.Title);
            }
        }

        if(Input.GetKeyDown(KeyCode.H) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) {
            Debug.Log("Hotloading configs");
            Config.FileManager.CheckHotload();
        }
    }

    public void PlayerKilled() {
        SetState(GameStateEnum.Postgame);
    }

    public void AIKilled() {
        m_score++;
        Score.text = string.Format("Score: {0}", m_score);
    }

    public PlayerController GetPlayer() {
        if(m_player == null) return null;
        return m_player;
    }

    int m_score = 0;

    float m_stateStartTime = 0;

    Transform m_title;

    PlayerController m_player;

    enum GameStateEnum { Title, Playing, Postgame };

    GameStateEnum m_gameState;
}