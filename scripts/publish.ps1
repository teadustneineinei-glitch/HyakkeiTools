# Publish HyakkeiTools as a single-file, framework-dependent exe into dist\.
# Requires .NET 10 runtime on the target machine (already present with the SDK).
# Usage: scripts\publish.ps1            -> dist\HyakkeiTools.exe
#        scripts\publish.ps1 -SelfContained  (bundle runtime, ~3x larger, runs without .NET installed)
# NOTE: keep this file ASCII-only (PowerShell 5.1 reads BOM-less files as ANSI).
param(
    [switch]$SelfContained,
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$dist = if ($OutDir) { $OutDir } else { Join-Path $root "dist" }
$project = Join-Path $root "src\Hyakkei.App\Hyakkei.App.csproj"

Get-Process HyakkeiTools -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

if (Test-Path $dist) {
    # keep user data (config + logs) across republishes
    Get-ChildItem $dist -Exclude config, logs | Remove-Item -Recurse -Force
}

$sc = if ($SelfContained) { "true" } else { "false" }
dotnet publish $project -c Release -r win-x64 -p:SelfContained=$sc `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=$sc `
    -p:SatelliteResourceLanguages=zh-Hans `
    -p:DebugType=none `
    -o $dist

if ($LASTEXITCODE -ne 0) { throw "publish failed" }

$exe = Join-Path $dist "HyakkeiTools.exe"
$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "Published: $exe ($size MB)"
