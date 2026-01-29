using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RemOSK.Services
{
    public class CaretEventListener : IDisposable
    {
        private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
        private const int OBJID_CARET = -8;

        private IntPtr _hook;
        private readonly WinEventDelegate _delegate; // Keep ref to prevent GC
        private GCHandle _delegateHandle;

        public event Action<IntPtr, int, int>? OnCaretMoved;

        public CaretEventListener()
        {
            _delegate = new WinEventDelegate(WinEventProc);
            _delegateHandle = GCHandle.Alloc(_delegate);
        }

        public void Start()
        {
            if (_hook != IntPtr.Zero) return;

            // Combine flags: OUTOFCONTEXT | SKIPOWNPROCESS
            uint flags = WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS;

            _hook = SetWinEventHook(
                EVENT_OBJECT_LOCATIONCHANGE, 
                EVENT_OBJECT_LOCATIONCHANGE, 
                IntPtr.Zero, 
                _delegate, 
                0, 
                0, 
                flags);
                
            Console.WriteLine($"[CaretListener] Hook installed: {_hook}");
        }

        public void Stop()
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWinEvent(_hook);
                _hook = IntPtr.Zero;
                Console.WriteLine("[CaretListener] Hook removed");
            }
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            // Only care about Caret
            if (idObject != OBJID_CARET) return;

            // Pass the raw IDs to the controller to resolve safely
            try
            {
                OnCaretMoved?.Invoke(hwnd, idObject, idChild);
            }
            catch
            {
                // Ignore
            }
        }

        public void Dispose()
        {
            Stop();
            if (_delegateHandle.IsAllocated)
            {
                _delegateHandle.Free();
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
    }
}
