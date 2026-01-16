using System.IO;
using System.Text.Json;
using RemOSK.Models;

namespace RemOSK.Services
{
    public class LayoutLoader
    {
        public KeyboardLayout LoadLayout(string path)
        {
            if (!File.Exists(path))
            {
                 // Fallback or empty
                 return new KeyboardLayout();
            }

            try
            {
                var json = File.ReadAllText(path);
                var layout = JsonSerializer.Deserialize<KeyboardLayout>(json);
                return layout ?? new KeyboardLayout();
            }
            catch
            {
                return new KeyboardLayout();
            }
        }
    }
}
