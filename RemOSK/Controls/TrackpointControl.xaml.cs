using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using UserControl = System.Windows.Controls.UserControl;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace RemOSK.Controls
{
    public partial class TrackpointControl : UserControl
    {
        private bool _isDragging;
        private Point _startPoint;
        private Vector _currentVector;
        private DispatcherTimer _timer;
        private TouchDevice? _capturedTouch;

        // Event to report movement vector
        public event EventHandler<Vector>? MoveRequested;

        public TrackpointControl()
        {
            InitializeComponent();
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(15);
            _timer.Tick += Timer_Tick;
        }

        // Mouse handlers
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.StylusDevice != null) return; // Touch will be handled by touch handlers
            
            _isDragging = true;
            _startPoint = e.GetPosition(this);
            Mouse.Capture(this);
            _timer.Start();
            Console.WriteLine("[Trackpoint] Mouse down");
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.StylusDevice == null)
            {
                UpdateVector(e.GetPosition(this));
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.StylusDevice == null)
            {
                StopDrag();
            }
        }

        // Touch handlers
        private void OnTouchDown(object sender, TouchEventArgs e)
        {
            _isDragging = true;
            _startPoint = e.GetTouchPoint(this).Position;
            _capturedTouch = e.TouchDevice;
            
            // Use CaptureTouch on the element that needs to track the touch
            this.CaptureTouch(e.TouchDevice);
            
            _timer.Start();
            Console.WriteLine("[Trackpoint] Touch down - captured");
            e.Handled = true;
        }

        private void OnTouchMove(object sender, TouchEventArgs e)
        {
            Console.WriteLine($"[Trackpoint] Touch move - isDragging:{_isDragging} device match:{e.TouchDevice == _capturedTouch}");
            if (_isDragging && e.TouchDevice == _capturedTouch)
            {
                UpdateVector(e.GetTouchPoint(this).Position);
            }
            e.Handled = true;
        }

        private void OnTouchUp(object sender, TouchEventArgs e)
        {
            Console.WriteLine("[Trackpoint] Touch up");
            if (e.TouchDevice == _capturedTouch)
            {
                StopDrag();
                this.ReleaseTouchCapture(e.TouchDevice);
                _capturedTouch = null;
            }
            e.Handled = true;
        }

        private void UpdateVector(Point currentPos)
        {
            // Calculate offset from center of the 90x90 circular trackpoint
            var center = new Point(45, 45);
            var vector = currentPos - center;

            // Cap the visual stick movement to 15px radius
            if (vector.Length > 15)
            {
                vector.Normalize();
                vector *= 15;
            }

            // Update visual stick position using the named transform
            StickTransform.X = vector.X;
            StickTransform.Y = vector.Y;
            
            // Sensitivity multiplier
            _currentVector = vector * 1.5;
        }
        
        /// <summary>
        /// Updates the stick visual from an external source (e.g., TrackpointWindow touch handling)
        /// </summary>
        public void UpdateStickVisual(Vector direction)
        {
            // Cap the visual stick movement to 15px radius
            var capped = direction;
            if (capped.Length > 15)
            {
                capped.Normalize();
                capped *= 15;
            }
            
            StickTransform.X = capped.X;
            StickTransform.Y = capped.Y;
        }
        
        /// <summary>
        /// Resets the stick to center position
        /// </summary>
        public void ResetStickVisual()
        {
            StickTransform.X = 0;
            StickTransform.Y = 0;
        }

        private void StopDrag()
        {
            _isDragging = false;
            Mouse.Capture(null);
            _timer.Stop();
            
            // Reset stick position
            StickTransform.X = 0;
            StickTransform.Y = 0;
            
            _currentVector = new Vector(0, 0);
            Console.WriteLine("[Trackpoint] Drag stopped");
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_isDragging && _currentVector.Length > 0.1)
            {
                MoveRequested?.Invoke(this, _currentVector);
            }
        }
        
        // Events for mouse button clicks (triggered by tap/hold gestures)
        public event EventHandler? OnLeftClick;
        public event EventHandler? OnRightClick;
    }
}

