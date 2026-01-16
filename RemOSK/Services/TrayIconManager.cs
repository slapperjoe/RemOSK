using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace RemOSK.Services
{
    public class TrayIconManager : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly KeyboardWindowManager _windowManager;

        private readonly ConfigService _configService;

        public TrayIconManager(KeyboardWindowManager windowManager, ConfigService configService)
        {
            _windowManager = windowManager;
            _configService = configService;
            _notifyIcon = new NotifyIcon();
        }

        public void Initialize()
        {
            _notifyIcon.Icon = SystemIcons.Application; // Default icon
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "RemOSK";
            
            // Handle Left Click
            _notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    _windowManager.ToggleVisibility();
                }
            };

            // Context Menu
            var contextMenu = new ContextMenuStrip();
            
            // Mouse Input Submenu
            var mouseMenu = new ToolStripMenuItem("Mouse Input");
            var modes = new[] { "Off", "Trackpoint", "Trackpad" };
            
            foreach (var mode in modes)
            {
                var item = new ToolStripMenuItem(mode, null, (s, e) =>
                {
                    _configService.CurrentConfig.MouseMode = mode;
                    _configService.SaveConfig();
                    _windowManager.UpdateMouseMode();
                    
                    // Update Checks
                    foreach (var dItem in mouseMenu.DropDownItems)
                    {
                        if (dItem is ToolStripMenuItem mItem)
                        {
                            mItem.Checked = mItem.Text == mode;
                        }
                    }
                })
                {
                    Checked = _configService.CurrentConfig.MouseMode == mode,
                    CheckOnClick = false // Managed manually
                };
                mouseMenu.DropDownItems.Add(item);
            }
            contextMenu.Items.Add(mouseMenu);
            
            // Dynamic Background Toggle
            var acrylicItem = new ToolStripMenuItem("Dynamic Background", null, (s, e) =>
            {
                 var enable = !_configService.CurrentConfig.EnableAcrylic;
                 _configService.CurrentConfig.EnableAcrylic = enable;
                 _configService.SaveConfig();
                 _windowManager.UpdateAcrylicState();
            })
            {
                Checked = _configService.CurrentConfig.EnableAcrylic,
                CheckOnClick = true
            };
            contextMenu.Items.Add(acrylicItem);

            // "Edit Mode" Toggle (Move/Scale)
            var editModeItem = new ToolStripMenuItem("Edit Mode (Move/Scale)", null, (s, e) =>
            {
                var enable = !_configService.CurrentConfig.IsEditModeEnabled;
                _configService.CurrentConfig.IsEditModeEnabled = enable;
                _configService.SaveConfig();
                _windowManager.UpdateEditMode();
            })
            {
                Checked = _configService.CurrentConfig.IsEditModeEnabled,
                CheckOnClick = true
            };
            contextMenu.Items.Add(editModeItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            // Layouts Submenu
            var layoutMenu = new ToolStripMenuItem("Layouts");
            layoutMenu.DropDownItems.Add("TKL", null, (s,e) => SwitchLayout("TKL"));
            layoutMenu.DropDownItems.Add("75%", null, (s,e) => SwitchLayout("75%"));
            layoutMenu.DropDownItems.Add("60%", null, (s,e) => SwitchLayout("60%"));
            
            contextMenu.Items.Add(layoutMenu);
            contextMenu.Items.Add("-");

            contextMenu.Items.Add("Reset to Factory Settings", null, (s, e) =>
            {
                if (System.Windows.Forms.MessageBox.Show("Are you sure you want to reset all settings? The app will restart.", "Factory Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    _configService.ResetConfig();
                    System.Windows.Forms.Application.Restart();
                    System.Windows.Application.Current.Shutdown();
                }
            });

            contextMenu.Items.Add("Exit", null, (s, e) =>
            {
                _windowManager.Close();
                System.Windows.Application.Current.Shutdown();
            });

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void SwitchLayout(string layoutName)
        {
            _configService.CurrentConfig.LastUsedLayout = layoutName;
            _configService.SaveConfig();
            _windowManager.ReloadLayout(layoutName);
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
