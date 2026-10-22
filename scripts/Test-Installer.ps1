$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location (Split-Path $PSScriptRoot -Parent)
$appProcess = $null
$installDir = Join-Path ([IO.Path]::GetTempPath()) 'Blackveil installer smoke'
$runPath = 'Software\Microsoft\Windows\CurrentVersion\Run'
try {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($runPath)
    try { if ($key -and $null -ne $key.GetValue('Blackveil')) { throw 'Test benötigt ein Benutzerprofil ohne bestehenden Blackveil-Autostart.' } }
    finally { if ($key) { $key.Dispose() } }
    [xml]$props = Get-Content Directory.Build.props
    $version = [string]$props.Project.PropertyGroup.Version
    $setup = (Resolve-Path "artifacts/release/Blackveil-Setup-$version.exe").Path
    $installedExe = Join-Path $installDir 'Blackveil.exe'
    $logPath = Join-Path (Get-Location).Path 'artifacts/installer-smoke.log'
    $setupProcess = Start-Process $setup -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/TASKS=autostart', "/DIR=`"$installDir`"", "/LOG=`"$logPath`"") -Wait -PassThru
    if ($setupProcess.ExitCode -ne 0 -or !(Test-Path $installedExe)) { throw 'Installation fehlgeschlagen.' }
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($runPath)
    try { if ($key.GetValue('Blackveil') -ne "`"$installedExe`" --autostart") { throw 'Autostart-Wert stimmt nicht überein.' } }
    finally { $key.Dispose() }
    if ((Get-Item $installedExe).VersionInfo.FileVersion -ne "$version.0") { throw 'Dateiversion stimmt nicht überein.' }
    $appProcess = Start-Process $installedExe -ArgumentList '--autostart' -PassThru
    if (!$appProcess.WaitForInputIdle(15000)) { throw 'Installierte App startet nicht.' }
    if ($appProcess.HasExited) { throw 'Installierte App wurde unerwartet beendet.' }
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class BlackveilInstallerProbe {
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wp, IntPtr lp);
}
'@
    $controller = [BlackveilInstallerProbe]::FindWindow($null, 'Blackveil.Controller')
    if ($controller -eq [IntPtr]::Zero) { throw 'Tray-Controller fehlt.' }
    $second = Start-Process $installedExe -PassThru
    if (!$second.WaitForExit(10000) -or $second.ExitCode -ne 0) { throw 'Mutex verhindert zweite Instanz nicht.' }
    # Exercise the existing Windows session-end cleanup path; no app-only test command.
    [void][BlackveilInstallerProbe]::PostMessage($controller, 0x16, [IntPtr]1, [IntPtr]::Zero)
    if (!$appProcess.WaitForExit(10000) -or $appProcess.ExitCode -ne 0) { throw 'Sauberes Beenden fehlgeschlagen.' }
} finally {
    if ($appProcess -and !$appProcess.HasExited) { Stop-Process -Id $appProcess.Id -Force }
    $uninstaller = Join-Path $installDir 'unins000.exe'
    if (Test-Path $uninstaller) {
        $uninstall = Start-Process $uninstaller -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -Wait -PassThru
        if ($uninstall.ExitCode -ne 0) { throw 'Deinstallation fehlgeschlagen.' }
    }
    Pop-Location
}
$key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($runPath)
try { if ($key -and $null -ne $key.GetValue('Blackveil')) { throw 'Deinstallation hinterlässt Autostart.' } }
finally { if ($key) { $key.Dispose() } }
if (Test-Path (Join-Path $installDir 'Blackveil.exe')) { throw 'Deinstallation hinterlässt die App.' }
Write-Host 'PASS: per-user install, version, HKCU autostart, installed self-contained EXE, single instance, session-end cleanup and uninstall.'
