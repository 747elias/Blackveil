$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location (Split-Path $PSScriptRoot -Parent)
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('Blackveil updater ' + [guid]::NewGuid().ToString('N'))
$dataRoot = Join-Path $env:LOCALAPPDATA 'Blackveil'
$installed = $null
$allowCleanup = $false
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class BlackveilUpdateProbe {
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(IntPtr cls, string title);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wp, IntPtr lp);
}
'@
function Wait-Until([scriptblock]$Condition, [string]$Name, [int]$Seconds = 30) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while (!( & $Condition )) {
        if ($clock.Elapsed.TotalSeconds -gt $Seconds) { throw "Timeout: $Name" }
        Start-Sleep -Milliseconds 100
    }
}
function Stop-Blackveil {
    $controller = [BlackveilUpdateProbe]::FindWindow([IntPtr]::Zero, 'Blackveil.Controller')
    if ($controller -ne [IntPtr]::Zero) { [void][BlackveilUpdateProbe]::PostMessage($controller, 0x16, [IntPtr]1, [IntPtr]::Zero) }
    Wait-Until { [BlackveilUpdateProbe]::FindWindow([IntPtr]::Zero, 'Blackveil.Controller') -eq [IntPtr]::Zero } 'Blackveil exit'
    Start-Sleep -Milliseconds 500
}
function Exercise-Update([string]$Target, [string]$Payload, [bool]$IsInstalled, [bool]$BadHash = $false) {
    $stage = Join-Path $dataRoot ('updates/' + [guid]::NewGuid().ToString('N'))
    New-Item $stage -ItemType Directory -Force | Out-Null
    $package = Join-Path $stage 'package.exe'
    Copy-Item $Payload $package
    $helper = Join-Path $stage 'Blackveil-Updater.exe'
    Copy-Item artifacts/publish/Blackveil.exe $helper
    $parent = Start-Process $Target -PassThru
    Wait-Until { [BlackveilUpdateProbe]::FindWindow([IntPtr]::Zero, 'Blackveil.Controller') -ne [IntPtr]::Zero } 'parent startup'
    $hash = (Get-FileHash $package -Algorithm SHA256).Hash
    if ($BadHash) { $hash = '0' * 64 }
    $plan = @{ ParentId=$parent.Id; ParentStartTicks=$parent.StartTime.ToUniversalTime().Ticks; Target=$Target; Package=$package;
        Hash=$hash; Installed=$IsInstalled; Version=$version; Autostart=$IsInstalled }
    $planPath = Join-Path $stage 'plan.json'
    $plan | ConvertTo-Json | Set-Content $planPath -Encoding utf8
    $worker = Start-Process $helper -ArgumentList @('--apply-update', "`"$planPath`"") -PassThru
    $controller = [BlackveilUpdateProbe]::FindWindow([IntPtr]::Zero, 'Blackveil.Controller')
    [void][BlackveilUpdateProbe]::PostMessage($controller, 0x16, [IntPtr]1, [IntPtr]::Zero)
    if (!$parent.WaitForExit(15000)) { throw 'Parent did not exit.' }
    if (!$worker.WaitForExit(180000)) { throw 'Update helper timed out.' }
    if ((!$BadHash -and $worker.ExitCode -ne 0) -or ($BadHash -and $worker.ExitCode -eq 0)) { throw "Unexpected helper result: $($worker.ExitCode)" }
    Wait-Until { [BlackveilUpdateProbe]::FindWindow([IntPtr]::Zero, 'Blackveil.Controller') -ne [IntPtr]::Zero } 'updated app restart'
    if ((Get-Item $Target).VersionInfo.FileVersion -ne "$version.0") { throw 'Target version mismatch.' }
    Wait-Until { [BlackveilUpdateProbe]::FindWindow([IntPtr]::Zero, "Neu in Blackveil $version") -ne [IntPtr]::Zero } 'post-update changes window'
    if ((Get-Content "$dataRoot/settings.json" -Raw | ConvertFrom-Json).Key -ne 75) { throw 'Shortcut settings were lost.' }
    if ((Get-Content "$dataRoot/update-settings.json" -Raw | ConvertFrom-Json).AutoCheck) { throw 'Update settings were lost.' }
    Stop-Blackveil
}
try {
    if (Test-Path "$dataRoot/settings.json") { throw 'Use a clean Windows test profile.' }
    if ((Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name Blackveil -ErrorAction SilentlyContinue)) { throw 'Existing Blackveil autostart must not be changed by tests.' }
    $allowCleanup = $true
    New-Item $testRoot,$dataRoot -ItemType Directory -Force | Out-Null
    [xml]$props = Get-Content Directory.Build.props
    $version = [string]$props.Project.PropertyGroup.Version
    $oldRelease = 'https://github.com/747elias/Blackveil/releases/download/v2.0.1'
    Invoke-WebRequest "$oldRelease/SHA256SUMS.txt" -OutFile "$testRoot/SHA256SUMS.txt"
    foreach ($name in @('Blackveil.exe', 'Blackveil-Setup-2.0.1.exe')) {
        Invoke-WebRequest "$oldRelease/$name" -OutFile "$testRoot/$name"
        $line = Get-Content "$testRoot/SHA256SUMS.txt" | Where-Object { $_.EndsWith("  $name") }
        if (!$line -or (Get-FileHash "$testRoot/$name" -Algorithm SHA256).Hash -ne $line.Substring(0,64)) { throw 'Old fixture checksum failed.' }
    }
    @{ AutoCheck=$false; AutoInstall=$false; CheckWhen=0; InstallWhen=0; CheckMinute=1080; InstallMinute=1200 } | ConvertTo-Json | Set-Content "$dataRoot/update-settings.json"
    @{ LastSeenVersion='2.0.1' } | ConvertTo-Json | Set-Content "$dataRoot/update-state.json"
    @{ Key=75; Modifiers=4 } | ConvertTo-Json | Set-Content "$dataRoot/settings.json"
    $portable = Join-Path $testRoot 'portable/Blackveil.exe'
    New-Item (Split-Path $portable) -ItemType Directory | Out-Null
    Copy-Item "$testRoot/Blackveil.exe" $portable
    Exercise-Update $portable (Resolve-Path artifacts/publish/Blackveil.exe).Path $false
    $before = (Get-FileHash $portable -Algorithm SHA256).Hash
    Exercise-Update $portable (Resolve-Path artifacts/publish/Blackveil.exe).Path $false $true
    if ((Get-FileHash $portable -Algorithm SHA256).Hash -ne $before) { throw 'Checksum failure modified target.' }
    $installed = Join-Path $testRoot 'installed'
    $setup = Start-Process "$testRoot/Blackveil-Setup-2.0.1.exe" -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/TASKS=autostart', "/DIR=`"$installed`"") -Wait -PassThru
    if ($setup.ExitCode -ne 0) { throw 'Old install fixture failed.' }
    dotnet run --project tests/Blackveil.Windows.Tests -c Release --no-build -- --assert-installed "$installed/Blackveil.exe"
    if ($LASTEXITCODE) { throw 'Installed-mode detection failed.' }
    Exercise-Update "$installed/Blackveil.exe" (Resolve-Path "artifacts/release/Blackveil-Setup-$version.exe").Path $true
    $run = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name Blackveil).Blackveil
    if ($run -ne "`"$installed\Blackveil.exe`" --autostart") { throw 'Installer update changed autostart.' }
    Write-Host 'PASS: real 2.0.1 -> current portable and installed upgrades, silent setup, restart, changes window, settings/autostart preservation, checksum failure recovery.'
} catch {
    Get-Content "$dataRoot/logs/*.log" -ErrorAction SilentlyContinue | Write-Host
    throw
} finally {
    if ($allowCleanup) {
        Stop-Blackveil
        if ($installed -and (Test-Path "$installed/unins000.exe")) {
            $uninstall = Start-Process "$installed/unins000.exe" -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -Wait -PassThru
            if ($uninstall.ExitCode -ne 0) { throw 'Cleanup uninstall failed.' }
        }
        if (Test-Path $testRoot) { Remove-Item $testRoot -Recurse -Force }
        if (Test-Path $dataRoot) { Remove-Item $dataRoot -Recurse -Force }
    }
    Pop-Location
}
