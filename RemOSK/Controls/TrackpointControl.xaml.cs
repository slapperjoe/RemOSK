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

        // Event to report movement vector
        public event EventHandler<Vector>? MoveRequested;

        public TrackpointControl()
        {
            InitializeComponent();
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(15);
            _timer.Tick += Timer_Tick;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _startPoint = e.GetPosition(this);
            Mouse.Capture(this);
            _timer.Start();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                var currentPos = e.GetPosition(this);
                // Calculate offset from center (30,30)
                var center = new Point(30, 30);
                var vector = currentPos - center;

                // Clamp visual movement logic could go here, but for now simple vector
                // Let's cap the visual stick movement to 15px radius
                if (vector.Length > 15)
                {
                    vector.Normalize();
                    vector *= 15;
                }

                Stick.RenderTransform = new TranslateTransform(vector.X, vector.Y);
                
                // Sensitivity multiplier
                _currentVector = vector * 1.5; 
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            StopDrag();
        }

        private void StopDrag()
        {
            _isDragging = false;
            Mouse.Capture(null);
            _timer.Stop();
            Stick.RenderTransform = new TranslateTransform(0, 0); // Reset
            _currentVector = new Vector(0, 0);
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_isDragging && _currentVector.Length > 0.1)
            {
                MoveRequested?.Invoke(this, _currentVector);
            }
        }
    }
}
