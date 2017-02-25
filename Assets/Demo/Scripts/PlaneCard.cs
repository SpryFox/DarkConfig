using System.Collections.Generic;
using UnityEngine;
using DarkConfig;

public class GunMount {
    public string Name;
    public Location Location;

    public GunCard Card {
        get {
            return GunCard.Cards[Name];
        }
    }
}

public class LootTableEntry {
    [ConfigMandatory]
    public float Weight;

    [ConfigAllowMissing]
    public int Health;

    [ConfigAllowMissing]
    public string CardName;
}

public class PlaneCard {
    public Location Fuselage = new Location(new Vector2(0, 0), new Vector2(1, 1));
    public Location Wing = new Location(new Vector2(0, 0), new Vector2(1, 1));
    public Location Stabilizer = new Location(new Vector2(0, -1), new Vector2(1, 1));

    public float RotationRate = 100;
    public float Speed = 15;
    public int HitPoints = 5;

    public float AIRange = 5;

    public List<GunMount> GunMounts;

    [ConfigMandatory]
    public List<LootTableEntry> LootTable;

    [ConfigAllowMissing]
    static Dictionary<string, PlaneCard> m_cards;

    public static Dictionary<string, PlaneCard> Cards {
        get { if(m_cards == null) LoadConfigs(); return m_cards; }
    }

    public static void LoadConfigs() {
        // loads all config files in the Planes directory
        Config.FileManager.RegisterCombinedFile(Config.FileManager.GetFilesByGlob("Planes/**"), "PlaneCards", Config.CombineDict);
        Config.Apply("PlaneCards", ref m_cards);
    }

    public System.Func<PlaneCard, PlaneCard> PostDoc;
}