using System.Xml.Linq;

namespace AutoDark;

internal static class SelfTests
{
    internal static void RenderForms(string directory)
    {
        ApplicationConfiguration.Initialize();
        Directory.CreateDirectory(directory);
        using var main = new Form1();
        using var location = new LocationForm(new Preferences { AutomaticLocation = false });
        foreach (Form form in new Form[] { main, location })
        {
            form.Show();
            Application.DoEvents();
            form.PerformLayout();
            using var image = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
            image.Save(Path.Combine(directory, form is Form1 ? "main.png" : "location.png"));
            form.Hide();
        }
    }

    internal static int Run()
    {
        int count = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Test failed: " + name);
            count++;
        }
        DateTimeOffset Utc(int year, int month, int day, int hour = 0, int minute = 0) => new(year, month, day, hour, minute, 0, TimeSpan.Zero);
        void Near(DateTimeOffset? actual, DateTimeOffset expected, double minutes, string name) =>
            Check(actual.HasValue && Math.Abs((actual.Value - expected).TotalMinutes) < minutes, name);

        // Published NOAA-style horizon references, deliberately allowing weather/refraction differences.
        var london = SolarCalculator.Plan(Utc(2026, 6, 21), 51.5074, -0.1278);
        Check(!london.Light, "London midnight is dark");
        Near(london.NextChange, Utc(2026, 6, 21, 3, 43), 3, "London solstice sunrise");
        var sunset = SolarCalculator.Plan(Utc(2026, 6, 21, 12), 51.5074, -0.1278);
        Check(sunset.Light, "London noon is light");
        Near(sunset.NextChange, Utc(2026, 6, 21, 20, 21), 3, "London solstice sunset");
        var equator = SolarCalculator.Plan(Utc(2026, 3, 20), 0, 0);
        Near(equator.NextChange, Utc(2026, 3, 20, 6, 4), 4, "Equatorial equinox sunrise");
        var sydney = SolarCalculator.Plan(Utc(2026, 12, 21, 12), -33.8688, 151.2093);
        Near(sydney.NextChange, Utc(2026, 12, 21, 18, 41), 4, "Southern hemisphere and UTC date rollover");
        var tromsoSummer = SolarCalculator.Plan(Utc(2026, 6, 21, 12), 69.6492, 18.9553);
        Check(tromsoSummer.Light && tromsoSummer.NextChange > Utc(2026, 7, 1), "Polar day skips absent sunsets");
        var tromsoWinter = SolarCalculator.Plan(Utc(2026, 12, 21, 12), 69.6492, 18.9553);
        Check(!tromsoWinter.Light && tromsoWinter.NextChange > Utc(2027, 1, 1), "Polar night skips absent sunrises");
        var pole = SolarCalculator.Plan(Utc(2026, 6, 21), 90, 0);
        Check(pole.Light && pole.NextChange is not null, "Exact pole seasonal transition");
        // Less than a minute of daylight, entirely between the five-minute samples.
        // Derive the near-tangent latitude from a densely sampled independent fixture.
        var peak = Utc(2026, 12, 21, 11, 50);
        double peakElevation = double.MinValue;
        for (int seconds = 0; seconds <= 1200; seconds++)
        {
            var instant = Utc(2026, 12, 21, 11, 50).AddSeconds(seconds);
            double value = SolarCalculator.Elevation(instant, 67.39, 0);
            if (value > peakElevation) { peakElevation = value; peak = instant; }
        }
        double lowerLatitude = 67, upperLatitude = 68;
        for (int i = 0; i < 35; i++)
        {
            double middle = (lowerLatitude + upperLatitude) / 2;
            if (SolarCalculator.Elevation(peak, middle, 0) > -0.83299) lowerLatitude = middle;
            else upperLatitude = middle;
        }
        var grazing = SolarCalculator.Plan(Utc(2026, 12, 21), lowerLatitude, 0);
        Near(grazing.NextChange, peak, 1, "Detect grazing polar sunrise between samples");
        Check(SolarCalculator.Plan(grazing.NextChange!.Value, lowerLatitude, 0).Light, "Grazing sunrise rounded into daylight");
        var now = Utc(2026, 3, 29, 0, 30);
        var plan = SolarCalculator.Plan(now, 47.4979, 19.0402);
        Check(plan == SolarCalculator.Plan(now.ToOffset(TimeSpan.FromHours(2)), 47.4979, 19.0402), "Same instant across DST offsets");
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");
        Check(TimeZoneInfo.ConvertTime(plan.NextChange!.Value, zone).Offset == TimeSpan.FromHours(2), "Transition displays post-DST offset");
        var after = SolarCalculator.Plan(london.NextChange!.Value, 51.5074, -0.1278);
        Check(after.Light && after.NextChange > london.NextChange, "Forward rounding prevents rescheduling same sunrise");
        Check(SolarCalculator.NextRun(now, new(false, now.AddMinutes(30))) == now.AddMinutes(30), "Schedule upcoming transition");
        Check(SolarCalculator.NextRun(now, new(true, now.AddDays(40))) == now.AddHours(24), "Bound polar maintenance delay");
        Check(SolarCalculator.NextRun(now, new(true, null)) == now.AddHours(24), "Missing transition maintenance");
        bool rejected = false;
        try { SolarCalculator.Plan(now, double.NaN, 0); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Reject invalid coordinates");
        string path = @"C:\A & B\AutoDark.exe";
        var xml = XDocument.Parse(Scheduler.Definition("S-1-5-21-test", path, now.AddHours(1), now));
        XNamespace ns = xml.Root!.Name.Namespace;
        Check(xml.Descendants(ns + "Command").Single().Value == path, "XML preserves executable path without shell quoting");
        Check(xml.Descendants(ns + "Arguments").Single().Value == "--scheduled", "Fixed CLI arguments");
        Check(xml.Descendants(ns + "LogonType").Single().Value == "InteractiveToken", "No stored password or elevation");
        Check(xml.Descendants(ns + "StartBoundary").First().Value.EndsWith('Z'), "UTC scheduling boundary");
        Check(xml.Descendants(ns + "StartWhenAvailable").Single().Value == "true", "Missed-run recovery");
        Check(xml.Descendants(ns + "MultipleInstancesPolicy").Single().Value == "IgnoreNew", "No duplicate task instances");
        var preferences = new Preferences
        {
            Enabled = true,
            PreviousTheme = new ThemeSnapshot { AppsUseLightTheme = 1, SystemUsesLightTheme = 0 }
        };
        var restoredPreferences = System.Text.Json.JsonSerializer.Deserialize<Preferences>(System.Text.Json.JsonSerializer.Serialize(preferences))!;
        Check(restoredPreferences.PreviousTheme is { AppsUseLightTheme: 1, SystemUsesLightTheme: 0 }, "Persist original mixed theme across process exits");
        preferences.PreviousTheme = new ThemeSnapshot { AppsUseLightTheme = null, SystemUsesLightTheme = 1 };
        restoredPreferences = System.Text.Json.JsonSerializer.Deserialize<Preferences>(System.Text.Json.JsonSerializer.Serialize(preferences))!;
        Check(restoredPreferences.PreviousTheme is { AppsUseLightTheme: null, SystemUsesLightTheme: 1 }, "Preserve originally absent theme values");
        Check(System.Text.Json.JsonSerializer.Deserialize<Preferences>("{\"Enabled\":true}")!.PreviousTheme == null, "Old preferences do not invent a previous theme");
        Console.WriteLine($"AutoDark: {count} focused checks passed.");
        return 0;
    }
}

