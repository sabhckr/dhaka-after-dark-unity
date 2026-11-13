using Dad.Core;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class NightLifeTests
    {
        [Test]
        public void NothingIsLitInDaylight()
        {
            var l = NightLife.At(12, 0);
            Assert.AreEqual(0, l.Windows);
            Assert.AreEqual(0, l.Lamps);
            Assert.AreEqual(0, l.Shops);
            Assert.AreEqual(0, l.Signs);
        }

        [Test]
        public void TheEveningIsBusierThanTheSmallHours()
        {
            var evening = NightLife.At(20, 1);
            var late = NightLife.At(3, 1);
            Assert.Greater(evening.Windows, 2 * late.Windows);
            Assert.Greater(evening.Shops, 4 * late.Shops);
            Assert.Greater(evening.Signs, 2 * late.Signs);
            Assert.AreEqual(1, evening.Lamps);
            Assert.AreEqual(1, late.Lamps, "the street lamps stay on all night");
        }

        [Test]
        public void NoShareStepsAcrossTheDay()
        {
            // A game minute at a time, right round the clock and across midnight, with the sky darkening as it does.
            var last = NightLife.At(0, 1);
            for (int m = 1; m <= 1440; m++)
            {
                double h = m / 60.0 % 24;
                float night = Mathf.Clamp01(Mathf.Abs((float)h - 12) / 6 - 0.5f);
                var now = NightLife.At(h, night);
                Assert.Less(Mathf.Abs(now.Windows - last.Windows), 0.03f, "windows at " + h);
                Assert.Less(Mathf.Abs(now.Shops - last.Shops), 0.03f, "shops at " + h);
                Assert.Less(Mathf.Abs(now.Signs - last.Signs), 0.03f, "signs at " + h);
                Assert.Less(Mathf.Abs(now.Lamps - last.Lamps), 0.03f, "lamps at " + h);
                last = now;
            }
        }

        [Test]
        public void GradeIsGentleAndNeverSaturates()
        {
            for (float alt = -30; alt <= 80; alt += 2)
            {
                float night = 1 - Mathf.Clamp01((alt + 10) / 13);
                var v = GradeModel.At(night, alt);
                Assert.LessOrEqual(v.Saturation, 0, "the city is never pushed brighter in colour");
                Assert.GreaterOrEqual(v.Saturation, -30, "and never drained");
                Assert.LessOrEqual(Mathf.Abs(v.Contrast), 12);
                Assert.LessOrEqual(v.Bloom, 0.35f);
                Assert.LessOrEqual(v.Vignette, 0.25f);
            }
        }

        [Test]
        public void DayIsWarmAndNightIsCool()
        {
            Assert.Greater(GradeModel.At(0, 50).Temperature, 0);
            Assert.Greater(GradeModel.At(0.1f, 4).Temperature, GradeModel.At(0, 50).Temperature, "a low sun is warmer");
            Assert.Less(GradeModel.At(1, -30).Temperature, 0);
        }

        [Test]
        public void TheLevelFollowsTheCommandLine()
        {
            Assert.AreEqual(Tier.Low, GraphicsTier.Choose(new[] { "game", "-dad-quality", "low" }));
            Assert.AreEqual(Tier.Medium, GraphicsTier.Choose(new[] { "game", "-dad-shot", "x", "-dad-quality", "MEDIUM" }));
            Assert.AreEqual(Tier.High, GraphicsTier.Choose(new[] { "game", "-dad-bench", "x" }), "benches stay comparable");
        }

        [Test]
        public void IntegratedGraphicsStartLow()
        {
            Assert.AreEqual(Tier.Low, GraphicsTier.ForHardware("Intel(R) UHD Graphics 630", 1024));
            Assert.AreEqual(Tier.Low, GraphicsTier.ForHardware("Some Card", 1024));
            Assert.AreEqual(Tier.Medium, GraphicsTier.ForHardware("Apple M2", 8192));
            Assert.AreEqual(Tier.Medium, GraphicsTier.ForHardware("NVIDIA GeForce RTX 3060", 12288), "High is never picked for you");
        }
    }
}
