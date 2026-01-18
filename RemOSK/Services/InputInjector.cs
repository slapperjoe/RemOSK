using System;
using System.Runtime.InteropServices;

namespace RemOSK.Services
{
    public class InputInjector
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
            public static int Size => Marshal.SizeOf(typeof(INPUT));
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        private const int INPUT_MOUSE = 0;
        private const int INPUT_KEYBOARD = 1;
        private const int INPUT_HARDWARE = 2;

        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_SCANCODE = 0x0008;

        public void SendKeystroke(ushort vkCode)
        {
            var inputs = new INPUT[2];

            // Key Down
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vkCode,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };

            // Key Up
            inputs[1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vkCode,
                        wScan = 0,
                        dwFlags = KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };

            var result = SendInput((uint)inputs.Length, inputs, INPUT.Size);
            if (result == 0)
            {
               Console.Error.WriteLine($"[InputInjector] SendInput sent 0 events. Error: {Marshal.GetLastWin32Error()}");
            }
            else
            {
               Console.Error.WriteLine($"[InputInjector] Sent Key: {vkCode}");
            }
        }

        public void SendKeyDown(ushort vkCode)
        {
             var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vkCode,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };
            SendInput(1, new[] { input }, INPUT.Size);
        }

        public void SendKeyUp(ushort vkCode)
        {
             var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vkCode,
                        wScan = 0,
                        dwFlags = KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };
            SendInput(1, new[] { input }, INPUT.Size);
        }

        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;

        [DllImport("user32.dll")]
        private static extern int ShowCursor(bool bShow);

         private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
        
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);
        
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
        
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
        
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public void SendMouseMove(int dx, int dy)
        {
            // Get current cursor position
            if (!GetCursorPos(out POINT currentPos))
                return;
            
            // Calculate new absolute position
            int newX = currentPos.X + dx;
            int newY = currentPos.Y + dy;
            
            // Get screen dimensions
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);
            
            // Clamp to screen bounds
            newX = Math.Max(0, Math.Min(screenWidth - 1, newX));
            newY = Math.Max(0, Math.Min(screenHeight - 1, newY));
            
            // Convert to normalized coordinates (0-65535 range)
            // Formula: (pixel * 65536) / screenSize
            int normalizedX = (newX * 65536) / screenWidth;
            int normalizedY = (newY * 65536) / screenHeight;
            
            // Send as ABSOLUTE mouse input - this emulates hardware mouse
            var input = new INPUT
            {
                type = INPUT_MOUSE,
                U = new InputUnion
                {
                    mi = new MOUSEINPUT
                    {
                        dx = normalizedX,
                        dy = normalizedY,
                        dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };
            
            SendInput(1, new[] { input }, INPUT.Size);
        }

        public void SendLeftClick()
        {
            Console.WriteLine("[InputInjector] Sending LEFT CLICK");
            var inputs = new INPUT[2];
            inputs[0] = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN } } };
            inputs[1] = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP } } };
            uint result = SendInput(2, inputs, INPUT.Size);
            if (result != 2)
            {
                int error = Marshal.GetLastWin32Error();
                Console.WriteLine($"[InputInjector] SendInput FAILED! Result={result}, Error={error}");
            }
            else
            {
                Console.WriteLine("[InputInjector] SendInput succeeded");
            }
        }

        public void SendRightClick()
        {
            Console.WriteLine("[InputInjector] Sending RIGHT CLICK");
            var inputs = new INPUT[2];
            inputs[0] = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_RIGHTDOWN } } };
            inputs[1] = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_RIGHTUP } } };
            SendInput(2, inputs, INPUT.Size);
        }
        
        /// <summary>
        /// Send a left click at specific absolute screen coordinates
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="screenWidth">Screen width for normalization (use RDP viewport width)</param>
        /// <param name="screenHeight">Screen height for normalization (use RDP viewport height)</param>
        public void SendLeftClickAt(int x, int y, int screenWidth, int screenHeight)
        {
            // Normalize coordinates for MOUSEEVENTF_ABSOLUTE (0-65535 range)
            // Uses provided screen bounds (should be RDP viewport, not host screen)
            int normalizedX = (x * 65535) / screenWidth;
            int normalizedY = (y * 65535) / screenHeight;
            
            Console.WriteLine($"[InputInjector] Sending LEFT CLICK at ({x},{y}) viewport ({screenWidth}x{screenHeight}) normalized ({normalizedX},{normalizedY})");
            
            var inputs = new INPUT[3];
            // First: move to position
            inputs[0] = new INPUT 
            { 
                type = INPUT_MOUSE, 
                U = new InputUnion 
                { 
                    mi = new MOUSEINPUT 
                    { 
                        dx = normalizedX, 
                        dy = normalizedY, 
                        dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK 
                    } 
                } 
            };
            // Then: click down
            inputs[1] = new INPUT 
            { 
                type = INPUT_MOUSE, 
                U = new InputUnion 
                { 
                    mi = new MOUSEINPUT 
                    { 
                        dx = normalizedX, 
                        dy = normalizedY, 
                        dwFlags = MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_ABSOLUTE 
                    } 
                } 
            };
            // Then: click up
            inputs[2] = new INPUT 
            { 
                type = INPUT_MOUSE, 
                U = new InputUnion 
                { 
                    mi = new MOUSEINPUT 
                    { 
                        dx = normalizedX, 
                        dy = normalizedY, 
                        dwFlags = MOUSEEVENTF_LEFTUP | MOUSEEVENTF_ABSOLUTE 
                    } 
                } 
            };
            
            uint result = SendInput(3, inputs, INPUT.Size);
            Console.WriteLine($"[InputInjector] SendInput result={result}");
        }
        
        /// <summary>
        /// Send a mouse move to specific absolute screen coordinates
        /// </summary>
        public void SendMouseMoveTo(int x, int y, int screenWidth, int screenHeight)
        {
            // Normalize coordinates for MOUSEEVENTF_ABSOLUTE (0-65535 range)
            int normalizedX = (x * 65535) / screenWidth;
            int normalizedY = (y * 65535) / screenHeight;
            
            var inputs = new INPUT[1];
            inputs[0] = new INPUT 
            { 
                type = INPUT_MOUSE, 
                U = new InputUnion 
                { 
                    mi = new MOUSEINPUT 
                    { 
                        dx = normalizedX, 
                        dy = normalizedY, 
                        dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK
                    } 
                } 
            };
            
            SendInput(1, inputs, INPUT.Size);
        }
        public void SendMiddleClick()
        {
            Console.WriteLine("[InputInjector] Sending MIDDLE CLICK");
            var inputs = new INPUT[2];
            inputs[0] = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_MIDDLEDOWN } } };
            inputs[1] = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_MIDDLEUP } } };
            SendInput(2, inputs, INPUT.Size);
        }
        
        public void SendLeftButtonDown()
        {
            Console.WriteLine("[InputInjector] Sending LEFT BUTTON DOWN (hold)");
            var inputs = new INPUT[1];
            inputs[0] = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN } } };
            SendInput(1, inputs, INPUT.Size);
        }
        
        public void SendLeftButtonUp()
        {
            Console.WriteLine("[InputInjector] Sending LEFT BUTTON UP (release)");
            var inputs = new INPUT[1];
            inputs[0] = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP } } };
            SendInput(1, inputs, INPUT.Size);
        }
    }
}
