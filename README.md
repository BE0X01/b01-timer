# B01 Timer

A minimal Windows timer with editable hours, minutes and seconds, reusable presets, and light/dark themes. Record automatically measures foreground time for up to five registered programs.

## Run

Download the ZIP from [GitHub Releases](https://github.com/BE0X01/b01-timer/releases/latest), extract it, then run `B01Timer.exe`. No installation or .NET runtime installation is needed. Windows 10/11 x64 is supported.

- Click a time field and type digits. `40` in Hours sets 40 hours; `408` in Minutes sets 04:08; `123000` in Seconds sets 12:30:00.
- Press Enter or move focus to confirm. Escape restores the previous input. Minutes and seconds normalize overflow; the maximum time is 99:59:59.
- Select a preset to set the time without starting. Use `+` to add a preset and right-click a preset for Edit/Remove.
- Play starts or resumes; Pause preserves remaining time. Reset restores the latest time you configured and waits.
- Editing a running timer pauses it. Adding/editing a preset leaves the timer running in the background.
- Space starts/pauses in Timer when you are outside a time input. Ctrl+R resets the current timer or selected record.
- The top-right sun/moon button switches themes.

## Record

Switch to Record and use `+` to enter a title and choose a currently running program. Register up to five different executable paths. The dropdown refreshes whenever you open it; start the target program first if it is absent. Chips show your titles. Right-click for Edit/Remove.

Each program automatically accumulates time while one of its windows is in the foreground. There is no start/pause action. Selecting a chip changes the displayed total; all registered programs remain eligible for tracking. Switching between Timer and Record keeps both features running. Reset clears only the displayed record. Closing and reopening a target program continues the same record when its executable path matches.

The clock displays `hh:mm:ss` within the current day. At 24 hours it wraps to `00:00:00` with a small `1d` badge inside the panel; seven days becomes `1w`, then `1w 1d`. The badge does not move the clock or controls. The app must remain open to measure time; sleep, lock and closed-app time do not accrue. Foreground means the active program, including time it remains focused without keyboard input.

Theme, timer presets, registered programs, accumulated record time and the latest configured time are stored in `B01Timer.ini` beside `B01Timer.exe` only once when the app closes normally. During a session, changes stay in memory and the INI is not rewritten; the first INI is created on the first normal close. Forced termination or a crash does not save that session's changes. Keep the EXE in a folder you can write to. Moving this folder keeps your presets and settings with the app. If an older `%LOCALAPPDATA%\B01Timer\settings.json` exists and no portable INI exists yet, its settings are read and written to the INI on normal close; the original JSON is kept. The timer opens in its waiting state; an active countdown is not resumed after closing the app. A completed timer shows `Time’s up` and plays two three-beep phrases. Reset or configuring a new time stops the alert.

## Build and verification

Requires the .NET 10 SDK. On Windows, run `./scripts/build.ps1`. It runs the core checks and publishes a self-contained single EXE to `artifacts/publish`. `./scripts/qa-ui.ps1 -ExePath ./artifacts/publish/B01Timer.exe` independently operates the real Windows UI and captures screenshots. `./scripts/qa-record.ps1 -ExePath ./artifacts/publish/B01Timer.exe` opens five distinct probe programs and verifies actual foreground tracking, tab switching, reset, restart, registration limit and day/week badges.

GitHub Actions builds the Windows executable and runs UI verification for application, test or build-script changes on main and for pull requests. Use the workflow's **Run workflow** action on main for a delivery build at any time. Each successful main build automatically publishes only the verified ZIP to GitHub Releases. The ZIP contains only `B01Timer.exe`, `LICENSE` and `THIRD-PARTY-NOTICES.txt`; checksums remain separate CI artifacts. Older separate EXE/checksum/license release assets are removed after a successful update. Pull request builds only produce verification artifacts. Screenshot and GUI check results are also retained as Actions artifacts.

The release version comes from `InformationalVersion` in `src/B01Timer/B01Timer.csproj` (currently `0.3`, with tag `v0.3` and title `Version 0.3`). Set the next version before the next versioned delivery. Rebuilding the same version replaces that release's assets and updates its tag to the successfully verified source commit. A failed test or packaging check prevents release publication. The release notes link to the source commit and Windows verification run.

The UI uses WPF and per-monitor DPI scaling. Pretendard 1.3.9 Regular/SemiBold is embedded in the EXE, including tabular timer digits, so no font installation is required. Timer and Record keep digits, colons and labels in the same positions while waiting, paused, running or recording. Buttons have Idle/Hover appearance, a filled Pause glyph and a centered Reset glyph; disabled controls remain visibly disabled. Countdown timing uses a monotonic clock and is independent from UI updates. Settings use readable INI sections and timer hh:mm:ss values, written through an atomic file replacement on normal close. Record sections store Title, ExecutablePath and ElapsedTicks (100ns units), including fractions of a second. Before saving, a pending valid time edit is committed and the last foreground interval is included in record totals. Play/Pause/Reset share a 20 DIP icon canvas with matched ink diameter and centering; the theme toggle is 36 × 36 DIP.

The source code uses the [MIT license](LICENSE). Icons come from [Reicon](https://reicon.dev/icons?weight=outline); their license notices are included in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt). Design specifications are in [docs/design-spec.md](docs/design-spec.md), and verified delivery results are in [docs/qa-report.md](docs/qa-report.md).

## Editable design guide

The [Figma style guide](https://www.figma.com/design/F3jN688KWp9JsFV7JSqPs7) contains color variables, typography, vector icons and reusable components with interaction states for both themes. [docs/figma-style-guide.md](docs/figma-style-guide.md) records its verified scope and limitations; [docs/figma-design-map.json](docs/figma-design-map.json) maps the actual Figma IDs to the source files. Figma preview fonts use Inter and Roboto Mono because Segoe UI and Consolas were unavailable in the connected editor; the Windows app now uses embedded Pretendard. The existing Figma preview represents the earlier design; the current implementation changes are listed in the guide until Figma editing is available again.

For a later design update, edit the relevant Figma variables or component variants and request implementation using the file or node link. The mapping lets the developer inspect the current Figma values and apply them to the corresponding WPF resources and controls. Edits are applied through an implementation request, with Windows verification before a new release.
