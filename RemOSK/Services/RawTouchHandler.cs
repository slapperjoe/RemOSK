using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RemOSK.Services
{
    /// <summary>
    /// Handles WM_TOUCH messages directly to implement relative touchpad movement
    /// without Windows converting touch to absolute mouse position.
    /// </summary>
    public class RawTouchHandler
    {
        private const int WM_TOUCH = 0x0240;
        private const int TOUCHEVENTF_MOVE = 0x0001;
        private const int TOUCHEVENTF_DOWN = 0x0002;
        private const int TOUCHEVENTF_UP = 0x0004;
        private const int TOUCHEVENTF_PRIMARY = 0x0010;

        [DllImport("user32.dll")]
        private static extern bool RegisterTouchWindow(IntPtr hWnd, uint ulFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetTouchInputInfo(IntPtr hTouchInput, int cInputs, 
            [Out] TOUCHINPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern void CloseTouchInputHandle(IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct TOUCHINPUT
        {
            public int x;
            public int y;
            public IntPtr hSource;
            public int dwID;
            public int dwFlags;
            public int dwMask;
            public int dwTime;
            public IntPtr dwExtraInfo;
            public int cxContact;
            public int cyContact;
        }

        private IntPtr _hwnd;
        private HwndSource? _hwndSource;
        private bool _isTracking;
        public bool IsRegistered { get; private set; } // Track registration success
        private int _lastX;
        private int _lastY;
        private int _trackingId = -1;

        public event EventHandler<Vector>? OnRelativeMove;
        public event EventHandler<Point>? OnAbsolutePosition;
        public event EventHandler? OnTouchDown;
        public event EventHandler? OnTouchUp;

        public void Attach(Window window)
        {
            if (PresentationSource.FromVisual(window) != null)
            {
                // Already initialized
                SetupHook(window);
            }
            else
            {
                window.SourceInitialized += (s, e) => SetupHook(window);
            }
        }

        private void SetupHook(Window window)
        {
            var helper = new WindowInteropHelper(window);
            _hwnd = helper.Handle;
            
            // Register for WM_TOUCH - use WANTPALM to ensure we get everything
            // 0x00000002 = TWF_WANTPALM
            bool result = RegisterTouchWindow(_hwnd, 2); 
            if (!result)
            {
                 int err = Marshal.GetLastWin32Error();
                 Console.WriteLine($"[RawTouchHandler] RegisterTouchWindow failed! Error: {err}");
                 IsRegistered = false;
            }
            else
            {
                 Console.WriteLine($"[RawTouchHandler] Attached to {window.Title} (RegisterTouchWindow Success)");
                 IsRegistered = true;
            }
            
            // Hook into WndProc
            _hwndSource = HwndSource.FromHwnd(_hwnd);
            _hwndSource?.AddHook(WndProc);
        }

        public void Detach()
        {
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource = null;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_TOUCH)
            {
                int touchCount = wParam.ToInt32() & 0xFFFF;
                var inputs = new TOUCHINPUT[touchCount];
                int structSize = Marshal.SizeOf(typeof(TOUCHINPUT));

                if (GetTouchInputInfo(lParam, touchCount, inputs, structSize))
                {
                    // Console.WriteLine($"[RawTouch] Got {touchCount} inputs");
                    foreach (var input in inputs)
                    {
                        // Convert from 100ths of a pixel to pixels
                        int x = input.x / 100;
                        int y = input.y / 100;

                        if ((input.dwFlags & TOUCHEVENTF_DOWN) != 0)
                        {
                            Console.WriteLine($"[RawTouch] DOWN id={input.dwID} x={x} y={y}");
                            // Touch down - start tracking
                            _isTracking = true;
                            _trackingId = input.dwID;
                            _lastX = x;
                            _lastY = y;
                            OnTouchDown?.Invoke(this, EventArgs.Empty);
                        }
                        else if ((input.dwFlags & TOUCHEVENTF_MOVE) != 0 && _isTracking && input.dwID == _trackingId)
                        {
                            // Touch move - calculate delta
                            int deltaX = x - _lastX;
                            int deltaY = y - _lastY;
                            _lastX = x;
                            _lastY = y;

                            if (Math.Abs(deltaX) > 0 || Math.Abs(deltaY) > 0)
                            {
                                // Console.WriteLine($"[RawTouch] MOVE dx={deltaX} dy={deltaY}");
                                OnRelativeMove?.Invoke(this, new Vector(deltaX * 1.5, deltaY * 1.5));
                                OnAbsolutePosition?.Invoke(this, new Point(x, y));
                            }
                        }
                        else if ((input.dwFlags & TOUCHEVENTF_UP) != 0 && input.dwID == _trackingId)
                        {
                            Console.WriteLine($"[RawTouch] UP id={input.dwID}");
                            // Touch up - stop tracking
                            _isTracking = false;
                            _trackingId = -1;
                            OnTouchUp?.Invoke(this, EventArgs.Empty);
                        }
                    }

                    CloseTouchInputHandle(lParam);
                    
                    // Mark as handled to PREVENT Windows from generating mouse events
                    handled = true;
                }
                else
                {
                    Console.WriteLine($"[RawTouch] GetTouchInputInfo failed: {Marshal.GetLastWin32Error()}");
                }
            }

            return IntPtr.Zero;
        }
    }
}
