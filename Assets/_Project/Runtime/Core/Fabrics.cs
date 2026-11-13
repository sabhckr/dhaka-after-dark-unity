// What each garment of the wardrobe is made of (2026-11-12): how glossy it is, the weave or grain laid over it, a colour it is toned down by and how faded it is;
// and, for the body and the eyes, how far light goes under the skin and how the lids shade the eyeball.
// The table is People/Wardrobe/fabrics.json; a material takes the first entry one of whose names it contains. A material the table does not name is left as it was.
using System;
using UnityEngine;

namespace Dad.Core
{
    [Serializable]
    public class Fabric
    {
        public string[] match;     // parts of material names
        public float smooth = -1;  // 0 matte to 1 mirror; below 0 leaves it; a roughness or smoothness map of the garment's own wins
        public string detail;      // a weave in People/Wardrobe/Detail, "<detail>_normal"
        public float tiling;       // how many times it repeats across the garment's map
        public float strength = 1; // how deep it is
        public string tint;        // "#rrggbb" the picture is multiplied by, or empty
        public float mute;         // 0 as painted to 1 grey: a colour faded by wear and washing
        public float sheen;        // cloth's soft glancing light (0 none, about 0.3 cotton, 0.6 knit); leather and metal take none
        public float scatter;      // skin: light carried under it, red furthest (0 none, 1 skin)
        public float eye;          // an eyeball: its edges shaded by the lids (0 none, 1 eye)
        public float hair;         // hair cards: two highlights along the strands (0 none, 1 hair)
        public string dye;         // "#rrggbb": all one colour, the picture only shading it (hair whose picture paints some strands grey), or empty
    }

    [Serializable]
    public class FabricTable { public Fabric[] fabrics; }

    public static class Fabrics
    {
        public const string File = Person.Folder + Person.Wardrobe + "fabrics";
        static FabricTable table;

        static readonly int SmoothId = Shader.PropertyToID("_Smoothness"), HasGlossId = Shader.PropertyToID("_HasGloss"), DetailId = Shader.PropertyToID("_DetailNormal"),
            TilingId = Shader.PropertyToID("_DetailTiling"), StrengthId = Shader.PropertyToID("_DetailStrength"), MaskId = Shader.PropertyToID("_MaskMap"), TintId = Shader.PropertyToID("_TintR"), SheenId = Shader.PropertyToID("_Sheen"), MuteId = Shader.PropertyToID("_Mute"),
            ScatterId = Shader.PropertyToID("_Scatter"), EyeId = Shader.PropertyToID("_Eye"), HairId = Shader.PropertyToID("_Hair"),
            DyeId = Shader.PropertyToID("_Dye"), WearsId = Shader.PropertyToID("_Wears");

        public static FabricTable Table
        {
            get
            {
                if (table != null) return table;
                var text = Resources.Load<TextAsset>(File);
                table = text != null ? JsonUtility.FromJson<FabricTable>(text.text) : new FabricTable();
                if (table.fabrics == null) table.fabrics = new Fabric[0];
                return table;
            }
        }

        public static Fabric For(string material)
        {
            if (string.IsNullOrEmpty(material)) return null;
            foreach (var f in Table.fabrics)
                if (f.match != null)
                    foreach (var part in f.match)
                        if (!string.IsNullOrEmpty(part) && material.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return f;
            return null;
        }

        /// <summary>Lays the fabric the table gives this material onto a Dad/Person material. Returns false when the table does not name it.</summary>
        public static bool Apply(Material m, string material)
        {
            var f = For(material);
            if (f == null) return false;
            if (f.smooth >= 0 && m.GetFloat(HasGlossId) < 0.5f) m.SetFloat(SmoothId, f.smooth);
            m.SetFloat(SheenId, f.sheen);
            m.SetFloat(MuteId, f.mute);
            m.SetFloat(ScatterId, f.scatter);
            m.SetFloat(EyeId, f.eye);
            m.SetFloat(HairId, f.hair);
            if (!string.IsNullOrEmpty(f.dye) && ColorUtility.TryParseHtmlString(f.dye, out var dyed))
            {
                m.SetFloat(DyeId, 1);
                m.SetColor(WearsId, dyed);
            }
            if (!string.IsNullOrEmpty(f.detail) && f.tiling > 0)
            {
                var weave = Resources.Load<Texture2D>(Person.Folder + Person.Wardrobe + "Detail/" + f.detail + "_normal");
                if (weave != null)
                {
                    m.SetTexture(DetailId, weave);
                    m.SetFloat(TilingId, f.tiling);
                    m.SetFloat(StrengthId, f.strength);
                }
            }
            if (!string.IsNullOrEmpty(f.tint) && ColorUtility.TryParseHtmlString(f.tint, out var tone))
            {
                m.SetTexture(MaskId, Texture2D.whiteTexture); // the whole picture, by the mask's first colour
                m.SetColor(TintId, tone);
            }
            return true;
        }
    }
}
