// The wardrobe's fabric table (2026-11-12): every garment the twenty Blender people wear says what it is made of, unless it brings its own
// roughness map, and every weave the table names is there as a normal map.
using System.Collections.Generic;
using System.Linq;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class FabricTests
    {
        // parts that are not clothes: the body, eyes, teeth, brows, lashes and hair
        static readonly string[] NotCloth = { "body", "high-poly", "teeth", "eyebrow", "eyelashes", "short0", "ponytail", "braid", "long0", "bob0", "afro", "blunt_bob", "messy_hair", "hair_bun", "culturalibre_hair" };

        static IEnumerable<string> People() => Enumerable.Range(1, 10).Select(i => $"npc_m{i:00}").Concat(Enumerable.Range(1, 10).Select(i => $"npc_f{i:00}"));

        [Test]
        public void EveryGarmentSaysWhatItIsMadeOf()
        {
            var missing = new SortedSet<string>();
            foreach (var who in People())
            {
                var model = Resources.Load<GameObject>(Person.Folder + who);
                Assert.IsNotNull(model, who);
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                {
                    if (NotCloth.Any(n => r.name.Contains(n))) continue;
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        bool own = Person.Map(who, r.name, m.name, "_roughness") != null || Person.Map(who, r.name, m.name, "_smoothness") != null;
                        if (!own && Fabrics.For(m.name) == null) missing.Add($"{who}/{r.name}: {m.name}");
                    }
                }
            }
            Assert.IsEmpty(missing, "not in fabrics.json:\n" + string.Join("\n", missing));
        }

        [Test]
        public void EveryWeaveIsANormalMap()
        {
            Assert.Greater(Fabrics.Table.fabrics.Length, 0, "fabrics.json did not load");
            foreach (var f in Fabrics.Table.fabrics.Where(f => !string.IsNullOrEmpty(f.detail)))
            {
                var stem = "Assets/_Project/Resources/" + Person.Folder + Person.Wardrobe + "Detail/" + f.detail + "_normal";
                var path = System.IO.File.Exists(stem + ".png") ? stem + ".png" : stem + ".jpg"; // photographed weaves are JPEG; the skin's, made here, PNG
                var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
                Assert.IsNotNull(importer, path);
                Assert.AreEqual(UnityEditor.TextureImporterType.NormalMap, importer.textureType, path);
                Assert.Greater(f.tiling, 0, f.detail);
            }
        }

        // The owner's bar (12 Nov): every picture on the twenty is 2K or more at its source. Two are not, and no free source at 2K is in hand:
        // the eye (every MakeHuman eye is 1024) and the plate carrier's magazines (1024 in the carrier's own files). Nothing else may join them.
        static readonly string[] Below2K = { "brown_eye", "carrier_mags" };

        [Test]
        public void EveryPictureOnThePeopleIsAtLeast2K()
        {
            var small = new SortedSet<string>();
            foreach (var who in People())
                foreach (var r in Resources.Load<GameObject>(Person.Folder + who).GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        foreach (var kind in new[] { "", "_normal", "_roughness", "_metallic", "_cuta", "_cut", "_opacity" })
                        {
                            var t = kind == "" ? m.mainTexture as Texture2D : Person.Map(who, r.name, m.name, kind);
                            if (t == null) continue;
                            var importer = UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(t)) as UnityEditor.TextureImporter;
                            if (importer == null) continue;
                            importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                            if (Mathf.Min(w, h) < 2048 && !Below2K.Any(b => t.name.StartsWith(b))) small.Add($"{who}/{r.name}: {t.name} {w}x{h}");
                        }
                    }
            Assert.IsEmpty(small, "under 2K:\n" + string.Join("\n", small));
        }

        [Test]
        public void NoLogoTeeIsWorn()
        {
            foreach (var who in People())
                foreach (var r in Resources.Load<GameObject>(Person.Folder + who).GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        Assert.IsFalse(m != null && (m.name.Contains("casualsuit01") || m.name.Contains("casualsuit02") || m.name.Contains("sportsuit01")),
                            $"{who} still wears a top printed with the MakeHuman logo ({m?.name})");
        }

        // Skin, eyes and hair are shaded as what they are (12 Nov): light under the skin, the lids' shade on the eyeball, highlights along the strands.
        [Test]
        public void SkinEyesAndHairAreShadedAsThemselves()
        {
            string[] hairParts = NotCloth.Where(n => n != "body" && n != "high-poly" && n != "teeth" && n != "eyebrow" && n != "eyelashes").ToArray();
            var wrong = new SortedSet<string>();
            foreach (var who in People())
                foreach (var r in Resources.Load<GameObject>(Person.Folder + who).GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        var f = Fabrics.For(m.name);
                        if (m.name.StartsWith("skin_") && (f == null || f.scatter <= 0)) wrong.Add($"{who}/{r.name}: {m.name} has no scatter");
                        if (m.name.StartsWith("brown_eye") && (f == null || f.eye <= 0)) wrong.Add($"{who}/{r.name}: {m.name} is not shaded as an eye");
                        if (hairParts.Any(n => r.name.Contains(n)) && (f == null || f.hair <= 0)) wrong.Add($"{who}/{r.name}: {m.name} has no strand highlights");
                    }
            Assert.IsEmpty(wrong, string.Join("\n", wrong));
        }

        [Test]
        public void ThePersonShaderCompiles()
        {
            var shader = Shader.Find("Dad/Person");
            Assert.IsNotNull(shader, "Dad/Person");
            Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader), "Dad/Person has compile errors: " +
                string.Join("; ", UnityEditor.ShaderUtil.GetShaderMessages(shader).Select(x => x.message)));
        }
    }
}
