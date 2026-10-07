param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [string]$ArtifactDirectory = (Join-Path $PSScriptRoot '../artifacts/qa-ui/record')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class RecordNative {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
}
'@
$ExePath = (Resolve-Path $ExePath).Path
New-Item -ItemType Directory -Force $ArtifactDirectory | Out-Null
$ArtifactDirectory = (Resolve-Path $ArtifactDirectory).Path
$settingsDirectory = Join-Path $ArtifactDirectory 'settings'
$ini = Join-Path $settingsDirectory 'B01Timer.ini'
$results = [System.Collections.Generic.List[object]]::new()
$probes = [System.Collections.Generic.List[object]]::new()
$app = $null
$main = $null
function Wait-For([scriptblock]$Probe, [int]$Timeout = 10000) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    do { $value = & $Probe; if ($null -ne $value -and $value -ne $false) { return $value }; Start-Sleep -Milliseconds 100 } while ($watch.ElapsedMilliseconds -lt $Timeout)
    throw 'Record UI wait timed out.'
}
function Check([bool]$Pass, [string]$Name) {
    $results.Add([pscustomobject]@{ name = $Name; passed = $Pass })
    if (-not $Pass) { throw "FAIL $Name" }; Write-Output "PASS $Name"
}
function Control([string]$Id, $Window = $script:main) {
    return Wait-For { $Window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $Id)) }
}
function Invoke([string]$Id, $Window = $script:main) {
    (Control $Id $Window).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 150
}
function Window-Named([string]$Name) {
    return Wait-For { [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.AndCondition]::new([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $script:app.Id), [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name))) }
}
function Start-App {
    $script:app = Start-Process $ExePath -ArgumentList @('--settings-dir', ('"' + $settingsDirectory + '"')) -PassThru
    $script:main = Window-Named 'B01 Timer'
    [RecordNative]::SetForegroundWindow([IntPtr]$script:main.Current.NativeWindowHandle) | Out-Null
}
function Stop-App {
    if ($null -ne $script:app) { $script:app.CloseMainWindow() | Out-Null; if (-not $script:app.WaitForExit(5000)) { $script:app.Kill() }; $script:app.Dispose(); $script:app = $null }
}
function Focus-Probe([int]$Index) {
    $probes[$Index].Refresh()
    $handle = $probes[$Index].MainWindowHandle
    [RecordNative]::SetForegroundWindow($handle) | Out-Null
    Wait-For { [RecordNative]::GetForegroundWindow() -eq $handle } | Out-Null
}
function Display-Time {
    return "$( (Control 'RecordHours').Current.Name ):$( (Control 'RecordMinutes').Current.Name ):$( (Control 'RecordSeconds').Current.Name )"
}
function Record-Ticks([int]$Index) {
    $text = Get-Content -Raw $ini
    $section = [regex]::Match($text, "(?ms)^\[Record$Index\]\r?\n(.*?)(?=^\[|\z)").Groups[1].Value
    return [long][regex]::Match($section, '(?m)^ElapsedTicks=(\d+)').Groups[1].Value
}
function Add-Record([int]$Index, [string]$Title) {
    Invoke 'AddRecordButton'; $dialog = Window-Named 'Add program'
    (Control 'RecordTitleInput' $dialog).GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Title)
    $combo = Control 'ProgramSelect' $dialog
    $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    $item = Wait-For {
        $all = $combo.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem))
        foreach ($element in $all) { if ($element.Current.Name -like "*RecordProbe$Index*") { return $element } }
        return $false
    }
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
    Invoke 'RecordSaveButton' $dialog
    Check ((Control ('RecordChip_' + ($Index - 1))).Current.Name -eq $Title) "Record $Index uses its user title"
}
function Screenshot([string]$Name) {
    [RecordNative]::SetForegroundWindow([IntPtr]$script:main.Current.NativeWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 150
    $rect = $script:main.Current.BoundingRectangle
    $bmp = [Drawing.Bitmap]::new([int]$rect.Width, [int]$rect.Height); $graphics = [Drawing.Graphics]::FromImage($bmp)
    try { $graphics.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size); $bmp.Save((Join-Path $ArtifactDirectory ($Name + '.png')), [Drawing.Imaging.ImageFormat]::Png) }
    finally { $graphics.Dispose(); $bmp.Dispose() }
}
try {
    $probeSource = @'
using System;
using System.Drawing;
using System.Windows.Forms;
public static class RecordProbe {
  [STAThread] public static void Main(string[] args) {
    Application.Run(new Form { Text = args[0], Width = 260, Height = 180, StartPosition = FormStartPosition.Manual, Location = new Point(20,40) });
  }
}
'@
    $probeExe = Join-Path $ArtifactDirectory 'RecordProbe1.exe'
    Add-Type -TypeDefinition $probeSource -ReferencedAssemblies System.Windows.Forms,System.Drawing -OutputAssembly $probeExe -OutputType WindowsApplication
    for ($i = 1; $i -le 5; $i++) {
        $path = Join-Path $ArtifactDirectory "RecordProbe$i.exe"
        if ($i -gt 1) { Copy-Item $probeExe $path }
        $probe = Start-Process $path -ArgumentList "RecordProbe$i" -PassThru; $probes.Add($probe)
        Wait-For { $probe.Refresh(); $probe.MainWindowHandle -ne [IntPtr]::Zero } | Out-Null
    }
    Start-App; Invoke 'RecordTab'
    Check ((Display-Time) -eq '00:00:00' -and (Control 'AddRecordButton').Current.IsEnabled) 'Record starts at zero with only an Add chip'
    Check (-not (Control 'ResetButton').Current.IsEnabled) 'Empty record reset is disabled'
    # Both title and program selection are required.
    Invoke 'AddRecordButton'; $dialog = Window-Named 'Add program'; Invoke 'RecordSaveButton' $dialog
    Check ($null -ne (Window-Named 'Add program')) 'Empty record form cannot be added'
    Invoke 'RecordCancelButton' $dialog
    Add-Record 1 'Work A'; Add-Record 2 'Work B'
    Invoke 'RecordChip_0'; Focus-Probe 0
    Wait-For { (Display-Time) -ne '00:00:00' } 6000 | Out-Null
    Check ((Display-Time) -match '^00:00:0[1-9]$') 'Foreground program accrues seconds automatically'
    Focus-Probe 1; Start-Sleep -Milliseconds 1500
    Invoke 'RecordChip_1'
    Wait-For { (Display-Time) -ne '00:00:00' } | Out-Null
    Check ((Display-Time) -ne '00:00:00') 'Unselected registered program continues to accrue time'
    [RecordNative]::SetForegroundWindow([IntPtr]$main.Current.NativeWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 300
    $paused = Display-Time; Start-Sleep -Milliseconds 1500
    Check ((Display-Time) -eq $paused) 'Own app focus excludes time'
    Invoke 'ResetButton'
    Check ((Display-Time) -eq '00:00:00') 'Record reset returns the selected record to zero'
    Invoke 'RecordChip_0'; Check ((Display-Time) -ne '00:00:00') 'Reset retains other program totals'
    # Countdown continues while Record is displayed; records also run on Timer tab.
    Invoke 'TimerTab'
    $input = Control 'SecondsInput'; $input.SetFocus(); [Windows.Forms.SendKeys]::SendWait('^a30{ENTER}'); Invoke 'StartPauseButton'
    Invoke 'RecordTab'; $before = Record-Ticks 1; Focus-Probe 0; Start-Sleep -Milliseconds 1500
    Invoke 'TimerTab'
    $remaining = (Control 'SecondsInput').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
    Check ([int]$remaining -lt 30) 'Countdown keeps running on Record tab'
    Focus-Probe 1; Start-Sleep -Milliseconds 1500; Invoke 'RecordTab'; Invoke 'RecordChip_1'
    Check ((Display-Time) -ne '00:00:00' -and (Record-Ticks 1) -gt $before) 'Record keeps accumulating across both tabs'
    Screenshot 'record-two-programs'
    Stop-App; $savedA = Record-Ticks 1; $savedB = Record-Ticks 2
    Start-Sleep -Milliseconds 1200; Start-App; Invoke 'RecordTab'
    Check ((Record-Ticks 1) -eq $savedA -and (Record-Ticks 2) -eq $savedB) 'Restart retains both totals without offline time'
    Check ((Control 'RecordChip_0').Current.Name -eq 'Work A' -and (Control 'RecordChip_1').Current.Name -eq 'Work B') 'Restart restores program registrations'
    $probe = $probes[1]; $probe.CloseMainWindow() | Out-Null; $probe.WaitForExit(3000) | Out-Null; $probe.Dispose()
    $probes[1] = Start-Process (Join-Path $ArtifactDirectory 'RecordProbe2.exe') -ArgumentList 'RecordProbe2 restarted' -PassThru
    Wait-For { $probes[1].Refresh(); $probes[1].MainWindowHandle -ne [IntPtr]::Zero } | Out-Null
    Focus-Probe 1; Wait-For { (Record-Ticks 2) -gt $savedB } 6000 | Out-Null
    Check ((Record-Ticks 2) -gt $savedB) 'Restarted target program matches its persisted executable'
    [RecordNative]::SetForegroundWindow([IntPtr]$main.Current.NativeWindowHandle) | Out-Null
    Add-Record 3 'Work C'; Add-Record 4 'Work D'; Add-Record 5 'Work E'
    Check (-not (Control 'AddRecordButton').Current.IsEnabled) 'Five programs disables further registration'
    Screenshot 'record-five-programs'
    Stop-App
    # Seed boundary times into the same persisted format, then run the real EXE.
    foreach ($case in @(@{ ticks = 864000000000L; badge = '1d'; image = 'record-day' }, @{ ticks = 6048000000000L; badge = '1w'; image = 'record-week' }, @{ ticks = 6948610000000L; badge = '1w 1d'; image = 'record-week-day' })) {
        $text = Get-Content -Raw $ini
        $text = [regex]::Replace($text, '(?m)^ElapsedTicks=\d+', ('ElapsedTicks=' + $case.ticks))
        [IO.File]::WriteAllText($ini, $text, [Text.UTF8Encoding]::new($false))
        Start-App; Invoke 'RecordTab'; Invoke 'RecordChip_0'
        Check ((Control 'RecordDays').Current.Name -eq $case.badge) "Actual EXE shows $($case.badge) at its rollover boundary"
        Check ((Display-Time) -eq $(if ($case.badge -eq '1w 1d') { '01:01:01' } else { '00:00:00' })) 'Record clock wraps days without overflowing hours'
        Screenshot $case.image; Stop-App
    }
    Write-Output "$($results.Count) real Windows Record checks passed."
} catch {
    if ($null -ne $main) { try { Screenshot 'record-failure' } catch {} }
    throw
} finally {
    Stop-App
    foreach ($probe in $probes) { if (-not $probe.HasExited) { $probe.Kill() }; $probe.Dispose() }
    $results | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 (Join-Path $ArtifactDirectory 'results.json')
}
