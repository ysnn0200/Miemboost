param(
    [string]$Runtime = "win-x64",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishScript = Join-Path $PSScriptRoot "publish-windows.ps1"
$publishPath = Join-Path $repoRoot "artifacts\publish\Miemboost-$Runtime"
$packagePath = Join-Path $repoRoot "artifacts\packages\Miemboost-$Runtime.zip"

& $publishScript -Runtime $Runtime -SelfContained:$SelfContained

$packageDirectory = Split-Path -Parent $packagePath
New-Item -ItemType Directory -Force -Path $packageDirectory | Out-Null

if (Test-Path $packagePath) {
    Remove-Item -LiteralPath $packagePath -Force
}

Compress-Archive -Path (Join-Path $publishPath "*") -DestinationPath $packagePath

Write-Host "Packaged Miemboost to $packagePath"
