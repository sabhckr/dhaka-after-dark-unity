// Sun and moon for the time-of-day cycle (D-03, ED-6). Plain C#, angles in degrees.
//
// Sun: Meeus' low-precision solar coordinates (Astronomical Algorithms, ch. 25; about 0.01°).
// Moon: the Astronomical Almanac's low-precision series (about 0.3° in longitude and latitude,
// with its horizontal parallax). Sidereal time: Meeus 12.4. Horizontal coordinates for an
// observer at Dhaka, with refraction near the horizon (Saemundsson) and, for the moon, the
// parallax that lowers it by up to 1°. Universal and terrestrial time are taken as equal: the
// 69 s between them moves the moon by under 0.01°.
using System;

namespace Dad.World
{
    public readonly struct Equatorial
    {
        public readonly double RightAscension, Declination;
        public Equatorial(double ra, double dec) { RightAscension = ra; Declination = dec; }
    }

    public readonly struct Horizontal
    {
        /// <summary>Altitude above the horizon, and azimuth from north through east.</summary>
        public readonly double Altitude, Azimuth;
        public Horizontal(double altitude, double azimuth) { Altitude = altitude; Azimuth = azimuth; }

        /// <summary>Unit vector towards the body in the Unity frame: x east, y up, z north.</summary>
        public (double x, double y, double z) Direction()
        {
            double h = Altitude * Celestial.Rad, a = Azimuth * Celestial.Rad;
            return (Math.Cos(h) * Math.Sin(a), Math.Sin(h), Math.Cos(h) * Math.Cos(a));
        }
    }

    public static class Celestial
    {
        public const double Rad = Math.PI / 180;

        /// <summary>Dhaka (ED-6), and Bangladesh Standard Time: UTC+6, no daylight saving.</summary>
        public const double DhakaLatitude = 23.8, DhakaLongitude = 90.4, UtcOffsetHours = 6;

        /// <summary>Julian day of a UTC date and time (Meeus 7.1, Gregorian calendar).</summary>
        public static double JulianDay(int year, int month, int day, double hours)
        {
            if (month <= 2) { year--; month += 12; }
            int a = year / 100, b = 2 - a + a / 4;
            return Math.Floor(365.25 * (year + 4716)) + Math.Floor(30.6001 * (month + 1)) + day + b - 1524.5 + hours / 24;
        }

        /// <summary>Julian day of a Bangladesh local date and time (hours may run past 24 or below 0).</summary>
        public static double JulianDayLocal(int year, int month, int day, double localHours) =>
            JulianDay(year, month, day, localHours - UtcOffsetHours);

        static double Centuries(double jd) => (jd - 2451545.0) / 36525;

        static double Wrap(double degrees)
        {
            double d = degrees % 360;
            return d < 0 ? d + 360 : d;
        }

        static double Obliquity(double t, double omega) =>
            23.4392911 - 0.0130041667 * t - 1.6389e-7 * t * t + 5.0361e-7 * t * t * t + 0.00256 * Math.Cos(omega * Rad);

        /// <summary>The sun's apparent place, and its apparent ecliptic longitude.</summary>
        public static Equatorial Sun(double jd, out double longitude)
        {
            double t = Centuries(jd);
            double l0 = 280.46646 + 36000.76983 * t + 0.0003032 * t * t;
            double m = (357.52911 + 35999.05029 * t - 0.0001537 * t * t) * Rad;
            double c = (1.914602 - 0.004817 * t - 0.000014 * t * t) * Math.Sin(m) + (0.019993 - 0.000101 * t) * Math.Sin(2 * m) + 0.000289 * Math.Sin(3 * m);
            double omega = 125.04 - 1934.136 * t;
            longitude = Wrap(l0 + c - 0.00569 - 0.00478 * Math.Sin(omega * Rad));
            return FromEcliptic(longitude, 0, Obliquity(t, omega));
        }

        /// <summary>The moon's geocentric place, its ecliptic longitude and latitude, and its horizontal parallax.</summary>
        public static Equatorial Moon(double jd, out double longitude, out double latitude, out double parallax)
        {
            double t = Centuries(jd);
            double S(double x) => Math.Sin(x * Rad);
            double C(double x) => Math.Cos(x * Rad);
            longitude = Wrap(218.32 + 481267.881 * t
                + 6.29 * S(135.0 + 477198.87 * t) - 1.27 * S(259.3 - 413335.36 * t) + 0.66 * S(235.7 + 890534.22 * t)
                + 0.21 * S(269.9 + 954397.74 * t) - 0.19 * S(357.5 + 35999.05 * t) - 0.11 * S(186.5 + 966404.03 * t));
            latitude = 5.13 * S(93.3 + 483202.02 * t) + 0.28 * S(228.2 + 960400.89 * t)
                - 0.28 * S(318.3 + 6003.15 * t) - 0.17 * S(217.6 - 407332.21 * t);
            parallax = 0.9508 + 0.0518 * C(135.0 + 477198.87 * t) + 0.0095 * C(259.3 - 413335.36 * t)
                + 0.0078 * C(235.7 + 890534.22 * t) + 0.0028 * C(269.9 + 954397.74 * t);
            return FromEcliptic(longitude, latitude, Obliquity(t, 125.04 - 1934.136 * t));
        }

        static Equatorial FromEcliptic(double lambda, double beta, double epsilon)
        {
            double l = lambda * Rad, b = beta * Rad, e = epsilon * Rad;
            double ra = Math.Atan2(Math.Sin(l) * Math.Cos(e) - Math.Tan(b) * Math.Sin(e), Math.Cos(l));
            double dec = Math.Asin(Math.Sin(b) * Math.Cos(e) + Math.Cos(b) * Math.Sin(e) * Math.Sin(l));
            return new Equatorial(Wrap(ra / Rad), dec / Rad);
        }

        /// <summary>Greenwich mean sidereal time (Meeus 12.4).</summary>
        public static double SiderealTime(double jd)
        {
            double t = Centuries(jd);
            return Wrap(280.46061837 + 360.98564736629 * (jd - 2451545.0) + 0.000387933 * t * t - t * t * t / 38710000);
        }

        /// <summary>Altitude and azimuth for an observer (longitude east positive), without refraction.</summary>
        public static Horizontal ToHorizontal(Equatorial e, double jd, double latitude, double longitude)
        {
            double h = (SiderealTime(jd) + longitude - e.RightAscension) * Rad;
            double phi = latitude * Rad, dec = e.Declination * Rad;
            double alt = Math.Asin(Math.Sin(phi) * Math.Sin(dec) + Math.Cos(phi) * Math.Cos(dec) * Math.Cos(h));
            double azSouth = Math.Atan2(Math.Sin(h), Math.Cos(h) * Math.Sin(phi) - Math.Tan(dec) * Math.Cos(phi));
            return new Horizontal(alt / Rad, Wrap(azSouth / Rad + 180));
        }

        /// <summary>Atmospheric refraction in degrees for a true altitude (Saemundsson), as seen at the horizon and above.</summary>
        public static double Refraction(double altitude)
        {
            double h = Math.Max(altitude, -1.9);
            return 1.02 / Math.Tan((h + 10.3 / (h + 5.11)) * Rad) / 60;
        }

        /// <summary>Where the sun appears from Dhaka at a local time, refraction included.</summary>
        public static Horizontal SunFromDhaka(double jd)
        {
            var h = ToHorizontal(Sun(jd, out _), jd, DhakaLatitude, DhakaLongitude);
            return new Horizontal(h.Altitude + Refraction(h.Altitude), h.Azimuth);
        }

        /// <summary>Where the moon appears from Dhaka: parallax lowers it, refraction raises it.</summary>
        public static Horizontal MoonFromDhaka(double jd)
        {
            var e = Moon(jd, out _, out _, out double parallax);
            var h = ToHorizontal(e, jd, DhakaLatitude, DhakaLongitude);
            double topocentric = h.Altitude - parallax * Math.Cos(h.Altitude * Rad);
            return new Horizontal(topocentric + Refraction(topocentric), h.Azimuth);
        }

        /// <summary>Fraction of the moon's disc that is lit, from the sun-moon elongation (geocentric, Meeus 48.2 with the distance ratio dropped).</summary>
        public static double MoonIllumination(double jd)
        {
            var s = Sun(jd, out _);
            var m = Moon(jd, out _, out _, out _);
            double d1 = s.Declination * Rad, d2 = m.Declination * Rad, da = (s.RightAscension - m.RightAscension) * Rad;
            double elongation = Math.Acos(Math.Clamp(Math.Sin(d1) * Math.Sin(d2) + Math.Cos(d1) * Math.Cos(d2) * Math.Cos(da), -1, 1));
            return (1 - Math.Cos(elongation)) / 2;
        }
    }
}
