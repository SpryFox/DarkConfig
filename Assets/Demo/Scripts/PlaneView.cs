using UnityEngine;

[ExecuteInEditMode]
public class PlaneView : MonoBehaviour {
    public SpriteRenderer Fuselage;
    public SpriteRenderer LeftWing;
    public SpriteRenderer RightWing;
    public SpriteRenderer LeftStabilizer;
    public SpriteRenderer RightStabilizer;

    public PlaneController Controller;

    public PlaneCard Card {
        get => _Card;
        set {
            // we hook up listeners to OnChanged so it gets called when the Card gets modified
            if (_Card != null) {
                _Card.OnChanged -= Refresh;
            }
            _Card = value;
            _Card.OnChanged += Refresh;
            Refresh(_Card);
        }
    }

    public void Refresh(PlaneCard card) {
        // Here, we're copying values from the card because we have to to interface with Unity.
        // However, we've arranged things so that this function gets called any time
        // the card gets modified.
        Fuselage.transform.localPosition = Card.Fuselage.Pos;
        Fuselage.transform.localScale = new Vector3(Card.Fuselage.Size.x, Card.Fuselage.Size.y, 1);
        LeftWing.transform.localPosition = new Vector2(-Card.Wing.Pos.x, Card.Wing.Pos.y);
        LeftWing.transform.localScale = new Vector3(Card.Wing.Size.x, Card.Wing.Size.y, 1);
        RightWing.transform.localPosition = Card.Wing.Pos;
        RightWing.transform.localScale = new Vector3(-Card.Wing.Size.x, Card.Wing.Size.y, 1);
        LeftStabilizer.transform.localPosition = new Vector2(-Card.Stabilizer.Pos.x, Card.Stabilizer.Pos.y);
        LeftStabilizer.transform.localScale = Card.Stabilizer.Size;
        RightStabilizer.transform.localPosition = Card.Stabilizer.Pos;
        RightStabilizer.transform.localScale = new Vector3(-Card.Stabilizer.Size.x, Card.Stabilizer.Size.y, 1);

        if (Controller == null) return;
        var healthPct = ((float) Controller.HitPoints) / Controller.MaxHitPoints;
        var color = Color.Lerp(Color.white, Color.Lerp(Color.red, Color.black, 0.2f), 1 - healthPct);
        Fuselage.color = color;
        LeftWing.color = color;
        RightWing.color = color;
        LeftStabilizer.color = color;
        RightStabilizer.color = color;
    }
    
    ////////////////////////////////////////////

    PlaneCard _Card;

    ////////////////////////////////////////////

    void OnDestroy() {
        // need to clean up this listener so it's not a memory leak
        if (Card != null) {
            Card.OnChanged -= Refresh;
        }
    }

    void Killed() { }
}