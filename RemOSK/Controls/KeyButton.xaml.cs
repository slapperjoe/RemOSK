using System.Windows;
using System.Windows.Controls;
using RemOSK.Services;
using UserControl = System.Windows.Controls.UserControl;

namespace RemOSK.Controls
{
    public partial class KeyButton : UserControl
    {
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register("Label", typeof(string), typeof(KeyButton), new PropertyMetadata("", OnLabelChanged));

        public string Label
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }

        public string BaseLabel { get; set; } = ""; // Original label for dynamic shifting

        public ushort VirtualKeyCode { get; set; }

        private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (KeyButton)d;
            control.KeyLabelText.Text = (string)e.NewValue;
        }

        // We can pass the injector via constructor or property, but for simple user control, we might just emit an event
        // or let the logical parent handle it. For now, let's just make it simple and static or singleton-ish?
        // Better: Event "KeyPressed".
        
        public event RoutedEventHandler? KeyPressed;

        public KeyButton()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Flash is now handled by the Manager to avoid flashing on wake-up
            KeyPressed?.Invoke(this, e);
        }

        public void Flash()
        {
            try
            {
                var sb = this.Resources["FlashAnimation"] as System.Windows.Media.Animation.Storyboard;
                sb?.Begin();
            }
            catch { /* Ignore animation errors */ }
        }
    }
}
