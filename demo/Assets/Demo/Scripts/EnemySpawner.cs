using UnityEngine;
using System.Collections.Generic;
using DarkConfig;

public class EnemySpawner : MonoBehaviour {
    public GameObject EnemyPrefab;

    /////////////////////////////////////////////////

    // Set by config
    int numEnemies = 5;
    int spawnDistanceFromPlayer = 30;

    // Instance vars
    List<AIController> enemies = new List<AIController>();
    PlayerController player;
    
    /////////////////////////////////////////////////

    void Start() {
        // This is _not_ the main initialization code, that's in LoadGame.cs.
        // This code runs in the editor, in the PlaneDemo scene, if LoadGame
        // hasn't run yet.
        // What happens here is that we add the source and then call ApplyThis
        // without calling Configs.Preload.  In the Unity editor (or as 
        // determined by the Platform implementation), it will trigger an 
        // "immediate preload" inside ApplyThis that will load/parse all the 
        // config files within the function call (so it'll be slow and drop a 
        // frame).
        if (Configs.CountConfigSources() == 0) {
            Configs.AddConfigSource(new FileSource(Application.dataPath + "/Demo/Resources/Configs", ".bytes"));
        }

        Configs.ApplyThis("enemy_spawning", this);
    }

    void Update() {
        if (player == null) {
            player = FindObjectOfType<PlayerController>();
        }

        if (player == null) {
            return;
        }

        // clean out destroyed enemies
        for (int i = 0; i < enemies.Count; i++) {
            if (enemies[i] == null) {
                enemies.RemoveAt(i);
                i--;
            }
        }

        if (enemies.Count < numEnemies) {
            // pick a card, any card (except the card the player currently has)
            PlaneCard chosenCard = null;
            while (chosenCard == null || chosenCard == player.Controller.Card) {
                var cardNames = new List<string>(PlaneCard.Cards.Keys);
                var chosenName = cardNames[(int) (Random.value * PlaneCard.Cards.Count)];
                chosenCard = PlaneCard.Cards[chosenName];
            }

            // pick a location near the player
            var spawnPos = player.transform.position + (Vector3)Random.insideUnitCircle.normalized * spawnDistanceFromPlayer;
            var spawnRotation = Quaternion.AngleAxis(Random.value * 360, Vector3.forward);

            // set up the ai
            var enemyObj = Instantiate(EnemyPrefab, spawnPos, spawnRotation);
            var controller = enemyObj.GetComponent<AIController>();
            controller.Setup(chosenCard);

            enemies.Add(controller);
        }
    }
}