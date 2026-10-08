using System.Text.Json;
using Windows.Devices.Geolocation;

namespace AutoDark;

internal sealed class Preferences
{
    public bool Enabled { get; set; }
    public ThemeSnapshot? PreviousTheme { get; set; }
    public bool AutomaticLocation { get; set; } = true;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTimeOffset? LocationUpdated { get; set; }
    public DateTimeOffset? NextChange { get; set; }
    public string? Warning { get; set; }
    public string? LastError { get; set; }
    public bool HasLocation => Latitude.HasValue && Longitude.HasValue;

    internal static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoDark");
    private static string FilePath => Path.Combine(DirectoryPath, "preferences.json");
    internal static Preferences Load()
    {
        if (!File.Exists(FilePath)) return new();
        var result = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath))
            ?? throw new InvalidDataException("AutoDark preferences are empty. Remove preferences.json to reset.");
        if (result.HasLocation) SolarCalculator.Validate(result.Latitude!.Value, result.Longitude!.Value);
        return result;
    }
    internal void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        string temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, true);
    }

    internal async Task RefreshLocationAsync(bool requestConsent)
    {
        if (!AutomaticLocation) return;
        try
        {
            if (requestConsent && await Geolocator.RequestAccessAsync() != GeolocationAccessStatus.Allowed)
                throw new InvalidOperationException("Windows location access is unavailable.");
            var locator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
            var position = await locator.GetGeopositionAsync(TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(8));
            Latitude = position.Coordinate.Point.Position.Latitude;
            Longitude = position.Coordinate.Point.Position.Longitude;
            SolarCalculator.Validate(Latitude.Value, Longitude.Value);
            LocationUpdated = DateTimeOffset.UtcNow;
            Warning = null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Warning = HasLocation
                ? "Windows location unavailable; using saved coordinates. Update Location if you moved."
                : "Location needs attention. Set latitude and longitude or allow Windows location.";
        }
    }
}
