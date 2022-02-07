using UnityEngine;

public class CameraFollow : MonoBehaviour {
    public Transform Target;

    public Vector3 RelativePosition = new Vector3(0, 0, -10);

    void LateUpdate() {
        if (Target == null) {
            return;
        }
        transform.position = Target.position + RelativePosition;
    }
}