# B01 Timer 0.1

A minimal Windows timer with editable hours, minutes and seconds, reusable presets, and light/dark themes. Record is reserved for a future update and is currently disabled.

## Run

Download the ZIP from [GitHub Releases](https://github.com/BE0X01/b01-timer/releases/latest), extract it, then run `B01Timer.exe`. No installation or .NET runtime installation is needed. Windows 10/11 x64 is supported.

- Click a time field and type digits. `40` in Hours sets 40 hours; `408` in Minutes sets 04:08; `123000` in Seconds sets 12:30:00.
- Press Enter or move focus to confirm. Escape restores the previous input. Minutes and seconds normalize overflow; the maximum time is 99:59:59.
- Select a preset to set the time without starting. Use `+` to add a preset and right-click a preset for Edit/Remove.
- Play starts or resumes; Pause preserves remaining time. Reset restores the latest time you configured and waits.
- Editing a running timer pauses it. Adding/editing a preset leaves the timer running in the background.
- Space starts/pauses when you are outside a time input. Ctrl+R resets.
- The top-right sun/moon button switches themes.

Theme, presets and the latest configured time are stored in `B01Timer.ini` beside `B01Timer.exe`. The INI is created on first launch; keep the EXE in a folder you can write to. Moving this folder keeps your presets and settings with the app. If an older `%LOCALAPPDATA%\B01Timer\settings.json` exists and no portable INI exists yet, its settings are imported once; the original JSON is kept. The timer opens in its waiting state; an active countdown is not resumed after closing the app. A completed timer shows `Time’s up` and plays two three-beep phrases. Reset or configuring a new time stops the alert.

## Build and verification

Requires the .NET 10 SDK. On Windows, run `./scripts/build.ps1`. It runs the core checks and publishes a self-contained single EXE to `artifacts/publish`. `./scripts/qa-ui.ps1 -ExePath ./artifacts/publish/B01Timer.exe` independently operates the real Windows UI and captures screenshots.

GitHub Actions builds the Windows executable and runs UI verification for application, test or build-script changes on main and for pull requests. Use the workflow's **Run workflow** action on main for a delivery build at any time. Each successful main build automatically publishes the EXE, verified ZIP, SHA256 checksums and license notices to GitHub Releases. Pull request builds only produce verification artifacts. Screenshot and GUI check results are also retained as Actions artifacts.

The release version comes from `InformationalVersion` in `src/B01Timer/B01Timer.csproj` (currently `0.1`, with tag `v0.1` and title `Version 0.1`). Set the next version before the next versioned delivery. Rebuilding the same version replaces that release's assets and updates its tag to the successfully verified source commit. A failed test or packaging check prevents release publication. The release notes link to the source commit and Windows verification run.

The UI uses WPF and per-monitor DPI scaling. Pretendard 1.3.9 Regular/SemiBold is embedded in the EXE, including tabular timer digits, so no font installation is required. Buttons have Idle/Hover appearance, a filled Pause glyph and a centered Reset glyph; disabled controls remain visibly disabled. Countdown timing uses a monotonic clock and is independent from UI updates. Settings use readable INI sections and hh:mm:ss values, written through an atomic file replacement.

The source code uses the [MIT license](LICENSE). Icons come from [Reicon](https://reicon.dev/icons?weight=outline); their license notices are included in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt). Design specifications are in [docs/design-spec.md](docs/design-spec.md), and verified delivery results are in [docs/qa-report.md](docs/qa-report.md).

## Editable design guide

The [Figma style guide](https://www.figma.com/design/F3jN688KWp9JsFV7JSqPs7) contains color variables, typography, vector icons and reusable components with interaction states for both themes. [docs/figma-style-guide.md](docs/figma-style-guide.md) records its verified scope and limitations; [docs/figma-design-map.json](docs/figma-design-map.json) maps the actual Figma IDs to the source files. Figma preview fonts use Inter and Roboto Mono because Segoe UI and Consolas were unavailable in the connected editor; the Windows app now uses embedded Pretendard. The existing Figma preview represents the earlier design; the current implementation changes are listed in the guide until Figma editing is available again.

For a later design update, edit the relevant Figma variables or component variants and request implementation using the file or node link. The mapping lets the developer inspect the current Figma values and apply them to the corresponding WPF resources and controls. Edits are applied through an implementation request, with Windows verification before a new release.
