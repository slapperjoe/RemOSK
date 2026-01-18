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
            UpdateKeyHighlight(160, manager.IsShiftActive); // LShift
            UpdateKeyHighlight(161, manager.IsShiftActive); // RShift
            
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
