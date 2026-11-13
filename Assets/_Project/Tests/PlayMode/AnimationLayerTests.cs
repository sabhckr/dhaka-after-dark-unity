// How bodies move between clips (2026-11-12, Docs/Characters/SHOOTER_BAR_AUDIT.md section 6 step 2): actions crossfade instead of cutting, the waist up plays its own clip over
// walking legs, a gun kicks and settles, the spine leans to the aim, a pistol is brought onto its target, and the player's jump and landing show.
using System.Collections;
using System.Linq;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class AnimationLayerTests
    {
        static string Model => NpcRoster.Load().people.First(w => w.Walks).model;
        static NpcRecord Fighter(string id) => NpcRoster.Load("People/fighters").people.First(p => p.id == id);

        static IEnumerator For(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) yield return null;
        }

        static void Clear(params GameObject[] also)
        {
            foreach (var p in Object.FindObjectsOfType<Person>()) Object.Destroy(p.gameObject);
            foreach (var rb in Object.FindObjectsOfType<Rigidbody>()) if (rb.GetComponentInParent<Person>() == null) Object.Destroy(rb.gameObject);
            foreach (var g in also) if (g != null) Object.Destroy(g);
            Time.captureFramerate = 0;
        }

        // An action comes in over a fifth of a second and goes over a quarter: in between, the body is part one and part the other, never cut from one to the next.
        [UnityTest]
        public IEnumerator ActionsCrossfade()
        {
            Time.captureFramerate = 30;
            var p = Person.Spawn(Model, Vector3.zero, 0);
            yield return null;
            p.Play("talk");
            Assert.AreEqual(0, p.ActWeight, 1e-4f, "it cut in");
            yield return null; yield return null;
            Assert.That(p.ActWeight, Is.InRange(0.2f, 0.5f), "two frames in");
            yield return For(0.3f);
            Assert.AreEqual(1, p.ActWeight, 1e-4f);
            var hand = p.Body.GetBoneTransform(HumanBodyBones.RightHand);
            var before = p.transform.InverseTransformPoint(hand.position);
            p.Play("phone"); // one act to another: crossfades too, the hand moving a little each frame
            float most = 0;
            var last = before;
            for (int i = 0; i < 9; i++)
            {
                yield return null;
                var now = p.transform.InverseTransformPoint(hand.position);
                most = Mathf.Max(most, (now - last).magnitude);
                last = now;
            }
            Debug.Log($"[Dad] act to act: the hand moved at most {most * 100:F1} cm in a frame, {(last - before).magnitude * 100:F1} cm in all");
            Assert.That(most, Is.LessThan(0.12f), "the hand jumped: a cut, not a crossfade");
            p.Stop();
            yield return null;
            Assert.That(p.ActWeight, Is.InRange(0.5f, 0.95f), "it cut out");
            yield return For(0.4f);
            Assert.AreEqual(0, p.ActWeight, 1e-4f);
            Assert.AreEqual(0, p.Play("talk", 0) > 0 ? 1 - p.ActWeight : 1, 1e-4f, "with no fade it is in at once");
            Clear();
        }

        // A clip over the waist up (a reload) moves the arms and leaves the legs to the walk: two people walking in step, one reloading, have their feet in the same place and their hands not.
        [UnityTest]
        public IEnumerator TheWaistUpPlaysOverWalkingLegs()
        {
            Time.captureFramerate = 30;
            var a = Person.Spawn(Model, new Vector3(-2, 0, 0), 0);
            var b = Person.Spawn(Model, new Vector3(2, 0, 0), 0);
            a.Speed = b.Speed = Person.WalkSpeed;
            yield return For(0.5f);
            float length = a.Upper("reload");
            Assert.That(length, Is.GreaterThan(0.5f));
            yield return For(Mathf.Min(1f, length / 2));
            Assert.That(a.UpperWeight, Is.GreaterThan(0.9f), "the reload is not showing");
            Vector3 Local(Person p, HumanBodyBones bone) => p.transform.InverseTransformPoint(p.Body.GetBoneTransform(bone).position);
            float feet = Mathf.Max((Local(a, HumanBodyBones.LeftFoot) - Local(b, HumanBodyBones.LeftFoot)).magnitude, (Local(a, HumanBodyBones.RightFoot) - Local(b, HumanBodyBones.RightFoot)).magnitude);
            float hands = (Local(a, HumanBodyBones.LeftHand) - Local(b, HumanBodyBones.LeftHand)).magnitude;
            Debug.Log($"[Dad] reloading while walking: feet {feet * 100:F2} cm apart from the one not reloading, left hand {hands * 100:F1} cm");
            Assert.That(feet, Is.LessThan(0.01f), "the reload moved the legs");
            Assert.That(hands, Is.GreaterThan(0.05f), "the reload did not move the arms");
            yield return For(length);
            Assert.AreEqual(0, a.UpperWeight, 1e-4f, "the reload did not end");
            Clear();
        }

        // A shot kicks the gun up and back, and it settles within the second; the spine leans to a target above; a pistol is brought onto its target.
        [UnityTest]
        public IEnumerator GunsKickLeanAndAim()
        {
            Time.captureFramerate = 30;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var rifle = Fighter("GANG_002");
            var pistol = Fighter("GANG_003");
            var r = Dad.Core.Fighter.Spawn(rifle, Dad.Core.Fighter.Side.Gang, new Vector3(-2, 0, 0), 0);
            var g = Dad.Core.Fighter.Spawn(pistol, Dad.Core.Fighter.Side.Gang, new Vector3(2, 0, 0), 0);
            r.HoldFire = g.HoldFire = true;
            r.enabled = g.enabled = false; // held still: no looking about for enemies, the targets are set here
            yield return For(0.5f);
            var up = r.transform.position + new Vector3(0, 6, 10); // well above: some 25 degrees up from the chest
            r.Gun.Target = up;
            var off = g.transform.position + new Vector3(4, 3, 10); // right and up
            g.Gun.Target = off;
            yield return For(0.6f);
            Debug.Log($"[Dad] lean: rifle {r.Gun.LeanPitch:F1} deg up, {r.Gun.LeanYaw:F1} round; pistol {g.Gun.LeanPitch:F1} up, {g.Gun.LeanYaw:F1} round");
            Assert.That(r.Gun.LeanPitch, Is.GreaterThan(8), "the spine did not lean up");
            var bore = g.Gun.Held.forward;
            float miss = Vector3.Angle(bore, off - g.Gun.Muzzle);
            Debug.Log($"[Dad] the pistol points {miss:F1} degrees off its target");
            Assert.That(miss, Is.LessThan(6), "the pistol is not on its target");
            var at = r.Gun.Held.position;
            var way = r.Gun.Held.forward;
            r.Gun.Fire();
            float kick = 0, lift = 0, back = 0;
            for (int i = 0; i < 6; i++)
            {
                yield return null;
                kick = Mathf.Max(kick, r.Gun.Recoil);
                lift = Mathf.Max(lift, Vector3.Angle(way, r.Gun.Held.forward));
                back = Mathf.Max(back, Vector3.Dot(at - r.Gun.Held.position, way));
            }
            Debug.Log($"[Dad] a rifle shot at its most: recoil {kick:F2}, the muzzle lifted {lift:F1} deg, the gun back {back * 100:F1} cm");
            Assert.That(kick, Is.GreaterThan(0.3f), "no kick");
            Assert.That(lift, Is.GreaterThan(0.8f), "the muzzle did not lift");
            yield return For(1f);
            Assert.That(r.Gun.Recoil, Is.LessThan(0.02f), "the kick did not settle");
            Assert.That(Vector3.Angle(way, r.Gun.Held.forward), Is.LessThan(1.5f), "the gun did not come back onto its aim");
            Clear(floor);
        }

        // A jump shows at once with its clip, standing or running, and the body goes no higher than the jump itself; a long drop lands with the landing's clip; all end on the feet.
        [UnityTest]
        public IEnumerator JumpAndLand()
        {
            Time.captureFramerate = 30;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(8, 1, 8);
            var you = OnFoot.Spawn(new Vector3(0, 0.05f, 0), 0, null, null);
            var hips = you.Person.Body.GetBoneTransform(HumanBodyBones.Hips);
            yield return For(0.5f);
            float stand = hips.position.y - you.transform.position.y;
            foreach (bool running in new[] { false, true })
            {
                if (running) { you.Override = new Vector2(0, 1); you.Run = true; yield return For(1f); }
                you.Jump();
                yield return null;
                Assert.IsTrue(you.Person.Acting && you.Leaping, (running ? "a running" : "a standing") + " jump did not show");
                float above = float.MinValue, top = 0, start = you.transform.position.y;
                for (int i = 0; i < 18; i++)
                {
                    yield return null;
                    above = Mathf.Max(above, hips.position.y - you.transform.position.y - stand);
                    top = Mathf.Max(top, you.transform.position.y - start);
                }
                Debug.Log($"[Dad] {(running ? "running" : "standing")} jump: the capsule rose {top * 100:F0} cm, the hips at most {above * 100:F0} cm above where they stand on it");
                Assert.That(above, Is.LessThan(0.12f), "the clip lifts the body above the jump");
                yield return For(1.5f);
                Assert.IsFalse(you.Person.Acting, "still in the air's clip after landing");
                Assert.AreEqual(0, you.Person.Body.transform.localPosition.y, 1e-3f, "the body was left lowered");
            }
            you.Override = null;
            you.Run = false;
            yield return For(0.5f);
            you.transform.position = new Vector3(0, 4, 0); // a drop of four metres: lands hard
            yield return null;
            bool landed = false;
            float until = Time.time + 3;
            while (Time.time < until && !landed)
            {
                yield return null;
                landed = you.GetComponent<CharacterController>().isGrounded && you.Person.Acting;
            }
            Assert.IsTrue(landed, "a long drop did not land with the landing's clip");
            yield return For(1f);
            Assert.IsFalse(you.Person.Acting, "the landing did not end");
            Clear(floor);
        }

        // Facing one way and going another (aiming), the player steps that way with its own clips: sideways, back, and half and half on a diagonal; the feet move the way the body goes.
        [UnityTest]
        public IEnumerator WalksEveryWay()
        {
            Time.captureFramerate = 30;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(6, 1, 6);
            var you = OnFoot.Spawn(new Vector3(0, 0.05f, 0), 0, null, null);
            you.Facing = 0;
            yield return For(0.3f);
            Assert.IsTrue(you.Person.Ways4(1), "the walk has no clips of stepping sideways and back");
            Assert.IsTrue(you.Person.Ways4(2), "the run has none");
            var foot = you.Person.Body.GetBoneTransform(HumanBodyBones.LeftFoot);
            float stepMost = 0; // how far a foot goes in a frame, stepping sideways steadily
            var cases = new (Vector2 move, int way, string name)[] { (new Vector2(1, 0), 1, "right"), (new Vector2(0, -1), 2, "back"), (new Vector2(-1, 0), 3, "left"), (new Vector2(0, 1), 0, "ahead") };
            foreach (var (move, way, name) in cases)
            {
                you.Override = move;
                yield return For(0.8f);
                Assert.That(you.Person.WayWeight(way), Is.GreaterThan(0.95f), "going " + name + " does not step " + name);
                // the foot's swing over a step, in the body's frame: wider along the way it goes than across it
                float along = 0, across = 0;
                var dir = new Vector3(move.x, 0, move.y);
                var first = you.transform.InverseTransformPoint(foot.position);
                float lo = float.MaxValue, hi = float.MinValue, lo2 = float.MaxValue, hi2 = float.MinValue;
                for (int i = 0; i < 40; i++)
                {
                    yield return null;
                    var at = you.transform.InverseTransformPoint(foot.position);
                    if (way == 1) stepMost = Mathf.Max(stepMost, (at - first).magnitude);
                    first = at;
                    float a = Vector3.Dot(at, dir), c = Vector3.Dot(at, Vector3.Cross(Vector3.up, dir));
                    lo = Mathf.Min(lo, a); hi = Mathf.Max(hi, a); lo2 = Mathf.Min(lo2, c); hi2 = Mathf.Max(hi2, c);
                }
                along = hi - lo; across = hi2 - lo2;
                Debug.Log($"[Dad] going {name}: the left foot swings {along * 100:F0} cm along the way and {across * 100:F0} cm across, facing {you.transform.eulerAngles.y:F0} deg");
                Assert.That(along, Is.GreaterThan(across), "going " + name + ": the feet step another way");
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(you.transform.eulerAngles.y, 0)), Is.LessThan(2), "the body turned away from where it aims");
            }
            you.Override = new Vector2(1, 1);
            yield return For(0.8f);
            Debug.Log($"[Dad] diagonal: ahead {you.Person.WayWeight(0):F2}, right {you.Person.WayWeight(1):F2}");
            Assert.AreEqual(0.5f, you.Person.WayWeight(0), 0.05f);
            Assert.AreEqual(0.5f, you.Person.WayWeight(1), 0.05f);
            // ahead to the side and back: the feet cross over between clips without jumping
            var toe = you.transform.InverseTransformPoint(foot.position);
            float most = 0;
            you.Override = new Vector2(1, 0);
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                var now = you.transform.InverseTransformPoint(foot.position);
                most = Mathf.Max(most, (now - toe).magnitude);
                toe = now;
            }
            Debug.Log($"[Dad] from the diagonal to the side: the foot moved at most {most * 100:F1} cm in a frame; stepping sideways steadily, at most {stepMost * 100:F1} cm");
            Assert.That(most, Is.LessThan(stepMost * 1.25f + 0.01f), "the feet jumped between clips");
            // with a gun, whichever way you step, the gun stays on the aim ahead
            foreach (var kind in new[] { Armed.Scar, Armed.Glock })
            {
                you.Gun = kind;
                var gun = Armed.Give(you.Person, kind.Name);
                gun.Target = you.transform.position + new Vector3(0, 1.4f, 30);
                foreach (var (move, way, name) in cases)
                {
                    you.Override = move;
                    yield return For(0.8f);
                    float off = 0;
                    for (int i = 0; i < 20; i++)
                    {
                        yield return null;
                        gun.Target = you.transform.position + new Vector3(0, 1.4f, 30);
                        off = Mathf.Max(off, Vector3.Angle(Vector3.ProjectOnPlane(gun.Held.forward, Vector3.up), Vector3.forward));
                    }
                    Debug.Log($"[Dad] {kind.Name} stepping {name}: the gun at most {off:F1} deg off the aim, round");
                    Assert.That(off, Is.LessThan(8), kind.Name + " stepping " + name + ": the gun leaves the aim");
                }
                Object.Destroy(gun.Held.gameObject);
                Object.Destroy(gun);
                you.Gun = null;
                yield return null;
            }
            Clear(floor);
        }

        // Every clip of stepping sideways or back carries the feet that way: a foot swings sideways in the side steps, ahead and back in the others. Measured with it, how far the
        // line of the shoulders turns from square (on average and at most). Mixamo's side steps all open the body toward the way they go, 40 to 65 degrees (the ten candidates
        // tried on 2026-11-12, SHOOTER_BAR_AUDIT.md section 9); with a gun in hand Armed keeps it on the aim (WalksEveryWay).
        [UnityTest]
        public IEnumerator SteppingClipsGoTheirWay()
        {
            var p = Person.Spawn(Model, Vector3.zero, 0);
            yield return null;
            var clips = new[] { "walk", "walk-left", "walk-right", "walk-back", "run", "run-left", "run-right", "run-back", "rifle-walk", "rifle-walk-left", "rifle-walk-right",
                "rifle-walk-back", "rifle-run-back", "pistol-walk", "pistol-walk-left", "pistol-walk-right", "pistol-walk-back" };
            var bad = new System.Collections.Generic.List<string>();
            foreach (var clip in clips)
            {
                float length = p.Play(clip, 0);
                yield return null;
                float most = 0, sum = 0, hipsMost = 0, x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
                int n = 0;
                for (float t = 0; t < length; t += 1 / 30f, n++)
                {
                    var across = p.At(HumanBodyBones.RightUpperArm, t) - p.At(HumanBodyBones.LeftUpperArm, t);
                    var hipsAcross = p.At(HumanBodyBones.RightUpperLeg, t) - p.At(HumanBodyBones.LeftUpperLeg, t);
                    float yaw = Vector3.SignedAngle(Vector3.right, Vector3.ProjectOnPlane(across, Vector3.up), Vector3.up);
                    float hipsYaw = Vector3.SignedAngle(Vector3.right, Vector3.ProjectOnPlane(hipsAcross, Vector3.up), Vector3.up);
                    most = Mathf.Max(most, Mathf.Abs(yaw));
                    hipsMost = Mathf.Max(hipsMost, Mathf.Abs(hipsYaw));
                    sum += yaw;
                    var foot = p.At(HumanBodyBones.LeftFoot, t);
                    x0 = Mathf.Min(x0, foot.x); x1 = Mathf.Max(x1, foot.x); z0 = Mathf.Min(z0, foot.z); z1 = Mathf.Max(z1, foot.z);
                }
                Debug.Log($"[Dad] {clip}: shoulders turn {sum / n:F0} deg on average, at most {most:F0}; hips at most {hipsMost:F0}; the left foot swings {(x1 - x0) * 100:F0} cm sideways, {(z1 - z0) * 100:F0} cm ahead-back");
                bool side = clip.EndsWith("-left") || clip.EndsWith("-right");
                float sideways = x1 - x0, along = z1 - z0;
                if (side ? sideways < 2 * along : along < 2 * sideways) bad.Add(clip);
            }
            Assert.IsEmpty(bad, "these clips step another way: " + string.Join(", ", bad));
            Clear();
        }

        // The jump clips: where in each the feet leave the ground and come down (measured, for OnFoot), and that they do leave it.
        [UnityTest]
        public IEnumerator JumpClipsLeaveTheGround()
        {
            var p = Person.Spawn(Model, Vector3.zero, 0);
            yield return null;
            foreach (var clip in new[] { "jump", "run-jump" })
            {
                float length = p.Play(clip, 0);
                Assert.That(length, Is.GreaterThan(0.3f), clip);
                yield return null;
                float rest = Mathf.Min(p.At(HumanBodyBones.LeftFoot, 0).y, p.At(HumanBodyBones.RightFoot, 0).y);
                float off = -1, on = -1, top = 0, topAt = 0;
                var line = new System.Text.StringBuilder();
                for (float t = 0; t <= length; t += 1 / 30f)
                {
                    float low = Mathf.Min(p.At(HumanBodyBones.LeftFoot, t).y, p.At(HumanBodyBones.RightFoot, t).y) - rest;
                    float hips = p.At(HumanBodyBones.Hips, t).y;
                    if (low > 0.06f && off < 0) off = t;
                    if (off >= 0 && on < 0 && low < 0.04f) on = t;
                    if (low > top) { top = low; topAt = t; }
                    line.Append($"{t:F2}:{low * 100:F0}/{hips * 100:F0} ");
                }
                Debug.Log($"[Dad] {clip} ({length:F2} s): the feet leave at {off:F2} s, highest {top * 100:F0} cm at {topAt:F2} s, down at {on:F2} s. Feet/hips cm: {line}");
                Assert.That(top, Is.GreaterThan(0.15f), clip + " does not leave the ground");
            }
            Clear();
        }

        // Turning on the spot steps the feet round: the body says how fast it is turning.
        [UnityTest]
        public IEnumerator TurningOnTheSpotSteps()
        {
            Time.captureFramerate = 30;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var you = OnFoot.Spawn(new Vector3(0, 0.05f, 0), 0, null, null);
            yield return For(0.3f);
            you.Facing = 170;
            yield return null; yield return null;
            Assert.That(you.Person.Turning, Is.GreaterThan(45), "turning on the spot is not seen");
            yield return For(0.6f);
            Assert.That(you.Person.Turning, Is.LessThan(1), "still turning");
            Clear(floor);
        }
    }
}
