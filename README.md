# RemOSK (Remote On-Screen Keyboard)

**RemOSK** is a specialized on-screen keyboard utility designed for Windows tablets and touch devices, with a specific focus on usability over Remote Desktop (RDP) sessions.

Unlike standard on-screen keyboards, RemOSK uses low-level input injection to ensure keystrokes and mouse events are correctly transmitted to remote sessions, making it an ideal companion for IT professionals and power users managing servers or desktops from a tablet.

## Features

### ⌨️ Adaptive Split Keyboard
- **Split Layout:** Ergonomic design splitting the keyboard into left/right halves for thumb typing on tablets.
- **Configurable:** Supports multiple layouts (TKL, 75%, 60%) via JSON configuration.
- **Design:** Modern "Acrylic" aesthetic with blur effects (Windows 10/11 style).

### 🖱️ Virtual Mouse Input
- **Trackpoint Mode:** A virtual joystick/trackpoint for precision cursor control.
- **Trackpad Mode:** A larger virtual touch surface.
- **Click Bar:** Dedicated window for Left, Middle, and Right mouse clicks, plus a "Hold" toggle for dragging.
- **RDP Support:** Direct input injection ensures mouse moves and clicks register inside RDP sessions.

### 🎮 Gesture Controls
- **Move:** 1-Finger drag to position any window (Keyboard halves, Trackpoint, Buttons).
- **Zoom:** 2-Finger Vertical Slide on any window to scale it up/down intuitively.
- **Edit Mode:** Long-press (3.5s) on any window to enter Edit Mode (indicated by green borders), allowing for layout adjustments.

## Getting Started

### Prerequisites
- Windows 10 or Windows 11
- .NET 10.0 SDK (preview) or later

### Installation / Building

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/RemOSK.git
   ```
2. Navigate to the project directory:
   ```bash
   cd RemOSK
   ```
3. Build the project:
   ```bash
   dotnet build
   ```
4. Run:
   ```bash
   dotnet run --project RemOSK
   ```

## Usage

- **Launch:** Run `RemOSK.exe`. The keyboard will animate onto the screen.
- **Tray Icon:** Use the system tray icon to Toggle Visibility, Access Settings (planned), or Exit.
- **Typing:** Tap keys to type. Modifier keys (Shift, Ctrl, Alt, Win) allow for combinations.
- **Mouse:** Use the Trackpoint/Trackpad window to move the cursor. Use the Click Bar for mouse clicks.
- **Configuration:** Layouts and settings are stored in `~/.remosk/config.json`.

## Technical Details

- **Framework:** WPF (.NET Core / .NET 10)
- **Input:** Utilizes `SendInput` (Win32 API) for synthesized hardware events.
- **Window Management:** Custom `DraggableWindow` base class handling advanced touch gestures and window styling (blur, transparency, hit-testing).

## Contributing

Contributions are welcome! Please submit a Pull Request or open an Issue for bugs and feature requests.

## License

[MIT License](LICENSE)
