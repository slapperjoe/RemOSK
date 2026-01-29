using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using RemOSK.Views;

namespace RemOSK.Services
{
    public class TextPreviewController : IDisposable
    {
        private readonly KeyboardWindowManager _manager;
        private DispatcherTimer? _pollTimer;
        private string? _lastText;
        private int _lastCursorPos = -1;
        private bool _isActive;

        public event Action<TextContextService.TextContext?>? OnTextPreviewUpdated;
        public event Action? OnActivityDetected;

        public TextPreviewController(KeyboardWindowManager manager)
        {
            _manager = manager;
        }

        public void Start()
        {
            if (_isActive) return;
            _isActive = true;
            Console.WriteLine("[TextPreview] Controller Started (Focus Mode)");

            // Force initial state
            OnTextPreviewUpdated?.Invoke(null); 

            _pollTimer = new DispatcherTimer();
            _pollTimer.Interval = TimeSpan.FromMilliseconds(200); // Check 5 times/sec
            _pollTimer.Tick += PollTimer_Tick;
            _pollTimer.Start();
        }

        public void Stop()
        {
            if (!_isActive) return;
            _isActive = false;
            Console.WriteLine("[TextPreview] Controller Stopped");

            _pollTimer?.Stop();
            _pollTimer = null;
        }

        private void PollTimer_Tick(object? sender, EventArgs e)
        {
            CheckFocusAndText();
        }

        private void CheckFocusAndText()
        {
            // Run UIA on background thread to avoid blocking UI
            System.Threading.Tasks.Task.Run(() =>
            {
                var context = TextContextService.Instance.GetTextFromFocusedElement();
                
                // Marshal back
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (context != null)
                    {
                        // We have valid text from a focused element -> ACTIVITY!
                        OnActivityDetected?.Invoke();
                    }

                    if (context != null && (context.Text != _lastText || context.CursorPosition != _lastCursorPos))
                    {
                        Console.WriteLine($"[TextPreview] Text: '{context.Text.Substring(0, Math.Min(context.Text.Length, 20))}...' Cursor: {context.CursorPosition}");
                        _lastText = context.Text;
                        _lastCursorPos = context.CursorPosition;
                        OnTextPreviewUpdated?.Invoke(context);
                    }
                });
            });
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
