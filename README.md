# AutoDark

A small Windows 11 theme switcher. **One purpose. One button. Zero bloat.**

Press **ON** to use light mode between astronomical sunrise and sunset, and dark mode at night. Close the window: AutoDark exits completely. Windows Task Scheduler launches brief executions at transitions and catch-up events. There is no tray icon, service, resident worker, timer, account, telemetry, or application network API.

## Use

1. Extract the portable win-x64 release to a permanent folder in your user account. Run `AutoDark.exe` normally, without administrator privileges.
2. Press **ON** and approve Windows location access if offered. If unavailable, the Location dialog opens. Uncheck **Use Windows location**, enter latitude/longitude, and Save. North/east are positive; south/west are negative.
3. AutoDark registers its task, applies the current theme immediately, and shows the next change in your current Windows time zone. Close the window.
4. Reopen and press **ON** to turn it **OFF**. OFF removes the task and restores the app/system theme settings saved before ON. If removal fails, the saved OFF state prevents future theme changes and the UI reports the remaining task so you can retry removal.

The button text shows the current state, not the next action. An older already-enabled installation has no pre-ON snapshot; its first OFF leaves the current theme in place, and subsequent ON/OFF cycles restore the previous settings. The small **Location** link lets you update coordinates or retry Windows location. Automatic location is refreshed at every scheduled execution; unavailable access uses saved coordinates with a warning. Manual coordinates must be updated when you travel. Do not move/delete the executable while enabled: turn OFF first, relocate, then turn ON from the new location. Multiple copies share one preference file and one task for the Windows user.

## Build and test in Visual Studio 2026

Install Visual Studio 2026 with **.NET desktop development**, the .NET 10 SDK, and a Windows SDK. Open the existing `AutoDark.slnx`. No additional project or third-party NuGet package is required; .NET restores Microsoft's Windows SDK projections for built-in WinRT location APIs.

1. Select **Release** and build the solution. Launch `AutoDark.exe` directly or use Ctrl+F5. Administrator privileges are not needed.
2. Right-click `Form1.cs` ? **View Designer**; also open `LocationForm.cs` in the Designer. Both use partial classes, standard controls, and `InitializeComponent`; construction performs no registry/task work. Check 100%, 150%, and 200% display scaling and keyboard navigation.
3. Press ON. Test both Windows location consent and manual fallback. Check both DWORDs in `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`: light = `1`, dark = `0` for `AppsUseLightTheme` and `SystemUsesLightTheme`.
4. Open Task Scheduler ? Task Scheduler Library. Find `AutoDark-<your Windows SID>`. It should use your account, **Run only when user is logged on**, and no highest-privilege option. Check the action points to the exact executable with `--scheduled`, and the next one-shot trigger matches the displayed change. Repeated ON/OFF cycles must not create additional tasks.
5. Close AutoDark and verify no `AutoDark.exe` remains in Task Manager. Select **Run** on its task; verify it exits and Last Run Result is `0x0`. Observe a real sunrise/sunset transition. Test sleep across a transition, resume/unlock, sign-out/logon, restart/logon, clock changes, time-zone changes, and battery operation.
6. Reopen, turn OFF, and verify the task disappears. Running `AutoDark.exe --scheduled` while OFF must not change the theme or create a task. Test location disabled and a moved executable; errors must be visible on reopening.

Command-line build and focused tests (PowerShell is used only for development commands, never by the application):

```powershell
dotnet restore AutoDark.csproj
dotnet build AutoDark.csproj -c Release --no-restore -warnaserror
dotnet bin/Release/net10.0-windows10.0.19041.0/AutoDark.dll --self-test
# Exit code 0 means all checks passed. No windows, registry writes, or task writes.
dotnet bin/Release/net10.0-windows10.0.19041.0/AutoDark.dll --validate-task
# Read-only validation through the actual Task Scheduler API; no task is registered.
dotnet publish AutoDark.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64
```

`--scheduled` returns 0 on success, 1 on failure; invalid arguments return 2. Scheduled mode branches before `ApplicationConfiguration.Initialize` and does not create a form/message loop. `--render-ui <directory>` is an optional visual verification mode that briefly opens both forms and saves PNGs. The dependency-free checks live in `SelfTests.cs` to keep the original single-project solution.

## How it works

- **Solar calculation:** NOAA/Meeus solar-position equations compute apparent sunrise/sunset at a geometric solar-center elevation of **-0.833�**, accounting for conventional refraction and the solar disk. AutoDark searches UTC instants locally at five-minute intervals, checks intervening extrema to catch grazing polar events, then bisects crossings to within half a second and rounds execution forward. It searches up to 370 days, allowing polar day/night. This numerical precision is not observational accuracy: expect roughly 1�2 minutes at ordinary latitudes under standard conditions, with larger deviations near the poles, mountains, unusual refraction, or an obstructed horizon. It does not model terrain/elevation/weather. [NOAA method and accuracy notes](https://gml.noaa.gov/grad/solcalc/calcdetails.html).
- **Time zone/DST:** transitions are absolute UTC instants. Windows `TimeZoneInfo.Local` converts them for display, with the offset applicable to that date. Switching time zones alone does not change the sun's position at saved coordinates; update location when travelling.
- **Theme:** the current user's two Personalize registry DWORDs are changed only when needed, then read back. A bounded `WM_SETTINGCHANGE` broadcast with `ImmersiveColorSet` informs other applications. Explorer is not restarted. Some third-party applications may require reopening; a hung/nonresponsive application may miss the notification.
- **Task Scheduler:** native `Schedule.Service` COM APIs register/update exactly one SID-named task. XML separates the executable path from fixed arguments and escapes paths safely; no shell, script, password, or elevation is involved. It uses an interactive user token, allows battery execution, `StartWhenAvailable`, three one-minute failure retries, and a two-minute execution limit. A named mutex serializes UI and CLI changes in the same session.
- **Catch-up:** the next transition (or a maintenance execution within 24 hours during polar conditions), daily calendar maintenance, user logon, session unlock, resume events, and clock/time-zone events recalculate the current theme and next schedule. No process stays resident. The task does not wake the device. Switching occurs when the machine is awake and the user is logged on; missed transitions catch up afterward. Event delivery and Task Scheduler launch delays depend on Windows. Resume/clock triggers use System event-log subscriptions; local policy can restrict scheduling or event access and registration failures are reported. [Microsoft task registration API](https://learn.microsoft.com/en-us/windows/win32/taskschd/taskfolder-registertask).

Preferences live in `%LOCALAPPDATA%\AutoDark\preferences.json`, written via atomic replacement. Scheduled errors are retained in preferences where possible and in a single overwritten `last-error.txt`; no endless log accumulation. The UI shows errors on reopening. If configuration JSON is damaged, turn off/remove the AutoDark task in Task Scheduler, remove the JSON, and configure again. Disable competing theme-switching tools.

## Validation and limitations

Development validation: .NET 10 restore, Release build with warnings as errors, 27 focused solar/scheduling and saved-theme checks, portable win-x64 publish and executable checks, native task XML validation without registration, and rendered standard-control forms. Full registry/theme broadcast, consent, actual task registration/execution, sleep/hibernation/restart recovery, and Visual Studio 2026 Designer interaction require the manual checklist above; native XML validation alone is not end-to-end integration testing.

AutoDark has no idle process after its UI closes; Windows still runs its own Task Scheduler service and launches AutoDark briefly for transitions/maintenance/catch-up. This is not a claim that operating-system resource use is zero. The task runs only while this user is logged on, deliberately avoiding credentials, a service, or administrator setup. Windows services/policies and extreme clock changes can delay recovery; reopening and toggling OFF/ON repairs the schedule. The UI's next-change label refreshes on reopening or an action, without a polling timer.

## Release and future Store distribution

GitHub Actions builds on Windows using .NET 10, runs focused tests, and publishes a self-contained win-x64 ZIP. Pushing a version tag such as `v1.0.0` creates a GitHub Release. Actions are pinned to immutable commits; only the tag release job receives repository write permission. Set the project version to the intended release version before tagging. Build output is ignored by Git.

The app has explicit version/description metadata and remains a conventional per-user desktop executable. MSIX/signing/Store identity are intentionally deferred. A future Store package will need full-trust desktop configuration, location capability/consent review, stable installed-path/update handling for its scheduled action, and Store-policy validation of Task Scheduler behavior. This repository is not a certified Store package.

[Privacy policy](PRIVACY.md) � [MIT license](LICENSE.txt)

Copyright (c) 2026 Danilo Stoletović. Released under the MIT license.
