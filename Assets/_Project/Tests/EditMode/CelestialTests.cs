// D-03 sun and moon: Celestial reproduces Meeus' worked examples (the sun to 0.0001°, sidereal
// time exactly, the moon within its series' 0.3°); new and full moons fall on the dates they did;
// noon altitudes at Dhaka are right at the solstices and the equinox; sunrise and sunset on the
// default date are in the right place; and the star field's rotation puts a body where the
// horizontal coordinates do.
using System;
using Dad.Core;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class CelestialTests
    {
        [Test]
        public void SunMatchesMeeusExample25a()
        {
            // 1992 October 13.0 TD: apparent right ascension 198.38083°, declination -7.78507°,
            // apparent longitude 199.90895° (Astronomical Algorithms, example 25.a).
            double jd = Celestial.JulianDay(1992, 10, 13, 0);
            Assert.AreEqual(2448908.5, jd);
            var e = Celestial.Sun(jd, out double longitude);
            Assert.AreEqual(198.38083, e.RightAscension, 1e-4);
            Assert.AreEqual(-7.78507, e.Declination, 1e-4);
            Assert.AreEqual(199.90895, longitude, 2e-4);
        }

        [Test]
        public void SiderealTimeMatchesMeeusExamples12aAnd12b()
        {
            // 1987 April 10, 0h UT: 13h10m46.3668s; at 19:21 UT: 8h34m57.0896s.
            Assert.AreEqual(197.693195, Celestial.SiderealTime(Celestial.JulianDay(1987, 4, 10, 0)), 1e-5);
            Assert.AreEqual(128.7378734, Celestial.SiderealTime(Celestial.JulianDay(1987, 4, 10, 19 + 21 / 60.0)), 1e-5);
        }

        [Test]
        public void MoonIsWithinItsAccuracyOfMeeusExample47a()
        {
            // 1992 April 12, 0h TD: longitude 133.162655°, latitude -3.229126°, parallax 0.991990°
            // from the full theory. The low-precision series is good to about 0.3°.
            Celestial.Moon(Celestial.JulianDay(1992, 4, 12, 0), out double lambda, out double beta, out double parallax);
            Debug.Log($"Moon, example 47.a: longitude {lambda:F4} (133.1627), latitude {beta:F4} (-3.2291), parallax {parallax:F4} (0.9920)");
            Assert.AreEqual(133.162655, lambda, 0.3);
            Assert.AreEqual(-3.229126, beta, 0.2);
            Assert.AreEqual(0.991990, parallax, 0.005);
        }

        [Test]
        public void NewAndFullMoonsFallWhenTheyDid()
        {
            // New moon 2024-04-08 18:21 UT (the total solar eclipse); full moon 2024-03-25 07:00 UT
            // (a penumbral lunar eclipse).
            double newMoon = Celestial.JulianDay(2024, 4, 8, 18 + 21 / 60.0), fullMoon = Celestial.JulianDay(2024, 3, 25, 7);
            Assert.AreEqual(0, Elongation(newMoon), 1.0, "new moon: moon and sun at one longitude");
            Assert.AreEqual(180, Elongation(fullMoon), 1.0, "full moon: opposite");
            Assert.Less(Celestial.MoonIllumination(newMoon), 0.01);
            Assert.Greater(Celestial.MoonIllumination(fullMoon), 0.99);
            // The default date: a waxing gibbous moon.
            double evening = Celestial.JulianDayLocal(2026, 1, 28, 21);
            Assert.That(Elongation(evening), Is.InRange(100.0, 150.0));
        }

        static double Elongation(double jd)
        {
            Celestial.Sun(jd, out double sun);
            Celestial.Moon(jd, out double moon, out _, out _);
            double d = (moon - sun) % 360;
            return d < 0 ? d + 360 : d > 359 ? d - 360 : d;
        }

        [Test]
        public void NoonAltitudesAtDhakaAreRight()
        {
            // The highest the sun climbs in a day is 90° - |latitude - declination|: about 89.6° at
            // the June solstice (declination 23.44°), 42.8° at the December one (-23.44°), and about
            // 66.1° on the March equinox (declination -0.14° at Dhaka's noon, 06:00 UT).
            foreach (var (month, day, expected, tolerance) in new[] { (6, 21, 89.64, 0.15), (12, 21, 42.76, 0.15), (3, 20, 66.06, 0.15) })
            {
                double best = -90, bestHour = 0;
                for (int minute = 9 * 60; minute <= 15 * 60; minute++)
                {
                    var h = Celestial.ToHorizontal(Celestial.Sun(Celestial.JulianDayLocal(2026, month, day, minute / 60.0), out _),
                        Celestial.JulianDayLocal(2026, month, day, minute / 60.0), Celestial.DhakaLatitude, Celestial.DhakaLongitude);
                    if (h.Altitude > best) { best = h.Altitude; bestHour = minute / 60.0; }
                }
                Debug.Log($"Dhaka, 2026-{month:00}-{day:00}: noon altitude {best:F2} at {bestHour:F2} h local");
                Assert.AreEqual(expected, best, tolerance, $"{month}/{day}");
                Assert.That(bestHour, Is.InRange(11.7, 12.3), "solar noon near 12:00 Bangladesh time (meridian 90° E)");
            }
        }

        [Test]
        public void SunriseAndSunsetOnTheDefaultDateAreInPlace()
        {
            // Late January at 23.8° N: the sun rises about 06:40 a little south of east and sets
            // about 17:40 a little south of west. The ranges are for plausibility; the exact minutes
            // come from the algorithm tested above.
            var d = DayCycle.DefaultDate;
            double rise = Crossing(d, 5, 8), set = Crossing(d, 16.5, 19);
            var riseAt = Celestial.SunFromDhaka(Celestial.JulianDayLocal(d.Year, d.Month, d.Day, rise));
            var setAt = Celestial.SunFromDhaka(Celestial.JulianDayLocal(d.Year, d.Month, d.Day, set));
            Debug.Log($"{d:yyyy-MM-dd}: sunrise {Clock(rise)} at azimuth {riseAt.Azimuth:F1}, sunset {Clock(set)} at {setAt.Azimuth:F1}");
            Assert.That(rise, Is.InRange(6 + 35 / 60.0, 6 + 50 / 60.0));
            Assert.That(set, Is.InRange(17 + 35 / 60.0, 17 + 50 / 60.0));
            Assert.That(riseAt.Azimuth, Is.InRange(105.0, 118.0));
            Assert.That(setAt.Azimuth, Is.InRange(242.0, 255.0));
        }

        /// <summary>Local hour at which the apparent sun's upper limb (centre at -0.27°) crosses the horizon, between a and b.</summary>
        static double Crossing(DateTime d, double a, double b)
        {
            double Alt(double h) => Celestial.SunFromDhaka(Celestial.JulianDayLocal(d.Year, d.Month, d.Day, h)).Altitude + 0.27;
            double fa = Alt(a);
            for (int i = 0; i < 60; i++)
            {
                double m = (a + b) / 2, fm = Alt(m);
                if (Math.Sign(fm) == Math.Sign(fa)) { a = m; fa = fm; } else b = m;
            }
            return (a + b) / 2;
        }

        static string Clock(double h) => $"{(int)h:00}:{(int)Math.Round((h - (int)h) * 60):00}";

        [Test]
        public void StarRotationAgreesWithTheHorizon()
        {
            // The sky shader turns the stars with DayCycle.StarRotation. Taking a body's horizontal
            // direction through it must give the body's equatorial direction.
            var rng = new System.Random(4);
            for (int i = 0; i < 200; i++)
            {
                double jd = Celestial.JulianDay(2026, 1, 1, 0) + rng.NextDouble() * 365;
                var e = new Equatorial(rng.NextDouble() * 360, rng.NextDouble() * 160 - 80);
                var (x, y, z) = Celestial.ToHorizontal(e, jd, Celestial.DhakaLatitude, Celestial.DhakaLongitude).Direction();
                var c = DayCycle.StarRotation(jd).MultiplyVector(new Vector3((float)x, (float)y, (float)z));
                double ra = e.RightAscension * Celestial.Rad, dec = e.Declination * Celestial.Rad;
                var want = new Vector3((float)(Math.Cos(dec) * Math.Cos(ra)), (float)(Math.Cos(dec) * Math.Sin(ra)), (float)Math.Sin(dec));
                Assert.Less((c - want).magnitude, 1e-3f, $"RA {e.RightAscension:F1}, dec {e.Declination:F1} at JD {jd:F3}");
            }
        }
    }
}
