using UnityEngine;
using System.Collections.Generic;
using DarkConfig;

public class PlayerController : MonoBehaviour {
    public static string StartingCard = null;

    public Dictionary<string, KeyCode> Keyboard;

    [ConfigIgnore]
    public PlaneController Controller;

    ////////////////////////////////////////////

    void Start() {
        // Get keyboard bindings from the config, and also automatically 
        // hotload them.
        Configs.ApplyThis("player", this);
        // The previous call to ApplyThis won't have touched the StartingCard
        // field, so for that we call ApplyStatic.
        Configs.ApplyStatic<PlayerController>("player");

        Controller.Setup(PlaneCard.Cards[StartingCard]);
    }

    void Update() {
        float rotation = 0;
        if (Input.GetKey(Keyboard["Left"])) {
            rotation += 1;
        }

        if (Input.GetKey(Keyboard["Right"])) {
            rotation -= 1;
        }

        if (Input.GetKey(Keyboard["Slow"])) {
            Controller.Throttle = 0.6f;
        }

        if (Input.GetKey(Keyboard["Boost"])) {
            Controller.Throttle = 1.4f;
        }

        Controller.RotationCommand = rotation;

        Controller.IsFiring = Input.GetKey(Keyboard["Fire"]);
    }

    void Killed() {
        MetaGame.Instance.PlayerKilled();
    }
}