<#
    Builds the mod and packs it into a Thunderstore-ready zip.
    Run from the repository root:
        powershell -ExecutionPolicy Bypass -File tools\pack.ps1
    Output: dist\NpcSpawner-<version>.zip
#>
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    Write-Host "=== Building ===" -ForegroundColor Cyan
    dotnet build -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }

    $ver = (Get-Content "manifest.json" -Raw | ConvertFrom-Json).version_number
    Write-Host ("Package version: " + $ver) -ForegroundColor Cyan

    $pkg = Join-Path $env:TEMP ("npcspawner_pkg_" + $ver)
    Remove-Item -Recurse -Force $pkg -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path (Join-Path $pkg "plugins") | Out-Null

    # the mod dll goes into plugins/ (mod managers install it to BepInEx/plugins)
    Copy-Item ("bin\" + $Configuration + "\NpcSpawner.dll") (Join-Path $pkg "plugins\NpcSpawner.dll") -Force

    # required package files go to the archive root
    Copy-Item "manifest.json", "icon.png", "README.md", "CHANGELOG.md" $pkg -Force

    New-Item -ItemType Directory -Force -Path "dist" | Out-Null
    $out = Join-Path "dist" ("NpcSpawner-" + $ver + ".zip")
    Remove-Item -Force $out -ErrorAction SilentlyContinue
    Compress-Archive -Path (Join-Path $pkg "*") -DestinationPath $out -Force

    Write-Host ""
    Write-Host ("Done: " + $out) -ForegroundColor Green
    Write-Host "Upload this archive to https://thunderstore.io/c/v-rising/create/" -ForegroundColor Green
}
finally {
    Pop-Location
}
