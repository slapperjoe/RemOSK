using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RemOSK.Controls
{
    /// <summary>
    /// Trackpad control that reads touch/mouse movement and reports relative deltas.
    /// Relies on the parent window having RegisterTouchWindow called so touch is 
    /// converted to mouse events by Windows (preventing cursor from jumping to touch position).
    /// </summary>
    public partial class TrackpadControl : System.Windows.Controls.UserControl
    {
        public event EventHandler<Vector>? OnMove;
        public event EventHandler? OnLeftClick;
        public event EventHandler? OnRightClick;

        private bool _isTracking;
        private System.Windows.Point _lastPosition;

        public TrackpadControl()
        {
            InitializeComponent();
        }

        private void TouchSurface_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as UIElement;
            if (element != null)
            {
                _lastPosition = e.GetPosition(element);
                _isTracking = true;
                element.CaptureMouse();
            }
            e.Handled = true;
        }

        private void TouchSurface_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isTracking && e.LeftButton == MouseButtonState.Pressed)
            {
                var element = sender as UIElement;
                if (element != null)
                {
                    var currentPos = e.GetPosition(element);
                    var delta = currentPos - _lastPosition;
                    _lastPosition = currentPos;
                    
                    // Only send if meaningful movement
                    if (Math.Abs(delta.X) > 0.5 || Math.Abs(delta.Y) > 0.5)
                    {
                        // Apply sensitivity multiplier
                        OnMove?.Invoke(this, new Vector(delta.X * 1.5, delta.Y * 1.5));
                    }
                }
            }
        }

        private void TouchSurface_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isTracking = false;
            var element = sender as UIElement;
            element?.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void LeftButton_Click(object sender, RoutedEventArgs e)
        {
            OnLeftClick?.Invoke(this, EventArgs.Empty);
        }

        private void RightButton_Click(object sender, RoutedEventArgs e)
        {
            OnRightClick?.Invoke(this, EventArgs.Empty);
        }
    }
}
