// Every model on the roster can play every clip there is (NPC Phase 3, 2026-11-08): they are all Humanoid, so one set of clips moves anyone.
using System.Collections;
using System.Linq;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class NpcClipTests
    {
        static readonly string[] Clips = { "idle", "walk", "run", "talk", "turn-left", "turn-right", "sit", "drive", "passenger", "horn", "enter-car", "exit-car", "fight-idle", "jab", "cross-punch", "hook-punch", "kick", "block", "head-hit", "death-back", "death-forward", "death-chest", "wave", "text-stand", "text-walk", "bag-walk", "argue",
            "startle", "cower", "flee", "sprint", "jog", "phone", "smoke", "lean", "drink", "yell", "point", "cover-idle", "pistol-kneel", "pistol-idle", "pistol-walk", "pistol-fire", "rifle-aim", "rifle-fire", "rifle-walk", "rifle-run", "reload", "crouch-idle", "crouch-walk", "crouch-aim" };

        [UnityTest]
        public IEnumerator EveryoneCanPlayEveryClip()
        {
            foreach (var model in NpcRoster.Load().people.Select(p => p.model).Distinct())
            {
                var p = Person.Spawn(model, Vector3.zero, 0);
                Assert.IsNotNull(p, model);
                foreach (var clip in Clips)
                {
                    Assert.Greater(p.Play(clip), 0f, model + " " + clip);
                    yield return null;
                    var h = p.Hips.position;
                    Assert.IsFalse(float.IsNaN(h.x + h.y + h.z), model + " " + clip);
                    Assert.Less(h.magnitude, 5f, model + " " + clip + ": the hips left the body");
                }
                p.Play("walk");
                var foot = p.Body.GetBoneTransform(HumanBodyBones.LeftFoot);
                var was = foot.position;
                yield return new WaitForSeconds(0.25f);
                Assert.Greater((foot.position - was).magnitude, 0.01f, model + ": the foot does not move in the walk");
                Object.Destroy(p.gameObject);
                yield return null;
            }
        }
    }
}
