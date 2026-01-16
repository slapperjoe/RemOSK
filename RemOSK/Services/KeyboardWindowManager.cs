using System.Windows;
using RemOSK.Views;
using System.Windows.Media.Animation;

namespace RemOSK.Services
{
using RemOSK.Models;

    public class KeyboardWindowManager
    {
        private Window? _leftWindow;
        private Window? _rightWindow;
        private bool _isVisible;
        
        private readonly LayoutLoader _layoutLoader;
        private readonly InputInjector _inputInjector;
        private readonly ConfigService _configService;
        private readonly ModifierStateManager _modifierManager;
        private KeyboardLayout _currentLayout = null!;

        public KeyboardWindowManager(ConfigService configService)
        {
            _configService = configService;
            _layoutLoader = new LayoutLoader();
            _inputInjector = new InputInjector();
            _modifierManager = new ModifierStateManager(_inputInjector);

            ReloadLayout(_configService.CurrentConfig.LastUsedLayout);
        }

        public void ReloadLayout(string layoutName)
        {
             // Map layout name to file
             string fileName = "DefaultLayout.json"; // Fallback to TKL
             
             if (layoutName == "75%") fileName = "Layout75.json";
             else if (layoutName == "60%") fileName = "Layout60.json";
             else fileName = "DefaultLayout.json";
            
            var layoutPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", fileName);
            _currentLayout = _layoutLoader.LoadLayout(layoutPath);

            // If windows are open, refresh them
            if (_isVisible)
            {
               Close(); 
               Show();
            }
        }

        public void ToggleVisibility()
        {
            if (_isVisible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }



        public void Show()
        {
            if (_leftWindow == null)
            {
                _leftWindow = new KeyboardWindow();
                // Initial Top Position (Will be synced/overwritten if necessary, but good to have default)
                // Actually, let's just create it. Position is set later or synced.
                // _leftWindow.Top = SystemParameters.PrimaryScreenHeight / 2 - (_leftWindow.Height / 2);
                
                ((KeyboardWindow)_leftWindow).LoadKeys(_currentLayout.LeftKeys);
                ((KeyboardWindow)_leftWindow).OnKeyPressed += Window_OnKeyPressed;
                
                // Scale Logic for Left Window
                ((KeyboardWindow)_leftWindow).SetScale(_configService.CurrentConfig.LeftUiScale);
                
                // Vertical Move Logic (Left)
                ((KeyboardWindow)_leftWindow).VerticalPositionChanged += (s, top) =>
                {
                    _configService.CurrentConfig.LeftWindowTop = top;
                    _configService.SaveConfig();
                };
                
                // Horizontal Move Logic (Left)
                ((KeyboardWindow)_leftWindow).HorizontalPositionChanged += (s, left) =>
                {
                    _configService.CurrentConfig.LeftWindowLeft = left;
                    _configService.SaveConfig();
                };

                ((KeyboardWindow)_leftWindow).ScaleChanged += (s, scale) =>
                {
                    _configService.CurrentConfig.LeftUiScale = scale;
                    _configService.SaveConfig();
                };

                _modifierManager.StateChanged += (s, e) => ((KeyboardWindow)_leftWindow).OnModifierStateChanged(_modifierManager);
            }
            
            // Set Initial Position for Left Window
             if (_configService.CurrentConfig.LeftWindowTop != -1)
             {
                 _leftWindow.Top = _configService.CurrentConfig.LeftWindowTop;
             }
             else
             {
                  _leftWindow.Top = SystemParameters.PrimaryScreenHeight / 2 - (_leftWindow.Height / 2);
             }


            if (_rightWindow == null)
            {
                _rightWindow = new KeyboardWindow();
                
                // Initial Top Position (Right)
                double startTop = _configService.CurrentConfig.RightWindowTop;
                if (startTop == -1) startTop = SystemParameters.PrimaryScreenHeight / 2 - (_rightWindow.Height / 2);
                _rightWindow.Top = startTop;

                ((KeyboardWindow)_rightWindow).LoadKeys(_currentLayout.RightKeys, true); // Right Aligned
                ((KeyboardWindow)_rightWindow).OnKeyPressed += Window_OnKeyPressed;
                
                // Scale Logic for Right Window
                ((KeyboardWindow)_rightWindow).SetScale(_configService.CurrentConfig.RightUiScale);
                
                // Vertical Move Logic (Right)
                ((KeyboardWindow)_rightWindow).VerticalPositionChanged += (s, top) =>
                {
                    _configService.CurrentConfig.RightWindowTop = top;
                    _configService.SaveConfig();
                };
                
                // Horizontal Move Logic (Right)
                ((KeyboardWindow)_rightWindow).HorizontalPositionChanged += (s, left) =>
                {
                    _configService.CurrentConfig.RightWindowLeft = left;
                    _configService.SaveConfig();
                };
                
                ((KeyboardWindow)_rightWindow).ScaleChanged += (s, scale) =>
                {
                     _configService.CurrentConfig.RightUiScale = scale;
                     _configService.SaveConfig();
                };

                _modifierManager.StateChanged += (s, e) => ((KeyboardWindow)_rightWindow!).OnModifierStateChanged(_modifierManager);
                
                ((KeyboardWindow)_rightWindow!).RequestExitEditMode += (s, e) =>
                {
                    _configService.CurrentConfig.IsEditModeEnabled = false;
                    _configService.SaveConfig();
                    UpdateEditMode();
                };
            }
            
            // Also subscribe Left Window
            if (_leftWindow != null)
            {
                 ((KeyboardWindow)_leftWindow!).RequestExitEditMode += (s, e) =>
                {
                    _configService.CurrentConfig.IsEditModeEnabled = false;
                    _configService.SaveConfig();
                    UpdateEditMode();
                };
            }
            
            // Mouse Window (Trackpoint/Trackpad)
            UpdateMouseMode();
            
            // FORCE Mouse Window Show if enabled (Fixes "will not reappear" bug)
            if (_configService.CurrentConfig.MouseMode != "Off" && _trackpointWindow != null)
            {
                _trackpointWindow.Show();
            }
            
            // Apply Acrylic State
            UpdateAcrylicState();
            
            // Apply Edit Mode State
            UpdateEditMode();

            // Animate Left Window
            if (_leftWindow != null)
            {
                var left = _leftWindow;
                double targetLeft = 0;
                if (_configService.CurrentConfig.LeftWindowLeft != -1) targetLeft = _configService.CurrentConfig.LeftWindowLeft;
                
                left.Left = -left.Width; // Start off-screen
                left.Show();
                var leftAnim = new DoubleAnimation(targetLeft, TimeSpan.FromMilliseconds(250)) { EasingFunction = new QuadraticEase() };
                leftAnim.Completed += (s, e) =>
                {
                    // Release animation lock so manual Left assignment works
                    left.BeginAnimation(Window.LeftProperty, null);
                };
                left.BeginAnimation(Window.LeftProperty, leftAnim);
            }

            // Animate Right Window
            if (_rightWindow != null)
            {
                var right = _rightWindow;
                double targetRight = SystemParameters.PrimaryScreenWidth - right.Width;
                if (_configService.CurrentConfig.RightWindowLeft != -1) targetRight = _configService.CurrentConfig.RightWindowLeft;

                right.Left = SystemParameters.PrimaryScreenWidth; // Start off-screen
                right.Show();
                var rightAnim = new DoubleAnimation(targetRight, TimeSpan.FromMilliseconds(250)) { EasingFunction = new QuadraticEase() };
                rightAnim.Completed += (s, e) =>
                {
                    // Release animation lock so manual Left assignment works
                    right.BeginAnimation(Window.LeftProperty, null);
                };
                right.BeginAnimation(Window.LeftProperty, rightAnim);
            }

            _isVisible = true;
        }

        public void UpdateEditMode()
        {
            bool enabled = _configService.CurrentConfig.IsEditModeEnabled;
            if (_leftWindow != null) ((KeyboardWindow)_leftWindow!).SetEditMode(enabled);
            if (_rightWindow != null) ((KeyboardWindow)_rightWindow!).SetEditMode(enabled);
            
            if (_trackpointWindow != null) ((TrackpointWindow)_trackpointWindow!).SetEditMode(enabled);
        }

        public void UpdateAcrylicState()
        {
            bool enable = _configService.CurrentConfig.EnableAcrylic;
            
            // Solid Mode: #99111111 (Matches XAML)
            var solidColor = (System.Windows.Media.SolidColorBrush)(new System.Windows.Media.BrushConverter().ConvertFrom("#99111111")!);
            
            var left = _leftWindow;
            if (left != null)
            {
                var border = ((KeyboardWindow)left).FindName("MainBorder") as System.Windows.Controls.Border;
                if (border != null)
                {
                    border.Background = enable ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0)) : solidColor;
                }
                WindowBlurHelper.EnableBlur(left, enable, unchecked((int)0x40000000));
            }

            var right = _rightWindow;
            if (right != null)
            {
                var border = ((KeyboardWindow)right).FindName("MainBorder") as System.Windows.Controls.Border;
                if (border != null)
                {
                    border.Background = enable ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0)) : solidColor;
                }
                WindowBlurHelper.EnableBlur(right, enable, unchecked((int)0x40000000));
            }
        }

        private Window? _trackpointWindow;

        public void UpdateMouseMode()
        {
            string mode = _configService.CurrentConfig.MouseMode;
            
            if (mode != "Off")
            {
                if (_trackpointWindow == null)
                {
                    var newWin = new TrackpointWindow();
                    // Default Center
                    newWin.Left = (SystemParameters.PrimaryScreenWidth / 2) - (newWin.Width / 2);
                    newWin.Top = (SystemParameters.PrimaryScreenHeight / 2) + 100; 
                    
                    newWin.OnMove += Window_OnTrackpointMove;
                    newWin.OnLeftClick += (s, e) => _inputInjector.SendLeftClick();
                    newWin.OnRightClick += (s, e) => _inputInjector.SendRightClick();
                    
                    _trackpointWindow = newWin;
                }
                
                var tpWindow = (TrackpointWindow)_trackpointWindow!;
                tpWindow.SetMode(mode);

                if (_isVisible || tpWindow.IsVisible == false) 
                {
                    if(_isVisible || _leftWindow?.IsVisible == true) tpWindow.Show();
                }
            }
            else
            {
                _trackpointWindow?.Hide();
            }
        }

        public void Hide()
        {
            var left = _leftWindow;
            if (left != null)
            {
                var leftAnim = new DoubleAnimation(-left.Width, TimeSpan.FromMilliseconds(250)) { EasingFunction = new QuadraticEase() };
                leftAnim.Completed += (s, e) => left.Hide();
                left.BeginAnimation(Window.LeftProperty, leftAnim);
            }

            var right = _rightWindow;
            if (right != null)
            {
                var rightAnim = new DoubleAnimation(SystemParameters.PrimaryScreenWidth, TimeSpan.FromMilliseconds(250)) { EasingFunction = new QuadraticEase() };
                rightAnim.Completed += (s, e) => right.Hide();
                right.BeginAnimation(Window.LeftProperty, rightAnim);
            }
            
            _trackpointWindow?.Hide();

            _isVisible = false;
        }

        public void Close()
        {
            if (_leftWindow != null) ((KeyboardWindow)_leftWindow).OnKeyPressed -= Window_OnKeyPressed;
            if (_rightWindow != null) ((KeyboardWindow)_rightWindow).OnKeyPressed -= Window_OnKeyPressed;
            
            _leftWindow?.Close();
            _rightWindow?.Close();
            _trackpointWindow?.Close();
            
            _leftWindow = null;
            _rightWindow = null;
            _trackpointWindow = null;
        }

        private void Window_OnKeyPressed(object? sender, RemOSK.Controls.KeyButton e)
        {
            Console.WriteLine($"[Manager] Key Pressed: {e.Label} (VK: {e.VirtualKeyCode})");
            // Use Modifier Manager to handle key
            _modifierManager.HandleKey(e.VirtualKeyCode, true);
        }

        private void Window_OnTrackpointMove(object? sender, System.Windows.Vector vector)
        {
            _inputInjector.SendMouseMove((int)vector.X, (int)vector.Y);
        }
    }
}
