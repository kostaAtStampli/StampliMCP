#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Publishes the Stampli MCP Acumatica server as a self-contained Windows executable with Native AOT

.DESCRIPTION
    Creates a small, fast, single-file executable for Windows x64 using Native AOT compilation.
    The resulting executable is approximately 15-20MB and starts instantly without requiring .NET runtime.

.PARAMETER Configuration
    Build configuration (Debug or Release). Default is Release.

.PARAMETER OutputPath
    Output directory for the published executable. Default is ./publish/win-x64

.EXAMPLE
    ./publish-windows.ps1
    ./publish-windows.ps1 -Configuration Release -OutputPath ./dist/windows
#>

param(
    [string]$Configuration = "Release",
    [string]$OutputPath = "./publish/win-x64"
)

$ErrorActionPreference = "Stop"

Write-Host "🚀 Publishing Stampli MCP Acumatica Server for Windows x64" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Output Path: $OutputPath" -ForegroundColor Yellow

# Clean previous build
if (Test-Path $OutputPath) {
    Write-Host "Cleaning previous build..." -ForegroundColor Gray
    Remove-Item -Path $OutputPath -Recurse -Force
}

# Restore dependencies
Write-Host "`n📦 Restoring dependencies..." -ForegroundColor Cyan
dotnet restore

# Build the project
Write-Host "`n🔨 Building project..." -ForegroundColor Cyan
dotnet build -c $Configuration --no-restore

# Publish with Native AOT
Write-Host "`n📤 Publishing with Native AOT..." -ForegroundColor Cyan
$publishArgs = @(
    "publish"
    "-c", $Configuration
    "-r", "win-x64"
    "-o", $OutputPath
    "--self-contained"
    "/p:PublishAot=true"
    "/p:PublishSingleFile=true"
    "/p:OptimizationPreference=Size"
    "/p:DebugType=None"
    "/p:DebugSymbols=false"
)

dotnet @publishArgs

if ($LASTEXITCODE -eq 0) {
    $exePath = Join-Path $OutputPath "stampli-mcp-acumatica.exe"
    if (Test-Path $exePath) {
        $fileInfo = Get-Item $exePath
        $sizeMB = [math]::Round($fileInfo.Length / 1MB, 2)

        Write-Host "`n✅ Build successful!" -ForegroundColor Green
        Write-Host "Executable: $exePath" -ForegroundColor White
        Write-Host "Size: ${sizeMB}MB" -ForegroundColor White

        # Show how to test the MCP server
        Write-Host "`n📝 To test the MCP server:" -ForegroundColor Cyan
        Write-Host "  $exePath" -ForegroundColor Yellow
        Write-Host "  This will start the MCP server using stdio transport" -ForegroundColor Gray

        Write-Host "`n🎯 To use with Claude Desktop:" -ForegroundColor Cyan
        Write-Host "  Add to claude_desktop_config.json:" -ForegroundColor Gray
        Write-Host '  "mcpServers": {' -ForegroundColor Gray
        Write-Host '    "stampli-acumatica": {' -ForegroundColor Gray
        Write-Host "      `"command`": `"$($exePath -replace '\\', '\\')`"" -ForegroundColor Gray
        Write-Host '    }' -ForegroundColor Gray
        Write-Host '  }' -ForegroundColor Gray
    }
    else {
        Write-Host "❌ Executable not found at expected location: $exePath" -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "❌ Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}