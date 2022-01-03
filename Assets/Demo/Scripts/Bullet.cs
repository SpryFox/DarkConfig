using UnityEngine;

public class Bullet : MonoBehaviour {
    public int Damage;
    public float Speed;
    public GameObject HitPrefab;

    [HideInInspector]
    public Transform Firer;

    ////////////////////////////////////////////

    void OnTriggerEnter2D(Collider2D c) {
        var trf = TopParent(c.transform);
        if (trf == Firer) return;
        var planeController = trf.GetComponent<PlaneController>();
        if (planeController == null) return;
        planeController.TakeDamage(Damage);

        var obj = Instantiate(HitPrefab, transform.position, transform.rotation);
        obj.transform.localScale = Vector3.one * Mathf.Sqrt(Damage);
        Destroy(obj, 2);
        Destroy(gameObject);
    }
    
    Transform TopParent(Transform t) {
        while (t.parent != null) {
            t = t.parent;
        }

        return t;
    }
}