using System;
using System.Reflection;
using System.Windows.Input;

namespace RemOSK.Services
{
    /// <summary>
    /// Disables WPF's tablet/touch support to force touch input to be treated as mouse input.
    /// This prevents the cursor from hiding when using touch on tablets.
    /// </summary>
    public static class TouchToMouseHelper
    {
        private static bool _disabled = false;

        /// <summary>
        /// Disables WPF tablet support so touch is treated as mouse input.
        /// Must be called after WPF initializes (e.g., after App.Startup).
        /// </summary>
        public static void DisableWPFTabletSupport()
        {
            if (_disabled) return;

            try
            {
                // Get the Tablet assembly and StylusLogic type
                var inputManagerType = typeof(InputManager);
                var stylusLogicProperty = inputManagerType.GetProperty("StylusLogic", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (stylusLogicProperty == null)
                {
                    Console.WriteLine("[TouchToMouseHelper] Could not find StylusLogic property");
                    return;
                }

                var inputManager = InputManager.Current;
                var stylusLogic = stylusLogicProperty.GetValue(inputManager);
                
                if (stylusLogic == null)
                {
                    Console.WriteLine("[TouchToMouseHelper] StylusLogic is null");
                    return;
                }

                var stylusLogicType = stylusLogic.GetType();
                
                // Get TabletDevices collection
                var tabletsProperty = stylusLogicType.GetProperty("TabletDevices",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (tabletsProperty == null)
                {
                    Console.WriteLine("[TouchToMouseHelper] Could not find TabletDevices property");
                    return;
                }

                var tabletDevices = tabletsProperty.GetValue(stylusLogic) as TabletDeviceCollection;
                
                if (tabletDevices == null || tabletDevices.Count == 0)
                {
                    Console.WriteLine("[TouchToMouseHelper] No tablet devices found");
                    _disabled = true;
                    return;
                }

                // Get the OnTabletRemoved method
                var onTabletRemovedMethod = stylusLogicType.GetMethod("OnTabletRemoved",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (onTabletRemovedMethod == null)
                {
                    Console.WriteLine("[TouchToMouseHelper] Could not find OnTabletRemoved method");
                    return;
                }

                // Remove all tablet devices
                int deviceCount = tabletDevices.Count;
                for (int i = deviceCount - 1; i >= 0; i--)
                {
                    var tablet = tabletDevices[i];
                    
                    // Get device ID - need to find property or use index
                    var idProperty = tablet.GetType().GetProperty("Id",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    uint deviceId = (uint)i; // Fallback
                    if (idProperty != null)
                    {
                        var idValue = idProperty.GetValue(tablet);
                        if (idValue != null)
                            deviceId = Convert.ToUInt32(idValue);
                    }

                    Console.WriteLine($"[TouchToMouseHelper] Removing tablet device {i} (ID: {deviceId})");
                    onTabletRemovedMethod.Invoke(stylusLogic, new object[] { deviceId });
                }

                Console.WriteLine($"[TouchToMouseHelper] Disabled WPF tablet support for {deviceCount} devices");
                _disabled = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TouchToMouseHelper] Error disabling tablet support: {ex.Message}");
            }
        }
    }
}
