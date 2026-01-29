using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace RemOSK.Services
{
    public class MagnifierController : IDisposable
    {
        private readonly KeyboardWindowManager _manager;
        private DispatcherTimer? _pollTimer;
        private bool _isActive;
        
        public event Action<BitmapSource?>? OnImageUpdated;
        public event Action? OnActivityDetected;

        public MagnifierController(KeyboardWindowManager manager)
        {
            _manager = manager;
        }

        public void Start()
        {
            if (_isActive) return;
            _isActive = true;
            Console.WriteLine("[Magnifier] Controller Started");

            _pollTimer = new DispatcherTimer();
            _pollTimer.Interval = TimeSpan.FromMilliseconds(200); // Standard polling
            _pollTimer.Tick += PollTimer_Tick;
            _pollTimer.Start();
        }

        public void Stop()
        {
            if (!_isActive) return;
            _isActive = false;
            Console.WriteLine("[Magnifier] Controller Stopped");

            _pollTimer?.Stop();
            _pollTimer = null;
        }

        private void PollTimer_Tick(object? sender, EventArgs e)
        {
            UpdateCapture();
        }

        private void UpdateCapture()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                Rect? targetRect = GetCaretRect();
                
                if (targetRect.HasValue)
                {
                    // Define Capture Region
                    var center = targetRect.Value;
                    double width = 400;
                    double height = 120; 
                    
                    double left = center.Left - (width / 2); 
                    double top = (center.Top + (center.Height / 2)) - (height / 2) + 25; 

                    var captureRect = new Rect(left, top, width, height);

                    var bmp = ScreenCaptureService.Instance.CaptureRegion(captureRect);
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (bmp != null)
                        {
                            OnActivityDetected?.Invoke();
                            OnImageUpdated?.Invoke(bmp);
                        }
                    });
                }
            });
        }

        private Rect? GetCaretRect()
        {
            try 
            {
                // 1. Try UIA Focused Element (TextPattern) - Works for Word, Notepad, WPF
                var element = AutomationElement.FocusedElement;
                if (element != null)
                {
                     object patternObj;
                     if (element.TryGetCurrentPattern(TextPattern.Pattern, out patternObj))
                     {
                         var textPattern = (TextPattern)patternObj;
                         var selection = textPattern.GetSelection();
                         if (selection != null && selection.Length > 0)
                         {
                             var rects = selection[0].GetBoundingRectangles();
                             if (rects != null && rects.Length > 0) 
                             {
                                 return rects[0];
                             }
                         }
                     }
                }

                // 2. Try Win32 Caret (Standard Windows Apps like Notepad, Legacy)
                var guiInfo = new GUITHREADINFO();
                guiInfo.cbSize = Marshal.SizeOf(guiInfo);
                
                IntPtr foreground = GetForegroundWindow();
                uint threadId = GetWindowThreadProcessId(foreground, out _);
                
                if (GetGUIThreadInfo(threadId, out guiInfo))
                {
                    if ((guiInfo.flags & GUI_CARETBLINKING) != 0 || guiInfo.rcCaret.Right > 0)
                    {
                        POINT p = new POINT { X = guiInfo.rcCaret.Left, Y = guiInfo.rcCaret.Top };
                        if (ClientToScreen(guiInfo.hwndCaret, ref p))
                        {
                            var r = new Rect(p.X, p.Y, guiInfo.rcCaret.Right - guiInfo.rcCaret.Left, guiInfo.rcCaret.Bottom - guiInfo.rcCaret.Top);
                            return r;
                        }
                    }
                }
                
                // 3. Try MSAA/IAccessible (Works for Windows Terminal, Chrome, modern apps)
                var caretRect = TryGetCaretFromAccessible(foreground);
                if (caretRect.HasValue)
                {
                    return caretRect;
                }
                
                // 4. Mouse Fallback - Only use if cursor is I-beam (text cursor)
                var cursorInfo = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
                if (GetCursorInfo(out cursorInfo))
                {
                    if (cursorInfo.flags == CURSOR_SHOWING)
                    {
                        // Check if it's a text cursor (I-beam)
                        IntPtr ibeamHandle = LoadCursor(IntPtr.Zero, IDC_IBEAM);
                        if (cursorInfo.hCursor == ibeamHandle)
                        {
                            return new Rect(cursorInfo.ptScreenPos.X, cursorInfo.ptScreenPos.Y, 1, 20);
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Magnifier] Error: {ex.Message}");
                return null;
            }
        }

        private Rect? TryGetCaretFromAccessible(IntPtr hwnd)
        {
            try
            {
                // Get IAccessible for the focused window using OBJID_CARET
                Guid iidAccessible = new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");
                object? obj = null;
                int result = AccessibleObjectFromWindow(hwnd, OBJID_CARET, ref iidAccessible, out obj);
                
                if (result == 0 && obj != null)
                {
                    // Use reflection to call accLocation on IAccessible
                    var type = obj.GetType();
                    var method = type.GetMethod("accLocation");
                    if (method != null)
                    {
                        var parameters = new object[] { 0, 0, 0, 0, 0 };
                        method.Invoke(obj, parameters);
                        
                        int left = (int)parameters[0];
                        int top = (int)parameters[1];
                        int width = (int)parameters[2];
                        int height = (int)parameters[3];
                        
                        if (width > 0 && height > 0)
                        {
                            return new Rect(left, top, width, height);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // MSAA not supported or failed - this is normal for many apps
                // Console.WriteLine($"[Magnifier] MSAA failed: {ex.Message}");
            }
            
            return null;
        }
        
        private const int CURSOR_SHOWING = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        private struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        public void Dispose()
        {
            Stop();
        }

        // --- P/Invoke ---
        [DllImport("user32.dll")]
        private static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, out GUITHREADINFO lpgui);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("oleacc.dll")]
        private static extern int AccessibleObjectFromWindow(
            IntPtr hwnd,
            int dwObjectID,
            ref Guid riid,
            [MarshalAs(UnmanagedType.IUnknown)] out object? ppvObject);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

        private const int OBJID_CARET = -8;
        private const int IDC_IBEAM = 32513;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const int GUI_CARETBLINKING = 0x00000001;
    }
}
