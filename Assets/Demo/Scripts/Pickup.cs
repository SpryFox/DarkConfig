using UnityEngine;
using System.Collections.Generic;
using DarkConfig;

public class Pickup : MonoBehaviour {
    public int Health = 0;

    public Transform PlaneViewPrefab;

    PlaneView View;

    public SpriteRenderer PlusSprite;

    void Start() {
        PlusSprite.color = Color.green;
    }

    Transform TopParent(Transform t) {
        while(t.parent != null) {
            t = t.parent;
        }
        return t;
    }

    void OnTriggerEnter2D(Collider2D c) {
        var trf = TopParent(c.transform);
        if(trf.tag != "Player") return;
        var planeController = trf.GetComponent<PlaneController>();
        if(planeController == null) return;
        if(Health > 0) {
            planeController.Heal(Health);
        }
        
        Destroy(gameObject);
    }
}