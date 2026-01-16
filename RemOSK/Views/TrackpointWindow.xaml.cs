using System.Windows;
using System.Windows.Input;
using RemOSK.Controls;
using RemOSK.Services;

namespace RemOSK.Views
{
    public partial class TrackpointWindow : Window
    {
        public event EventHandler<System.Windows.Vector>? OnMove;
        public event EventHandler? OnLeftClick;
        public event EventHandler? OnRightClick;

        private RawTouchHandler? _rawTouchHandler;
        private string _currentMode = "";

        public TrackpointWindow()
        {
            InitializeComponent();
        }

        public void SetMode(string mode)
        {
            _currentMode = mode;
            InputContainer.Content = null;
            
            // Detach old handler if any
            _rawTouchHandler?.Detach();
            _rawTouchHandler = null;
            
            if (mode == "Trackpad")
            {
                this.Width = 250;
                this.Height = 200;
                
                // Use RawTouchHandler for raw WM_TOUCH processing
                // This prevents Windows from moving cursor to touch position
                _rawTouchHandler = new RawTouchHandler();
                _rawTouchHandler.Attach(this);
                _rawTouchHandler.OnRelativeMove += (s, v) => OnMove?.Invoke(this, v);
                
                // Create trackpad control for visual display and buttons only
                var pad = new TrackpadControl();
                pad.OnLeftClick += (s, e) => OnLeftClick?.Invoke(this, e);
                pad.OnRightClick += (s, e) => OnRightClick?.Invoke(this, e);
                InputContainer.Content = pad;
            }
            else // Trackpoint or Fallback
            {
                this.Width = 120;
                this.Height = 120;
                var tp = new TrackpointControl();
                tp.MoveRequested += (s, v) => OnMove?.Invoke(this, v); // Remap event
                
                // Center the trackpoint
                tp.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                tp.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                
                InputContainer.Content = tp;
            }
        }

        public void SetEditMode(bool enabled)
        {
            // Visual Feedback
            MainBorder.BorderBrush = enabled ? System.Windows.Media.Brushes.LimeGreen : 
                                               (System.Windows.Media.SolidColorBrush)(new System.Windows.Media.BrushConverter().ConvertFrom("#55FFFFFF")!);
            MainBorder.BorderThickness = new Thickness(enabled ? 3 : 1);
            
            // Logic
            MainBorder.IsManipulationEnabled = enabled;
            InputContainer.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
        }

        // Gesture State - track in SCREEN coordinates to avoid feedback loop
        private System.Windows.Point _gestureStartScreenPoint;
        private double _startWindowTop;
        private double _startWindowLeft;
        private double _startWidth;
        private double _startHeight;
        private bool _isGestureActive;

        private void MainBorder_ManipulationStarting(object sender, ManipulationStartingEventArgs e)
        {
            e.ManipulationContainer = this;
            e.Mode = ManipulationModes.All;
            e.IsSingleTouchEnabled = true;
            e.Handled = true;
        }

        private void MainBorder_ManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            // Capture start position in SCREEN coordinates
            _gestureStartScreenPoint = PointToScreen(e.ManipulationOrigin);
            _startWindowTop = this.Top;
            _startWindowLeft = this.Left;
            _startWidth = this.Width;
            _startHeight = this.Height;
            _isGestureActive = true;
        }

        private void MainBorder_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
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

            // 1. SCALE (Pinch) - Use cumulative scale
            var cumulative = e.CumulativeManipulation;
            double cumulativeScale = (cumulative.Scale.X + cumulative.Scale.Y) / 2.0;
            
            if (Math.Abs(cumulativeScale - 1.0) > 0.02)
            {
                double newWidth = _startWidth * cumulativeScale;
                double newHeight = _startHeight * cumulativeScale;

                newWidth = Math.Max(50, Math.Min(800, newWidth));
                newHeight = Math.Max(50, Math.Min(800, newHeight));

                this.Width = newWidth;
                this.Height = newHeight;
            }

            // 2. MOVE - Apply screen-space delta directly
            double newLeft = _startWindowLeft + screenDx;
            double newTop = _startWindowTop + screenDy;

            if (Math.Abs(this.Left - newLeft) > 1.0 || Math.Abs(this.Top - newTop) > 1.0)
            {
                this.Left = newLeft;
                this.Top = newTop;
            }
             
            e.Handled = true;
        }

        private void MainBorder_ManipulationInertiaStarting(object sender, ManipulationInertiaStartingEventArgs e)
        {
            e.TranslationBehavior.DesiredDeceleration = 10000.0;
            e.ExpansionBehavior.DesiredDeceleration = 10000.0;
            e.Handled = true;
        }
    }
}
