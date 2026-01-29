using System;
using System.Windows;
using RemOSK.Views;
using System.Windows.Media.Animation;
using System.Windows.Threading;

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
        
        // Inactivity transparency
        private DispatcherTimer? _inactivityTimer;
        private bool _isFadedOut;
        private const double FADED_OPACITY = 0.3;
        private const double NORMAL_OPACITY = 1.0;
        private bool _isInteracting;
        
        // Focus tracking - continuously track last "user" window (not tray/system)
        private IntPtr _lastUserForegroundWindow = IntPtr.Zero;
        private DispatcherTimer? _focusTrackingTimer;

        public KeyboardWindowManager(ConfigService configService)
        {
            _configService = configService;
            _layoutLoader = new LayoutLoader();
            _inputInjector = new InputInjector();
            _modifierManager = new ModifierStateManager(_inputInjector);
            
            // Initialize inactivity timer (5 seconds)
            _inactivityTimer = new DispatcherTimer();
            _inactivityTimer.Interval = TimeSpan.FromSeconds(5);
            _inactivityTimer.Tick += InactivityTimer_Tick;
            
            // Initialize focus tracking timer (every 100ms)
            _focusTrackingTimer = new DispatcherTimer();
            _focusTrackingTimer.Interval = TimeSpan.FromMilliseconds(100);
            _focusTrackingTimer.Tick += FocusTrackingTimer_Tick;
            _focusTrackingTimer.Start();
            
            InitializeTextPreview();

            ReloadLayout(_configService.CurrentConfig.LastUsedLayout);
        }
        
        private int _focusDebugCounter = 0;
        private void FocusTrackingTimer_Tick(object? sender, EventArgs e)
        {
            IntPtr current = GetForegroundWindow();
            if (current == IntPtr.Zero) return;
            
            string title = GetWindowTitle(current);
            
            // Debug: Log every 50th check to see what we're seeing
            _focusDebugCounter++;
                        
            // Skip our actual RemOSK windows (exact titles) and unknown windows
            // Don't skip VS Code just because it has "RemOSK" in the path!
            bool isOurWindow = title == "RemOSK Keyboard" || 
                               title == "RemOSK Trackpoint" || 
                               title == "RemOSK Clicks";
            if (isOurWindow || title == "(unknown)" || string.IsNullOrEmpty(title))
            {
                return;
            }
            
            // Skip if it's a shell/tray window
            if (title.Contains("Shell_TrayWnd") || title.Contains("Taskbar"))
                return;
            
            // Update tracked window and log when it changes
            if (current != _lastUserForegroundWindow)
            {
                Console.WriteLine($"[FOCUS TRACK] New user window: '{title}' (0x{current:X})");
                _lastUserForegroundWindow = current;
            }
        }

        public void ReloadLayout(string layoutName)
        {
             // Map layout name to file
             string fileName = "DefaultLayout.json"; // Fallback to TKL
             
             if (layoutName == "75%") fileName = "Layout75.json";
             else if (layoutName == "60%") fileName = "Layout60.json";
             else if (layoutName.StartsWith("Alice")) fileName = "AliceLayout.json";
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
            // Pause tracking during toggle to prevent race conditions
            _focusTrackingTimer?.Stop();
            
            // Capture the window we want to restore BEFORE anything else
            IntPtr windowToRestore = _lastUserForegroundWindow;
            string windowName = GetWindowTitle(windowToRestore);
            Console.WriteLine($"[FOCUS DIAG] === TOGGLE START === Window to restore: 0x{windowToRestore:X} = '{windowName}'");
            
            if (_isVisible)
            {
                Hide();
            }
            else
            {
                IntPtr currentFg = GetForegroundWindow();
                Console.WriteLine($"[FOCUS DIAG] Current foreground: 0x{currentFg:X} = '{GetWindowTitle(currentFg)}'");
                Show();
            }
            
            // Schedule focus restore with a small delay to let the tray click fully complete
            // The tray click steals focus AFTER our SetForegroundWindow, so we need to wait
            if (windowToRestore != IntPtr.Zero)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() =>
                    {
                        System.Threading.Thread.Sleep(200); // Wait for Tray/Taskbar to release focus
                        Console.WriteLine($"[FOCUS DIAG] Delayed restore to: '{windowName}'");
                        bool success = SetForegroundWindow(windowToRestore);
                        Console.WriteLine($"[FOCUS DIAG] SetForegroundWindow returned: {success}");
                        Console.WriteLine($"[FOCUS DIAG] Final foreground: '{GetWindowTitle(GetForegroundWindow())}'");
                        
                        // Resume tracking after restore completes
                        _focusTrackingTimer?.Start();
                    }));
            }
            else
            {
                // No window to restore, just resume tracking
                _focusTrackingTimer?.Start();
            }
            
            Console.WriteLine($"[FOCUS DIAG] === TOGGLE END ===");
        }
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);
        
        private static string GetWindowTitle(IntPtr hWnd)
        {
            const int nChars = 256;
            var buff = new System.Text.StringBuilder(nChars);
            if (GetWindowText(hWnd, buff, nChars) > 0)
                return buff.ToString();
            return "(unknown)";
        }


        public void Show()
        {
            Console.WriteLine("[FOCUS DIAG] >>> Show() ENTRY");
            RegisterActivity(); // Reset inactivity timer and restore opacity immediately
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
                
                // Edit mode events
                ((KeyboardWindow)_leftWindow).RequestExitEditMode += (s, e) =>
                {
                    _configService.CurrentConfig.IsEditModeEnabled = false;
                    _configService.SaveConfig();
                    UpdateEditMode();
                };
                
                ((KeyboardWindow)_leftWindow).RequestEnterEditMode += (s, e) =>
                {
                    Console.WriteLine("[Manager] Long-press on Left - entering edit mode");
                    _configService.CurrentConfig.IsEditModeEnabled = true;
                    _configService.SaveConfig();
                    UpdateEditMode();
                };
            }
            
            // Set Initial Position for Left Window
              // Set Initial Position for Left Window: align to bottom
              // Set Initial Position for Left Window: align to bottom (WorkArea.Bottom accounts for taskbar)
             double leftTop = SystemParameters.WorkArea.Bottom - _leftWindow.Height;
             // Ensure it doesn't go negative (if window is taller than screen?)
             if (leftTop < 0) leftTop = 0;
             
             _leftWindow.Top = leftTop;


            if (_rightWindow == null)
            {
                _rightWindow = new KeyboardWindow();
                
                // Initial Top Position (Right)
                // Vertical: Force align to bottom (WorkArea.Bottom accounts for taskbar)
                double rightTop = SystemParameters.WorkArea.Bottom - _rightWindow.Height;
                if (rightTop < 0) rightTop = 0;
                
                _rightWindow.Top = rightTop;

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
                
                ((KeyboardWindow)_rightWindow!).RequestEnterEditMode += (s, e) =>
                {
                    Console.WriteLine("[Manager] Long-press detected - entering edit mode");
                    _configService.CurrentConfig.IsEditModeEnabled = true;
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
            
            // Show Click Buttons Window when mouse mode is enabled
            if (_configService.CurrentConfig.MouseMode != "Off")
            {
                if (_clickButtonsWindow == null)
                {
                    _clickButtonsWindow = new ClickButtonsWindow();
                    
                    // Use saved position or default
                    if (_configService.CurrentConfig.ClickButtonsWindowLeft != -1)
                        _clickButtonsWindow.Left = _configService.CurrentConfig.ClickButtonsWindowLeft;
                    else
                        _clickButtonsWindow.Left = (SystemParameters.PrimaryScreenWidth / 2) + 70;
                    
                    if (_configService.CurrentConfig.ClickButtonsWindowTop != -1)
                        _clickButtonsWindow.Top = _configService.CurrentConfig.ClickButtonsWindowTop;
                    else
                        _clickButtonsWindow.Top = (SystemParameters.PrimaryScreenHeight / 2) + 100;
                    
                    // Apply saved scale
                    _clickButtonsWindow.SetScale(_configService.CurrentConfig.ClickButtonsUiScale);
                    _clickButtonsWindow.ScaleChanged += (s, scale) =>
                    {
                        _configService.CurrentConfig.ClickButtonsUiScale = scale;
                        _configService.SaveConfig();
                    };
                    
                    // Wire up click events (with activity registration and click-through)
                    _clickButtonsWindow.OnLeftClick += (s, e) => 
                    { 
                        RegisterActivity();
                        SetClickThrough(true);
                        try
                        {
                            // Use absolute position from cursor overlay with RDP viewport bounds
                            if (_cursorOverlay != null)
                            {
                                var pos = _cursorOverlay.CurrentPosition;
                                var bounds = _cursorOverlay.ScreenBounds;
                                _inputInjector.SendLeftClickAt(pos.X, pos.Y, bounds.Width, bounds.Height);
                            }
                            else
                            {
                                _inputInjector.SendLeftClick();
                            }
                        }
                        finally
                        {
                            SetClickThrough(false);
                        }
                    };
                    _clickButtonsWindow.OnMiddleClick += (s, e) => 
                    { 
                        RegisterActivity();
                        SetClickThrough(true);
                        try { _inputInjector.SendMiddleClick(); }
                        finally { SetClickThrough(false); }
                    };
                    _clickButtonsWindow.OnRightClick += (s, e) => 
                    {
                        RegisterActivity();
                        SetClickThrough(true);
                        try { _inputInjector.SendRightClick(); }
                        finally { SetClickThrough(false); }
                    };
                    _clickButtonsWindow.OnHoldToggle += (s, isHolding) =>
                    {
                        RegisterActivity();
                        if (isHolding)
                            _inputInjector.SendLeftButtonDown();
                        else
                            _inputInjector.SendLeftButtonUp();
                    };
                    
                    // Position persistence
                    _clickButtonsWindow.HorizontalPositionChanged += (s, left) =>
                    {
                        _configService.CurrentConfig.ClickButtonsWindowLeft = left;
                        _configService.SaveConfig();
                    };
                    _clickButtonsWindow.VerticalPositionChanged += (s, top) =>
                    {
                        _configService.CurrentConfig.ClickButtonsWindowTop = top;
                        _configService.SaveConfig();
                    };
                    
                    // Long-press to enter edit mode
                    _clickButtonsWindow.RequestEnterEditMode += (s, e) =>
                    {
                        Console.WriteLine("[Manager] Long-press on ClickButtons - entering edit mode");
                        _configService.CurrentConfig.IsEditModeEnabled = true;
                        _configService.SaveConfig();
                        UpdateEditMode();
                    };
                }
                _clickButtonsWindow.Show();
            }
            
            // Apply Acrylic State
            UpdateAcrylicState();
            
            // --- Overlap Prevention & Auto-Scaling ---
            // "In this scenario when having to move left... also need both... scale smaller until they don't overlap"
            if (_leftWindow != null && _rightWindow != null)
            {
                var lWin = (KeyboardWindow)_leftWindow;
                var rWin = (KeyboardWindow)_rightWindow;
                
                double lScale = _configService.CurrentConfig.LeftUiScale;
                double rScale = _configService.CurrentConfig.RightUiScale;
                bool needsRescale = false;

                // Resolve intended Left Target (default to 0 if not set)
                double finalTargetLeft = 0;
                if (_configService.CurrentConfig.LeftWindowLeft != -1) 
                    finalTargetLeft = _configService.CurrentConfig.LeftWindowLeft;

                // Iterative Scaling Step-down (2% per step)
                // "Go back to the old method and make it 2%"
                for(int i=0; i<100; i++) 
                {
                    double lWidth = lWin.GetExpectedWidth(lScale);
                    double rWidth = rWin.GetExpectedWidth(rScale);
                    
                    // User Intended Right Position
                    double userRightPos = _configService.CurrentConfig.RightWindowLeft;
                    
                    // Clamped Right Position (The "stay on screen" logic)
                    double clampedRightPos = SystemParameters.PrimaryScreenWidth - rWidth;
                    
                    // Effective Right Position: Use User's unless it's off-screen
                    double effectiveRight = (userRightPos != -1) ? userRightPos : clampedRightPos;
                    if (effectiveRight + rWidth > SystemParameters.PrimaryScreenWidth)
                        effectiveRight = SystemParameters.PrimaryScreenWidth - rWidth;
                        
                    // Check Overlap
                    double lRightEdge = finalTargetLeft + lWidth;
                    
                    if (lRightEdge > effectiveRight)
                    {
                         needsRescale = true;
                         lScale *= 0.98; // Reduce by 2%
                         rScale *= 0.98;
                         
                         // Safety break for min scale
                         if (lScale < 0.3 || rScale < 0.3) break; 
                    }
                    else
                    {
                        break; // Fits!
                    }
                }
                
                if (needsRescale)
                {
                    Console.WriteLine($"[Manager] Overlap detected. Auto-scaled down to L:{lScale:F2}, R:{rScale:F2}");
                    lWin.SetScale(lScale);
                    rWin.SetScale(rScale);
                }
            }

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

                // Ensure right window is fully on-screen (move left if extending off-screen)
                if (targetRight + right.Width > SystemParameters.PrimaryScreenWidth)
                {
                    targetRight = SystemParameters.PrimaryScreenWidth - right.Width;
                }

                // Vertical: Force align to bottom (resolution safety & taskbar aware)
                double forcedTop = SystemParameters.WorkArea.Bottom - right.Height;
                if (forcedTop < 0) forcedTop = 0;
                right.Top = forcedTop;

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
            _textPreviewController?.Start();
        }

        public void UpdateEditMode()
        {
            bool enabled = _configService.CurrentConfig.IsEditModeEnabled;
            if (_leftWindow != null) ((KeyboardWindow)_leftWindow!).SetEditMode(enabled);
            if (_rightWindow != null) ((KeyboardWindow)_rightWindow!).SetEditMode(enabled);
            
            if (_trackpointWindow != null) ((TrackpointWindow)_trackpointWindow!).SetEditMode(enabled);
            _clickButtonsWindow?.SetEditMode(enabled);

            // Disable inactivity timer in Edit Mode
            if (enabled)
            {
                _inactivityTimer?.Stop();
                RestoreWindows(); // Ensure we are fully visible while editing
            }
            else
            {
                _inactivityTimer?.Start();
                RegisterActivity(); // Reset timer
            }
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
        private CursorOverlay? _cursorOverlay;
        private ClickButtonsWindow? _clickButtonsWindow;

        public void UpdateMouseMode()
        {
            string mode = _configService.CurrentConfig.MouseMode;
            
            if (mode != "Off")
            {
                if (_trackpointWindow == null)
                {
                    var newWin = new TrackpointWindow();
                    
                    // Use saved position or default center
                    if (_configService.CurrentConfig.TrackpointWindowLeft != -1)
                        newWin.Left = _configService.CurrentConfig.TrackpointWindowLeft;
                    else
                        newWin.Left = (SystemParameters.PrimaryScreenWidth / 2) - (newWin.Width / 2);
                    
                    if (_configService.CurrentConfig.TrackpointWindowTop != -1)
                        newWin.Top = _configService.CurrentConfig.TrackpointWindowTop;
                    else
                        newWin.Top = (SystemParameters.PrimaryScreenHeight / 2) + 100;
                    
                    // Apply saved scale
                    newWin.SetScale(_configService.CurrentConfig.TrackpointUiScale);
                    newWin.ScaleChanged += (s, scale) =>
                    {
                        _configService.CurrentConfig.TrackpointUiScale = scale;
                        _configService.SaveConfig();
                    };
                    
                    newWin.OnMove += Window_OnTrackpointMove;
                    
                    // Transparency Logic
                    newWin.OnInteractionStart += (s, e) => 
                    {
                        SetKeyboardTransparency(0.2);
                        RegisterActivity();
                    };
                    newWin.OnInteractionEnd += (s, e) => SetKeyboardTransparency(1.0);
                    

                    
                    // Position persistence
                    newWin.HorizontalPositionChanged += (s, left) =>
                    {
                        _configService.CurrentConfig.TrackpointWindowLeft = left;
                        _configService.SaveConfig();
                    };
                    newWin.VerticalPositionChanged += (s, top) =>
                    {
                        _configService.CurrentConfig.TrackpointWindowTop = top;
                        _configService.SaveConfig();
                    };
                    
                    // Long-press to enter edit mode
                    newWin.RequestEnterEditMode += (s, e) =>
                    {
                        Console.WriteLine("[Manager] Long-press on Trackpoint - entering edit mode");
                        _configService.CurrentConfig.IsEditModeEnabled = true;
                        _configService.SaveConfig();
                        UpdateEditMode();
                    };
                    
                    // Create cursor overlay for touch mode
                    if (_cursorOverlay == null)
                    {
                        _cursorOverlay = new CursorOverlay();
                        // _cursorOverlay.OnTextContextAvailable += CursorOverlay_OnTextContextAvailable; // Logic moved to TextPreviewController
                    }
                    
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
                _cursorOverlay?.DisableOverlay();
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
            _trackpointWindow?.Hide();
            _cursorOverlay?.DisableOverlay();
            _clickButtonsWindow?.Hide();
            _textPreviewController?.Stop();

            _isVisible = false;
        }

        public void Close()
        {
            if (_leftWindow != null) ((KeyboardWindow)_leftWindow).OnKeyPressed -= Window_OnKeyPressed;
            if (_rightWindow != null) ((KeyboardWindow)_rightWindow).OnKeyPressed -= Window_OnKeyPressed;
            
            _leftWindow?.Close();
            _rightWindow?.Close();
            _trackpointWindow?.Close();
            _clickButtonsWindow?.Close();
            _clickButtonsWindow?.Close();
            _textPreviewController?.Dispose();
            
            _leftWindow = null;
            _rightWindow = null;
            _trackpointWindow = null;
            _clickButtonsWindow = null;
            _textPreviewController = null;
        }

        private void Window_OnKeyPressed(object? sender, RemOSK.Controls.KeyButton e)
        {
            // Reset inactivity timer - if waking up, consume this touch
            if (RegisterActivity())
            {
                Console.WriteLine("[Manager] Consumed wake-up touch, skipping key press");
                return;
            }
            
            // Manually flash since we removed auto-flash to support wake-up logic
            e.Flash();
            
            Console.WriteLine($"[Manager] Key Pressed: {e.Label} (VK: {e.VirtualKeyCode})");
            // Use Modifier Manager to handle key
            _modifierManager.HandleKey(e.VirtualKeyCode, true);
        }

        private DispatcherTimer? _cursorHideTimer;

        private void Window_OnTrackpointMove(object? sender, System.Windows.Vector vector)
        {
            RegisterActivity(); // Reset inactivity timer
            
            // Enable cursor overlay when trackpoint is used
            _cursorOverlay?.EnableOverlay();
            
            // Manage Auto-Hide Timer (2 seconds persistence)
            if (_cursorHideTimer == null)
            {
                _cursorHideTimer = new DispatcherTimer();
                _cursorHideTimer.Interval = TimeSpan.FromSeconds(2);
                _cursorHideTimer.Tick += (s, e) =>
                {
                    _cursorHideTimer.Stop();
                    // Only hide if we aren't currently interacting (dragging)
                    // The trackpoint window sends OnInteractionEnd which handles opacity,
                    // but we manage cursor visibility here to keep it alive a bit longer.
                    _cursorOverlay?.DisableOverlay();
                };
            }
            _cursorHideTimer.Stop();
            _cursorHideTimer.Start();
            
            // Move our custom cursor (updates local state)
            if (_cursorOverlay != null)
            {
                _cursorOverlay.MoveCursor(vector.X, vector.Y);
                
                // Now send the ACTUAL system move via InputInjector
                // This is crucial for RDP - SendInput triggers the remote session to acknowledge mouse movement
                // which then triggers window hit-testing and cursor shape updates.
                var pos = _cursorOverlay.CurrentPosition;
                var bounds = _cursorOverlay.ScreenBounds;
                _inputInjector.SendMouseMoveTo(pos.X, pos.Y, bounds.Width, bounds.Height);
                
                // Force shape update is now handled inside MoveCursor via simulation
                // _cursorOverlay.UpdateCursorShape();
            }
            }

        
        private TextPreviewController? _textPreviewController;

        public void InitializeTextPreview()
        {
            if (_textPreviewController == null)
            {
                _textPreviewController = new TextPreviewController(this);
                _textPreviewController.OnTextPreviewUpdated += (context) => 
                {
                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        if (_leftWindow != null)
                        {
                            ((KeyboardWindow)_leftWindow).UpdatePreviewText(context);
                        }
                        if (_rightWindow != null)
                        {
                            ((KeyboardWindow)_rightWindow).UpdatePreviewText(context);
                        }
                    });
                };
                _textPreviewController.OnActivityDetected += () => 
                {
                    Dispatcher.CurrentDispatcher.Invoke(() => RegisterActivity());
                };
                _textPreviewController.Start();
            }
        }
        
        // Old handlers removed
        
        // Inactivity transparency methods
        private void InactivityTimer_Tick(object? sender, EventArgs e)
        {
            // Safety check: Don't hide if in edit mode
            if (_configService.CurrentConfig.IsEditModeEnabled)
            {
                _inactivityTimer?.Stop(); // Fix state
                return;
            }

            _inactivityTimer?.Stop();
            
            if (!_isFadedOut)
            {
                // Stage 1: Fade out
                FadeOutWindows();
                // Restart timer for Stage 2 (Hide)
                _inactivityTimer?.Start();
            }
            else
            {
                // Stage 2: Hide (Slide off)
                Console.WriteLine("[Manager] Auto-hiding windows due to extended inactivity");
                Hide();
            }
        }
        
        /// <summary>
        /// Registers user activity. Returns true if this was a "wake up" touch that should be consumed.
        /// </summary>
        public bool RegisterActivity()
        {
            // Restart the timer
            _inactivityTimer?.Stop();
            _inactivityTimer?.Start();
            
            // If faded, restore opacity and return true to indicate this touch should be consumed
            if (_isFadedOut)
            {
                if (!_isInteracting)
                {
                    RestoreWindows();
                }
                else
                {
                    // If interacting, we are "awake" but keeping low opacity
                    // Just clear the faded flag so we don't fade out again immediately
                    _isFadedOut = false;
                }
                return true; // Consume this touch - it was just to wake up
            }
            return false; // Normal touch, don't consume
        }
        
        private void FadeOutWindows()
        {
            _isFadedOut = true;
            Console.WriteLine("[Manager] Fading windows due to inactivity");
            
            _leftWindow?.Dispatcher.Invoke(() => _leftWindow.Opacity = FADED_OPACITY);
            _rightWindow?.Dispatcher.Invoke(() => _rightWindow.Opacity = FADED_OPACITY);
            _trackpointWindow?.Dispatcher.Invoke(() => _trackpointWindow.Opacity = FADED_OPACITY);
            _clickButtonsWindow?.Dispatcher.Invoke(() => _clickButtonsWindow.Opacity = FADED_OPACITY);
        }
        
        private void RestoreWindows()
        {
            _isFadedOut = false;
            Console.WriteLine("[Manager] Restoring window opacity");
            
            _leftWindow?.Dispatcher.Invoke(() => _leftWindow.Opacity = NORMAL_OPACITY);
            _rightWindow?.Dispatcher.Invoke(() => _rightWindow.Opacity = NORMAL_OPACITY);
            _trackpointWindow?.Dispatcher.Invoke(() => _trackpointWindow.Opacity = NORMAL_OPACITY);
            _clickButtonsWindow?.Dispatcher.Invoke(() => _clickButtonsWindow.Opacity = NORMAL_OPACITY);
        }
        
        /// <summary>
        /// Set click-through mode for all windows. When enabled, virtual mouse clicks pass through to underlying apps.
        /// </summary>
        public void SetClickThrough(bool enabled)
        {
            if (enabled)
            {
                (_leftWindow as KeyboardWindow)?.EnableClickThrough();
                (_rightWindow as KeyboardWindow)?.EnableClickThrough();
                (_trackpointWindow as TrackpointWindow)?.EnableClickThrough();
                (_clickButtonsWindow as ClickButtonsWindow)?.EnableClickThrough();
            }
            else
            {
                (_leftWindow as KeyboardWindow)?.DisableClickThrough();
                (_rightWindow as KeyboardWindow)?.DisableClickThrough();
                (_trackpointWindow as TrackpointWindow)?.DisableClickThrough();
                (_clickButtonsWindow as ClickButtonsWindow)?.DisableClickThrough();
            }
        }

        
        public void SetKeyboardTransparency(double opacity)
        {
            if (_isFadedOut) return; // Don't override fade-out
            
            _isInteracting = (opacity < 1.0);
            
            _leftWindow?.Dispatcher.Invoke(() => _leftWindow.Opacity = opacity);
            _rightWindow?.Dispatcher.Invoke(() => _rightWindow.Opacity = opacity);
        }
    }
}
