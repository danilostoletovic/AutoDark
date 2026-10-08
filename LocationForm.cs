namespace AutoDark;

internal partial class LocationForm : Form
{
    internal Preferences Result { get; private set; }
    internal LocationForm(Preferences source)
    {
        InitializeComponent();
        Result = new Preferences
        {
            Enabled = source.Enabled, PreviousTheme = source.PreviousTheme, AutomaticLocation = source.AutomaticLocation,
            Latitude = source.Latitude, Longitude = source.Longitude,
            LocationUpdated = source.LocationUpdated, Warning = source.Warning
        };
        automaticBox.Checked = source.AutomaticLocation;
        latitudeInput.Value = (decimal)(source.Latitude ?? 0);
        longitudeInput.Value = (decimal)(source.Longitude ?? 0);
        UpdateInputs();
    }
    private void UpdateInputs() => latitudeInput.Enabled = longitudeInput.Enabled = !automaticBox.Checked;
    private void AutomaticBox_CheckedChanged(object? sender, EventArgs e) => UpdateInputs();
    private void SaveButton_Click(object? sender, EventArgs e)
    {
        Result.AutomaticLocation = automaticBox.Checked;
        if (!automaticBox.Checked)
        {
            Result.Latitude = (double)latitudeInput.Value;
            Result.Longitude = (double)longitudeInput.Value;
            Result.LocationUpdated = DateTimeOffset.UtcNow;
            Result.Warning = null;
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}
