# RemOSK - Copilot Instructions

RemOSK is a specialized on-screen keyboard for Windows tablets, designed for Remote Desktop (RDP) sessions. It uses low-level Win32 input injection to ensure keystrokes and mouse events work correctly over RDP.

## Build & Run

**Build:**
```bash
dotnet build
```

**Run:**
```bash
dotnet run --project RemOSK
```

**Target Framework:** .NET 10.0 with WPF + Windows Forms

**Build Release:**
```bash
dotnet build -c Release
```

**Create Installer:**
```powershell
.\build-installer.ps1
```
Creates `RemOSK-Setup.exe` using NSIS. See `INSTALLER.md` for details.

**Note:** If build fails with "file in use" error, close the running application first. The application can auto-restart after successful builds (see `.agent/workflows/smart_build.md`).

## Architecture Overview

### Application Initialization Flow

**App.xaml.cs** startup sequence:
1. **ConfigService** - Loads/saves `~/.remosk/config.json` (window positions, scales, layout selection)
2. **KeyboardWindowManager** - Central coordinator managing all windows and their lifecycle
3. **TrayIconManager** - System tray control for visibility toggle and exit

### Window System

All windows inherit from **DraggableWindow** base class:
- **Left/Right Keyboard Windows** - Split keyboard layout (each half independently positioned/scaled)
- **Trackpoint/Trackpad Window** - Virtual mouse control
- **Click Buttons Window** - Left/Middle/Right clicks + Hold toggle

**KeyboardWindowManager** responsibilities:
- Manages visibility and lifecycle of all 3 window types
- Tracks foreground window every 100ms to restore focus after typing (excludes RemOSK windows)
- 5-second inactivity timer fades windows to 30% opacity
- Persists window positions and scales to config on every change
- Supports 4 layouts: DefaultTKL, 75%, 60%, Alice (hot-reload via close/reopen)

### Touch Gesture System

**DraggableWindow.cs** implements multi-touch gestures:
- **1-Finger Drag:** Repositions window (absolute offset from initial touch)
- **2-Finger Vertical Slide:** Scales window (200px = ±1.0 scale, range 0.5-4.0)
- **Long-Press (3.5s):** Enters edit mode for layout adjustments

Touch events stored in dictionary by touch ID; smooth transition when dropping from 2→1 fingers.

### Input Injection

**InputInjector.cs** wraps Win32 `SendInput` API:
- **Keyboard:** `SendKeystroke()`, `SendKeyDown()`, `SendKeyUp()` using VK codes
- **Mouse Movement:** 
  - `SendMouseMove()` - Relative delta (clamped to screen)
  - `SendMouseMoveTo()` - Absolute coordinates (normalized to 0-65535)
- **Mouse Clicks:** `SendLeftClick()`, `SendRightClick()`, `SendMiddleClick()`
  - `SendLeftClickAt()` - Absolute click with screen-bounds normalization (RDP aware)
  - `SendLeftButtonDown()/Up()` - Hold/release for drag operations

All mouse operations use `MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK` flags for virtual desktop/RDP support.

### Configuration System

**ConfigService.cs** manages `~/.remosk/config.json`:
```json
{
  "LastUsedLayout": "DefaultTKL",
  "MouseMode": "Trackpoint|Trackpad|Off",
  "EnableAcrylic": false,
  "LeftUiScale": 1.0,
  "RightUiScale": 1.0,
  "LeftWindowTop": 100,
  "LeftWindowLeft": 50,
  "RightWindowTop": 100,
  "RightWindowLeft": 800,
  "TrackpointWindowTop": 400,
  "TrackpointWindowLeft": 400,
  "TrackpointWindowUiScale": 1.0,
  "ClickButtonsWindowTop": 600,
  "ClickButtonsWindowLeft": 400,
  "ClickButtonsWindowUiScale": 1.0,
  "IsEditModeEnabled": false,
  "IsRdpMode": false
}
```

- Auto-creates directory on first save
- Events trigger saves on position/scale changes and mode toggles
- Returns default AppConfig if file missing/corrupted

### Keyboard Layouts

Layouts stored in `RemOSK/Assets/*.json` (DefaultLayout, Layout75, Layout60, AliceLayout):
```json
{
  "Name": "Default TKL",
  "LeftKeys": [
    {
      "Label": "Esc",
      "VirtualKeyCode": 27,
      "Row": 0,
      "Column": 0
    }
  ],
  "RightKeys": [...]
}
```

**LayoutLoader.cs** loads JSON and creates key button grids. Use VK codes from Win32 API.

## Key Conventions

### Window Styling
- All windows use `WS_EX_NOACTIVATE` to prevent stealing focus from target applications
- `WS_EX_TRANSPARENT` flag toggles click-through mode
- Acrylic blur effects managed by **WindowBlurHelper** (Win32 DWM API)

### Modifier Key Handling
- **ModifierStateManager** tracks Shift/Ctrl/Alt/Win sticky-key state; keyboard windows implement `IModifierObserver` to receive `OnModifierStateChanged()` callbacks for UI updates (key highlighting)
- Shift: single tap = latch, double-tap within 500ms = caps-lock style lock, third tap = release
- Other modifiers (Ctrl/Alt/Win) toggle on/off; all auto-release after the next non-modifier key

### Focus Management
- **KeyboardWindowManager** uses 100ms timer to poll foreground window
- Restores focus to last active window after visibility toggle (200ms delay to avoid tray interference)
- Excludes RemOSK windows from focus tracking to prevent self-focus loops

### Touch Event Capture
- Border elements capture touch via `CaptureTouch()` API
- Touch dictionaries keyed by device ID for multi-touch tracking
- Release capture on `TouchUp` to prevent stuck touches

### Assets Management
- Layout JSON files copied to output directory (`PreserveNewest`)
- Resources embedded via `<Resource Include="Resources\**" />`

## Namespace Structure

All code uses `namespace RemOSK;` (file-scoped):
- `RemOSK.Models` - Data structures (KeyLayout)
- `RemOSK.Services` - Business logic and Win32 interop
- `RemOSK.Views` - Window classes
- `RemOSK.Controls` - Custom WPF controls

## Dependencies

No external NuGet packages - uses built-in .NET 10 WPF/Windows Forms APIs and Win32 P/Invoke.
