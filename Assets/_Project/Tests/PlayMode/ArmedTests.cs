using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class ArmedTests
    {
        // Each person with each gun in its stance: the right palm is on the grip, the left closed on a rifle's fore-end, a rifle's sights are under the right eye, the gun is as long as the real one, the spare
        // magazines laid beside it in the file are not drawn, and a shot is heard by the street from the muzzle.
        [UnityTest]
        public IEnumerator EveryoneHoldsEveryGun()
        {
            var people = NpcRoster.Load().people.Select(w => w.model).Distinct().ToArray();
            var wrong = new List<string>();
            Vector3 heard = Vector3.zero;
            int shots = 0;
            void Hear(Vector3 at, float reach, Danger.Kind kind) { if (kind == Danger.Kind.Shot) { heard = at; shots++; } }
            Danger.Heard += Hear;
            try
            {
                foreach (var who in people)
                    foreach (var gun in new[] { Armed.Scar, Armed.Ump, Armed.Glock })
                    {
                        var p = Person.Spawn(who, new Vector3(0, 0, 0), 180);
                        Assert.IsNotNull(p, who);
                        p.Play(gun.Idle);
                        var armed = Armed.Give(p, gun.Name);
                        Assert.IsNotNull(armed, gun.Name);
                        yield return new WaitForSeconds(0.5f);
                        string name = who + " with the " + gun.Name;
                        float hand = Vector3.Distance(armed.PalmOf(true), armed.Grip);
                        if (hand > 0.03f) wrong.Add($"{name}: palm {hand * 100:F1} cm off the grip");
                        if (!gun.Pistol)
                        {
                            float support = Vector3.Distance(armed.PalmOf(false), armed.Fore);
                            if (support > 0.03f) wrong.Add($"{name}: left palm {support * 100:F1} cm off the fore-end");
                            float eye = Vector3.Distance(Armed.EyeOf(p.Body, armed.transform), armed.SightLine);
                            if (eye > 0.03f) wrong.Add($"{name}: eye {eye * 100:F1} cm off the sights");
                        }
                        var drawn = armed.Held.GetComponentsInChildren<Renderer>();
                        foreach (var r in drawn)
                            foreach (var spare in gun.Spare)
                                if (r.name.StartsWith(spare, System.StringComparison.Ordinal)) wrong.Add($"{name}: spare part {r.name} is drawn");
                        Armed.Extent(armed.Held, armed.Held.worldToLocalMatrix, gun.Added, out var back, out var front);
                        float length = (front.z - back.z) * armed.Held.lossyScale.x;
                        if (Mathf.Abs(length - gun.Length) > gun.Length * 0.03f) wrong.Add($"{name}: {length:F3} m long, not {gun.Length} m");
                        int before = shots;
                        armed.Fire();
                        if (shots != before + 1) wrong.Add($"{name}: the shot was not heard");
                        else if (Vector3.Distance(heard, armed.Muzzle) > 0.01f) wrong.Add($"{name}: the shot was heard away from the muzzle");
                        Debug.Log($"[Dad] {name}: palm {hand * 100:F1} cm off the grip");
                        Object.Destroy(p.gameObject);
                        yield return null;
                    }
            }
            finally { Danger.Heard -= Hear; }
            Assert.IsEmpty(wrong, string.Join("\n", wrong));
        }
    }
}
