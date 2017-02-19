using System.Collections.Generic;
using UnityEngine;
using DarkConfig;

public class GunCard {
    public Vector2 BulletSize = new Vector2(1, 1);
    public int BulletDamage;
    public float BulletSpeed;
    public float BulletRange;

    public float RPS;

    public float FireInterval {
        get {
            return 1f/RPS;
        }
    }

    [ConfigAllowMissing]
    static Dictionary<string, GunCard> m_cards;

    public static Dictionary<string, GunCard> Cards {
        get { 
            if(m_cards == null) Config.Apply("guns", ref m_cards);
            return m_cards;
        }
    }
}