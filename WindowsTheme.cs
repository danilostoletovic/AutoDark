using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace AutoDark;

internal sealed class ThemeSnapshot
{
    public int? AppsUseLightTheme { get; set; }
    public int? SystemUsesLightTheme { get; set; }
}

internal static class WindowsTheme
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    internal static ThemeSnapshot Capture()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return new ThemeSnapshot
        {
            AppsUseLightTheme = ReadValue(key, "AppsUseLightTheme"),
            SystemUsesLightTheme = ReadValue(key, "SystemUsesLightTheme")
        };
    }

    private static int? ReadValue(RegistryKey? key, string name)
    {
        object? value = key?.GetValue(name);
        if (value == null) return null;
        if (value is int number && key!.GetValueKind(name) == RegistryValueKind.DWord) return number;
        throw new IOException($"Windows theme setting {name} is not a DWORD.");
    }

    internal static void Apply(bool light) => Restore(new ThemeSnapshot
    {
        AppsUseLightTheme = light ? 1 : 0,
        SystemUsesLightTheme = light ? 1 : 0
    });

    internal static void Restore(ThemeSnapshot theme)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, true)
            ?? throw new IOException("Cannot open Windows theme preferences.");
        bool changed = false;
        foreach (var (name, value) in new[]
        {
            ("AppsUseLightTheme", theme.AppsUseLightTheme),
            ("SystemUsesLightTheme", theme.SystemUsesLightTheme)
        })
        {
            object? current = key.GetValue(name);
            if (value.HasValue)
            {
                if (current is int number && number == value.Value && key.GetValueKind(name) == RegistryValueKind.DWord) continue;
                key.SetValue(name, value.Value, RegistryValueKind.DWord);
            }
            else
            {
                if (current == null) continue;
                key.DeleteValue(name, false);
            }
            changed = true;
        }
        if (ReadValue(key, "AppsUseLightTheme") != theme.AppsUseLightTheme
            || ReadValue(key, "SystemUsesLightTheme") != theme.SystemUsesLightTheme)
            throw new IOException("Windows did not retain the requested theme settings.");
        if (changed)
        {
            // Bounded broadcast: hung third-party windows cannot block the app indefinitely.
            SendMessageTimeout(new IntPtr(0xffff), 0x001a, IntPtr.Zero, "ImmersiveColorSet", 0x0002, 200, out _);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam,
        string lParam, uint flags, uint timeout, out UIntPtr result);
}
