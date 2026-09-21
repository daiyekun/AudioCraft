# AudioCraft Build Script
param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

Write-Host "=====================================" -ForegroundColor Green
Write-Host "AudioCraft Build Script" -ForegroundColor Green
Write-Host "Version: $Version" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""

# 1. Build Release
Write-Host "[1/4] Building Release..." -ForegroundColor Yellow
dotnet clean -c Release 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Clean failed" }

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

Write-Host "Build completed!" -ForegroundColor Green
Write-Host ""

# 2. Check Inno Setup
Write-Host "[2/4] Checking Inno Setup..." -ForegroundColor Yellow
$isccPath = ""
$possiblePaths = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)

foreach ($path in $possiblePaths) {
    if (Test-Path $path) {
        $isccPath = $path
        break
    }
}

if ($isccPath -eq "") {
    Write-Host "Inno Setup not found!" -ForegroundColor Red
    Write-Host "Please install Inno Setup 6: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    exit 1
}
Write-Host "Found Inno Setup: $isccPath" -ForegroundColor Green
Write-Host ""

# 3. Update version
Write-Host "[3/4] Updating version..." -ForegroundColor Yellow
$setupScript = Get-Content "installer\setup.iss" -Raw
$setupScript = $setupScript -replace 'AppVersion=.*', "AppVersion=$Version"
$setupScript = $setupScript -replace 'OutputBaseFilename=.*', "OutputBaseFilename=AudioCraft_Setup_$Version"
Set-Content -Path "installer\setup.iss" -Value $setupScript -Encoding UTF8
Write-Host "Version updated to $Version" -ForegroundColor Green
Write-Host ""

# 4. Build installer
Write-Host "[4/4] Building installer..." -ForegroundColor Yellow
Push-Location "installer"
& $isccPath "setup.iss"
Pop-Location

if ($LASTEXITCODE -ne 0) {
    Write-Host "Installer build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Green
Write-Host "Build completed!" -ForegroundColor Green
Write-Host "Installer: installer\AudioCraft_Setup_$Version.exe" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
