using System.Collections.Generic;

namespace RemOSK.Services
{
    public static class KeyLabelHelper
    {
        private static readonly Dictionary<string, string> ShiftMap = new Dictionary<string, string>
        {
            { "`", "~" }, { "1", "!" }, { "2", "@" }, { "3", "#" }, { "4", "$" }, { "5", "%" },
            { "6", "^" }, { "7", "&" }, { "8", "*" }, { "9", "(" }, { "0", ")" }, { "-", "_" }, { "=", "+" },
            { "[", "{" }, { "]", "}" }, { "\\", "|" }, { ";", ":" }, { "'", "\"" }, { ",", "<" }, { ".", ">" }, { "/", "?" }
        };

        public static string GetLabel(string baseLabel, bool shiftActive, bool capsActive)
        {
            if (string.IsNullOrEmpty(baseLabel)) return baseLabel;

            // Single letters
            if (baseLabel.Length == 1 && char.IsLetter(baseLabel[0]))
            {
                // XOR logic: Shift+a -> A, Caps+a -> A, Shift+Caps+a -> a
                bool upper = shiftActive ^ capsActive; 
                return upper ? baseLabel.ToUpper() : baseLabel.ToLower();
            }

            // Symbols / Numbers (Shift only, Caps usually doesn't affect 1->!)
            if (shiftActive)
            {
                if (ShiftMap.TryGetValue(baseLabel, out var shifted))
                {
                    return shifted;
                }
            }

            return baseLabel;
        }
    }
}
