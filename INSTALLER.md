# Building the RemOSK Installer

This directory contains the NSIS installer script for RemOSK.

## Prerequisites

1. **NSIS (Nullsoft Scriptable Install System)** - Download from:
   - Official site: https://nsis.sourceforge.io/Download
   - Chocolatey: `choco install nsis`
   - winget: `winget install NSIS.NSIS`

2. **.NET 10.0 SDK** - Required to build the application

## Quick Start

Run the build script:

```powershell
.\build-installer.ps1
```

This will:
1. Clean previous builds
2. Build RemOSK in Release mode
3. Create `RemOSK-Setup.exe`

## Manual Build

If you prefer to build manually:

```powershell
# Build the application
dotnet build RemOSK\RemOSK.csproj -c Release

# Create the installer
"C:\Program Files (x86)\NSIS\makensis.exe" installer.nsi
```

## Installer Features

- **Installation Directory**: `C:\Program Files\RemOSK` (default)
- **Start Menu Shortcuts**: Creates RemOSK program group
- **Desktop Shortcut**: Optional shortcut on desktop
- **Startup Option**: Checkbox on finish page to add RemOSK to Windows startup
- **Uninstaller**: Fully removes application and registry entries
- **Upgrade Support**: Detects and removes previous versions automatically

## Customization

Edit `installer.nsi` to customize:
- Product version (`PRODUCT_VERSION`)
- Website URL (`PRODUCT_WEB_SITE`)
- Installation directory
- Shortcuts and startup behavior

## Distribution

The generated `RemOSK-Setup.exe` is a self-contained installer that can be distributed to users. No additional files are required.
