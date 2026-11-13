// Who lives in the city: one record for each person, read from Resources/People/roster (NPC Phase 3, 2026-11-08). The records only say who someone is and what they look like; NpcCrowd puts
// them on the streets. A record with Drives set is the figure seated in that kind of vehicle, posed into its mesh at bake time (Tools/models/bake.py): it has no body of its own to walk.
using System;
using UnityEngine;

namespace Dad.Core
{
    [Serializable]
    public sealed class NpcRecord
    {
        public string id, sex, model, role, district, behaviour = "walk", drives = "";
        public float height = 1.68f;
        /// <summary>How light or dark the skin is drawn against the model's own, 1 being as painted.</summary>
        public float skin = 1;
        /// <summary>Colour of the garment in the model's picture (hex, empty for none), the colour this person wears instead, and how near a colour must be to the first to change.</summary>
        public string garment = "", wears = "";
        public float reach = 0.09f;
        /// <summary>The model's parts that are hair (their names, any separator), drawn matt in one colour (hex, empty for near black).</summary>
        public string hair = "", hairColour = "";

        /// <summary>Colours (hex, comma between) for the red, green, blue and alpha of the model's own _mask map; empty leaves that part as painted.</summary>
        public string tints = "";

        /// <summary>The model's parts no bone holds that ride on the chest (their names, comma between). Any other such part is a prop of the model's render, frozen where it was posed, and is not put on the street.</summary>
        public string worn = "";
        public string gait = "";
        /// <summary>The gun this person carries (Armed: scar, ump, glock), for the fighters' roster; empty for none.</summary>
        public string gun = "";

        public bool Walks => string.IsNullOrEmpty(drives);

        /// <summary>The same person in another body (Cast.Other: never yours).</summary>
        public NpcRecord As(string other)
        {
            var r = (NpcRecord)MemberwiseClone();
            r.model = other;
            return r;
        }
    }

    [Serializable]
    public sealed class NpcRoster
    {
        public const string Path = "People/roster";
        public NpcRecord[] people = new NpcRecord[0];

        public static NpcRoster Parse(string json) => JsonUtility.FromJson<NpcRoster>(json);

        public static NpcRoster Load(string path = Path)
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) { Debug.LogError("[Dad] no Resources/" + path); return new NpcRoster(); }
            return Parse(asset.text);
        }

        /// <summary>A hex colour, or the fallback where it is empty or not a colour.</summary>
        public static Color Hex(string hex, Color fallback) => !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : fallback;
    }
}
