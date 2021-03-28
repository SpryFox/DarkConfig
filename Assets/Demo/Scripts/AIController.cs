using UnityEngine;
using System.Collections.Generic;
using DarkConfig;
using SpryFox.Common;

public class AIController : MonoBehaviour {
    public PlaneController Controller;

    public Transform PickupPrefab;

    public void Setup(PlaneCard card) {
        Controller.Setup(card);
    }

    void Update() {
        // try and target the player (which is a singleton because it's a single player game)
        var player = MetaGame.Instance.GetPlayer();
        if (player == null) return;
        var directionToPlayer = player.transform.position - transform.position;
        var angleToPlayer =
            MathPlus.NormalizeAngle(Mathf.Atan2(directionToPlayer.y, directionToPlayer.x) * Mathf.Rad2Deg - 90);
        var myAngle = MathPlus.NormalizeAngle(transform.eulerAngles.z);

        var turnAmount = Mathf.Clamp((angleToPlayer - myAngle) / 45, -1, 1);

        if (Controller == null) return;
        Controller.RotationCommand = turnAmount;

        var playerIsClose = directionToPlayer.magnitude < Controller.Card.AIRange;
        Controller.IsFiring = playerIsClose && (Mathf.Abs(angleToPlayer - myAngle) < 20);

        Controller.Throttle = 1;
    }

    void Killed() {
        MetaGame.Instance.AIKilled();

        // spawn loot from the loot table
        var lootTable = Controller.Card.LootTable;
        if (lootTable == null) {
            Debug.LogError("null lootTable " + Controller.Card);
            return;
        }

        var totalWeight = 0f;
        foreach (var entry in lootTable) {
            if (entry == null) {
                Debug.Log("entry null");
                continue;
            }

            totalWeight += entry.Weight;
        }

        var rnd = totalWeight * Random.value;

        for (int i = 0; i < lootTable.Count; i++) {
            if (rnd > lootTable[i].Weight) {
                rnd -= lootTable[i].Weight;
            } else {
                var loot = lootTable[i];
                if (loot.Health == 0 && loot.CardName == null) return;

                var pickupObj = (Transform) Instantiate(PickupPrefab, transform.position, Quaternion.identity);
                var pickup = pickupObj.GetComponent<Pickup>();

                pickup.Health = loot.Health;
            }
        }
    }
}