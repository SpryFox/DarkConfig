using UnityEngine;
using System.Collections.Generic;
using DarkConfig;

public class PlayerController : MonoBehaviour {
    public static string StartingCard = null;

    public PlaneController Controller;

    public Dictionary<string, KeyCode> Keyboard;

    void Start() {
        Config.ApplyThis("player", this);
        Config.ApplyStatic<PlayerController>("player");  // this is just to exercise ApplyStatic

        Controller.Setup(PlaneCard.Cards[StartingCard]);
    }

    void Update () {
        float rotation = 0;
        if(Input.GetKey(Keyboard["Left"])) {
            rotation += 1;
        }
        if(Input.GetKey(Keyboard["Right"])) {
            rotation -= 1;
        }

        if(Input.GetKey(Keyboard["Slow"])) {
            Controller.Throttle = 0.6f;
        }

        if(Input.GetKey(Keyboard["Boost"])) {
            Controller.Throttle = 1.4f;
        }

        Controller.RotationCommand = rotation;

        Controller.IsFiring = Input.GetKey(Keyboard["Fire"]);
    }

    void Killed() {
        MetaGame.Instance.PlayerKilled();
    }
}
