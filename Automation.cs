namespace AutoDark;

internal static class Automation
{
    internal static T Exclusive<T>(Func<T> action)
    {
        using var mutex = new Mutex(false, @"Local\AutoDark-" + Scheduler.UserSid);
        bool acquired;
        try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(20)); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) throw new IOException("AutoDark is busy in another window. Try again shortly.");
        try { return action(); }
        finally { mutex.ReleaseMutex(); }
    }

    internal static Preferences Enable(Preferences location) => Exclusive(() =>
    {
        var settings = Preferences.Load();
        settings.Latitude = location.Latitude;
        settings.Longitude = location.Longitude;
        settings.AutomaticLocation = location.AutomaticLocation;
        settings.LocationUpdated = location.LocationUpdated;
        settings.Warning = location.Warning;
        if (!settings.HasLocation) throw new InvalidOperationException("Configure Location before enabling AutoDark.");
        var now = DateTimeOffset.UtcNow;
        var plan = SolarCalculator.Plan(now, settings.Latitude!.Value, settings.Longitude!.Value);
        // Capture only on OFF -> ON, preserving mixed system/app modes and missing values.
        var previousTheme = settings.Enabled ? settings.PreviousTheme : WindowsTheme.Capture();
        // Register before persisting enabled state or modifying the theme. A failed registration never appears as ON.
        Scheduler.Register(SolarCalculator.NextRun(now, plan));
        settings.PreviousTheme = previousTheme;
        settings.Enabled = true;
        settings.NextChange = plan.NextChange;
        settings.LastError = null;
        settings.Save();
        try { WindowsTheme.Apply(plan.Light); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            settings.LastError = ex.Message;
            settings.Save();
            throw;
        }
        return settings;
    });

    internal static Preferences Disable() => Exclusive(() =>
    {
        var settings = Preferences.Load();
        // Persist OFF first: even a task that cannot be removed must not change the theme again.
        settings.Enabled = false;
        settings.NextChange = null;
        settings.Save();
        Exception? removalError = null;
        try { Scheduler.Remove(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { removalError = ex; }
        try
        {
            // OFF is already saved, so a remaining task cannot undo this restoration.
            if (settings.PreviousTheme is { } previousTheme)
            {
                WindowsTheme.Restore(previousTheme);
                settings.PreviousTheme = null;
                settings.LastError = null;
                settings.Save();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            settings.LastError = "Previous theme could not be restored: " + ex.Message;
            if (removalError != null) settings.LastError += " Task removal also failed: " + removalError.Message;
            settings.Save();
            if (removalError != null) throw new AggregateException(settings.LastError, removalError, ex);
            throw;
        }
        if (removalError != null)
        {
            settings.LastError = "Task removal failed: " + removalError.Message;
            settings.Save();
            throw new IOException(settings.LastError, removalError);
        }
        settings.LastError = null;
        settings.Save();
        return settings;
    });

    internal static int Scheduled() => Exclusive(() =>
    {
        var settings = Preferences.Load();
        if (!settings.Enabled) { Scheduler.Remove(); return 0; }
        try
        {
            // Never request consent from the unattended path; only use previously granted access.
            settings.RefreshLocationAsync(false).GetAwaiter().GetResult();
            if (!settings.HasLocation) throw new InvalidOperationException("Saved location is missing. Open AutoDark and configure Location.");
            var now = DateTimeOffset.UtcNow;
            var plan = SolarCalculator.Plan(now, settings.Latitude!.Value, settings.Longitude!.Value);
            Scheduler.Register(SolarCalculator.NextRun(now, plan));
            WindowsTheme.Apply(plan.Light);
            settings.NextChange = plan.NextChange;
            settings.LastError = null;
            settings.Save();
            return 0;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            settings.LastError = ex.Message;
            settings.Save();
            throw;
        }
    });
}
