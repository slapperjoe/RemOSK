using System.Configuration;
using System.Data;
using System.Windows;
using RemOSK.Services;
using Application = System.Windows.Application;

namespace RemOSK
{
    public partial class App : Application
    {
        private TrayIconManager? _trayIconManager;
        private KeyboardWindowManager? _keyboardManager;

        private ConfigService? _configService;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            AttachConsole(-1); // Attach to parent console

            // Disable WPF tablet support to force touch-to-mouse conversion
            // This prevents the cursor from hiding on tablets/touch devices
            TouchToMouseHelper.DisableWPFTabletSupport();

            _configService = new ConfigService();
            _keyboardManager = new KeyboardWindowManager(_configService);
            _trayIconManager = new TrayIconManager(_keyboardManager, _configService);
            _trayIconManager.Initialize();
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            _trayIconManager?.Dispose();
            _keyboardManager?.Close();
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);
    }
}
