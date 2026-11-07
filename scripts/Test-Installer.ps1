$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location (Split-Path $PSScriptRoot -Parent)
$appProcess = $null
$installDir = Join-Path ([IO.Path]::GetTempPath()) 'Blackveil installer smoke'
$runPath = 'Software\Microsoft\Windows\CurrentVersion\Run'
try {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($runPath)
    try { if ($key -and $null -ne $key.GetValue('Blackveil')) { throw 'The test needs a profile without an existing Blackveil startup entry.' } }
    finally { if ($key) { $key.Dispose() } }
    [xml]$props = Get-Content Directory.Build.props
    $version = [string]$props.Project.PropertyGroup.Version
    $setup = (Resolve-Path "artifacts/release/Blackveil-Setup-$version.exe").Path
    $installedExe = Join-Path $installDir 'Blackveil.exe'
    $logPath = Join-Path (Get-Location).Path 'artifacts/installer-smoke.log'
    $setupProcess = Start-Process $setup -ArgumentList @('/CURRENTUSER', '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/TASKS=autostart', "/DIR=`"$installDir`"", "/LOG=`"$logPath`"") -Wait -PassThru
    if ($setupProcess.ExitCode -ne 0 -or !(Test-Path $installedExe)) { throw 'Installation failed.' }
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($runPath)
    try { if ($key.GetValue('Blackveil') -ne "`"$installedExe`" --autostart") { throw 'The startup entry does not match.' } }
    finally { $key.Dispose() }
    if ((Get-Item $installedExe).VersionInfo.FileVersion -ne "$version.0") { throw 'The file version does not match.' }
    $appProcess = Start-Process $installedExe -ArgumentList '--autostart' -PassThru
    if (!$appProcess.WaitForInputIdle(15000)) { throw 'The installed app did not start.' }
    if ($appProcess.HasExited) { throw 'Installierte App wurde unerwartet beendet.' }
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class BlackveilInstallerProbe {
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(IntPtr cls, string title);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wp, IntPtr lp);
}
'@
    $startupClock = [Diagnostics.Stopwatch]::StartNew()
    do {
        $controller = [BlackveilInstallerProbe]::FindWindow([IntPtr]::Zero, 'Blackveil.Controller')
        if ($controller -ne [IntPtr]::Zero -or $appProcess.HasExited) { break }
        Start-Sleep -Milliseconds 100
    } while ($startupClock.Elapsed.TotalSeconds -lt 15)
    if ($controller -eq [IntPtr]::Zero) { throw 'The tray controller is missing.' }
    $second = Start-Process $installedExe -PassThru
    if (!$second.WaitForExit(10000) -or $second.ExitCode -ne 0) { throw 'The mutex did not prevent a second instance.' }
    # Exercise the existing Windows session-end cleanup path; no app-only test command.
    [void][BlackveilInstallerProbe]::PostMessage($controller, 0x16, [IntPtr]1, [IntPtr]::Zero)
    if (!$appProcess.WaitForExit(10000) -or $appProcess.ExitCode -ne 0) { throw 'The app did not exit cleanly.' }
} catch {
    Get-Content "$env:LOCALAPPDATA/Blackveil/logs/*.log" -ErrorAction SilentlyContinue | Write-Host
    throw
} finally {
    if ($appProcess -and !$appProcess.HasExited) { Stop-Process -Id $appProcess.Id -Force }
    $uninstaller = Join-Path $installDir 'unins000.exe'
    if (Test-Path $uninstaller) {
        $uninstall = Start-Process $uninstaller -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -Wait -PassThru
        if ($uninstall.ExitCode -ne 0) { throw 'Uninstall failed.' }
    }
    Pop-Location
}
$key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($runPath)
try { if ($key -and $null -ne $key.GetValue('Blackveil')) { throw 'Uninstall left a startup entry behind.' } }
finally { if ($key) { $key.Dispose() } }
if (Test-Path (Join-Path $installDir 'Blackveil.exe')) { throw 'Uninstall left the app behind.' }
Write-Host 'PASS: per-user install, version, HKCU autostart, installed self-contained EXE, single instance, session-end cleanup and uninstall.'
