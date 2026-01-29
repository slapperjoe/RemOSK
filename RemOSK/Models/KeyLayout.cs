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
        
        // Advanced Positioning
        public double Rotation { get; set; } = 0; // Degrees
        public double X { get; set; } = -1; // -1 means use Grid (Column)
        public double Y { get; set; } = -1; // -1 means use Grid (Row)
        
        // Fine-grained offsets relative to Grid position
        public double XOffset { get; set; } = 0;
        public double YOffset { get; set; } = 0;
        
        public bool IsHalfSplit { get; set; } // If true, belongs to Right half? Or we define separate arrays?
    }

    public class KeyboardLayout
    {
        public string Name { get; set; } = "Default";
        public List<KeyModel> LeftKeys { get; set; } = new List<KeyModel>();
        public List<KeyModel> RightKeys { get; set; } = new List<KeyModel>();
    }
}
