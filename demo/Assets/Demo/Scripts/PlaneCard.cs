using System.Collections.Generic;
using UnityEngine;
using DarkConfig;

public class GunMount {
    public string Name;
    public Location Location;

    // Note that this is assuming that GunCard.Cards is loaded by the 
    // time we get into this property accessor.
    public GunCard Card => GunCard.Cards[Name];
}

public class LootTableEntry {
    // Don't permit this field to be missing in the config, even if DarkConfig 
    // isn't being strict.
    [ConfigMandatory]
    public float Weight;

    // Permit this field to be missing in the config, even if DarkConfig is 
    // being strict.
    [ConfigAllowMissing]
    public int Health;

    [ConfigAllowMissing]
    public string CardName;
}

public class PlaneCard {
    // Fields that are expected to be set in configs. 
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

    /////////////////////////////////////////////////////////

    // this field can't be set by DarkConfig because it's a function; it's 
    // also ignored for clarity
    [ConfigIgnore]
    public System.Action<PlaneCard> OnChanged;

    // see LoadConfigs for where this is hooked up
    [ConfigIgnore]
    static Dictionary<string, PlaneCard> _Cards;

    // DarkConfig can't currently set properties
    public static Dictionary<string, PlaneCard> Cards {
        get {
            if (_Cards == null) {
                LoadConfigs();
            }
            return _Cards;
        }
    }

    public static void LoadConfigs() {
        // loads all config files in the Planes directory
        Configs.FileManager.RegisterCombinedFile(
            Configs.FileManager.GetFilenamesMatchingGlob("Planes/**"),
            "PlaneCards",
            Configs.CombineDict);
        Configs.Apply("PlaneCards", ref _Cards);
    }

    // We have a few places in the code which need to be notified when their
    // PlaneCard is modified.  These are places where we had no choice but to 
    // copy some of the values from the PlaneCard into some other object, and 
    // therefore need to re-copy the values when the PlaneCard gets hotloaded.
    // 
    // To see those use cases, look for usage of OnChanged in PlaneView.cs. 
    // See hotloading.md for more information on hotloading in general.
    public static PlaneCard PostDoc(PlaneCard existing) {
        existing.OnChanged?.Invoke(existing);
        return existing;
    }
}