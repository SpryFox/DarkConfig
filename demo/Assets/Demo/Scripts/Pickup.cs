using UnityEngine;

public class Pickup : MonoBehaviour {
    // these two fields are used if it's a health pickup
    public int Health;
    public SpriteRenderer PlusSprite;

    ////////////////////////////////////////////

    void Start() {
        PlusSprite.color = Color.green;
    }

    void OnTriggerEnter2D(Collider2D c) {
        var topParent = c.transform;
        
        while (topParent.parent != null) {
            topParent = topParent.parent;
        }
        
        if (!topParent.CompareTag("Player")) {
            return;
        }
        
        var planeController = topParent.GetComponent<PlaneController>();
        if (planeController != null) {
            if (Health > 0) {
                planeController.Heal(Health);
            }

            Destroy(gameObject);
        }
    }
}