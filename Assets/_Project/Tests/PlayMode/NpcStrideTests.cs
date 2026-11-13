using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class NpcStrideTests
    {
        // A planted foot travels under the body at the clip's own pace: the figures Person plays them at must be that pace, or the feet slide.
        [UnityTest]
        public IEnumerator EachClipPlaysAtTheSpeedItsFeetTravel()
        {
            var wrong = new List<string>(); // every clip that is off is reported, not only the first
            foreach (var model in new[] { OnFoot.Who })
            {
                var p = Person.Spawn(model, Vector3.zero, 0);
                yield return null;
                float head = p.Body.GetBoneTransform(HumanBodyBones.Head).position.y;
                foreach (var (clip, perHead) in new[] { ("walk", Person.PerHead("walk")), ("text-walk", Person.PerHead("text-walk")), ("bag-walk", Person.PerHead("bag-walk")), ("run", Person.RunPerHead), ("flee", Person.RunPace("flee")), ("sprint", Person.RunPace("sprint")), ("jog", Person.RunPace("jog")) })
                {
                    float len = p.Play(clip);
                    const int n = 200;
                    var foot = p.Body.GetBoneTransform(HumanBodyBones.RightFoot);
                    var at = new Vector3[n];
                    for (int i = 0; i < n; i++) { p.Pose(0, len * i / n); at[i] = foot.position; }
                    float low = at.Min(v => v.y), high = at.Max(v => v.y);
                    var speeds = Enumerable.Range(0, n).Where(i => at[i].y < low + (high - low) * 0.12f)
                        .Select(i => { var d = at[(i + 1) % n] - at[i]; d.y = 0; return d.magnitude / (len / n); }).OrderBy(v => v).ToArray();
                    float pace = speeds[speeds.Length / 2] / head;
                    if (Mathf.Abs(pace - perHead) > perHead * 0.1f) wrong.Add($"{model} {clip}: {pace:F3} a second per metre of height, {perHead:F3} expected");
                    float drift = (at[n - 1] - at[0]).magnitude;
                    if (drift >= 0.08f) wrong.Add($"{model} {clip} travels {drift:F3} m instead of walking on the spot");
                }
                Object.Destroy(p.gameObject);
            }
            Assert.IsEmpty(wrong, string.Join("\n", wrong));
        }
    }
}
