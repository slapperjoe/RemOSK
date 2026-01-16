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
                    ToggleShift((ushort)virtualKeyCode);
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
                    // Regular key. If modifiers are active, send them first? 
                    // No, InputInjector should just send the key. 
                    // BUT, if we have sticky keys, we might want to release them after the next non-modifier key.
                    // For simply "holding" modifiers, we keep them KeyDown.
                    
                     _injector.SendKeystroke((ushort)virtualKeyCode);
                    
                    // Auto-release logic for sticky keys (Shift usually releases after next char)
                    if (IsShiftActive) ToggleShift(160); 
                    if (IsCtrlActive) ToggleCtrl(162);
                    if (IsAltActive) ToggleAlt(164);
                    if (IsWinActive) ToggleWin(91);
                    break;
            }
        }

        private void ToggleShift(ushort vk)
        {
            IsShiftActive = !IsShiftActive;
            if (IsShiftActive) _injector.SendKeyDown(vk); else _injector.SendKeyUp(vk);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ToggleCtrl(ushort vk)
        {
            IsCtrlActive = !IsCtrlActive;
             if (IsCtrlActive) _injector.SendKeyDown(vk); else _injector.SendKeyUp(vk);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ToggleAlt(ushort vk)
        {
            IsAltActive = !IsAltActive;
             if (IsAltActive) _injector.SendKeyDown(vk); else _injector.SendKeyUp(vk);
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
