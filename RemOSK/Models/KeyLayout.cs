using System.Collections.Generic;

namespace RemOSK.Models
{
    public class KeyModel
    {
        public string Label { get; set; } = "";
        public int VirtualKeyCode { get; set; }
        // Grid/Positioning logic
        public double Row { get; set; }
        public double Column { get; set; }
        public double WidthUnits { get; set; } = 1.0; 
        public bool IsHalfSplit { get; set; } // If true, belongs to Right half? Or we define separate arrays?
    }

    public class KeyboardLayout
    {
        public string Name { get; set; } = "Default";
        public List<KeyModel> LeftKeys { get; set; } = new List<KeyModel>();
        public List<KeyModel> RightKeys { get; set; } = new List<KeyModel>();
    }
}
