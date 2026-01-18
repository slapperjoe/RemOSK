using System;
using System.Drawing;
using System.IO;

class Program
{
    static void Main()
    {
        string pngPath = @"d:\RemOSK\RemOSK\Resources\Icons\app_icon.png";
        string icoPath = @"d:\RemOSK\RemOSK\Resources\Icons\app.ico";
        
        using (var bitmap = new Bitmap(pngPath))
        {
            // Resize to standard icon size (256x256 max for ICO)
            using (var resized = new Bitmap(bitmap, new Size(256, 256)))
            {
                IntPtr hIcon = resized.GetHicon();
                using (var icon = Icon.FromHandle(hIcon))
                using (var fs = new FileStream(icoPath, FileMode.Create))
                {
                    icon.Save(fs);
                    Console.WriteLine($"Icon saved to {icoPath}");
                }
            }
        }
    }
}
