using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RemOSK.Services
{
    /// <summary>
    /// Uses RegisterTouchWindow to make Windows convert touch input to mouse events.
    /// This prevents the cursor from jumping to the touch position while allowing
    /// the trackpad to read touch deltas as mouse movement deltas.
    /// </summary>
    public static class TouchToMouseConverter
    {
        private const int TWF_WANTPALM = 0x00000002;

        [DllImport("user32.dll")]
        private static extern bool RegisterTouchWindow(IntPtr hWnd, uint ulFlags);

        [DllImport("user32.dll")]
        private static extern bool UnregisterTouchWindow(IntPtr hWnd);

        /// <summary>
        /// Registers a window to receive touch events as WM_TOUCH messages.
        /// This also tells Windows to convert touch to mouse events, preventing
        /// WPF's touch-to-absolute-position behavior.
        /// </summary>
        public static void RegisterForTouchToMouse(Window window)
        {
            if (window == null) return;
            
            window.SourceInitialized += (s, e) =>
            {
                var helper = new WindowInteropHelper(window);
                RegisterTouchWindow(helper.Handle, TWF_WANTPALM);
                Console.WriteLine($"[TouchToMouseConverter] Registered window {window.Title} for touch-to-mouse");
            };
        }

        /// <summary>
        /// Call this after window handle is available if SourceInitialized already fired
        /// </summary>
        public static void RegisterForTouchToMouseNow(Window window)
        {
            if (window == null) return;
            
            var helper = new WindowInteropHelper(window);
            if (helper.Handle != IntPtr.Zero)
            {
                RegisterTouchWindow(helper.Handle, TWF_WANTPALM);
                Console.WriteLine($"[TouchToMouseConverter] Registered window {window.Title} for touch-to-mouse (immediate)");
            }
        }

        public static void Unregister(Window window)
        {
            if (window == null) return;
            
            var helper = new WindowInteropHelper(window);
            if (helper.Handle != IntPtr.Zero)
            {
                UnregisterTouchWindow(helper.Handle);
            }
        }
    }
}
