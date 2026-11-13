// The twenty people made in Blender (2026-11-12, MPFB on the Mixamo skeleton): each comes in as a humanoid with every finger, stands on its feet at a believable height,
// is dressed from the wardrobe, and moves its fingers when a clip of the library does.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class BlenderPeopleTests
    {
        static readonly string[] People = Enumerable.Range(1, 10).Select(i => $"npc_m{i:00}").Concat(Enumerable.Range(1, 10).Select(i => $"npc_f{i:00}")).ToArray();

        static readonly HumanBodyBones[] Fingers = Enumerable.Range((int)HumanBodyBones.LeftThumbProximal, (int)HumanBodyBones.RightLittleDistal - (int)HumanBodyBones.LeftThumbProximal + 1)
            .Select(i => (HumanBodyBones)i).ToArray();

        [UnityTest]
        public IEnumerator EveryoneStandsWithEveryFingerAndGrips()
        {
            Assert.AreEqual(30, Fingers.Length);
            var wrong = new List<string>();
            foreach (var who in People)
            {
                var p = Person.Spawn(who, Vector3.zero, 0);
                if (p == null) { wrong.Add(who + ": does not spawn"); continue; }
                yield return null;
                if (!p.Body.isHuman || p.Body.avatar == null || !p.Body.avatar.isValid) { wrong.Add(who + ": not a valid humanoid"); Object.Destroy(p.gameObject); continue; }
                var missing = Fingers.Where(f => p.Body.GetBoneTransform(f) == null).ToList();
                if (missing.Count > 0) wrong.Add($"{who}: {missing.Count} finger bones unmapped ({missing[0]}...)");

                var head = p.Body.GetBoneTransform(HumanBodyBones.Head).position.y;
                var foot = Mathf.Min(p.Body.GetBoneTransform(HumanBodyBones.LeftFoot).position.y, p.Body.GetBoneTransform(HumanBodyBones.RightFoot).position.y);
                if (head < 1.2f || head > 1.85f) wrong.Add($"{who}: head bone at {head:F2} m");
                if (foot < -0.05f || foot > 0.2f) wrong.Add($"{who}: feet at {foot:F2} m");

                bool dressed = p.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Any(m => m != null && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null);
                if (!dressed) wrong.Add(who + ": no picture on any material (wardrobe not found)");

                var index = p.Body.GetBoneTransform(HumanBodyBones.RightIndexIntermediate);
                p.Play("idle");
                yield return new WaitForSeconds(0.3f);
                var open = index != null ? index.localRotation : Quaternion.identity;
                p.Play("Library/rifle-idle");
                yield return new WaitForSeconds(0.6f);
                if (index != null && Quaternion.Angle(open, index.localRotation) < 3) wrong.Add($"{who}: the right index finger does not move from idle to the rifle grip");
                Debug.Log($"[Dad] {who}: head {head:F2} m, index moved {(index != null ? Quaternion.Angle(open, index.localRotation) : 0):F1} degrees");
                Object.Destroy(p.gameObject);
                yield return null;
            }
            yield return Resources.UnloadUnusedAssets(); // twenty people's maps are most of a gigabyte: let them go before the next test measures frame times
            System.GC.Collect();
            Assert.IsEmpty(wrong, string.Join("\n", wrong));
        }
    }
}
