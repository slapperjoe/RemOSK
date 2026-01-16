using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RemOSK.Services
{
    public static class WindowBlurHelper
    {
        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        internal enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        internal enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4, // Windows 10 1803+
            ACCENT_INVALID_STATE = 5
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor; // AABBGGRR
            public int AnimationId;
        }

        public static void EnableBlur(Window window, bool enable, int gradientColor = unchecked((int)0x99000000))
        {
            var windowHelper = new WindowInteropHelper(window);
            var accent = new AccentPolicy();
            var accentStructSize = Marshal.SizeOf(accent);

            if (enable)
            {
                // Use Acrylic if available, otherwise BlurBehind
                accent.AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND;
                accent.GradientColor = gradientColor;
                // Note: If Redstone 4+ (1803), this color works. 
            }
            else
            {
                accent.AccentState = AccentState.ACCENT_DISABLED;
            }

            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = accentStructSize,
                Data = accentPtr
            };

            SetWindowCompositionAttribute(windowHelper.Handle, ref data);

            Marshal.FreeHGlobal(accentPtr);
        }
    }
}
