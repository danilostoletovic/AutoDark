namespace AutoDark;

internal readonly record struct SolarPlan(bool Light, DateTimeOffset? NextChange);

internal static class SolarCalculator
{
    private const double Horizon = -0.833; // Solar radius and standard atmospheric refraction.
    private static double Rad(double degrees) => degrees * Math.PI / 180;
    private static double Deg(double radians) => radians * 180 / Math.PI;
    private static double Normalize(double degrees) => (degrees % 360 + 360) % 360;

    // NOAA/Meeus solar position equations. UTC instants avoid DST and date-line ambiguity.
    internal static double Elevation(DateTimeOffset instant, double latitude, double longitude)
    {
        double jd = instant.ToUnixTimeMilliseconds() / 86400000.0 + 2440587.5;
        double t = (jd - 2451545) / 36525;
        double l = Normalize(280.46646 + t * (36000.76983 + t * 0.0003032));
        double m = Rad(357.52911 + t * (35999.05029 - 0.0001537 * t));
        double e = 0.016708634 - t * (0.000042037 + 0.0000001267 * t);
        double c = Math.Sin(m) * (1.914602 - t * (0.004817 + 0.000014 * t))
            + Math.Sin(2 * m) * (0.019993 - 0.000101 * t) + Math.Sin(3 * m) * 0.000289;
        double omega = Rad(125.04 - 1934.136 * t);
        double apparent = Rad(l + c - 0.00569 - 0.00478 * Math.Sin(omega));
        double obliquity = Rad(23 + (26 + (21.448 - t * (46.815 + t * (0.00059 - t * 0.001813))) / 60) / 60
            + 0.00256 * Math.Cos(omega));
        double declination = Math.Asin(Math.Sin(obliquity) * Math.Sin(apparent));
        double y = Math.Pow(Math.Tan(obliquity / 2), 2);
        double lr = Rad(l);
        double equation = 4 * Deg(y * Math.Sin(2 * lr) - 2 * e * Math.Sin(m)
            + 4 * e * y * Math.Sin(m) * Math.Cos(2 * lr) - 0.5 * y * y * Math.Sin(4 * lr)
            - 1.25 * e * e * Math.Sin(2 * m));
        double minutes = instant.UtcDateTime.TimeOfDay.TotalMinutes;
        double hourAngle = Rad(Normalize((minutes + equation + 4 * longitude) / 4) - 180);
        double lat = Rad(latitude);
        return Deg(Math.Asin(Math.Clamp(Math.Sin(lat) * Math.Sin(declination)
            + Math.Cos(lat) * Math.Cos(declination) * Math.Cos(hourAngle), -1, 1)));
    }

    internal static SolarPlan Plan(DateTimeOffset now, double latitude, double longitude)
    {
        Validate(latitude, longitude);
        bool light = Elevation(now, latitude, longitude) >= Horizon;
        DateTimeOffset previous = now;
        DateTimeOffset beforePrevious = now;
        double previousElevation = Elevation(now, latitude, longitude);
        double beforeElevation = previousElevation;
        // Bounded search also handles months of polar day/night. No resident polling.
        for (int i = 1; i <= 370 * 24 * 12; i++)
        {
            DateTimeOffset candidate = now.AddMinutes(i * 5);
            double elevation = Elevation(candidate, latitude, longitude);
            // A grazing polar sunrise/sunset can fit entirely between samples. Inspect
            // solar maxima/minima rather than silently skipping these short periods.
            bool maximum = previousElevation > beforeElevation && previousElevation > elevation;
            bool minimum = previousElevation < beforeElevation && previousElevation < elevation;
            if (i > 1 && (maximum || minimum) && (elevation >= Horizon) == light)
            {
                var left = beforePrevious;
                var right = candidate;
                for (int j = 0; j < 30; j++)
                {
                    var a = left + (right - left) / 3;
                    var b = right - (right - left) / 3;
                    bool aHigher = Elevation(a, latitude, longitude) > Elevation(b, latitude, longitude);
                    if (aHigher == maximum) right = b; else left = a;
                }
                var extremum = left + (right - left) / 2;
                if ((Elevation(extremum, latitude, longitude) >= Horizon) != light)
                {
                    previous = beforePrevious;
                    candidate = extremum;
                    elevation = Elevation(candidate, latitude, longitude);
                }
            }
            if ((elevation >= Horizon) != light)
            {
                while ((candidate - previous).TotalSeconds > 0.5)
                {
                    var middle = previous + (candidate - previous) / 2;
                    if ((Elevation(middle, latitude, longitude) >= Horizon) == light) previous = middle;
                    else candidate = middle;
                }
                // Round forward: executing just before the crossing would reschedule the same event.
                return new(light, DateTimeOffset.FromUnixTimeSeconds(candidate.ToUnixTimeSeconds() + 1));
            }
            beforePrevious = previous;
            beforeElevation = previousElevation;
            previous = candidate;
            previousElevation = elevation;
        }
        return new(light, null);
    }

    internal static void Validate(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new ArgumentException("Latitude must be -90 to 90; longitude must be -180 to 180.");
    }

    internal static DateTimeOffset NextRun(DateTimeOffset now, SolarPlan plan) =>
        plan.NextChange is { } next && next < now.AddHours(24) ? next : now.AddHours(24);
}
