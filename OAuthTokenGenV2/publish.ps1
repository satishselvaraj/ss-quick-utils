# ============================================================================
# GitLab OAuth Token Generator V2 - Publish Script
# ============================================================================
# Publishes the application as a self-contained single-file Windows executable.
# The output .exe can be shared and run on any Windows x64 machine
# without requiring the .NET SDK or runtime to be installed.
# ============================================================================

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = ".\publish"
)

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  Publishing GitLab OAuth Token Generator V2" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# Clean previous publish output
if (Test-Path $OutputDir) {
    Write-Host "  Cleaning previous publish output..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $OutputDir
}

# Publish as self-contained single-file executable
Write-Host "  Publishing as self-contained single-file executable..." -ForegroundColor White
Write-Host ""

dotnet publish OAuthTokenGenV2.csproj `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $OutputDir `
    /p:PublishSingleFile=true `
    /p:PublishTrimmed=true `
    /p:PublishReadyToRun=true `
    /p:EnableCompressionInSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "  =============================================" -ForegroundColor Green
    Write-Host "  BUILD SUCCESSFUL!" -ForegroundColor Green
    Write-Host "  =============================================" -ForegroundColor Green
    Write-Host ""

    $exePath = Join-Path $OutputDir "GitLabOAuthTokenGenerator.exe"
    if (Test-Path $exePath) {
        $fileInfo = Get-Item $exePath
        $sizeMB = [math]::Round($fileInfo.Length / 1MB, 2)
        Write-Host "  Executable: $exePath" -ForegroundColor Yellow
        Write-Host "  File size:  $sizeMB MB" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "  This .exe is fully self-contained and can be" -ForegroundColor White
        Write-Host "  shared with anyone on Windows x64 - no .NET" -ForegroundColor White
        Write-Host "  SDK or runtime installation required." -ForegroundColor White
    }
} else {
    Write-Host ""
    Write-Host "  BUILD FAILED!" -ForegroundColor Red
    Write-Host "  Check the errors above." -ForegroundColor Red
}

Write-Host ""
