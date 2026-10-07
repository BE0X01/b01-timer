param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [string]$ArtifactDirectory = (Join-Path $PSScriptRoot '../artifacts/qa-ui'),
    [string]$ExpectedFileVersion = ''
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
if (-not $ExpectedFileVersion) {
    [xml]$project = Get-Content -Raw (Join-Path $PSScriptRoot '../src/B01Timer/B01Timer.csproj')
    $ExpectedFileVersion = [string]$project.Project.PropertyGroup.FileVersion
}
$script:Results = [System.Collections.Generic.List[object]]::new()
$script:Process = $null
$script:Main = $null
$script:SettingsDirectory = Join-Path $ArtifactDirectory 'settings'
$script:LegacyJsonPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'B01Timer/settings.json'
$script:LegacyTouched = $false
$script:LegacyHadFile = $false
$script:LegacyOriginalBytes = $null
$script:LegacyDirectoryExisted = $true

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
    # WPF owned modal windows can be nested under the owner in the UIA tree.
    return [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Start-App([switch]$Portable) {
    if ($Portable) {
        $script:Process = Start-Process -FilePath $ExePath -WorkingDirectory $ArtifactDirectory -PassThru
    } else {
        $script:Process = Start-Process -FilePath $ExePath -ArgumentList @('--settings-dir', ('"' + $script:SettingsDirectory + '"')) -WorkingDirectory $ArtifactDirectory -PassThru
    }
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

function Get-Status { return (Get-Control 'StatusText').Current.Name }

function Wait-Status([string]$Expected) {
    try { Wait-Result { (Get-Status) -eq $Expected } 6000 | Out-Null }
    catch { throw "Expected status=$Expected; displayed=$(Get-Time); status=$(Get-Status). $($_.Exception.Message)" }
}

function Wait-Time-Decrease([int]$Before) {
    try {
        return Wait-Result {
            $remaining = Time-Seconds (Get-Time)
            if ($remaining -lt $Before -and $remaining -gt 0) { return $remaining }
            return $false
        } 6000
    } catch { throw "Countdown did not decrease from $Before within 6 seconds; displayed=$(Get-Time); status=$(Get-Status). $($_.Exception.Message)" }
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
    $overrideIni = Join-Path $script:SettingsDirectory 'B01Timer.ini'
    Wait-Result { Test-Path $overrideIni } | Out-Null
    Check (Test-Path $overrideIni) 'Settings override creates B01Timer.ini in the requested test folder'
    Check (-not (Get-Control 'RecordTab').Current.IsEnabled) 'Record is disabled'
    Set-Time '000000'
    Check (-not (Get-Control 'StartPauseButton').Current.IsEnabled) 'Zero seconds cannot start'
    Enter-Field 'MinutesInput' '20' $script:Main ''
    Click-Button 'StartPauseButton'
    Wait-Status 'Running'
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
    $transform = $script:Main.GetCurrentPattern([System.Windows.Automation.TransformPattern]::Pattern)
    $transform.Resize(460, 340)
    Start-Sleep -Milliseconds 250
    Capture-Window 'min-size-validation'
    $transform.Resize(520, 360)
    Start-Sleep -Milliseconds 250

    Set-Time '000015'
    Press-Button 'StartPauseButton'
    Wait-Status 'Running'
    $running = Wait-Time-Decrease 15
    Check ($running -lt 15 -and $running -gt 0) 'Countdown runs' "displayed=$(Get-Time); status=$(Get-Status)"
    Press-Button 'StartPauseButton'
    Wait-Status 'Paused'
    $paused = Get-Time
    Start-Sleep -Milliseconds 1200
    Check-Time $paused 'Pause freezes countdown'
    Press-Button 'StartPauseButton'
    Wait-Status 'Running'
    $resumed = Wait-Time-Decrease (Time-Seconds $paused)
    Check ($resumed -lt (Time-Seconds $paused)) 'Resume continues countdown' "displayed=$(Get-Time); status=$(Get-Status)"
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
    Wait-Status 'Running'
    $beforeDialog = Time-Seconds (Get-Time)
    $beforeCount = @(Get-Presets).Count
    Press-Button 'AddPresetButton'
    $dialog = Wait-Result { Find-Window 'Add preset' }
    $duringDialog = Wait-Time-Decrease $beforeDialog
    Check ($duringDialog -lt $beforeDialog) 'Countdown continues with preset dialog open' "displayed=$(Get-Time); status=$(Get-Status)"
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
    Capture-Window 'preset-menu'
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
    Press-Button 'AddPresetButton'
    $dialog = Wait-Result { Find-Window 'Add preset' }
    Capture-Window 'alternate-theme-dialog' $dialog
    Press-Button 'PresetCancelButton' $dialog
    Check (@(Get-Presets).Count -eq $beforeCount + 1) 'Canceling Add preserves existing presets'
    Press-Button 'AddPresetButton'
    $dialog = Wait-Result { Find-Window 'Add preset' }
    Enter-Field 'PresetSecondsInput' '000055' $dialog '{ESC}'
    Check ($null -eq (Find-Window 'Add preset')) 'Escape from an edited modal field cancels the dialog'
    Check (@(Get-Presets).Count -eq $beforeCount + 1) 'Escape cancellation preserves existing presets'
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

    # Test the real default storage location by copying only the standalone EXE,
    # launching without --settings-dir, and using a different working directory.
    Stop-App
    $portableDirectory = Join-Path $ArtifactDirectory 'portable'
    New-Item -ItemType Directory -Force -Path $portableDirectory | Out-Null
    $portableExe = Join-Path $portableDirectory 'B01Timer.exe'
    Copy-Item -Force -Path $ExePath -Destination $portableExe
    $ExePath = $portableExe
    $portableIni = Join-Path $portableDirectory 'B01Timer.ini'
    if (Test-Path $portableIni) { Remove-Item -Force $portableIni }
    $script:LegacyDirectoryExisted = Test-Path (Split-Path $script:LegacyJsonPath -Parent)
    $script:LegacyHadFile = Test-Path $script:LegacyJsonPath
    if ($script:LegacyHadFile) { $script:LegacyOriginalBytes = [IO.File]::ReadAllBytes($script:LegacyJsonPath) }
    New-Item -ItemType Directory -Force -Path (Split-Path $script:LegacyJsonPath -Parent) | Out-Null
    $legacyJson = '{"Theme":"dark","LastSeconds":3723,"Favorites":[{"Id":"qa-legacy","Seconds":45}]}'
    $script:LegacyTouched = $true
    [IO.File]::WriteAllText($script:LegacyJsonPath, $legacyJson)
    Start-App -Portable
    Wait-Result { Test-Path $portableIni } | Out-Null
    Check (Test-Path $portableIni) 'Default first launch creates INI next to the copied EXE'
    Check (-not (Test-Path (Join-Path $ArtifactDirectory 'B01Timer.ini'))) 'Default INI uses the EXE folder rather than the working directory'
    Check-Time '01:02:03' 'Default first launch migrates configured time from AppData JSON'
    Check ($null -ne (Preset-Named '00:00:45') -and @(Get-Presets).Count -eq 1) 'Default first launch migrates legacy preset'
    Check ([IO.File]::ReadAllText($script:LegacyJsonPath) -eq $legacyJson) 'Migration preserves the legacy JSON file unchanged'
    $actualFileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($ExePath).FileVersion
    Check ($actualFileVersion -eq $ExpectedFileVersion) 'Windows executable declares the configured release version' "expected=$ExpectedFileVersion actual=$actualFileVersion"
    Set-Time '040506'
    Press-Button 'ThemeButton'
    $portableTheme = (Get-Control 'ThemeButton').Current.Name
    Press-Button 'AddPresetButton'
    $dialog = Wait-Result { Find-Window 'Add preset' }
    Enter-Field 'PresetSecondsInput' '000037' $dialog '{TAB}'
    Press-Button 'PresetSaveButton' $dialog
    Wait-Result { Preset-Named '00:00:37' } | Out-Null
    $iniText = [IO.File]::ReadAllText($portableIni)
    Check ($iniText -match '(?m)^\[Settings\]' -and $iniText -match '(?m)^\[Presets\]') 'Portable settings are readable INI sections'
    Check ($iniText -match '(?m)^LastTime\s*=\s*04:05:06\s*$') 'Portable INI stores the configured time'
    Check ($iniText -match '(?m)^Theme\s*=\s*light\s*$') 'Portable INI stores the selected theme'
    Check ($iniText -match '(?m)^Count\s*=\s*2\s*$' -and $iniText -match '(?m)^Time\s*=\s*00:00:37\s*$') 'Portable INI stores favorites'
    Capture-Window 'portable-ini-main'
    Stop-App
    [IO.File]::WriteAllText($script:LegacyJsonPath, '{"Theme":"dark","LastSeconds":0,"Favorites":[]}')
    Start-App -Portable
    Check-Time '04:05:06' 'Portable configured time survives relaunch and ignores changed legacy JSON'
    Check ((Get-Control 'ThemeButton').Current.Name -eq $portableTheme) 'Portable theme survives relaunch'
    Check ($null -ne (Preset-Named '00:00:37') -and $null -ne (Preset-Named '00:00:45') -and @(Get-Presets).Count -eq 2) 'Portable favorites survive relaunch without a second migration'
    Stop-App
    [IO.File]::WriteAllText($portableIni, "[Settings]`r`nTheme=dark`r`nLastTime=00:00:08`r`n[Presets]`r`nCount=0`r`n")
    Start-App -Portable
    Check (@(Get-Presets).Count -eq 0) 'An INI with Count=0 keeps favorites empty'
    Check-Time '00:00:08' 'A readable edited INI loads its configured time'
    Stop-App
    [IO.File]::WriteAllText($portableIni, "[Settings]`r`nTheme=unknown`r`nLastTime=invalid`r`n[Presets]`r`nCount=1`r`n[Preset1]`r`nId=broken`r`nTime=invalid`r`n")
    Start-App -Portable
    Check-Time '00:00:00' 'Invalid INI time recovers safely on actual Windows launch'
    Check ((Get-Control 'ThemeButton').Current.Name -eq 'Switch to light theme') 'Invalid INI theme recovers to Dark'
    Check (@(Get-Presets).Count -eq 0) 'Invalid INI preset is discarded without crashing'
    # Capture the actual EXE's filled Pause and its hover/pressed appearance.
    Set-Time '000015'
    Press-Button 'StartPauseButton'
    Wait-Status 'Running'
    $startButton = Get-Control 'StartPauseButton'
    $startButton.SetFocus()
    $mainRect = $script:Main.Current.BoundingRectangle
    [QaNative]::SetCursorPos([int]($mainRect.X + 20), [int]($mainRect.Bottom - 25)) | Out-Null
    Start-Sleep -Milliseconds 150
    Capture-Window 'running-pause-idle'
    $buttonRect = $startButton.Current.BoundingRectangle
    [QaNative]::SetCursorPos([int]($buttonRect.X + $buttonRect.Width / 2), [int]($buttonRect.Y + $buttonRect.Height / 2)) | Out-Null
    Start-Sleep -Milliseconds 120
    Capture-Window 'button-hover'
    [QaNative]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    try { Start-Sleep -Milliseconds 120; Capture-Window 'button-pressed' }
    finally { [QaNative]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero) }
    Wait-Status 'Paused'
    [QaNative]::SetCursorPos([int]($mainRect.X + 20), [int]($mainRect.Bottom - 25)) | Out-Null
    Start-Sleep -Milliseconds 120
    Capture-Window 'paused-no-stroke'
    Press-Button 'ResetButton'
    Write-Output "$($script:Results.Count) independent UI checks passed."
} catch {
    $script:Results.Add([pscustomobject]@{ name = 'Unhandled test failure'; passed = $false; detail = $_.Exception.ToString() })
    if ($null -ne $script:Process) {
        try {
            $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $script:Process.Id)
            $elements = [System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
            $tree = foreach ($element in $elements) {
                [pscustomobject]@{ name = $element.Current.Name; id = $element.Current.AutomationId; type = $element.Current.ControlType.ProgrammaticName; enabled = $element.Current.IsEnabled }
            }
            $tree | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 (Join-Path $ArtifactDirectory 'uia-tree.json')
        } catch { Write-Warning $_ }
    }
    if ($null -ne $script:Main) { try { Capture-Window 'failure' } catch { Write-Warning $_ } }
    throw
} finally {
    $script:Results | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 (Join-Path $ArtifactDirectory 'results.json')
    Stop-App
    if ($script:LegacyTouched) {
        if ($script:LegacyHadFile) { [IO.File]::WriteAllBytes($script:LegacyJsonPath, $script:LegacyOriginalBytes) }
        else { Remove-Item -Force -ErrorAction SilentlyContinue $script:LegacyJsonPath }
        if (-not $script:LegacyDirectoryExisted) {
            $legacyDirectory = Split-Path $script:LegacyJsonPath -Parent
            if ((Get-ChildItem -Force $legacyDirectory | Measure-Object).Count -eq 0) { Remove-Item -Force $legacyDirectory }
        }
    }
}
