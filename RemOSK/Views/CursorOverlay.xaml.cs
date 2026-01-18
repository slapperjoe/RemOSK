using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace RemOSK.Views
{
    /// <summary>
    /// A custom cursor overlay window that follows mouse position.
    /// Used when Windows hides the system cursor due to touch input.
    /// </summary>
    public partial class CursorOverlay : Window
    {
        private DispatcherTimer _updateTimer;
        private bool _isEnabled;

        // Win32 APIs for getting cursor position and making window click-through
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public CursorOverlay()
        {
            InitializeComponent();

            // Setup timer to track cursor position
            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps
            _updateTimer.Tick += UpdatePosition;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Make window click-through and invisible to task switcher
            var hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
        }

        // Track our own position instead of following system cursor
        private double _cursorX;
        private double _cursorY;
        private int _screenWidth;
        private int _screenHeight;
        
        /// <summary>
        /// Get the current cursor position
        /// </summary>
        public (int X, int Y) CurrentPosition => ((int)_cursorX, (int)_cursorY);
        
        /// <summary>
        /// Get the screen bounds being used (important for RDP viewport)
        /// </summary>
        public (int Width, int Height) ScreenBounds => (_screenWidth, _screenHeight);

        public void EnableOverlay()
        {
            if (_isEnabled) return;
            _isEnabled = true;
            
            Console.WriteLine("[CursorOverlay] Enabling overlay");
            
            // Get screen dimensions - for RDP, the host reports full resolution
            // but the client viewport may be smaller. Use WPF's work area which respects DPI.
            // Fallback: use hardcoded reasonable bounds
            var screenWidth = GetSystemMetrics(SM_CXSCREEN);
            var screenHeight = GetSystemMetrics(SM_CYSCREEN);
            
            // For RDP: limit to visible viewport (typically smaller than host screen)
            // Use the minimum of detected size or a reasonable RDP viewport
            _screenWidth = Math.Min(screenWidth, 1660);  // RDP client width
            _screenHeight = Math.Min(screenHeight, 1024); // RDP client height
            
            Console.WriteLine($"[CursorOverlay] Using screen bounds: {_screenWidth}x{_screenHeight} (detected: {screenWidth}x{screenHeight})");
            
            // Always start at CENTER of visible screen
            _cursorX = _screenWidth / 2;
            _cursorY = _screenHeight / 2;
            
            this.Left = _cursorX;
            this.Top = _cursorY;
            this.Show();
            // NOTE: Do NOT call Activate() - it steals focus from other apps
            this.Topmost = true;
            
            Console.WriteLine($"[CursorOverlay] Started at center: {_cursorX},{_cursorY}");
            
            _updateTimer.Start();
        }

        public void DisableOverlay()
        {
            if (!_isEnabled) return;
            _isEnabled = false;
            
            Console.WriteLine("[CursorOverlay] Disabling overlay");
            _updateTimer.Stop();
            this.Hide();
        }

        /// <summary>
        /// Move the cursor by the specified delta. Called by trackpoint.
        /// </summary>
        public void MoveCursor(double dx, double dy)
        {
            _cursorX += dx;
            _cursorY += dy;
            
            // Clamp to screen bounds
            _cursorX = Math.Max(0, Math.Min(_screenWidth - 1, _cursorX));
            _cursorY = Math.Max(0, Math.Min(_screenHeight - 1, _cursorY));
            
            // NOTE: We do NOT call SetCursorPos here anymore.
            // The manager calls InputInjector.SendMouseMoveTo() which handles the actual system move
            // and ensures cursor shape updates work correctly via the input stack.
            
            // Update shape (optimization: update local shape based on system state at this new pos)
            UpdateCursorShape();
        }
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int X, int Y);

        private void UpdatePosition(object? sender, EventArgs e)
        {
            // Update visual position to our tracked position
            this.Left = _cursorX;
            this.Top = _cursorY;
            
            // Update cursor shape based on system cursor
            UpdateCursorShape();
        }
        
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
        
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        
        // Cursor shape detection
        [DllImport("user32.dll")]
        private static extern bool GetCursorInfo(out CURSORINFO pci);
        
        [DllImport("user32.dll")]
        private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);
        
        [StructLayout(LayoutKind.Sequential)]
        private struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }
        
        // Standard cursor IDs
        private const int IDC_ARROW = 32512;
        private const int IDC_IBEAM = 32513;
        private const int IDC_HAND = 32649;
        private const int IDC_SIZEWE = 32644;  // Horizontal resize
        private const int IDC_SIZENS = 32645;  // Vertical resize
        private const int IDC_SIZENESW = 32643; // Diagonal resize
        private const int IDC_SIZENWSE = 32642; // Diagonal resize
        
        private IntPtr _arrowHandle;
        private IntPtr _ibeamHandle;
        private IntPtr _handHandle;
        private IntPtr _sizeweHandle;
        private IntPtr _lastCursorHandle = IntPtr.Zero;
        
        private void InitCursorHandles()
        {
            _arrowHandle = LoadCursor(IntPtr.Zero, IDC_ARROW);
            _ibeamHandle = LoadCursor(IntPtr.Zero, IDC_IBEAM);
            _handHandle = LoadCursor(IntPtr.Zero, IDC_HAND);
            _sizeweHandle = LoadCursor(IntPtr.Zero, IDC_SIZEWE);
        }
        
        public void UpdateCursorShape()
        {
            // Initialize handles if needed
            if (_arrowHandle == IntPtr.Zero)
            {
                InitCursorHandles();
            }
            
            var info = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
            if (!GetCursorInfo(out info)) return;
            
            // Skip if cursor hasn't changed
            if (info.hCursor == _lastCursorHandle) return;
            _lastCursorHandle = info.hCursor;
            
            // Hide all cursors first
            ArrowCursor.Visibility = Visibility.Collapsed;
            TextCursor.Visibility = Visibility.Collapsed;
            HandCursor.Visibility = Visibility.Collapsed;
            ResizeHCursor.Visibility = Visibility.Collapsed;
            
            // Show the matching cursor
            if (info.hCursor == _ibeamHandle)
            {
                TextCursor.Visibility = Visibility.Visible;
            }
            else if (info.hCursor == _handHandle)
            {
                HandCursor.Visibility = Visibility.Visible;
            }
            else if (info.hCursor == _sizeweHandle)
            {
                ResizeHCursor.Visibility = Visibility.Visible;
            }
            else
            {
                // Default to arrow
                ArrowCursor.Visibility = Visibility.Visible;
            }
        }
    }
}
