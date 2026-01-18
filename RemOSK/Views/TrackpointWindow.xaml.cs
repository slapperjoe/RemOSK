using System.Windows;
using System.Windows.Input;
using RemOSK.Controls;
using RemOSK.Services;

namespace RemOSK.Views
{
    public partial class TrackpointWindow : DraggableWindow
    {
        public event EventHandler<System.Windows.Vector>? OnMove;
        public event EventHandler? OnLeftClick;
        public event EventHandler? OnRightClick;
        
        private RawTouchHandler? _rawTouchHandler;
        private string _currentMode = "";
        private TrackpointControl? _trackpointControl;

        private bool _isEditModeEnabled;

        public TrackpointWindow()
        {
            InitializeComponent();
            SetupDragBehavior(MainBorder);
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
                _baseWidth = 250;
                _baseHeight = 200;
                
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
                _baseWidth = 100;
                _baseHeight = 100;
                
                // Use RawTouchHandler for Trackpoint too - bypasses WPF touch issues
                _rawTouchHandler = new RawTouchHandler();
                _rawTouchHandler.Attach(this);
                
                // For trackpoint, we want CONTINUOUS movement based on distance from center
                // The RawTouchHandler gives us absolute position, so we calculate offset from center
                _rawTouchHandler.OnTouchDown += (s, e) => Console.WriteLine("[TrackpointWindow] Raw touch down");
                _rawTouchHandler.OnTouchUp += (s, e) => Console.WriteLine("[TrackpointWindow] Raw touch up");
                _rawTouchHandler.OnRelativeMove += HandleTrackpointMove;
                
                _trackpointControl = new TrackpointControl();
                
                // Wire up click events
                _trackpointControl.OnLeftClick += (s, e) => OnLeftClick?.Invoke(this, e);
                _trackpointControl.OnRightClick += (s, e) => OnRightClick?.Invoke(this, e);
                
                _trackpointControl.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                _trackpointControl.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                
                InputContainer.Content = _trackpointControl;
            }
            
            // Apply current scale to new base dimensions
            SetScale(_currentScale);
        }
        
        // --- ZOOM LOGIC ---
        private double _baseWidth = 100;
        private double _baseHeight = 100;

        public override void SetScale(double scale)
        {
            base.SetScale(scale);
            WindowScaleTransform.ScaleX = scale;
            WindowScaleTransform.ScaleY = scale;
            
            // Re-calculate window size based on current mode base size
            this.Width = _baseWidth * scale; 
            this.Height = _baseHeight * scale;
        }
        // ------------------
        
        private void HandleTrackpointMove(object? sender, System.Windows.Vector v)
        {
            // Forward to OnMove - RawTouchHandler handles the relative movement
            OnMove?.Invoke(this, v);
        }

        protected override void OnEditModeChanged(bool enabled)
        {
            _isEditModeEnabled = enabled;
            
            // Visual Feedback
            MainBorder.BorderBrush = enabled ? System.Windows.Media.Brushes.LimeGreen : 
                                               (System.Windows.Media.SolidColorBrush)(new System.Windows.Media.BrushConverter().ConvertFrom("#55FFFFFF")!);
            MainBorder.BorderThickness = new Thickness(enabled ? 3 : 2);
            
            // Logic
            InputContainer.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
        }

        // Window-level touch handlers for Trackpoint mode
        private TouchDevice? _windowCapturedTouch;
        private System.Windows.Point _windowTouchCenter;
        private bool _windowTouchActive;
        private System.Windows.Threading.DispatcherTimer? _trackpointTimer;
        private System.Windows.Vector _currentTrackpointVector;
        
        private void Window_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            Console.WriteLine("[Window] PreviewTouchDown");
            
            // In edit mode, don't handle as trackpoint - let manipulation events handle it
            if (_isEditModeEnabled) return;
            
            if (_currentMode == "Trackpoint" && !_windowTouchActive)
            {
                _windowTouchActive = true;
                _windowCapturedTouch = e.TouchDevice;
                
                // Trackpoint center is always center of current window size (which might be scaled)
                _windowTouchCenter = new System.Windows.Point(this.ActualWidth / 2, this.ActualHeight / 2);
                
                e.TouchDevice.Capture(this);
                
                // Start timer for continuous movement
                if (_trackpointTimer == null)
                {
                    _trackpointTimer = new System.Windows.Threading.DispatcherTimer();
                    _trackpointTimer.Interval = TimeSpan.FromMilliseconds(16);
                    _trackpointTimer.Tick += TrackpointTimer_Tick;
                }
                _trackpointTimer.Start();
                
                // Calculate initial vector
                var touchPos = e.GetTouchPoint(this).Position;
                _currentTrackpointVector = touchPos - _windowTouchCenter;
                
                e.Handled = true;
            }
        }
        
        private void Window_PreviewTouchMove(object sender, TouchEventArgs e)
        {
            if (_currentMode == "Trackpoint" && _windowTouchActive && e.TouchDevice == _windowCapturedTouch)
            {
                var touchPos = e.GetTouchPoint(this).Position;
                
                // Only move if touch is inside the window
                if (touchPos.X >= 0 && touchPos.X <= this.ActualWidth &&
                    touchPos.Y >= 0 && touchPos.Y <= this.ActualHeight)
                {
                    _currentTrackpointVector = touchPos - _windowTouchCenter;
                }
                else
                {
                    // Touch went outside - stop movement
                    _currentTrackpointVector = new System.Windows.Vector(0, 0);
                }
                e.Handled = true;
            }
        }
        
        private void Window_PreviewTouchUp(object sender, TouchEventArgs e)
        {
            Console.WriteLine("[Window] PreviewTouchUp");
            if (_currentMode == "Trackpoint" && e.TouchDevice == _windowCapturedTouch)
            {
                _windowTouchActive = false;
                _trackpointTimer?.Stop();
                _windowCapturedTouch = null;
                _currentTrackpointVector = new System.Windows.Vector(0, 0);
                
                // Reset stick visual to center
                _trackpointControl?.ResetStickVisual();
                
                this.ReleaseTouchCapture(e.TouchDevice);
                e.Handled = true;
            }
        }
        
        private void TrackpointTimer_Tick(object? sender, EventArgs e)
        {
            if (_windowTouchActive && _currentTrackpointVector.Length > 3)
            {
                // Calculate movement based on distance from center
                var v = _currentTrackpointVector;
                
                // Update stick visual to show direction
                _trackpointControl?.UpdateStickVisual(v);
                
                // Scale: further from center = MUCH faster movement (3x increase)
                // Max window radius is about 60px, so scale from 3px to 60px
                double distanceFromCenter = v.Length;
                double speed = Math.Min(distanceFromCenter / 5.0, 15.0); // Speed multiplier 0-15 (was 0-5)
                
                if (v.Length > 0.1)
                {
                    v.Normalize();
                    v *= speed;
                }
                
                // Log occasionally to verify timer is working
                if (DateTime.Now.Millisecond < 30)
                {
                    Console.WriteLine($"[Trackpoint] Moving: dx={v.X:F1} dy={v.Y:F1} speed={speed:F1}");
                }
                
                // Send the movement
                OnMove?.Invoke(this, v);
            }
            else if (!_windowTouchActive)
            {
                // Reset stick when not active
                _trackpointControl?.ResetStickVisual();
            }
        }
    }
}

