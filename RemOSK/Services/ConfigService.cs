using System;
using System.IO;
using System.Text.Json;

namespace RemOSK.Services
{
    public class AppConfig
    {
        public string LastUsedLayout { get; set; } = "DefaultTKL";
        public string MouseMode { get; set; } = "Trackpoint"; // "Off", "Trackpoint", "Trackpad"
        public bool EnableAcrylic { get; set; } = false;
        
        // Independent State
        public double LeftUiScale { get; set; } = 1.0;
        public double RightUiScale { get; set; } = 1.0;
        public double LeftWindowTop { get; set; } = -1;
        public double LeftWindowLeft { get; set; } = -1;
        public double RightWindowTop { get; set; } = -1;
        public double RightWindowLeft { get; set; } = -1;
        
        // Trackpoint window position
        public double TrackpointWindowTop { get; set; } = -1;
        public double TrackpointWindowLeft { get; set; } = -1;
        public double TrackpointUiScale { get; set; } = 1.0;
        
        // Click buttons window position
        public double ClickButtonsWindowTop { get; set; } = -1;
        public double ClickButtonsWindowLeft { get; set; } = -1;
        public double ClickButtonsUiScale { get; set; } = 1.0;
        
        public bool IsEditModeEnabled { get; set; } = false; // "Config Item for Size and Scale"
        public bool IsRdpMode { get; set; } = false; // RDP mode - mouse clicks disabled as they don't work over RDP
    }

    public class ConfigService
    {
        private string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".remosk", "config.json");
        public AppConfig CurrentConfig { get; private set; }

        public ConfigService()
        {
            CurrentConfig = LoadConfig();
        }

        private AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Config] Error loading config: {ex.Message}");
            }
            return new AppConfig();
        }

        public void SaveConfig()
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir) && dir != null)
                {
                    Directory.CreateDirectory(dir);
                }
                var json = JsonSerializer.Serialize(CurrentConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Config] Error saving config: {ex.Message}");
            }
        }

        public void ResetConfig()
        {
            CurrentConfig = new AppConfig();
            SaveConfig();
        }
    }
}
