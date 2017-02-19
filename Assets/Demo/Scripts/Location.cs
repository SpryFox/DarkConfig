using System.Collections.Generic;
using UnityEngine;
using DarkConfig;

public class Location {
    public Vector2 Pos;

    [ConfigAllowMissing]
    public Vector2 Size =  new Vector2(1, 1);

    public Location(Vector2 pos, Vector2 size) {
        Pos = pos;
        Size = size;
    }

    // let's take over the parsing of Location, because we want to be able to have a short form for the common case of specifying only the position
    public static Location FromDoc(Location existing, DocNode doc) {
        if(existing == null) {
            // want to modify an existing Location if possible, but if it's null we need to instantiate it
            existing = new Location(default(Vector2), default(Vector2));
        }
        if(doc.Type == DocNodeType.List) {
            // it's a list, so we treat it as a position only, and size is 1,1
            Config.Reify(ref existing.Pos, doc);
            existing.Size = new Vector2(1, 1);
        } else {
            Config.SetFieldsOnObject(ref existing, doc);
        }
        return existing;
    }
}