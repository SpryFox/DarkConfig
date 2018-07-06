using UnityEngine;
using System.Collections;
using SpryFox.Common;

[ExecuteInEditMode]
public class PlaneView : MonoBehaviour {
    public SpriteRenderer Fuselage;
    public SpriteRenderer LeftWing;
    public SpriteRenderer RightWing;
    public SpriteRenderer LeftStabilizer;
    public SpriteRenderer RightStabilizer;

    public PlaneController Controller;

    PlaneCard m_card;
    public PlaneCard Card {
        get { return m_card; }
        set {
            // we hook up listeners to OnChanged so it gets called when the Card gets modified
            if(m_card != null) m_card.OnChanged -= Refresh;
            m_card = value;
            m_card.OnChanged += Refresh;
            Refresh(m_card);
        }
    }

    public void Refresh(PlaneCard card) {
        // Here, we're copying values from the card because we have to to interface with Unity.
        // However, we've arranged things so that this function gets called any time
        // the card gets modified.
        Fuselage.transform.localPosition = Card.Fuselage.Pos;
        Fuselage.transform.localScale = Card.Fuselage.Size.XYZ1();
        LeftWing.transform.localPosition = new Vector2(-Card.Wing.Pos.x, Card.Wing.Pos.y);
        LeftWing.transform.localScale = Card.Wing.Size.XYZ1();
        RightWing.transform.localPosition = Card.Wing.Pos;
        RightWing.transform.localScale = new Vector2(-Card.Wing.Size.x, Card.Wing.Size.y).XYZ1();
        LeftStabilizer.transform.localPosition = new Vector2(-Card.Stabilizer.Pos.x, Card.Stabilizer.Pos.y);
        LeftStabilizer.transform.localScale = Card.Stabilizer.Size;
        RightStabilizer.transform.localPosition = Card.Stabilizer.Pos;
        RightStabilizer.transform.localScale = new Vector2(-Card.Stabilizer.Size.x, Card.Stabilizer.Size.y).XYZ1();

        if(Controller == null) return;
        var healthPct = ((float)Controller.HitPoints) / Controller.MaxHitPoints;
        var color = Color.Lerp(Color.white, Color.Lerp(Color.red, Color.black, 0.2f), 1 - healthPct);
        Fuselage.color = color;
        LeftWing.color = color;
        RightWing.color = color;
        LeftStabilizer.color = color;
        RightStabilizer.color = color;
    }

    void OnDestroy() {
        // need to clean up this listener so it's not a memory leak
        if(Card != null) Card.OnChanged -= Refresh;
    }

    void Killed() {
        
    }
}
