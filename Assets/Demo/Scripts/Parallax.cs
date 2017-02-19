using UnityEngine;
using System.Collections;

public class Parallax : MonoBehaviour {
    public Transform ViewpointObject;

    public bool drift = false;

    void Awake() {
        m_startPos = transform.position;
        m_currentPos = m_startPos;
    }

    void Init(Transform obj) {
        ViewpointObject = obj;
        m_viewpointStartPos = ViewpointObject.position;
    }

    void LateUpdate() {
        if (ViewpointObject != null) {
            Vector3 pos = ViewpointObject.transform.position - m_viewpointStartPos;
            var depth = Mathf.Max(1, transform.position.z);
            pos *= 1 - 1 / depth;
            var newPos = m_startPos + pos;
            newPos.z = depth;
            m_currentPos = newPos;
        }

        if(drift) {
            transform.position = m_currentPos + 
                      new Vector3(Mathf.Sin(Time.time * 0.152f + (name.GetHashCode() & 0xFF)) * 10, 
                                  Mathf.Cos(Time.time * 0.1193f + ((name.GetHashCode() & 0xFF00) >> 8)) * 10, 0);
        } else {
            transform.position = m_currentPos;
        }
    }

    Vector3 m_currentPos;

    Vector3 m_startPos;
    Vector3 m_viewpointStartPos;
}
