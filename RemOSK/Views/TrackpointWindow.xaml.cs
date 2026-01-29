using System;
using System.Windows;
using System.Windows.Input;
using RemOSK.Controls;
using RemOSK.Services;

namespace RemOSK.Views
{
    public partial class TrackpointWindow : DraggableWindow
    {
        public event EventHandler<System.Windows.Vector>? OnMove;

        
        public event EventHandler? OnInteractionStart;
        public event EventHandler? OnInteractionEnd;
        
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
                MainBorder.CornerRadius = new CornerRadius(15);
                
                // Visuals Only
                var pad = new TrackpadControl();
                InputContainer.Content = pad;
            }
            else // Trackpoint or Fallback
            {
                _baseWidth = 100;
                _baseHeight = 100;
                MainBorder.CornerRadius = new CornerRadius(50);
                
                _trackpointControl = new TrackpointControl();
                
                // Wire up click events

                
                _trackpointControl.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                _trackpointControl.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                
                InputContainer.Content = _trackpointControl;
            }
            
            // Disable RawTouchHandler for now as it causes regression (swallows inputs)
            // _rawTouchHandler = new RawTouchHandler();
            // _rawTouchHandler.Attach(this);
            // ...
            
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
        


        protected override void OnEditModeChanged(bool enabled)
        {
            _isEditModeEnabled = enabled;
            
            // Visual Feedback
            MainBorder.BorderBrush = enabled ? System.Windows.Media.Brushes.LimeGreen : 
                                               (System.Windows.Media.SolidColorBrush)(new System.Windows.Media.BrushConverter().ConvertFrom("#55FFFFFF")!);
            MainBorder.BorderThickness = new Thickness(enabled ? 3 : 2);
            
            // Logic
            // Logic
            InputContainer.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
            
            if (enabled)
            {
                // Edit Mode: Detach raw handler to let WPF/DraggableWindow handle moves
                _rawTouchHandler?.Detach();
                // Don't null it, just detach so we can re-attach
                // Actually, Detach removes hook. We need to re-hook later.
            }
            else
            {
                // Normal Mode: Re-attach raw handler
                _rawTouchHandler?.Attach(this);
            }
        }

        // Shared touch state
        private TouchDevice? _windowCapturedTouch;
        private Point _lastTouchPosition;
        private bool _windowTouchActive;
        
        // Trackpoint specific (Timer-based)
        private System.Windows.Point _windowTouchCenter;
        private System.Windows.Threading.DispatcherTimer? _trackpointTimer;
        private System.Windows.Vector _currentTrackpointVector;
        
        private void Window_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            // In edit mode / drag mode, let manipulation/WPF events handle it
            if (_isEditModeEnabled) return;
            
            // Priority: RawTouchHandler (Disabled due to regression)
            // if (_rawTouchHandler != null && _rawTouchHandler.IsRegistered) { ... }

            // Fallback: WPF Touch Logic
            if (!_windowTouchActive)
            {
                _windowTouchActive = true;
                _windowCapturedTouch = e.TouchDevice;
                _lastTouchPosition = e.GetTouchPoint(this).Position;
                
                OnInteractionStart?.Invoke(this, EventArgs.Empty);
                e.TouchDevice.Capture(this);
                
                if (_currentMode == "Trackpoint")
                {
                    _windowTouchCenter = new System.Windows.Point(this.ActualWidth / 2, this.ActualHeight / 2);
                    StartTrackpointTimer();
                    _currentTrackpointVector = _lastTouchPosition - _windowTouchCenter;
                }
                
                e.Handled = true;
            }
        }
        
        private void Window_PreviewTouchMove(object sender, TouchEventArgs e)
        {
             if (_isEditModeEnabled) return;
             // if (_rawTouchHandler != null && _rawTouchHandler.IsRegistered) 
             // {
             //     e.Handled = true;
             //     return;
             // }

             // Fallback Logic
             if (_windowTouchActive && e.TouchDevice == _windowCapturedTouch)
             {
                var currentPos = e.GetTouchPoint(this).Position;
                
                if (_currentMode == "Trackpoint")
                {
                    bool inside = currentPos.X >= 0 && currentPos.X <= this.ActualWidth &&
                                  currentPos.Y >= 0 && currentPos.Y <= this.ActualHeight;
                    if (inside)
                    {
                        _currentTrackpointVector = currentPos - _windowTouchCenter;
                    }
                    else
                    {
                        _currentTrackpointVector = new System.Windows.Vector(0, 0);
                    }
                }
                else if (_currentMode == "Trackpad")
                {
                    var delta = currentPos - _lastTouchPosition;
                    if (delta.Length > 0)
                    {
                        OnMove?.Invoke(this, delta * 1.5);
                        _lastTouchPosition = currentPos;
                    }
                }
                e.Handled = true;
             }
        }
        
        private void Window_PreviewTouchUp(object sender, TouchEventArgs e)
        {
             if (_isEditModeEnabled) return;
             // if (_rawTouchHandler != null && _rawTouchHandler.IsRegistered) 
             // {
             //     e.Handled = true;
             //     return;
             // }

             // Fallback Logic
             if (e.TouchDevice == _windowCapturedTouch)
             {
                _windowTouchActive = false;
                _windowCapturedTouch = null;
                
                if (_currentMode == "Trackpoint")
                {
                    StopTrackpointTimer();
                    _currentTrackpointVector = new System.Windows.Vector(0, 0);
                    _trackpointControl?.ResetStickVisual();
                }
                
                OnInteractionEnd?.Invoke(this, EventArgs.Empty);
                this.ReleaseTouchCapture(e.TouchDevice);
                e.Handled = true;
             }
        }

        // --- Raw Touch Handler Events ---

        private void OnRawTouchDown(object? sender, EventArgs e)
        {
            _windowTouchActive = true;
            OnInteractionStart?.Invoke(this, EventArgs.Empty);

            if (_currentMode == "Trackpoint")
            {
               StartTrackpointTimer();
            }
        }

        private void OnRawTouchUp(object? sender, EventArgs e)
        {
            _windowTouchActive = false;
            OnInteractionEnd?.Invoke(this, EventArgs.Empty);
            
            if (_currentMode == "Trackpoint")
            {
                StopTrackpointTimer();
                _currentTrackpointVector = new System.Windows.Vector(0, 0);
                _trackpointControl?.ResetStickVisual();
            }
        }

        private void OnRawRelativeMove(object? sender, System.Windows.Vector delta)
        {
            if (_currentMode == "Trackpad")
            {
                 OnMove?.Invoke(this, delta);
            }
        }

        private void OnRawAbsolutePosition(object? sender, Point screenPos)
        {
            if (_currentMode == "Trackpoint")
            {
                // Convert Screen to Client to get Vector from Center
                 var clientPos = this.PointFromScreen(screenPos);
                 var center = new Point(this.ActualWidth / 2, this.ActualHeight / 2);
                 
                 // Check if inside window (should be, but RawTouch captures whole window)
                 // RawTouchHandler doesn't clamp to client area, but if we are touching the window we are good.
                 
                 _currentTrackpointVector = clientPos - center;
                 
                 // Update visual immediately? No, Timer handles it for joystick feel
            }
        }

        private void StartTrackpointTimer()
        {
            if (_trackpointTimer == null)
            {
                _trackpointTimer = new System.Windows.Threading.DispatcherTimer();
                _trackpointTimer.Interval = TimeSpan.FromMilliseconds(16);
                _trackpointTimer.Tick += TrackpointTimer_Tick;
            }
            _trackpointTimer.Start();
        }

        private void StopTrackpointTimer()
        {
            _trackpointTimer?.Stop();
        }

        private void TrackpointTimer_Tick(object? sender, EventArgs e)
        {
            if (_windowTouchActive && _currentTrackpointVector.Length > 0.1) // Lower threshold
            {
                var v = _currentTrackpointVector;
                _trackpointControl?.UpdateStickVisual(v);
                
                double distanceFromCenter = v.Length;
                
                // Deadzone
                if (distanceFromCenter < 5.0) 
                {
                     return; 
                }

                // Joystick Math
                double speed = Math.Min((distanceFromCenter - 5.0) / 4.0, 25.0); // 5px deadzone, then linear speed up to max 25
                
                if (speed > 0)
                {
                    v.Normalize();
                    v *= speed;
                    OnMove?.Invoke(this, v);
                }
            }
            else if (!_windowTouchActive)
            {
                _trackpointControl?.ResetStickVisual();
            }
        }
    }
}
