# B01 Timer

A minimal Windows timer with editable hours, minutes and seconds, reusable presets, and light/dark themes. Record is reserved for a future update and is currently disabled.

## Run

Download the Windows x64 build, then run `B01Timer.exe`. No installation or .NET runtime installation is needed. Windows 10/11 x64 is supported.

- Click a time field and type digits. `40` in Hours sets 40 hours; `408` in Minutes sets 04:08; `123000` in Seconds sets 12:30:00.
- Press Enter or move focus to confirm. Escape restores the previous input. Minutes and seconds normalize overflow; the maximum time is 99:59:59.
- Select a preset to set the time without starting. Use `+` to add a preset and right-click a preset for Edit/Remove.
- Play starts or resumes; Pause preserves remaining time. Reset restores the latest time you configured and waits.
- Editing a running timer pauses it. Adding/editing a preset leaves the timer running in the background.
- Space starts/pauses when you are outside a time input. Ctrl+R resets.
- The top-right sun/moon button switches themes.

Theme, presets and the latest configured time are stored in `%LOCALAPPDATA%\B01Timer\settings.json`. The timer opens in its waiting state; an active countdown is not resumed after closing the app. A completed timer shows `Time’s up` and plays a short system sound.

## Build and verification

Requires the .NET 10 SDK. On Windows, run `./scripts/build.ps1`. It runs the core checks and publishes a self-contained single EXE to `artifacts/publish`. `./scripts/qa-ui.ps1 -ExePath ./artifacts/publish/B01Timer.exe` independently operates the real Windows UI and captures screenshots.

GitHub Actions builds the Windows executable and runs UI verification for pushes to main and pull requests. Artifacts contain the EXE, required license notices, screenshots and GUI check results.

The UI uses WPF and per-monitor DPI scaling. Countdown timing uses a monotonic clock and is independent from UI updates. Settings are written as an atomic JSON file replacement.

The source code uses the [MIT license](LICENSE). Outline icons come from [Reicon](https://reicon.dev/icons?weight=outline); their license notices are included in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt). Design specifications are in [docs/design-spec.md](docs/design-spec.md).
