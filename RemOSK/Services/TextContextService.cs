using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace RemOSK.Services
{
    public class TextContextService
    {
        private static TextContextService? _instance;
        public static TextContextService Instance => _instance ??= new TextContextService();

        private TextContextService() { }

        public class TextContext
        {
            public string Text { get; set; } = "";
            public int CursorPosition { get; set; } = -1;
        }

        public TextContext? GetTextFromFocusedElement()
        {
            try
            {
                var element = AutomationElement.FocusedElement;
                if (element == null) return null;

                object patternObj;
                
                // 1. Try TextPattern (Best for editors) - Get multiple lines of context
                if (element.TryGetCurrentPattern(TextPattern.Pattern, out patternObj))
                {
                    try
                    {
                        var textPattern = (TextPattern)patternObj;
                        var selection = textPattern.GetSelection();
                        
                        if (selection != null && selection.Length > 0)
                        {
                            var cursorRange = selection[0];
                            var contextRange = cursorRange.Clone();
                            
                            // Expand to get more context - multiple lines around cursor
                            contextRange.ExpandToEnclosingUnit(TextUnit.Line);
                            
                            // Try to get the previous and next lines for more context
                            var startRange = contextRange.Clone();
                            startRange.Move(TextUnit.Line, -2); // Go back 2 lines
                            startRange.MoveEndpointByRange(TextPatternRangeEndpoint.End, contextRange, TextPatternRangeEndpoint.End);
                            
                            var endRange = contextRange.Clone();
                            endRange.MoveEndpointByRange(TextPatternRangeEndpoint.Start, contextRange, TextPatternRangeEndpoint.Start);
                            endRange.Move(TextUnit.Line, 2); // Go forward 2 lines
                            
                            // Combine: previous lines + current + next lines
                            contextRange.MoveEndpointByRange(TextPatternRangeEndpoint.Start, startRange, TextPatternRangeEndpoint.Start);
                            contextRange.MoveEndpointByRange(TextPatternRangeEndpoint.End, endRange, TextPatternRangeEndpoint.End);
                            
                            string fullText = contextRange.GetText(1000);
                            
                            // Calculate cursor position within the context
                            // Compare the start of context range to the cursor position
                            int cursorPos = -1;
                            try
                            {
                                var beforeCursor = contextRange.Clone();
                                beforeCursor.MoveEndpointByRange(TextPatternRangeEndpoint.End, cursorRange, TextPatternRangeEndpoint.Start);
                                string textBeforeCursor = beforeCursor.GetText(-1);
                                cursorPos = textBeforeCursor.Length;
                            }
                            catch
                            {
                                // Couldn't determine position, that's okay
                            }
                            
                            return new TextContext 
                            { 
                                Text = fullText, 
                                CursorPosition = cursorPos 
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[TextContext] TextPattern failed: {ex.Message}");
                    }
                }
                
                // 2. Try ValuePattern (Simple inputs, or fallback)
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out patternObj))
                {
                    try
                    {
                        var valuePattern = (ValuePattern)patternObj;
                        string val = valuePattern.Current.Value;
                        
                        if (string.IsNullOrEmpty(val)) 
                            return new TextContext { Text = "", CursorPosition = 0 };

                        // For single-line inputs, show last ~200 chars for context
                        string displayText = val;
                        if (val.Length > 200) 
                            displayText = "..." + val.Substring(val.Length - 200);
                        
                        // ValuePattern doesn't give us cursor position easily
                        // Show cursor at end as best guess
                        return new TextContext 
                        { 
                            Text = displayText, 
                            CursorPosition = displayText.Length 
                        };
                    }
                    catch (Exception ex)
                    {
                         Console.WriteLine($"[TextContext] ValuePattern failed: {ex.Message}");
                    }
                }
                
                return null;
            }
            catch (Exception)
            {
                // Element might have vanished
                return null;
            }
        }
    }
}
