# RemOSK Installer Build Script
# This script builds the application in Release mode and creates the NSIS installer

Write-Host "=== RemOSK Installer Builder ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Check if NSIS is installed
Write-Host "Checking for NSIS installation..." -ForegroundColor Yellow
$nsisPath = $null
$possiblePaths = @(
    "C:\Program Files (x86)\NSIS\makensis.exe",
    "C:\Program Files\NSIS\makensis.exe"
)

foreach ($path in $possiblePaths) {
    if (Test-Path $path) {
        $nsisPath = $path
        break
    }
}

if (-not $nsisPath) {
    Write-Host "ERROR: NSIS not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install NSIS from: https://nsis.sourceforge.io/Download" -ForegroundColor Yellow
    Write-Host "Or use Chocolatey: choco install nsis" -ForegroundColor Yellow
    Write-Host "Or use winget: winget install NSIS.NSIS" -ForegroundColor Yellow
    exit 1
}

Write-Host "Found NSIS at: $nsisPath" -ForegroundColor Green
Write-Host ""

# Step 2: Clean previous build
Write-Host "Cleaning previous build..." -ForegroundColor Yellow
if (Test-Path "RemOSK\bin\Release") {
    Remove-Item "RemOSK\bin\Release" -Recurse -Force
}
if (Test-Path "RemOSK\obj\Release") {
    Remove-Item "RemOSK\obj\Release" -Recurse -Force
}
Write-Host "Clean complete." -ForegroundColor Green
Write-Host ""

# Step 3: Build the application in Release mode
Write-Host "Building RemOSK in Release mode..." -ForegroundColor Yellow
$buildOutput = & dotnet build RemOSK\RemOSK.csproj -c Release 2>&1
$buildExitCode = $LASTEXITCODE

if ($buildExitCode -ne 0) {
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    Write-Host $buildOutput
    exit 1
}

Write-Host "Build successful!" -ForegroundColor Green
Write-Host ""

# Step 4: Verify required files exist
Write-Host "Verifying build output..." -ForegroundColor Yellow
$requiredFiles = @(
    "RemOSK\bin\Release\net10.0-windows\RemOSK.exe",
    "RemOSK\bin\Release\net10.0-windows\RemOSK.dll",
    "RemOSK\bin\Release\net10.0-windows\RemOSK.runtimeconfig.json"
)

$missingFiles = @()
foreach ($file in $requiredFiles) {
    if (-not (Test-Path $file)) {
        $missingFiles += $file
    }
}

if ($missingFiles.Count -gt 0) {
    Write-Host "ERROR: Missing required files:" -ForegroundColor Red
    $missingFiles | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "All required files present." -ForegroundColor Green
Write-Host ""

# Step 5: Create the installer
Write-Host "Creating NSIS installer..." -ForegroundColor Yellow
$installerOutput = & $nsisPath installer.nsi 2>&1
$installerExitCode = $LASTEXITCODE

if ($installerExitCode -ne 0) {
    Write-Host "ERROR: Installer creation failed!" -ForegroundColor Red
    Write-Host $installerOutput
    exit 1
}

Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Installer created: RemOSK-Setup.exe" -ForegroundColor Green
Write-Host ""

# Display file size
if (Test-Path "RemOSK-Setup.exe") {
    $fileSize = (Get-Item "RemOSK-Setup.exe").Length / 1MB
    Write-Host ("Installer size: {0:N2} MB" -f $fileSize) -ForegroundColor Cyan
}
