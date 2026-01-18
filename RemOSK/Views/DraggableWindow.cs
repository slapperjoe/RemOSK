using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace RemOSK.Views
{
    /// <summary>
    /// Base class for windows that support touch-based drag and scroll-to-zoom in edit mode.
    /// 1 Finger: Absolute Drag.
    /// 2 Fingers: Vertical Scroll to Zoom (No Movement).
    /// </summary>
    public class DraggableWindow : Window
    {
        // Events for position/scale persistence
        public event EventHandler<double>? HorizontalPositionChanged;
        public event EventHandler<double>? VerticalPositionChanged;
        public event EventHandler<double>? ScaleChanged;
        
        // Events for edit mode
        public event EventHandler? RequestExitEditMode;
        public event EventHandler? RequestEnterEditMode;
        
        // Touch tracking
        private Dictionary<int, System.Windows.Point> _activeTouches = new Dictionary<int, System.Windows.Point>();
        
        // Zoom tracking - 2-finger vertical
        private double _initialTwoFingerY = 0;
        
        // Absolute offset approach for single-finger drag
        private System.Windows.Point _initialTouchPos;
        private double _initialWindowLeft;
        private double _initialWindowTop;
        
        // Long-press timer for entering edit mode
        private System.Windows.Threading.DispatcherTimer? _longPressTimer;
        
        // The border element that handles touch - set via SetupDragBehavior
        private Border? _dragBorder;
        
        // Win32 constants
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        
        protected void SetupDragBehavior(Border dragBorder)
        {
            _dragBorder = dragBorder;
            _dragBorder.TouchDown += DragBorder_TouchDown;
            _dragBorder.TouchUp += DragBorder_TouchUp;
            _dragBorder.TouchMove += DragBorder_TouchMove;
        }
        
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            int exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
        }
        
        public void SetEditMode(bool enabled)
        {
            if (_dragBorder != null)
            {
                _dragBorder.IsManipulationEnabled = enabled;
            }
            OnEditModeChanged(enabled);
        }
        
        protected virtual void OnEditModeChanged(bool enabled) { }
        
        public void EnableClickThrough()
        {
            var helper = new WindowInteropHelper(this);
            SetWindowLong(helper.Handle, GWL_EXSTYLE,
                GetWindowLong(helper.Handle, GWL_EXSTYLE) | WS_EX_TRANSPARENT);
        }
        
        public void DisableClickThrough()
        {
            var helper = new WindowInteropHelper(this);
            int style = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, style & ~WS_EX_TRANSPARENT);
        }
        
        protected virtual bool IsTouchOnControl(DependencyObject source) => false;
        
        private System.Windows.Point GetScreenTouchPosition(TouchEventArgs e)
        {
            var touchPoint = e.GetTouchPoint(this);
            var screenPos = this.PointToScreen(touchPoint.Position);
            return new System.Windows.Point(screenPos.X, screenPos.Y);
        }
        
        private void DragBorder_TouchDown(object sender, TouchEventArgs e)
        {
            if (_dragBorder != null && !_dragBorder.IsManipulationEnabled)
            {
                StartLongPressTimer();
            }
            
            if (_dragBorder == null || !_dragBorder.IsManipulationEnabled) return;
            if (e.OriginalSource is DependencyObject source && IsTouchOnControl(source)) return;
            
            _dragBorder.CaptureTouch(e.TouchDevice);
            
            var touchPos = GetScreenTouchPosition(e);
            _activeTouches[e.TouchDevice.Id] = touchPos;
            
            // Store initial positions for first touch (absolute offset approach)
            if (_activeTouches.Count == 1)
            {
                _initialTouchPos = touchPos;
                _initialWindowLeft = this.Left;
                _initialWindowTop = this.Top;
            }
            
            // Start Two-Finger Gesture (ZOOM)
            if (_activeTouches.Count == 2)
            {
                var positions = _activeTouches.Values.ToArray();
                double avgY = (positions[0].Y + positions[1].Y) / 2;
                _initialTwoFingerY = avgY;
                OnZoomStart();
            }
            
            e.Handled = true;
        }
        
        private void DragBorder_TouchUp(object sender, TouchEventArgs e)
        {
            CancelLongPressTimer();
            
            if (_activeTouches.ContainsKey(e.TouchDevice.Id))
            {
                _dragBorder?.ReleaseTouchCapture(e.TouchDevice);
                _activeTouches.Remove(e.TouchDevice.Id);
                
                // If dropping from 2 fingers, we can call End if we were zooming.
                // But simplified: 
                // 1 -> 0: Stop drag.
                // 2 -> 1: Stop zoom. Resuming drag might be jarring, so we usually just stop everything effectively until next gesture.
                // We'll let the 1 remaining finger continue "tracking" but since we don't re-init logic, it might jump.
                // Better: If counting down from 2 to 1, re-init the drag anchor for the remaining finger to prevent jump.
                if (_activeTouches.Count == 1)
                {
                   // Re-init drag for the remaining finger so it doesn't jump to the old 1-finger anchor
                   var remaining = _activeTouches.First();
                   _initialTouchPos = remaining.Value;
                   _initialWindowLeft = this.Left;
                   _initialWindowTop = this.Top;
                }
                
                e.Handled = true;
            }
        }
        
        private void DragBorder_TouchMove(object sender, TouchEventArgs e)
        {
            if (!_activeTouches.ContainsKey(e.TouchDevice.Id)) return;
            
            var currentPos = GetScreenTouchPosition(e);
            _activeTouches[e.TouchDevice.Id] = currentPos;
            
            if (_activeTouches.Count == 1)
            {
                // === 1 Finger: ABSOLUTE OFFSET DRAG ===
                double offsetX = currentPos.X - _initialTouchPos.X;
                double offsetY = currentPos.Y - _initialTouchPos.Y;
                
                double newLeft = _initialWindowLeft + offsetX;
                double newTop = _initialWindowTop + offsetY;
                
                this.Left = newLeft;
                this.Top = newTop;
                HorizontalPositionChanged?.Invoke(this, this.Left);
                VerticalPositionChanged?.Invoke(this, this.Top);
            }
            else if (_activeTouches.Count == 2)
            {
                // === 2 Fingers: VERTICAL ZOOM (No Movement) ===
                var positions = _activeTouches.Values.ToArray();
                double currentY = (positions[0].Y + positions[1].Y) / 2;
                
                // Delta from start of gesture (Up = Negative Y in screen, but we want Up = Zoom In)
                // Screen coordinates: Y increases downwards.
                // Move Up -> Y decreases -> (Initial - Current) > 0.
                // Move Down -> Y increases -> (Initial - Current) < 0.
                double totalDeltaY = _initialTwoFingerY - currentY;
                
                OnZoomStep(totalDeltaY);
            }
            
            e.Handled = true;
        }
        
        protected virtual void OnZoomStart() 
        {
            _scaleAtZoomStart = _currentScale;
        }
        
        /// <summary>
        /// Called with total vertical distance moved since 2-finger touchdown.
        /// Positive = Moved Up (Zoom In). Negative = Moved Down (Zoom Out).
        /// Default implementation calculates target scale and calls SetScale.
        /// </summary>
        protected virtual void OnZoomStep(double totalDeltaY) 
        {
             // Sensitivity: 200 pixels up = +1.0 scale factor
             double scaleDelta = totalDeltaY * 0.005;
             double targetScale = _scaleAtZoomStart + scaleDelta;
             SetScale(Math.Max(MinScale, Math.Min(MaxScale, targetScale)));
        }
        
        // --- Shared Scale Logic ---
        protected double _currentScale = 1.0;
        private double _scaleAtZoomStart = 1.0;
        
        protected virtual double MinScale => 0.5;
        protected virtual double MaxScale => 4.0;

        public virtual void SetScale(double scale)
        {
            _currentScale = scale;
            RaiseScaleChanged(scale);
        }
        // --------------------------

        protected void RaiseScaleChanged(double newScale)
        {
            ScaleChanged?.Invoke(this, newScale);
        }
        
        private void StartLongPressTimer()
        {
            CancelLongPressTimer();
            _longPressTimer = new System.Windows.Threading.DispatcherTimer();
            _longPressTimer.Interval = TimeSpan.FromMilliseconds(3500);
            _longPressTimer.Tick += (s, ev) =>
            {
                _longPressTimer?.Stop();
                Console.WriteLine($"[{this.GetType().Name}] Long-press detected - requesting edit mode");
                RequestEnterEditMode?.Invoke(this, EventArgs.Empty);
            };
            _longPressTimer.Start();
        }
        
        private void CancelLongPressTimer()
        {
            _longPressTimer?.Stop();
            _longPressTimer = null;
        }
        
        protected void RaiseRequestExitEditMode()
        {
            RequestExitEditMode?.Invoke(this, EventArgs.Empty);
        }
    }
}

