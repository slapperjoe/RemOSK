using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls.Primitives;

namespace RemOSK.Views
{
    public partial class ClickButtonsWindow : DraggableWindow
    {
        public event EventHandler? OnLeftClick;
        public event EventHandler? OnMiddleClick;
        public event EventHandler? OnRightClick;
        public event EventHandler<bool>? OnHoldToggle;
        

        
        public ClickButtonsWindow()
        {
            InitializeComponent();
            SetupDragBehavior(MainBorder);
        }
        
        protected override void OnEditModeChanged(bool enabled)
        {
            // Show/hide buttons in edit mode
            ButtonsContainer.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
            
            // Visual feedback
            MainBorder.BorderBrush = enabled ? System.Windows.Media.Brushes.LimeGreen : 
                                                (System.Windows.Media.SolidColorBrush)(new System.Windows.Media.BrushConverter().ConvertFrom("#55FFFFFF")!);
        }

        protected override bool IsTouchOnControl(DependencyObject source)
        {
            var parent = source;
            while (parent != null && parent != this)
            {
                if (parent is System.Windows.Controls.Button || parent is System.Windows.Controls.Primitives.ToggleButton)
                    return true;
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            return false;
        }
        
        // Debounce: prevent clicks within 200ms of each other
        private DateTime _lastClickTime = DateTime.MinValue;
        private const int DEBOUNCE_MS = 200;
        
        private bool ShouldFireClick()
        {
            var now = DateTime.Now;
            if ((now - _lastClickTime).TotalMilliseconds < DEBOUNCE_MS)
            {
                Console.WriteLine("[ClickButtons] Debounced - ignoring duplicate click");
                return false;
            }
            _lastClickTime = now;
            return true;
        }
        
        // Button click handlers - Left
        private void LmbButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ShouldFireClick()) return;
            Console.WriteLine("[ClickButtons] Left click");
            OnLeftClick?.Invoke(this, EventArgs.Empty);
        }
        
        // Button click handlers - Middle
        private void MmbButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ShouldFireClick()) return;
            Console.WriteLine("[ClickButtons] Middle click");
            OnMiddleClick?.Invoke(this, EventArgs.Empty);
        }
        
        // Button click handlers - Right
        private void RmbButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ShouldFireClick()) return;
            Console.WriteLine("[ClickButtons] Right click");
            OnRightClick?.Invoke(this, EventArgs.Empty);
        }
        
        // Hold toggle handlers
        private void HoldButton_Checked(object sender, RoutedEventArgs e)
        {

            HoldButton.Background = System.Windows.Media.Brushes.DarkRed;
            HoldButton.Foreground = System.Windows.Media.Brushes.White;
            Console.WriteLine("[ClickButtons] Hold ON - left button down");
            OnHoldToggle?.Invoke(this, true);
        }
        
        private void HoldButton_Unchecked(object sender, RoutedEventArgs e)
        {

            HoldButton.Background = (System.Windows.Media.SolidColorBrush)(new System.Windows.Media.BrushConverter().ConvertFrom("#444")!);
            HoldButton.Foreground = (System.Windows.Media.SolidColorBrush)(new System.Windows.Media.BrushConverter().ConvertFrom("#AAA")!);
            Console.WriteLine("[ClickButtons] Hold OFF - left button up");
            OnHoldToggle?.Invoke(this, false);
        }
        
        private void HoldButton_TouchDown(object sender, TouchEventArgs e)
        {
            // Toggle the hold state
            HoldButton.IsChecked = !HoldButton.IsChecked;
            e.Handled = true;
        }
        
        public override void SetScale(double scale)
        {
            base.SetScale(scale);
            WindowScaleTransform.ScaleX = scale;
            WindowScaleTransform.ScaleY = scale;
            
            // Re-calculate window size (Base: 160x50)
            this.Width = 160 * scale; 
            this.Height = 50 * scale;
        }
    }
}
