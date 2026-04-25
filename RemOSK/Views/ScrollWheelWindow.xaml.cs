using System;
using System.Windows;
using System.Windows.Input;

namespace RemOSK.Views
{
    public partial class ScrollWheelWindow : DraggableWindow
    {
        public event EventHandler<int>? OnScroll;
        public event EventHandler? OnInteractionStart;
        public event EventHandler? OnInteractionEnd;

        private const double BASE_PIXELS_PER_TICK = 30.0;

        private bool _isEditModeEnabled;
        private TouchDevice? _capturedTouch;
        private double _lastTouchY;
        private double _scrollAccumulator;

        // Long-press timer for entering edit mode from normal mode
        private System.Windows.Threading.DispatcherTimer? _longPressTimer;

        public ScrollWheelWindow()
        {
            InitializeComponent();
            SetupDragBehavior(MainBorder);
            this.LostTouchCapture += OnLostTouchCapture;
        }

        protected override void OnEditModeChanged(bool enabled)
        {
            _isEditModeEnabled = enabled;
            ContentContainer.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
            MainBorder.BorderBrush = enabled
                ? System.Windows.Media.Brushes.LimeGreen
                : (System.Windows.Media.SolidColorBrush)(new System.Windows.Media.BrushConverter().ConvertFrom("#55FFFFFF")!);
            MainBorder.BorderThickness = new Thickness(enabled ? 3 : 2);
        }

        public override void SetScale(double scale)
        {
            base.SetScale(scale);
            WindowScaleTransform.ScaleX = scale;
            WindowScaleTransform.ScaleY = scale;
            this.Width = 40 * scale;
            this.Height = 140 * scale;
        }

        private void Window_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            if (_isEditModeEnabled) return;

            if (_capturedTouch == null)
            {
                _capturedTouch = e.TouchDevice;
                _lastTouchY = e.GetTouchPoint(this).Position.Y;
                _scrollAccumulator = 0;

                OnInteractionStart?.Invoke(this, EventArgs.Empty);
                e.TouchDevice.Capture(this);
                StartLongPressTimer();
                e.Handled = true;
            }
        }

        private void Window_PreviewTouchMove(object sender, TouchEventArgs e)
        {
            if (_isEditModeEnabled) return;
            if (e.TouchDevice != _capturedTouch) return;

            double currentY = e.GetTouchPoint(this).Position.Y;
            double deltaY = currentY - _lastTouchY;
            _lastTouchY = currentY;

            // Cancel long-press if the finger has moved significantly
            if (Math.Abs(deltaY) > 3)
                CancelLongPressTimer();

            // Drag down → negative accumulator → negative ticks → scroll down (negative delta)
            // Drag up   → positive accumulator → positive ticks → scroll up (positive delta)
            _scrollAccumulator -= deltaY;

            double threshold = BASE_PIXELS_PER_TICK;
            int ticks = (int)(_scrollAccumulator / threshold);
            if (ticks != 0)
            {
                _scrollAccumulator -= ticks * threshold;
                OnScroll?.Invoke(this, ticks);
            }

            e.Handled = true;
        }

        private void Window_PreviewTouchUp(object sender, TouchEventArgs e)
        {
            if (_isEditModeEnabled) return;
            if (e.TouchDevice != _capturedTouch) return;

            ResetTouchState();
            this.ReleaseTouchCapture(e.TouchDevice);
            OnInteractionEnd?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnLostTouchCapture(object? sender, TouchEventArgs e)
        {
            if (e.TouchDevice == _capturedTouch)
            {
                ResetTouchState();
                OnInteractionEnd?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ResetTouchState()
        {
            _capturedTouch = null;
            _scrollAccumulator = 0;
            CancelLongPressTimer();
        }

        private void StartLongPressTimer()
        {
            CancelLongPressTimer();
            _longPressTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(3500)
            };
            _longPressTimer.Tick += (s, ev) =>
            {
                _longPressTimer?.Stop();
                Console.WriteLine("[ScrollWheel] Long-press detected - requesting edit mode");
                RaiseRequestEnterEditMode();
            };
            _longPressTimer.Start();
        }

        private void CancelLongPressTimer()
        {
            _longPressTimer?.Stop();
            _longPressTimer = null;
        }
    }
}
