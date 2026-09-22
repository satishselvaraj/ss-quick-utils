# ============================================================
# OAuthTokenGenExt - Build & Install Script
# ============================================================
# Builds the Visual Studio extension VSIX and optionally
# installs it. No admin access required for per-user install.
#
# Usage:
#   .\build.ps1                    # Build and install
#   .\build.ps1 -BuildOnly         # Build only
#   .\build.ps1 -Configuration Release  # Release build
# ============================================================

param(
    [switch]$BuildOnly,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$extensionDir = $PSScriptRoot

Write-Host ""
Write-Host "  =============================================" -ForegroundColor Cyan
Write-Host "  OAuthTokenGenExt - Visual Studio Extension" -ForegroundColor Cyan
Write-Host "  Build & Install" -ForegroundColor Cyan
Write-Host "  =============================================" -ForegroundColor Cyan
Write-Host ""

# Check for dotnet
if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    Write-Host "  [ERROR] .NET SDK is required but not found." -ForegroundColor Red
    exit 1
}

Push-Location $extensionDir

try {
    # Restore
    Write-Host "  [1/3] Restoring packages..." -ForegroundColor White
    dotnet restore 2>&1 | ForEach-Object { Write-Host "        $_" -ForegroundColor Gray }
    if ($LASTEXITCODE -ne 0) { throw "Restore failed" }
    Write-Host "  [OK] Packages restored" -ForegroundColor Green

    # Build
    Write-Host "  [2/3] Building extension ($Configuration)..." -ForegroundColor White
    dotnet build -c $Configuration 2>&1 | ForEach-Object { Write-Host "        $_" -ForegroundColor Gray }
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
    Write-Host "  [OK] Build succeeded" -ForegroundColor Green

    # Find the VSIX
    $vsixFile = Get-ChildItem -Path $extensionDir -Filter "*.vsix" -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($vsixFile) {
        Write-Host "  [OK] VSIX: $($vsixFile.FullName)" -ForegroundColor Green
    } else {
        Write-Host "  [WARNING] No .vsix file found in output." -ForegroundColor Yellow
        Write-Host "  The extension may need to be built from Visual Studio IDE." -ForegroundColor Yellow
    }

    # Install
    if (-not $BuildOnly -and $vsixFile) {
        Write-Host "  [3/3] Installing extension..." -ForegroundColor White

        # Find VSIXInstaller.exe
        $vsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
        $vsInstallPath = $null
        if (Test-Path $vsWhere) {
            $vsInstallPath = & $vsWhere -latest -property installationPath 2>$null
        }

        if ($vsInstallPath) {
            $vsixInstaller = Join-Path $vsInstallPath "Common7\IDE\VSIXInstaller.exe"
            if (Test-Path $vsixInstaller) {
                Write-Host "  Using: $vsixInstaller" -ForegroundColor Gray
                & $vsixInstaller /quiet $vsixFile.FullName 2>&1 | ForEach-Object { Write-Host "        $_" -ForegroundColor Gray }
                Write-Host "  [OK] Extension installed!" -ForegroundColor Green
                Write-Host ""
                Write-Host "  Restart Visual Studio to activate." -ForegroundColor Yellow
                Write-Host "  Then: Tools > GitLab OAuth Token Generator" -ForegroundColor Yellow
            } else {
                Write-Host "  [WARNING] VSIXInstaller.exe not found at expected path." -ForegroundColor Yellow
                Write-Host "  Install manually: double-click $($vsixFile.FullName)" -ForegroundColor Yellow
            }
        } else {
            Write-Host "  [WARNING] Visual Studio installation not found via vswhere." -ForegroundColor Yellow
            Write-Host "  Install manually: double-click $($vsixFile.FullName)" -ForegroundColor Yellow
        }
    } elseif (-not $BuildOnly) {
        Write-Host "  [3/3] No VSIX to install." -ForegroundColor Gray
    } else {
        Write-Host "  [3/3] Skipping install (BuildOnly mode)" -ForegroundColor Gray
    }

    Write-Host ""
    Write-Host "  =============================================" -ForegroundColor Cyan
    Write-Host "  Done!" -ForegroundColor Cyan
    Write-Host "  =============================================" -ForegroundColor Cyan
    Write-Host ""
}
finally {
    Pop-Location
}
