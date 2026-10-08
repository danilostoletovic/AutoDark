# AutoDark

A small Windows 11 theme switcher. **One purpose. One button. Zero bloat.**

Press **ON** to use light mode between astronomical sunrise and sunset, and dark mode at night. Close the window: AutoDark exits completely. Windows Task Scheduler launches brief executions at transitions and catch-up events. There is no tray icon, service, resident worker, timer, account, telemetry, or application network API.

## Use

1. Download and run `AutoDark-Setup-win-x64.exe` from GitHub Releases, then open AutoDark from Start. It installs for your Windows account without administrator privileges. Alternatively, extract `AutoDark-win-x64.zip` to a permanent folder and run `AutoDark.exe`.
2. Press **ON** and approve Windows location access if offered. If unavailable, the Location dialog opens. Uncheck **Use Windows location**, enter latitude/longitude, and Save. North/east are positive; south/west are negative.
3. AutoDark registers its task, applies the current theme immediately, and shows the next change in your current Windows time zone. Close the window.
4. Reopen and press **ON** to turn it **OFF**. OFF removes the task and restores the app/system theme settings saved before ON. If removal fails, the saved OFF state prevents future theme changes and the UI reports the remaining task so you can retry removal.

The button text shows the current state, not the next action. An older already-enabled installation has no pre-ON snapshot; its first OFF leaves the current theme in place, and subsequent ON/OFF cycles restore the previous settings. The small **Location** link lets you update coordinates or retry Windows location. Automatic location is refreshed at every scheduled execution; unavailable access uses saved coordinates with a warning. Manual coordinates must be updated when you travel. Do not move/delete the executable while enabled: turn OFF first, relocate, then turn ON from the new location. Multiple copies share one preference file and one task for the Windows user.

## Build and test in Visual Studio 2026

Install Visual Studio 2026 with **.NET desktop development**, the .NET 10 SDK, and a Windows SDK. Open the existing `AutoDark.slnx`. No additional project or third-party NuGet package is required; .NET restores Microsoft's Windows SDK projections for built-in WinRT location APIs.

1. Select **Release** and build the solution. Launch `AutoDark.exe` directly or use Ctrl+F5. Administrator privileges are not needed.
2. Right-click `Form1.cs` and select **View Designer**; also open `LocationForm.cs` in the Designer. Both use partial classes, standard controls, and `InitializeComponent`; construction performs no registry/task work. Check 100%, 150%, and 200% display scaling and keyboard navigation.
3. Press ON. Test both Windows location consent and manual fallback. Check both DWORDs in `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`: light = `1`, dark = `0` for `AppsUseLightTheme` and `SystemUsesLightTheme`.
4. Open **Task Scheduler > Task Scheduler Library**. Find `AutoDark-<your Windows SID>`. It should use your account, **Run only when user is logged on**, and no highest-privilege option. Check the action points to the portable executable (or the packaged app execution alias) with `--scheduled`, and the next one-shot trigger matches the displayed change. Repeated ON/OFF cycles must not create additional tasks.
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
./scripts/Test-ReleaseTools.ps1
./scripts/Build-Portable.ps1 -Tag v1.0.1 -Output artifacts/release-local
```

`--scheduled` returns 0 on success, 1 on failure; invalid arguments return 2. Scheduled mode branches before `ApplicationConfiguration.Initialize` and does not create a form/message loop. `--render-ui <directory>` is an optional visual verification mode that briefly opens both forms and saves PNGs. The dependency-free checks live in `SelfTests.cs` to keep the original single-project solution.

## How it works

- **Solar calculation:** NOAA/Meeus solar-position equations compute apparent sunrise/sunset at a geometric solar-center elevation of **-0.833 degrees**, accounting for conventional refraction and the solar disk. AutoDark searches UTC instants locally at five-minute intervals, checks intervening extrema to catch grazing polar events, then bisects crossings to within half a second and rounds execution forward. It searches up to 370 days, allowing polar day/night. This numerical precision is not observational accuracy: expect roughly 1-2 minutes at ordinary latitudes under standard conditions, with larger deviations near the poles, mountains, unusual refraction, or an obstructed horizon. It does not model terrain/elevation/weather. [NOAA method and accuracy notes](https://gml.noaa.gov/grad/solcalc/calcdetails.html).
- **Time zone/DST:** transitions are absolute UTC instants. Windows `TimeZoneInfo.Local` converts them for display, with the offset applicable to that date. Switching time zones alone does not change the sun's position at saved coordinates; update location when travelling.
- **Theme:** the current user's two Personalize registry DWORDs are changed only when needed, then read back. A bounded `WM_SETTINGCHANGE` broadcast with `ImmersiveColorSet` informs other applications. Explorer is not restarted. Some third-party applications may require reopening; a hung/nonresponsive application may miss the notification.
- **Task Scheduler:** native `Schedule.Service` COM APIs register/update exactly one SID-named task. XML separates the executable path from fixed arguments and escapes paths safely; no shell, script, password, or elevation is involved. It uses an interactive user token, allows battery execution, `StartWhenAvailable`, three one-minute failure retries, and a two-minute execution limit. A named mutex serializes UI and CLI changes in the same session.
- **Catch-up:** the next transition (or a maintenance execution within 24 hours during polar conditions), daily calendar maintenance, user logon, session unlock, resume events, and clock/time-zone events recalculate the current theme and next schedule. No process stays resident. The task does not wake the device. Switching occurs when the machine is awake and the user is logged on; missed transitions catch up afterward. Event delivery and Task Scheduler launch delays depend on Windows. Resume/clock triggers use System event-log subscriptions; local policy can restrict scheduling or event access and registration failures are reported. [Microsoft task registration API](https://learn.microsoft.com/en-us/windows/win32/taskschd/taskfolder-registertask).

Preferences live in `%LOCALAPPDATA%\AutoDark\preferences.json`, written via atomic replacement. Scheduled errors are retained in preferences where possible and in a single overwritten `last-error.txt`; no endless log accumulation. The UI shows errors on reopening. If configuration JSON is damaged, turn off/remove the AutoDark task in Task Scheduler, remove the JSON, and configure again. Disable competing theme-switching tools.

## Validation and limitations

Development validation: .NET 10 restore, Release build with warnings as errors, 29 focused solar/scheduling and saved-theme checks, portable win-x64 publish and executable checks, native task XML validation without registration, and rendered standard-control forms. Full registry/theme broadcast, consent, actual task registration/execution, sleep/hibernation/restart recovery, and Visual Studio 2026 Designer interaction require the manual checklist above; native XML validation alone is not end-to-end integration testing.

AutoDark has no idle process after its UI closes; Windows still runs its own Task Scheduler service and launches AutoDark briefly for transitions/maintenance/catch-up. This is not a claim that operating-system resource use is zero. The task runs only while this user is logged on, deliberately avoiding credentials, a service, or administrator setup. Windows services/policies and extreme clock changes can delay recovery; reopening and toggling OFF/ON repairs the schedule. The UI's next-change label refreshes on reopening or an action, without a polling timer.

## Distribution and releases

**GitHub Releases:** use `AutoDark-Setup-win-x64.exe` for a normal per-user installation in `%LOCALAPPDATA%\Programs\AutoDark`, with a Start menu shortcut and an entry in **Settings > Apps > Installed apps**. Close AutoDark before installing an update into the same folder. Uninstall through Installed apps; the uninstaller calls `--disable` to remove the scheduled task and restore the saved theme before removing files. Cleanup failure stops uninstall and shows an error. Preferences are retained for reinstalling. Uninstall initialization disables automation even if you cancel the later Windows uninstall confirmation.

The optional `AutoDark-win-x64.zip` contains the standalone EXE for portable use. Turn OFF before moving a portable copy or switching to the installer, then ON from the installed copy. Both use the same preferences and user task. No separate .NET installation is required. Downloads are unsigned and Windows SmartScreen may warn. The bundled native components extract to a per-user temporary cache on first run; no service or resident process is added.

The existing workflow runs Release builds, application checks, and release-tool checks on PRs and pushes to main/master. Stable `vMAJOR.MINOR.PATCH` tags additionally publish/validate the application and ZIP, compile the Inno Setup installer, then create or update one GitHub Release with the installer and portable ZIP. The Store job additionally attaches its validated MSIX when Store packaging is enabled. New releases stay drafts until uploads succeed. Concurrency prevents conflicting runs; only the release job has write permission. PRs receive no Store secrets. Successful retries replace assets; do not retag v1.0.0.

Versions come from tags, independent of the development project version: `v1.0.0` maps to package/file version `1.0.0.0`, `v1.0.1` to `1.0.1.0`, and `v1.1.0` to `1.1.0.0`. Prerelease/build suffixes, leading zeros, major zero, and components above 65534 are rejected (the .NET assembly version limit is narrower than MSIX's). Unsupported tags fail before publishing. CI checks x64 PE architecture, GUI subsystem, icon resources, self-contained .NET 10 bundle contents, file sizes, EXE version, ZIP contents/hash, and CLI self-tests on both the original and extracted EXE. These checks do not prove theme switching or Task Scheduler operation on an interactive PC.

Local portable and installer builds (Windows, .NET 10):

```powershell
./scripts/Build-Portable.ps1 -Tag v1.0.1 -Output artifacts/release-local
# Use fresh output directories; the scripts refuse stale output.
./scripts/Install-InnoCompiler.ps1
./scripts/Build-Installer.ps1 -Tag v1.0.1 `
  -Executable artifacts/release-local/AutoDark.exe -Output artifacts/installer-local `
  -CompilerPath artifacts/inno-tools/compiler/ISCC.exe
```

**Microsoft Store:** the manifest contains AutoDark's actual Partner Center identity. Store packaging runs on version tags by default. Repository Actions variables can override the committed identity when needed:

| Variable | Value |
| --- | --- |
| `STORE_PACKAGING_ENABLED` | `false` skips the Store job; unset/true enables it |
| `STORE_IDENTITY_NAME` | Package identity name from Partner Center |
| `STORE_PUBLISHER` | Exact publisher distinguished name from Partner Center |
| `STORE_PUBLISHER_DISPLAY_NAME` | Exact publisher display name from Partner Center |

Use Partner Center for product and account identifiers. Invalid identity overrides fail explicitly. The Store job runs separately from GitHub Release publication; its failure makes the workflow report failure but does not withhold valid installer/ZIP assets.

Download `AutoDark-Store-submission-<version>` from the tag's Actions run (14-day retention) and extract the `.msix` for manual Partner Center submission. No encryption key or signing secret is required. The validated MSIX is also attached to the corresponding GitHub Release after packaging and installer/ZIP publication succeed. It is an unsigned Store submission package; use the Inno Setup installer for normal installation. [Actions artifacts are accessible to repository readers](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/download-workflow-artifacts); artifacts in a public repository are not confidential.

For a local Store package, install the Windows 11 SDK, build the portable EXE for the same tag, then run (no identity environment variables are needed):

```powershell
./scripts/Build-Store.ps1 -Tag v1.0.1 `
  -Executable artifacts/release-local/AutoDark.exe -Output artifacts/store-local
# Optional -MakeAppxPath points to a Microsoft-signed SDK MakeAppx.exe.
```

MakeAppx validates the manifest without `/nv`, packages the tested EXE and resized existing icon assets, then unpacks it for identity/version/architecture/entry-point/asset/hash checks. Output is `AutoDark-1.0.1.0-x64.msix`: an **unsigned Store submission candidate**, not a public sideload release. Upload the `.msix` under Partner Center's MSIX app submission Packages section, complete listing/privacy/capability declarations, and run Windows App Certification Kit plus the integration checklist below before submission. [Microsoft documents Store signing of MSIX submissions](https://learn.microsoft.com/en-us/windows/apps/publish/get-started): a CA-trusted signature is not needed for this Store submission path; the Store signs packages after certification. Identity checks, restricted-capability approval, and certification remain mandatory; unsigned packages are not automatically accepted or normally user-installable. Sideloading would need separate trusted SHA-256 signing with a certificate matching Publisher; this pipeline publishes no sideload MSIX.

**Packaged Task Scheduler compatibility:** the only application-code change detects package identity and schedules `%LOCALAPPDATA%\Microsoft\WindowsApps\<PackageFamilyName>\AutoDark.Store.exe --scheduled`. Windows manages this [app execution alias](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/desktop-to-uwp-extensions), with a version-independent per-family path instead of a versioned WindowsApps installation directory. User SID, task name, interactive token, triggers, OFF removal, and zero-resident-process architecture stay the same. No shell/helper/service is installed. The alias must remain enabled in Windows Settings; an unavailable alias produces an explicit error. [Microsoft documents per-family aliases and their removal on uninstall](https://learn.microsoft.com/en-us/sysinternals/downloads/microsoft-store).

The Windows 11-only package declares `runFullTrust`, `location`, and `unvirtualizedResources`, with a [narrow registry virtualization exclusion](https://learn.microsoft.com/en-us/windows/msix/desktop/flexible-virtualization) for the existing Personalize theme key. Otherwise registry read-back could falsely succeed while the real Windows theme remains unchanged. Partner Center must approve restricted capabilities. AppData uses normal MSIX virtualization; saved configuration survives updates but portable/packaged copies must not be enabled together. Keep package identity, Application Id, and alias unchanged across upgrades.

**Remaining Store blocker:** [MSIX has no general uninstall hook or declarative Task Scheduler cleanup](https://github.com/microsoft/WindowsAppSDK/discussions/3061). Turn OFF before uninstalling; this removes the task and restores the saved theme. Uninstalling while ON removes the alias but can leave an inert scheduled task behind. This pipeline does not claim unconditional uninstall cleanup or Store approval; production Store rollout needs this limitation reviewed in certification. Adding a resident cleanup service or fragile uninstall workaround would conflict with AutoDark's purpose.

Before a release, manually verify installer installation, Start shortcut, Installed apps entry, same-folder upgrade, and uninstall while ON (task removed and previous theme restored). Also verify on Windows 11 x64: UI/icon/high DPI; ON applies both real registry values; a task runs and exits with the UI closed; OFF removes the task and restores the original mixed theme; resume/restart/logon recovery; no resident process. For MSIX, use a properly signed development package or Partner Center flight, test installation/consent/real registry writes, alias `--self-test` and `--scheduled` invocation, then update to the next version without opening the UI and run the existing task. Confirm OFF-before-uninstall cleanup; also record the known orphan-task behavior if uninstalling while ON. CI is not Windows integration or certification testing.

Troubleshooting: invalid tags fail version validation; compilation/test failures stop releases; extra single-file outputs or malformed bundles stop artifact publication; invalid Store identity overrides or missing SDK tools fail only the Store job; MakeAppx/capability errors require the job log and manifest review. `-SkipAudit` on the local portable script is only for an offline build with already-cached Microsoft dependencies when NuGet's audit endpoint is unavailable; CI keeps auditing enabled.

Create the next release after committing the changes:

```powershell
git tag v1.0.1
git push origin v1.0.1
```

[Privacy policy](PRIVACY.md) | [MIT license](LICENSE.txt)

Copyright (c) 2026 Danilo Stoletović. Released under the MIT license.
