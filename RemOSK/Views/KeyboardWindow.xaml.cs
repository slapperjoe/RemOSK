using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using RemOSK.Controls;
using RemOSK.Models;
using RemOSK.Services;

namespace RemOSK.Views
{
    public partial class KeyboardWindow : DraggableWindow, IModifierObserver
    {
        public event EventHandler<KeyButton>? OnKeyPressed;
        public event EventHandler<Vector>? OnTrackpointMove;
        
        // Dictionary to track keys by virtual key code for quick updates
        private Dictionary<int, KeyButton> _keyMap = new Dictionary<int, KeyButton>();

        // Current Scale Factor exposed property
        public double CurrentScale => _currentScale;

        public KeyboardWindow()
        {
            InitializeComponent();
            SetupDragBehavior(MainBorder);
        }

        // ... (OnModifierStateChanged, etc.) ...

        public override void SetScale(double scale)
        {
            // Call base to update _currentScale and raise events
            base.SetScale(scale);
            
            WindowScaleTransform.ScaleX = scale;
            WindowScaleTransform.ScaleY = scale;
            
            // Re-calculate window size based on content and new scale
            UpdateWindowSize();
        }

        protected override bool IsTouchOnControl(DependencyObject source)
        {
             var parent = source;
             while (parent != null && parent != this)
             {
                 if (parent is KeyButton || parent is System.Windows.Controls.Button || parent is System.Windows.Controls.Primitives.ButtonBase)
                     return true;
                 parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
             }
             return false;
        }
        
        // OnZoomStart / OnZoomStep removed - using base implementation

        public void OnModifierStateChanged(ModifierStateManager manager)
        {
            // Shift - Special handling for Locked state
            var shiftColor = manager.IsShiftLocked ? 
                             new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 140, 0)) : // DarkOrange for Locked
                             (manager.IsShiftActive ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 100, 200)) : // Blue for Active
                                                      new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51))); // Gray for Normal

            if (_keyMap.TryGetValue(160, out var lShift)) lShift.Background = shiftColor;
            if (_keyMap.TryGetValue(161, out var rShift)) rShift.Background = shiftColor;
            
            UpdateKeyHighlight(162, manager.IsCtrlActive);  // LCtrl
            UpdateKeyHighlight(163, manager.IsCtrlActive);  // RCtrl
            
            UpdateKeyHighlight(164, manager.IsAltActive);   // LAlt
            UpdateKeyHighlight(165, manager.IsAltActive);   // RAlt
            
            UpdateKeyHighlight(91, manager.IsWinActive);    // LWin

            // Update Labels and CapsLock highlight
            bool caps = (GetKeyState(0x14) & 0x0001) != 0;
            UpdateKeyHighlight(20, caps);  // CapsLock VK=20 (0x14)
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

        public void SetDragHandleVisibility(bool visible)
        {
            // No-op: Handles removed in favor of gestures
        }

        public double GetExpectedWidth(double scale)
        {
             return (_unscaledWidth + 10) * scale + 20; 
        }

        
        private double _dpiScale = 1.0;
        
        private double _unscaledWidth = 0;
        private double _unscaledHeight = 0;
        private bool _isRightAligned = false;

        private void UpdateWindowSize()
        {
             double oldWidth = this.Width;
             if (double.IsNaN(oldWidth)) oldWidth = this.ActualWidth; // Fallback

             double newWidth = (_unscaledWidth + 10) * _currentScale + 20;
             double newHeight = (_unscaledHeight + 10) * _currentScale + 20;
             
             // Measure preview with constrained width
             // It has fixed height in XAML (64) + padding (4) + margin (4) approx
             // We can just rely on DesiredSize
             PreviewBorder.Measure(new Size(newWidth - 20, double.PositiveInfinity));
             newHeight += PreviewBorder.DesiredSize.Height + 5; // +5 for margin/separator

             // If Right Aligned, we must adjust Left to keep the Right Edge fixed
             if (_isRightAligned)
             {
                 if (oldWidth > 0 && !double.IsNaN(this.Left))
                 {
                     double currentRight = this.Left + oldWidth;
                     double newLeft = currentRight - newWidth;
                     this.Left = newLeft;
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

                // Position Logic (Advanced: Absolute vs Grid)
                double left, top;
                
                if (key.X != -1 && key.Y != -1)
                {
                    // Absolute positioning (Pixels)
                    left = key.X;
                    top = key.Y;
                }
                else
                {
                    // Grid positioning with Offsets
                    left = key.Column * 55 + 10 + key.XOffset;
                    top = key.Row * 55 + 10 + key.YOffset;
                }
                
                Canvas.SetLeft(btn, left); 
                Canvas.SetTop(btn, top);
                
                // Rotation
                if (key.Rotation != 0)
                {
                    btn.RenderTransformOrigin = new Point(0.5, 0.5);
                    btn.RenderTransform = new System.Windows.Media.RotateTransform(key.Rotation);
                }

                // Calculate bounds (approximate for rotated keys)
                // If rotated, expand bounds slightly to prevent clipping if tightly packed, 
                // but for window sizing we just need the extent.
                // Rotating a 50x50 rect by 45 deg -> 70.7 width/height.
                double expansion = (key.Rotation != 0) ? 25 : 0;
                double right = left + btn.Width + expansion;
                double bottom = top + btn.Height + expansion;

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

        protected override void OnEditModeChanged(bool enabled)
        {
            // Visual Feedback: Green Border when editing
            MainBorder.BorderBrush = enabled ? System.Windows.Media.Brushes.LimeGreen : 
                                               (System.Windows.Media.SolidColorBrush)(new System.Windows.Media.BrushConverter().ConvertFrom("#88FFFFFF")!);
            MainBorder.BorderThickness = new Thickness(enabled ? 3 : 2);

            // Toggle Keys Visibility: Hide keys in Edit Mode to show only outlines
            KeysCanvas.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
            
            // Exit button removed - use tray icon to exit edit mode
        }        

        public void UpdateKeyLabels(bool shift, bool caps) 
        {
            foreach (var btn in _keyMap.Values)
            {
                btn.Label = KeyLabelHelper.GetLabel(btn.BaseLabel, shift, caps);
            }
        }
        
        public void UpdatePreviewText(RemOSK.Services.TextContextService.TextContext? context)
        {
            if (context != null)
            {
                // Insert cursor marker at the cursor position
                string displayText = context.Text;
                if (context.CursorPosition >= 0 && context.CursorPosition <= displayText.Length)
                {
                    // Insert a visual cursor indicator (yellow pipe character)
                    displayText = displayText.Insert(context.CursorPosition, "█");
                }
                PreviewText.Text = displayText;
            }
            else
            {
                PreviewText.Text = "Waiting for text focus...";
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // DPI Detection
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                _dpiScale = source.CompositionTarget.TransformToDevice.M11;
                Console.WriteLine($"[KeyboardWindow] DPI Scale detected: {_dpiScale}");
            }
        }
    }
}
