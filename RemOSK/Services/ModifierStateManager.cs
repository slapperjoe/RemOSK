using System;
using System.Collections.Generic;
using RemOSK.Models;

namespace RemOSK.Services
{
    public class ModifierStateManager
    {
        private readonly InputInjector _injector;

        public bool IsShiftActive { get; private set; }
        public bool IsCtrlActive { get; private set; }
        public bool IsAltActive { get; private set; }
        public bool IsWinActive { get; private set; }

        public bool IsShiftLocked { get; private set; }
        private DateTime _lastShiftTime = DateTime.MinValue;

        public event EventHandler? StateChanged;

        public ModifierStateManager(InputInjector injector)
        {
            _injector = injector;
        }

        public void HandleKey(int virtualKeyCode, bool isPressed)
        {
            // Simple toggle logic for now - "Sticky Keys"
            // If modifier is pressed, toggle internal state and hold/release key
            switch (virtualKeyCode)
            {
                case 160: // LShift
                case 161: // RShift
                    HandleShift((ushort)virtualKeyCode);
                    break;
                case 162: // LCtrl
                case 163: // RCtrl
                    ToggleCtrl((ushort)virtualKeyCode);
                    break;
                case 164: // LAlt
                case 165: // RAlt
                    ToggleAlt((ushort)virtualKeyCode);
                    break;
                case 91: // LWin
                    ToggleWin((ushort)virtualKeyCode);
                    break;
                default:
                    // Regular key. 
                    _injector.SendKeystroke((ushort)virtualKeyCode);
                    
                    // Auto-release logic for sticky keys
                    if (IsShiftActive && !IsShiftLocked) 
                    {
                        Console.WriteLine($"[ModifierManager] Auto-releasing Shift after {virtualKeyCode}");
                        ToggleShift(160); // This will KeyUp
                    }
                    if (IsCtrlActive) ToggleCtrl(162);
                    if (IsAltActive) ToggleAlt(164);
                    if (IsWinActive) ToggleWin(91);
                    break;
            }
        }

        private void HandleShift(ushort vk)
        {
            // If already locked, any press unlocks it
            if (IsShiftLocked)
            {
                IsShiftLocked = false;
                IsShiftActive = false;
                // Force Release Both
                _injector.SendKeyUp(160);
                _injector.SendKeyUp(161);
                StateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Check for Double-Tap
            var now = DateTime.Now;
            if ((now - _lastShiftTime).TotalMilliseconds < 500)
            {
                // Double tap detected! Lock it.
                IsShiftLocked = true;
                IsShiftActive = true;
                _injector.SendKeyDown(vk); // Ensure down
                
                Console.WriteLine("[ModifierManager] Shift Locked");
            }
            else
            {
                // Normal Latch (Toggle)
                ToggleShift(vk);
            }
            _lastShiftTime = now;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ToggleShift(ushort vk)
        {
            IsShiftActive = !IsShiftActive;
            if (IsShiftActive) 
            {
                _injector.SendKeyDown(vk);
            }
            else 
            {
                // Force Release Both to prevent "wrong shift stuck" issues
                _injector.SendKeyUp(160); 
                _injector.SendKeyUp(161);
            }
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ToggleCtrl(ushort vk)
        {
            IsCtrlActive = !IsCtrlActive;
             if (IsCtrlActive) 
             {
                 _injector.SendKeyDown(vk);
             }
             else 
             {
                 _injector.SendKeyUp(162);
                 _injector.SendKeyUp(163);
             }
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ToggleAlt(ushort vk)
        {
            IsAltActive = !IsAltActive;
             if (IsAltActive) 
             {
                 _injector.SendKeyDown(vk);
             }
             else 
             {
                 _injector.SendKeyUp(164);
                 _injector.SendKeyUp(165);
             }
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        
         private void ToggleWin(ushort vk)
        {
            IsWinActive = !IsWinActive;
             if (IsWinActive) _injector.SendKeyDown(vk); else _injector.SendKeyUp(vk);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
