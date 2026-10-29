<#
.SYNOPSIS
    Builds the self-contained Taskly.Dev MSI installer.

.DESCRIPTION
    1. Publishes TasklyDev.App as a self-contained win-x64 app (no .NET install needed by users).
    2. Regenerates the installer license text from the repository LICENSE file.
    3. Builds the WiX installer project into artifacts\TasklyDev-<Version>-win-x64.msi.
    4. Writes a SHA-256 checksum file next to the MSI.

.EXAMPLE
    .\installer\Build-Installer.ps1 -Version 1.0.0
#>
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0',

    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifacts = Join-Path $repoRoot 'artifacts'
$publishDir = Join-Path $artifacts 'publish'
$installerDir = Join-Path $PSScriptRoot 'TasklyDev.Installer'
$msiName = "TasklyDev-$Version-win-x64.msi"

function Invoke-DotNet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($args -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Write-Host "==> Publishing Taskly.Dev $Version (self-contained, win-x64)"
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

Invoke-DotNet publish (Join-Path $repoRoot 'src\TasklyDev.App\TasklyDev.App.csproj') `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDir `
    "-p:Version=$Version" `
    "-p:DebugType=None" `
    "-p:DebugSymbols=false" `
    "-p:SatelliteResourceLanguages=en"

Write-Host '==> Generating installer license text'
$licenseText = (Get-Content (Join-Path $repoRoot 'LICENSE') -Raw) -replace '\\', '\\\\' -replace '\{', '\{' -replace '\}', '\}'
$licenseBody = ($licenseText -split "\r?\n" | ForEach-Object { "$_\par" }) -join "`r`n"
$rtf = "{\rtf1\ansi\deff0{\fonttbl{\f0\fmodern Consolas;}}\f0\fs18`r`n$licenseBody`r`n}"
[System.IO.File]::WriteAllText((Join-Path $installerDir 'License.rtf'), $rtf, [System.Text.Encoding]::ASCII)

Write-Host '==> Building MSI'
Invoke-DotNet build (Join-Path $installerDir 'TasklyDev.Installer.wixproj') `
    --configuration $Configuration `
    "-p:Version=$Version" `
    "-p:AppPublishDir=$publishDir"

$msi = Get-ChildItem (Join-Path $installerDir 'bin') -Recurse -Filter $msiName | Select-Object -First 1
if (-not $msi) {
    throw "Expected installer '$msiName' was not produced."
}

Copy-Item $msi.FullName (Join-Path $artifacts $msiName) -Force
$hash = (Get-FileHash (Join-Path $artifacts $msiName) -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $msiName" | Set-Content (Join-Path $artifacts "$msiName.sha256") -Encoding ascii

Write-Host ''
Write-Host "Installer: $(Join-Path $artifacts $msiName)"
Write-Host "SHA-256:   $hash"
