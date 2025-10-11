#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Cross-platform publish script for Stampli MCP Acumatica server (without Native AOT)

.DESCRIPTION
    Creates self-contained executables for multiple platforms without Native AOT.
    This allows cross-compilation from any OS to any OS, but results in larger file sizes (~60-80MB).
    Use this when you need to build for Mac/Linux from Windows or vice versa.

.PARAMETER Platforms
    Comma-separated list of target platforms. Default is "win-x64,osx-arm64,osx-x64,linux-x64"

.PARAMETER Configuration
    Build configuration (Debug or Release). Default is Release.

.PARAMETER OutputPath
    Base output directory for published executables. Default is ./publish

.EXAMPLE
    ./publish-portable.ps1
    ./publish-portable.ps1 -Platforms "osx-arm64,linux-x64"
#>

param(
    [string]$Platforms = "win-x64,osx-arm64,osx-x64,linux-x64",
    [string]$Configuration = "Release",
    [string]$OutputPath = "./publish"
)

$ErrorActionPreference = "Stop"

# Parse platforms
$platformList = $Platforms -split ','

Write-Host "🚀 Cross-Platform Publishing for Stampli MCP Acumatica Server" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Platforms: $($platformList -join ', ')" -ForegroundColor Yellow
Write-Host "Note: These builds are self-contained but NOT using Native AOT (for cross-platform compatibility)" -ForegroundColor Gray

# Clean and restore once
Write-Host "`n📦 Restoring dependencies..." -ForegroundColor Cyan
dotnet restore

Write-Host "`n🔨 Building project..." -ForegroundColor Cyan
dotnet build -c $Configuration --no-restore

# Publish for each platform
foreach ($platform in $platformList) {
    $platform = $platform.Trim()
    $platformOutputPath = Join-Path $OutputPath "$platform-portable"

    Write-Host "`n📤 Publishing for $platform..." -ForegroundColor Cyan

    # Clean platform-specific directory
    if (Test-Path $platformOutputPath) {
        Remove-Item -Path $platformOutputPath -Recurse -Force
    }

    # Determine executable extension
    $executableName = "stampli-mcp-acumatica"
    if ($platform -like "win-*") {
        $executableName += ".exe"
    }

    # Publish without AOT for cross-platform compatibility
    $publishArgs = @(
        "publish"
        "-c", $Configuration
        "-r", $platform
        "-o", $platformOutputPath
        "--self-contained"
        "/p:PublishSingleFile=true"
        "/p:PublishTrimmed=true"
        "/p:PublishAot=false"  # Explicitly disable AOT for cross-compilation
        "/p:DebugType=None"
        "/p:DebugSymbols=false"
    )

    dotnet @publishArgs

    if ($LASTEXITCODE -eq 0) {
        $exePath = Join-Path $platformOutputPath $executableName
        if (Test-Path $exePath) {
            $fileInfo = Get-Item $exePath
            $sizeMB = [math]::Round($fileInfo.Length / 1MB, 2)

            Write-Host "✅ $platform build successful!" -ForegroundColor Green
            Write-Host "   Path: $exePath" -ForegroundColor White
            Write-Host "   Size: ${sizeMB}MB" -ForegroundColor White

            # Make executable on Unix-like systems
            if ($platform -notlike "win-*" -and $IsLinux -or $IsMacOS) {
                chmod +x $exePath
            }
        }
    }
    else {
        Write-Host "❌ $platform build failed" -ForegroundColor Red
    }
}

Write-Host "`n📊 Build Summary:" -ForegroundColor Cyan
Write-Host "All portable builds completed. These are self-contained but larger than Native AOT builds." -ForegroundColor White
Write-Host "For smallest file size (~15-20MB), use platform-specific Native AOT builds:" -ForegroundColor Yellow
Write-Host "  - Windows: ./publish-windows.ps1" -ForegroundColor Gray
Write-Host "  - macOS: ./publish-mac.sh (must run on Mac)" -ForegroundColor Gray
Write-Host "  - Linux: ./publish-linux.sh (must run on Linux)" -ForegroundColor Gray

Write-Host "`n🎯 Usage with Claude Desktop:" -ForegroundColor Cyan
Write-Host "Copy the appropriate executable to the target machine and configure in claude_desktop_config.json" -ForegroundColor Gray