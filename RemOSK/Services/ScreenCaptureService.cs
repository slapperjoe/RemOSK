using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace RemOSK.Services
{
    public class ScreenCaptureService
    {
        private static ScreenCaptureService? _instance;
        public static ScreenCaptureService Instance => _instance ??= new ScreenCaptureService();

        private ScreenCaptureService() { }

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        public BitmapSource? CaptureRegion(Rect region)
        {
            try
            {
                if (region.Width <= 0 || region.Height <= 0) return null;

                using (var bmp = new Bitmap((int)region.Width, (int)region.Height))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen((int)region.Left, (int)region.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
                    }

                    // Convert to BitmapSource
                    IntPtr hBitmap = bmp.GetHbitmap();
                    try
                    {
                        var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                            hBitmap,
                            IntPtr.Zero,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                            
                        source.Freeze(); // Make cross-thread accessible
                        return source;
                    }
                    finally
                    {
                        DeleteObject(hBitmap);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScreenCapture] Error: {ex.Message}");
                return null;
            }
        }
    }
}
