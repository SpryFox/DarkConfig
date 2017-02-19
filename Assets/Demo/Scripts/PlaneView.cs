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
            if(m_card != null) m_card.PostDoc -= Refresh;
            m_card = value;
            m_card.PostDoc += Refresh;
            Refresh(m_card);
        }
    }

    public PlaneCard Refresh(PlaneCard card) {
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

        if(Controller == null) return card;
        var healthPct = ((float)Controller.HitPoints) / Controller.MaxHitPoints;
        var color = Color.Lerp(Color.white, Color.Lerp(Color.red, Color.black, 0.2f), 1 - healthPct);
        Fuselage.color = color;
        LeftWing.color = color;
        RightWing.color = color;
        LeftStabilizer.color = color;
        RightStabilizer.color = color;

        return card;
    }

    void OnDestroy() {
        if(Card != null) Card.PostDoc -= Refresh;
    }

    void Killed() {
        
    }
}
