using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RemOSK.Controls
{
    /// <summary>
    /// Trackpad control - Visual only. Logic is now handled by RawTouchHandler in TrackpointWindow.
    /// </summary>
    public partial class TrackpadControl : System.Windows.Controls.UserControl
    {


        public TrackpadControl()
        {
            InitializeComponent();
        }
    }
}
