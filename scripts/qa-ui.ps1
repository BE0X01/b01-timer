param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [string]$ArtifactDirectory = (Join-Path $PSScriptRoot '../artifacts/qa-ui')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class QaNative {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr handle);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
}
'@

$ExePath = (Resolve-Path $ExePath).Path
New-Item -ItemType Directory -Force -Path $ArtifactDirectory | Out-Null
$ArtifactDirectory = (Resolve-Path $ArtifactDirectory).Path
$script:Results = [System.Collections.Generic.List[object]]::new()
$script:Process = $null
$script:Main = $null
$script:SettingsDirectory = Join-Path $ArtifactDirectory 'settings'

function Wait-Result([scriptblock]$Probe, [int]$TimeoutMilliseconds = 10000) {
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    do {
        $value = & $Probe
        if ($null -ne $value -and $value -ne $false) { return $value }
        Start-Sleep -Milliseconds 100
    } while ($watch.ElapsedMilliseconds -lt $TimeoutMilliseconds)
    throw "Timed out after ${TimeoutMilliseconds}ms."
}

function Find-Element($Parent, [string]$Id, [string]$Name = '') {
    $property = [System.Windows.Automation.AutomationElement]::AutomationIdProperty
    $value = $Id
    if ($Name) { $property = [System.Windows.Automation.AutomationElement]::NameProperty; $value = $Name }
    $condition = [System.Windows.Automation.PropertyCondition]::new($property, $value)
    return $Parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Get-Control([string]$Id, $Window = $script:Main) {
    return Wait-Result { Find-Element $Window $Id }
}

function Find-Window([string]$Name = '') {
    $conditions = [System.Collections.Generic.List[System.Windows.Automation.Condition]]::new()
    $conditions.Add([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $script:Process.Id))
    $conditions.Add([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window))
    if ($Name) { $conditions.Add([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name)) }
    $condition = [System.Windows.Automation.AndCondition]::new($conditions.ToArray())
    return [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
}

function Start-App {
    $script:Process = Start-Process -FilePath $ExePath -ArgumentList @('--settings-dir', ('"' + $script:SettingsDirectory + '"')) -PassThru
    $script:Main = Wait-Result { Find-Window }
    [QaNative]::SetForegroundWindow([IntPtr]$script:Main.Current.NativeWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 250
}

function Stop-App {
    if ($null -eq $script:Process) { return }
    if (-not $script:Process.HasExited) {
        $script:Process.CloseMainWindow() | Out-Null
        if (-not $script:Process.WaitForExit(5000)) { $script:Process.Kill() }
    }
    $script:Process.Dispose()
    $script:Process = $null
}

function Invoke-Control($Control) {
    $pattern = $Control.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
    Start-Sleep -Milliseconds 120
}

function Press-Button([string]$Id, $Window = $script:Main) { Invoke-Control (Get-Control $Id $Window) }

function Click-Button([string]$Id, $Window = $script:Main) {
    $rect = (Get-Control $Id $Window).Current.BoundingRectangle
    [QaNative]::SetCursorPos([int]($rect.X + $rect.Width / 2), [int]($rect.Y + $rect.Height / 2)) | Out-Null
    [QaNative]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [QaNative]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 150
}

function Get-Value([string]$Id, $Window = $script:Main) {
    $pattern = (Get-Control $Id $Window).GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    return $pattern.Current.Value
}

function Enter-Field([string]$Id, [string]$Text, $Window = $script:Main, [string]$Commit = '{ENTER}') {
    [QaNative]::SetForegroundWindow([IntPtr]$Window.Current.NativeWindowHandle) | Out-Null
    (Get-Control $Id $Window).SetFocus()
    [System.Windows.Forms.SendKeys]::SendWait('^a')
    [System.Windows.Forms.SendKeys]::SendWait($Text + $Commit)
    Start-Sleep -Milliseconds 120
}

function Get-Time($Window = $script:Main, [string]$Prefix = '') {
    return "$(Get-Value ($Prefix + 'HoursInput') $Window):$(Get-Value ($Prefix + 'MinutesInput') $Window):$(Get-Value ($Prefix + 'SecondsInput') $Window)"
}

function Time-Seconds([string]$Time) {
    $parts = $Time.Split(':')
    return 3600 * [int]$parts[0] + 60 * [int]$parts[1] + [int]$parts[2]
}

function Check([bool]$Passed, [string]$Name, [string]$Detail = '') {
    $script:Results.Add([pscustomobject]@{ name = $Name; passed = $Passed; detail = $Detail })
    if (-not $Passed) { throw "FAIL $Name $Detail" }
    Write-Output "PASS $Name"
}

function Check-Time([string]$Expected, [string]$Name) {
    $actual = Get-Time
    Check ($actual -eq $Expected) $Name "expected=$Expected actual=$actual"
}

function Set-Time([string]$Digits) { Enter-Field 'SecondsInput' $Digits }

function Get-Presets {
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
    $all = $script:Main.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
    $presets = @()
    foreach ($control in $all) { if ($control.Current.AutomationId -match '^PresetChip_\d+$') { $presets += $control } }
    return $presets
}

function Preset-Named([string]$Name) {
    foreach ($preset in @(Get-Presets)) { if ($preset.Current.Name.EndsWith($Name)) { return $preset } }
    return $null
}

function Get-MenuItem([string]$Name) {
    return Wait-Result {
        [System.Windows.Automation.Condition[]]$parts = @(
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $script:Process.Id),
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name),
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::MenuItem)
        )
        $conditions = [System.Windows.Automation.AndCondition]::new($parts)
        [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $conditions)
    }
}

function Open-Context($Preset) {
    $Preset.SetFocus()
    [System.Windows.Forms.SendKeys]::SendWait('+{F10}')
    Get-MenuItem 'Edit' | Out-Null
}

function Capture-Window([string]$Name, $Window = $script:Main) {
    $rect = $Window.Current.BoundingRectangle
    if ($rect.Width -le 0 -or $rect.Height -le 0) { throw "Cannot capture an invisible window." }
    $bitmap = [System.Drawing.Bitmap]::new([int][Math]::Ceiling($rect.Width), [int][Math]::Ceiling($rect.Height))
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bitmap.Size)
        $bitmap.Save((Join-Path $ArtifactDirectory ($Name + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
}

try {
    Start-App
    Check (-not (Get-Control 'RecordTab').Current.IsEnabled) 'Record is disabled'
    Set-Time '000000'
    Check (-not (Get-Control 'StartPauseButton').Current.IsEnabled) 'Zero seconds cannot start'
    Enter-Field 'MinutesInput' '20' $script:Main ''
    Click-Button 'StartPauseButton'
    Check ((Get-Control 'StartPauseButton').Current.Name -eq 'Pause timer') 'Valid edit from zero starts through immediate mouse click'
    Press-Button 'ResetButton'
    Check-Time '00:20:00' 'Immediate Start commits edited duration as Reset baseline'
    Set-Time '000000'
    Enter-Field 'HoursInput' '40'
    Check-Time '40:00:00' 'Hours 40'
    Set-Time '000000'
    Enter-Field 'MinutesInput' '20'
    Check-Time '00:20:00' 'Minutes 20'
    Enter-Field 'MinutesInput' '8'
    Check-Time '00:08:00' 'Minutes 8 pads to 08'
    Enter-Field 'MinutesInput' '408'
    Check-Time '04:08:00' 'Minutes 408 carries to hours'
    Enter-Field 'SecondsInput' '123000'
    Check-Time '12:30:00' 'Seconds 123000 groups as HHMMSS'
    Set-Time '010203'
    Enter-Field 'SecondsInput' '90'
    Check-Time '01:03:30' 'Seconds overflow preserves higher units'
    Enter-Field 'SecondsInput' '1234'
    Check-Time '01:12:34' 'Four-digit seconds preserve hours'
    Enter-Field 'SecondsInput' '999999'
    Check-Time '01:12:34' 'Overflow rejected without truncation'
    Enter-Field 'MinutesInput' '45' $script:Main '{ESC}'
    Check-Time '01:12:34' 'Escape restores original time'
    Enter-Field 'SecondsInput' '999999' $script:Main ''
    Click-Button 'StartPauseButton'
    Check-Time '01:12:34' 'Invalid edit followed by actual Start click restores previous value'
    Start-Sleep -Milliseconds 1200
    Check-Time '01:12:34' 'Invalid edit followed by Start click cannot launch previous duration'

    Set-Time '000015'
    Press-Button 'StartPauseButton'
    Start-Sleep -Milliseconds 1300
    $running = Time-Seconds (Get-Time)
    Check ($running -lt 15 -and $running -gt 0) 'Countdown runs'
    Press-Button 'StartPauseButton'
    $paused = Get-Time
    Start-Sleep -Milliseconds 1200
    Check-Time $paused 'Pause freezes countdown'
    Press-Button 'StartPauseButton'
    Start-Sleep -Milliseconds 1300
    Check ((Time-Seconds (Get-Time)) -lt (Time-Seconds $paused)) 'Resume continues countdown'
    Press-Button 'ResetButton'
    Check-Time '00:00:15' 'Reset restores configured duration'
    Start-Sleep -Milliseconds 1200
    Check-Time '00:00:15' 'Reset leaves timer waiting'

    $presets = @(Get-Presets)
    Check ($presets.Count -gt 0) 'Initial presets are present'
    Invoke-Control $presets[0]
    $selected = Get-Time
    Start-Sleep -Milliseconds 1200
    Check-Time $selected 'Preset selection does not auto-start'
    Set-Time '000015'
    Press-Button 'StartPauseButton'
    Invoke-Control (@(Get-Presets)[0])
    $selected = Get-Time
    Start-Sleep -Milliseconds 1200
    Check-Time $selected 'Selecting preset while running stops and configures'
    Press-Button 'ResetButton'
    Check-Time $selected 'Preset becomes new Reset baseline'

    Set-Time '000015'
    Press-Button 'StartPauseButton'
    $beforeDialog = Time-Seconds (Get-Time)
    $beforeCount = @(Get-Presets).Count
    Press-Button 'AddPresetButton'
    $dialog = Wait-Result { Find-Window 'Add preset' }
    Start-Sleep -Milliseconds 1300
    Check ((Time-Seconds (Get-Time)) -lt $beforeDialog) 'Countdown continues with preset dialog open'
    Capture-Window 'add-dialog' $dialog
    Enter-Field 'PresetSecondsInput' '999999' $dialog ''
    Click-Button 'PresetSaveButton' $dialog
    Check ($null -ne (Find-Window 'Add preset')) 'Invalid preset followed by actual Save click keeps dialog open'
    Enter-Field 'PresetSecondsInput' '000017' $dialog '{TAB}'
    Press-Button 'PresetSaveButton' $dialog
    Wait-Result { Preset-Named '00:00:17' } | Out-Null
    Check (@(Get-Presets).Count -eq $beforeCount + 1) 'Adding preset creates exactly one chip'
    Press-Button 'ResetButton'
    Check-Time '00:00:15' 'Adding preset preserves timer Reset baseline'
    Invoke-Control (Preset-Named '00:00:17')
    Check-Time '00:00:17' 'New preset configures its duration'
    Open-Context (Preset-Named '00:00:17')
    Check ($null -ne (Get-MenuItem 'Edit') -and $null -ne (Get-MenuItem 'Remove')) 'Preset context exposes Edit and Remove'
    Invoke-Control (Get-MenuItem 'Edit')
    $dialog = Wait-Result { Find-Window 'Edit preset' }
    Enter-Field 'PresetSecondsInput' '000019' $dialog
    Check ($null -eq (Find-Window 'Edit preset')) 'Enter confirms valid preset edit'
    Wait-Result { Preset-Named '00:00:19' } | Out-Null
    Check (@(Get-Presets).Count -eq $beforeCount + 1) 'Edit replaces existing preset'
    Check-Time '00:00:17' 'Editing preset does not change active duration'

    $themeBefore = (Get-Control 'ThemeButton').Current.Name
    Capture-Window 'theme-initial'
    Press-Button 'ThemeButton'
    $themeAfter = (Get-Control 'ThemeButton').Current.Name
    Check ($themeAfter -ne $themeBefore) 'Theme toggle changes its accessible action'
    Capture-Window 'theme-toggled'
    Stop-App
    Start-App
    Check ((Get-Control 'ThemeButton').Current.Name -eq $themeAfter) 'Theme survives relaunch'
    Check ($null -ne (Preset-Named '00:00:19')) 'Edited preset survives relaunch'
    Open-Context (Preset-Named '00:00:19')
    Invoke-Control (Get-MenuItem 'Remove')
    Check (@(Get-Presets).Count -eq $beforeCount) 'Remove deletes only selected preset'

    Set-Time '000002'
    Press-Button 'StartPauseButton'
    Wait-Result { (Get-Time) -eq '00:00:00' } 6000 | Out-Null
    Start-Sleep -Milliseconds 500
    Check-Time '00:00:00' 'Completion remains at zero'
    Check ((Get-Control 'StatusText').Current.Name -match 'Time') 'Completion status is visible'
    Press-Button 'ResetButton'
    Check-Time '00:00:02' 'Reset after completion restores duration'
    Write-Output "$($script:Results.Count) independent UI checks passed."
} catch {
    $script:Results.Add([pscustomobject]@{ name = 'Unhandled test failure'; passed = $false; detail = $_.Exception.ToString() })
    if ($null -ne $script:Main) { try { Capture-Window 'failure' } catch { Write-Warning $_ } }
    throw
} finally {
    $script:Results | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 (Join-Path $ArtifactDirectory 'results.json')
    Stop-App
}
