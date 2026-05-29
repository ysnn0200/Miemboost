param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $repoRoot "..\.dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    $dotnet = "dotnet"
}

$outputPath = Join-Path $repoRoot "artifacts\publish\Miemboost-$Runtime"

if (Test-Path $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

$publishArgs = @(
    "publish",
    (Join-Path $repoRoot "src\Miemboost.App\Miemboost.App.csproj"),
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--output", $outputPath,
    "-p:PublishSingleFile=false",
    "-p:IncludeNativeLibrariesForSelfExtract=true"
)

if ($SelfContained) {
    $publishArgs += "--self-contained"
    $publishArgs += "true"
} else {
    $publishArgs += "--self-contained"
    $publishArgs += "false"
}

& $dotnet @publishArgs

Write-Host "Published Miemboost to $outputPath"
