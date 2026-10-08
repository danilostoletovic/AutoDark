namespace AutoDark;

public partial class Form1 : Form
{
    private Preferences settings = new();
    private bool busy;

    public Form1()
    {
        InitializeComponent();
        Shown += (_, _) => RefreshState();
    }

    private void RefreshState()
    {
        try
        {
            settings = Automation.Exclusive(Preferences.Load);
            bool registered = Scheduler.Exists();
            toggleButton.Text = settings.Enabled ? "ON" : "OFF";
            statusLabel.Text = settings.Enabled ? "Automatic switching enabled" : "Automatic switching disabled";
            nextLabel.Text = settings.Enabled && settings.NextChange is { } next
                ? $"Next: {TimeZoneInfo.ConvertTime(next, TimeZoneInfo.Local):ddd, d MMM · HH:mm} · {(SolarCalculator.Elevation(next, settings.Latitude!.Value, settings.Longitude!.Value) >= -0.833 ? "Light" : "Dark") }"
                : settings.Enabled ? "Polar day/night · schedule checked daily" : "Light at sunrise. Dark at sunset.";
            attentionLabel.Text = settings.LastError is { } error ? "Needs attention: " + error
                : settings.Enabled && !registered ? "Schedule missing or disabled. Turn OFF, then ON to repair."
                : !settings.Enabled && registered ? "Task still exists. Press OFF again to remove it."
                : settings.Warning ?? (!settings.HasLocation ? "Location will be requested when you turn ON." : "");
            if (!settings.Enabled && registered) toggleButton.Text = "OFF · Remove Task";
            else if (!settings.Enabled && settings.PreviousTheme != null) toggleButton.Text = "OFF · Restore Theme";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            attentionLabel.Text = "Needs attention: " + ex.Message;
        }
    }

    private async void ToggleButton_Click(object? sender, EventArgs e)
    {
        if (busy) return;
        SetBusy(true);
        try
        {
            settings = Automation.Exclusive(Preferences.Load);
            if (settings.Enabled || settings.PreviousTheme != null || Scheduler.Exists()) settings = Automation.Disable();
            else
            {
                await settings.RefreshLocationAsync(true);
                if (!settings.HasLocation)
                {
                    using var dialog = new LocationForm(settings);
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    settings = dialog.Result;
                }
                settings = Automation.Enable(settings);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            MessageBox.Show(this, ex.Message + "\n\nCheck Windows location or Task Scheduler, then try again.", "AutoDark needs attention", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { if (!IsDisposed) { SetBusy(false); RefreshState(); } }
    }

    private async void LocationLink_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (busy) return;
        SetBusy(true);
        try
        {
            settings = Automation.Exclusive(Preferences.Load);
            using var dialog = new LocationForm(settings);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            var location = dialog.Result;
            await location.RefreshLocationAsync(true);
            if (!location.HasLocation) throw new InvalidOperationException("Windows location unavailable. Choose manual coordinates in Location.");
            if (settings.Enabled) settings = Automation.Enable(location);
            else Automation.Exclusive(() => { location.Save(); return 0; });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            MessageBox.Show(this, ex.Message, "Location needs attention", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { if (!IsDisposed) { SetBusy(false); RefreshState(); } }
    }

    private void SetBusy(bool value)
    {
        busy = value;
        toggleButton.Enabled = !value;
        locationLink.Enabled = !value;
        UseWaitCursor = value;
    }
}
