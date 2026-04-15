using System;
using System.Runtime.InteropServices;

namespace RemOSK.Services
{
    public static class ScreenOrientationHelper
    {
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        public static bool IsPortrait()
        {
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);
            return screenHeight > screenWidth;
        }

        public static bool IsLandscape()
        {
            return !IsPortrait();
        }

        public static string GetOrientationName()
        {
            return IsPortrait() ? "Portrait" : "Landscape";
        }
    }
}
