param([string]$Configuration = 'Release', [string]$IsccPath = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    [xml]$props = Get-Content Directory.Build.props
    $version = [string]$props.Project.PropertyGroup.Version
    if ($env:GITHUB_REF -like 'refs/tags/*' -and $env:GITHUB_REF -ne "refs/tags/v$version") { throw 'Tag und zentrale Version stimmen nicht überein.' }
    $root = (Get-Location).Path
    $publish = Join-Path $root 'artifacts/publish'
    $release = Join-Path $root 'artifacts/release'
    if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
    if (Test-Path $release) { Remove-Item $release -Recurse -Force }
    New-Item $publish,$release -ItemType Directory -Force | Out-Null
    dotnet publish src/Blackveil/Blackveil.csproj -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -o $publish
    if ($LASTEXITCODE) { throw 'dotnet publish fehlgeschlagen' }
    Copy-Item "$publish/Blackveil.exe" $release
    Copy-Item README.md,LICENSE "$publish/"
    Compress-Archive -Path "$publish/*" -DestinationPath "$release/Blackveil-$version-win-x64.zip"
    if (!$IsccPath) {
        $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
        if ($command) { $IsccPath = $command.Source }
    }
    if (!$IsccPath) { $IsccPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" }
    if (!(Test-Path $IsccPath)) { throw 'Inno Setup 6 fehlt; -IsccPath angeben.' }
    & $IsccPath "/DAppVersion=$version" "/DSourceDir=$publish" "/DOutputDir=$release" installer/Blackveil.iss
    if ($LASTEXITCODE) { throw 'Inno Setup fehlgeschlagen' }
    $expected = @('Blackveil.exe', "Blackveil-Setup-$version.exe", "Blackveil-$version-win-x64.zip")
    $lines = foreach ($name in $expected) {
        $hash = (Get-FileHash (Join-Path $release $name) -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $name"
    }
    [IO.File]::WriteAllLines((Join-Path $release 'SHA256SUMS.txt'), [string[]]$lines, [Text.UTF8Encoding]::new($false))
    $changelog = Get-Content CHANGELOG.md -Raw
    $escaped = [regex]::Escape($version)
    $match = [regex]::Match($changelog, "(?ms)^## \[$escaped\][^\r\n]*\r?\n(.*?)(?=^## |\z)")
    if (!$match.Success) { throw "CHANGELOG enthält keinen Abschnitt für $version" }
    Set-Content artifacts/release-notes.md -Value $match.Groups[1].Value.Trim() -Encoding utf8
} finally { Pop-Location }
