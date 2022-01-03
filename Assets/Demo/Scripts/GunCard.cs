using System.Collections.Generic;
using UnityEngine;
using DarkConfig;

/// This is a "config data class", which mostly has public fields and not much else.
public class GunCard {
    public Vector2 BulletSize = new Vector2(1, 1);
    public int BulletDamage;
    public float BulletSpeed;
    public float BulletRange;

    public float RPS;

    public float FireInterval => 1f / RPS;

    ////////////////////////////////////////////

    // This static collection allows us to manage the configs for the GunCard
    // completely within the GunCard class.  Just have to be a little careful
    // to not access Cards before the preload finishes.
    [ConfigIgnore]
    static Dictionary<string, GunCard> _Cards;

    public static Dictionary<string, GunCard> Cards {
        get {
            if (_Cards == null) {
                Config.Apply("guns", ref _Cards);
            }
            return _Cards;
        }
    }
}