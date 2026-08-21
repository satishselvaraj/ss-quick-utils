# ============================================================
# GitLab OAuth Token Generator V3 - Local Web Server
# ============================================================
# Starts a local HTTP server to host the static web app.
# Opens the browser automatically.
#
# Usage:
#   .\serve.ps1           # Default port 8585
#   .\serve.ps1 -Port 9090  # Custom port
# ============================================================

param(
    [int]$Port = 8585
)

$publicDir = Join-Path $PSScriptRoot "public"

if (-not (Test-Path $publicDir)) {
    Write-Host "Error: 'public' folder not found at $publicDir" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "  =============================================" -ForegroundColor Cyan
Write-Host "  GitLab OAuth Token Generator V3" -ForegroundColor Cyan
Write-Host "  Local Web Server" -ForegroundColor Cyan
Write-Host "  =============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Serving from: $publicDir" -ForegroundColor White
Write-Host "  URL:          http://localhost:$Port/" -ForegroundColor Yellow
Write-Host ""
Write-Host "  Press Ctrl+C to stop the server." -ForegroundColor DarkGray
Write-Host ""

# Try Python first, then Node, then .NET
$pythonCmd = Get-Command python -ErrorAction SilentlyContinue
$python3Cmd = Get-Command python3 -ErrorAction SilentlyContinue
$nodeCmd = Get-Command npx -ErrorAction SilentlyContinue
$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue

# Open browser after a short delay
Start-Job -ScriptBlock {
    Start-Sleep -Seconds 2
    Start-Process "http://localhost:$using:Port/"
} | Out-Null

if ($pythonCmd) {
    Write-Host "  Using Python HTTP server..." -ForegroundColor Green
    Push-Location $publicDir
    python -m http.server $Port
    Pop-Location
} elseif ($python3Cmd) {
    Write-Host "  Using Python3 HTTP server..." -ForegroundColor Green
    Push-Location $publicDir
    python3 -m http.server $Port
    Pop-Location
} elseif ($nodeCmd) {
    Write-Host "  Using Node.js http-server..." -ForegroundColor Green
    Push-Location $publicDir
    npx http-server -p $Port -c-1
    Pop-Location
} elseif ($dotnetCmd) {
    Write-Host "  Using .NET SimpleServer..." -ForegroundColor Green
    Write-Host "  (Creating minimal Kestrel server)" -ForegroundColor DarkGray

    # Create a temporary .NET minimal API to serve static files
    $tempDir = Join-Path $env:TEMP "OAuthTokenGenV3Server"
    if (Test-Path $tempDir) { Remove-Item -Recurse -Force $tempDir }
    New-Item -ItemType Directory -Path $tempDir | Out-Null

    $csproj = @"
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
"@

    $programCs = @"
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:$Port");
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.Run();
"@

    Set-Content -Path (Join-Path $tempDir "server.csproj") -Value $csproj
    Set-Content -Path (Join-Path $tempDir "Program.cs") -Value $programCs

    # Copy static files to wwwroot
    $wwwroot = Join-Path $tempDir "wwwroot"
    New-Item -ItemType Directory -Path $wwwroot | Out-Null
    Copy-Item -Path "$publicDir\*" -Destination $wwwroot -Recurse

    Push-Location $tempDir
    dotnet run --project server.csproj
    Pop-Location

    # Cleanup
    Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue
} else {
    Write-Host "  No supported server found!" -ForegroundColor Red
    Write-Host "  Install one of: Python, Node.js, or .NET SDK" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Alternative: Open the file directly:" -ForegroundColor White
    Write-Host "    Start-Process '$publicDir\index.html'" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Opening index.html directly..." -ForegroundColor Green
    Start-Process (Join-Path $publicDir "index.html")
}
