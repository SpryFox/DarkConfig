using UnityEngine;
using System.Collections.Generic;
using SpryFox.Common;
using DarkConfig;

public class EnemySpawner : MonoBehaviour {
    public Transform EnemyPrefab;

    void Start() {
        // This is _not_ the main initialization code, that's in LoadGame.cs.
        // This code runs in the editor, in the PlaneDemo scene, if LoadGame
        // hasn't run yet.
        // What happens here is that we add the source and then call ApplyThis
        // without calling Config.Preload.  In the Unity editor (or as 
        // determined by the Platform implementation), it will trigger an 
        // "immediate preload" inside ApplyThis that will load/parse all the 
        // config files within the function call (so it'll be slow and drop a 
        // frame).
        if(Config.FileManager.CountSources() == 0) {
            Config.FileManager.AddSource(new FileSource(Application.dataPath + "/Demo/Resources/Configs"));
        }

        Config.ApplyThis("enemy_spawning", this);
    }

    void Update() {
        if(m_player == null) {
            m_player = FindObjectOfType<PlayerController>();
        }
        if(m_player == null) return;

        // clean out destroyed enemies
        for(int i = 0; i < m_enemies.Count; i++) {
            if(m_enemies[i] == null) {
                m_enemies.RemoveAt(i);
                i--;
            }
        }

        if(m_enemies.Count < c_NumEnemies) {
            // pick a card, any card (except the card the player currently has)
            PlaneCard chosenCard = null;
            while(chosenCard == null || chosenCard == m_player.Controller.Card) {
                var cardNames = new List<string>(PlaneCard.Cards.Keys);
                var chosenName = cardNames[(int)(Random.value * PlaneCard.Cards.Count)];
                chosenCard = PlaneCard.Cards[chosenName];
            }

            // pick a location near the player
            var spawnPos = m_player.transform.position + Random.insideUnitCircle.XYZ().normalized * c_SpawnDistanceFromPlayer;
            var spawnRotation = Quaternion.AngleAxis(Random.value * 360, Vector3.forward);

            // set up the ai
            var enemyObj = (Transform)Instantiate(EnemyPrefab, spawnPos, spawnRotation);
            var controller = enemyObj.GetComponent<AIController>();
            controller.Setup(chosenCard);

            m_enemies.Add(controller);
        }
    }

    /// config variables
    int c_NumEnemies = 5;
    int c_SpawnDistanceFromPlayer = 30;

    /// private variables
    List<AIController> m_enemies = new List<AIController>();
    PlayerController m_player;
}