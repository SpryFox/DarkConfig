using UnityEngine;

public class Parallax : MonoBehaviour {
    public bool drift;
    public Transform ViewpointObject;

    /////////////////////////////////////////////////

    Vector3 currentPos;
    Vector3 startPos;
    Vector3 viewpointStartPos;

    /////////////////////////////////////////////////

    void Awake() {
        startPos = transform.position;
        currentPos = startPos;
    }

    void LateUpdate() {
        if (ViewpointObject != null) {
            var pos = ViewpointObject.transform.position - viewpointStartPos;
            float depth = Mathf.Max(1, transform.position.z);
            pos *= 1 - 1 / depth;
            var newPos = startPos + pos;
            newPos.z = depth;
            currentPos = newPos;
        }

        if (drift) {
            transform.position = currentPos + new Vector3(
                Mathf.Sin(Time.time * 0.152f + (name.GetHashCode() & 0xFF)) * 10,
                Mathf.Cos(Time.time * 0.1193f + ((name.GetHashCode() & 0xFF00) >> 8)) * 10,
                0);
        } else {
            transform.position = currentPos;
        }
    }
    
    void Init(Transform obj) {
        ViewpointObject = obj;
        viewpointStartPos = ViewpointObject.position;
    }
}