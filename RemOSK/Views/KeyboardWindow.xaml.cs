using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using RemOSK.Controls;
using RemOSK.Models;

using RemOSK.Services;

namespace RemOSK.Views
{
    public partial class KeyboardWindow : Window, IModifierObserver
    {
        public event EventHandler<KeyButton>? OnKeyPressed;
        public event EventHandler<Vector>? OnTrackpointMove;
        public event EventHandler? RequestExitEditMode; // New Event
        
        // Dictionary to track keys by virtual key code for quick updates
        private Dictionary<int, KeyButton> _keyMap = new Dictionary<int, KeyButton>();



        public void OnModifierStateChanged(ModifierStateManager manager)
        {
            UpdateKeyHighlight(160, manager.IsShiftActive); // LShift
            UpdateKeyHighlight(161, manager.IsShiftActive); // RShift
            
            UpdateKeyHighlight(162, manager.IsCtrlActive);  // LCtrl
            UpdateKeyHighlight(163, manager.IsCtrlActive);  // RCtrl
            
            UpdateKeyHighlight(164, manager.IsAltActive);   // LAlt
            UpdateKeyHighlight(165, manager.IsAltActive);   // RAlt
            
            UpdateKeyHighlight(91, manager.IsWinActive);    // LWin

            // Update Labels
            bool caps = (GetKeyState(0x14) & 0x0001) != 0;
            UpdateKeyLabels(manager.IsShiftActive, caps);
        }

        private void UpdateKeyHighlight(int vk, bool isActive)
        {
            if (_keyMap.TryGetValue(vk, out var btn))
            {
                btn.Background = isActive ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 100, 200)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51));
            }
        }

        public void AddTrackpoint(int x, int y)
        {
            var tp = new TrackpointControl();
            Canvas.SetLeft(tp, x);
            Canvas.SetTop(tp, y);
            tp.MoveRequested += (s, vector) => OnTrackpointMove?.Invoke(this, vector);
            KeysCanvas.Children.Add(tp);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        public event EventHandler<double>? ScaleChanged;
        public event EventHandler<double>? VerticalPositionChanged;
        public event EventHandler<double>? HorizontalPositionChanged;

        // Current Scale Factor
        private double _currentScale = 1.0;
        public double CurrentScale => _currentScale;

        public KeyboardWindow()
        {
            InitializeComponent();
        }

        public void SetDragHandleVisibility(bool visible)
        {
            // No-op: Handles removed in favor of gestures
        }

        public double GetExpectedWidth(double scale)
        {
             return (_unscaledWidth + 10) * scale + 20; 
        }

        public void SetScale(double scale)
        {
            _currentScale = scale;
            WindowScaleTransform.ScaleX = scale;
            WindowScaleTransform.ScaleY = scale;
            
            // Re-calculate window size based on content and new scale
            UpdateWindowSize();
        }
        // Gesture state - track in SCREEN coordinates to avoid feedback loop
        private System.Windows.Point _gestureStartScreenPoint;
        private double _startWindowTop;
        private double _startWindowLeft;
        private double _startScale;
        private bool _isGestureActive;

        private void MainBorder_ManipulationStarting(object sender, System.Windows.Input.ManipulationStartingEventArgs e)
        {
            // Cancel manipulation if user touches a button (let button handle the click)
            if (e.OriginalSource is DependencyObject source)
            {
                var parent = source;
                while (parent != null && parent != this)
                {
                    if (parent is KeyButton || parent is System.Windows.Controls.Button || parent is System.Windows.Controls.Primitives.ButtonBase)
                    {
                        e.Cancel(); 
                        e.Handled = false; 
                        return;
                    }
                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                }
            }

            e.ManipulationContainer = this;
            e.Mode = System.Windows.Input.ManipulationModes.All;
            e.IsSingleTouchEnabled = true;
        }

        private void MainBorder_ManipulationInertiaStarting(object sender, System.Windows.Input.ManipulationInertiaStartingEventArgs e)
        {
            // Stop inertia immediately
            e.TranslationBehavior.DesiredDeceleration = 10000.0;
            e.ExpansionBehavior.DesiredDeceleration = 10000.0;
            e.Handled = true;
        }

        private void ExitEditModeButton_Click(object sender, RoutedEventArgs e)
        {
            RequestExitEditMode?.Invoke(this, EventArgs.Empty);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void MainBorder_ManipulationStarted(object sender, System.Windows.Input.ManipulationStartedEventArgs e)
        {
            // Capture start position in SCREEN coordinates to avoid feedback loop
            _gestureStartScreenPoint = PointToScreen(e.ManipulationOrigin);
            _startWindowTop = this.Top;
            _startWindowLeft = this.Left;
            _startScale = _currentScale;
            _isGestureActive = true;
        }

        private void MainBorder_ManipulationDelta(object sender, System.Windows.Input.ManipulationDeltaEventArgs e)
        {
            if (!_isGestureActive)
            {
                e.Complete();
                return;
            }

            if (e.IsInertial)
            {
                e.Complete();
                _isGestureActive = false;
                return;
            }

            // Get CURRENT touch point in SCREEN coordinates
            var currentScreenPoint = PointToScreen(e.ManipulationOrigin);
            
            // Calculate delta in screen space (stable, independent of window position)
            double screenDx = currentScreenPoint.X - _gestureStartScreenPoint.X;
            double screenDy = currentScreenPoint.Y - _gestureStartScreenPoint.Y;

            // 1. SCALE (Pinch) - Use cumulative scale (scale is relative, not absolute)
            var cumulative = e.CumulativeManipulation;
            double cumulativeScale = (cumulative.Scale.X + cumulative.Scale.Y) / 2.0;
            
            if (Math.Abs(cumulativeScale - 1.0) > 0.02)
            {
                double newScale = _startScale * cumulativeScale;
                newScale = Math.Max(0.5, Math.Min(3.0, newScale));
                
                if (Math.Abs(newScale - _currentScale) > 0.01)
                {
                    SetScale(newScale);
                    ScaleChanged?.Invoke(this, newScale);
                }
            }

            // 2. MOVE - Apply screen-space delta directly
            double newLeft = _startWindowLeft + screenDx;
            double newTop = _startWindowTop + screenDy;

            // Only update if meaningful change to reduce jitter
            if (Math.Abs(this.Left - newLeft) > 1.0 || Math.Abs(this.Top - newTop) > 1.0)
            {
                this.Left = newLeft;
                this.Top = newTop;
                
                HorizontalPositionChanged?.Invoke(this, this.Left);
                VerticalPositionChanged?.Invoke(this, this.Top);
            }

            e.Handled = true;
        }

        private double _unscaledWidth = 0;
        private double _unscaledHeight = 0;
        private bool _isRightAligned = false;

        private void UpdateWindowSize()
        {
             double oldWidth = this.Width;
             if (double.IsNaN(oldWidth)) oldWidth = this.ActualWidth; // Fallback

             double newWidth = (_unscaledWidth + 10) * _currentScale + 20;
             double newHeight = (_unscaledHeight + 10) * _currentScale + 20;

             // If Right Aligned, we must adjust Left to keep the Right Edge fixed
             if (_isRightAligned)
             {
                 if (oldWidth > 0 && !double.IsNaN(this.Left))
                 {
                     double currentRight = this.Left + oldWidth;
                     double newLeft = currentRight - newWidth;
                     // Console.WriteLine($"[Resize] RightAligned: OldW={oldWidth}, NewW={newWidth}, Right={currentRight}, NewLeft={newLeft}");
                     this.Left = newLeft;
                 }
                 else
                 {
                     // Console.WriteLine("[Resize] RightAligned but invalid width/left");
                 }
             }

             this.Width = newWidth;
             this.Height = newHeight;
        }

        public void LoadKeys(List<KeyModel> keys, bool isRightAligned = false)
        {
            _isRightAligned = isRightAligned;
            // Set Alignment for Scaling Anchor
            MainBorder.HorizontalAlignment = isRightAligned ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left;

            KeysCanvas.Children.Clear();
            _keyMap.Clear();

            double maxRight = 0;
            double maxBottom = 0;

            foreach (var key in keys)
            {
                var btn = new KeyButton
                {
                    Label = key.Label,
                    BaseLabel = key.Label,
                    VirtualKeyCode = (ushort)key.VirtualKeyCode,
                    Width = 50 * key.WidthUnits, 
                    Height = 50
                };

                // Position
                double left = key.Column * 55 + 10;
                double top = key.Row * 55 + 10;
                
                Canvas.SetLeft(btn, left); 
                Canvas.SetTop(btn, top);

                double right = left + btn.Width;
                double bottom = top + btn.Height;

                if (right > maxRight) maxRight = right;
                if (bottom > maxBottom) maxBottom = bottom;

                btn.KeyPressed += (s, e) =>
                {
                    OnKeyPressed?.Invoke(this, btn);
                };
                
                if (!_keyMap.ContainsKey(key.VirtualKeyCode)) 
                {
                    _keyMap[key.VirtualKeyCode] = btn;
                }

                KeysCanvas.Children.Add(btn);
            }

            // Resize Window to fit content
            // User requested: "one padding width taller at the bottom and two wider at the right"
            // Assuming "padding width" is ~10-15px based on layout.
            // Previous: Width = maxRight + 15, Height = maxBottom + 20
            
            // Store unscaled dimensions (max content + internal padding)
            _unscaledWidth = maxRight + 10; 
            _unscaledHeight = maxBottom + 10;

            // Fix for HorizontalAlignment.Right: Canvas needs explicit size to prevent Border from collapsing to 0
            KeysCanvas.Width = _unscaledWidth;
            KeysCanvas.Height = _unscaledHeight;

            UpdateWindowSize();

            // Set Initial Labels
            bool caps = (GetKeyState(0x14) & 0x0001) != 0;
            UpdateKeyLabels(false, caps);
        }

        public void SetEditMode(bool enabled)
        {
            MainBorder.IsManipulationEnabled = enabled;
            
            // Visual Feedback: Green Border when editing
            MainBorder.BorderBrush = enabled ? System.Windows.Media.Brushes.LimeGreen : 
                                               (System.Windows.Media.SolidColorBrush)(new System.Windows.Media.BrushConverter().ConvertFrom("#88FFFFFF")!);
            MainBorder.BorderThickness = new Thickness(enabled ? 3 : 2);

            // Toggle Keys Visibility: Hide keys in Edit Mode to show only outlines
            KeysCanvas.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
            
            // Show Exit Button
            ExitEditModeButton.Visibility = enabled ? Visibility.Visible : Visibility.Hidden;
        }        

        public void UpdateKeyLabels(bool shift, bool caps) 
        {
            foreach (var btn in _keyMap.Values)
            {
                btn.Label = KeyLabelHelper.GetLabel(btn.BaseLabel, shift, caps);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // Set WS_EX_NOACTIVATE
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, 
                GetWindowLong(helper.Handle, GWL_EXSTYLE) | WS_EX_NOACTIVATE);
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    }
}
