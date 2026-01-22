using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using Rect = System.Windows.Rect;

namespace SNOMEDLookup;

/// <summary>
/// Provides methods to get the screen position of selected text in the foreground window.
/// </summary>
public static class SelectionPositionHelper
{
    /// <summary>
    /// Attempts to get the bounding rectangle of the selected text in the specified window.
    /// </summary>
    /// <param name="windowHandle">The handle of the target window.</param>
    /// <returns>The bounding rectangle if available, null otherwise.</returns>
    public static Rectangle? GetSelectionBounds(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            Log.Debug("SelectionPositionHelper: No window handle provided");
            return null;
        }

        try
        {
            // Try UI Automation first
            var rect = GetSelectionBoundsViaAutomation(windowHandle);
            if (rect.HasValue)
            {
                Log.Debug($"SelectionPositionHelper: Got selection bounds via UI Automation: {rect.Value}");
                return rect;
            }

            Log.Debug("SelectionPositionHelper: UI Automation did not return selection bounds");
            return null;
        }
        catch (Exception ex)
        {
            Log.Debug($"SelectionPositionHelper: Failed to get selection bounds: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Uses Windows UI Automation to get the bounding rectangle of selected text.
    /// </summary>
    private static Rectangle? GetSelectionBoundsViaAutomation(IntPtr windowHandle)
    {
        try
        {
            // Get the automation element for the window
            var element = AutomationElement.FromHandle(windowHandle);
            if (element == null)
            {
                Log.Debug("SelectionPositionHelper: Could not get AutomationElement from window handle");
                return null;
            }

            // Try to find a focused element with text selection
            var focusedElement = GetFocusedTextElement(element);
            if (focusedElement == null)
            {
                Log.Debug("SelectionPositionHelper: No focused text element found");
                return null;
            }

            // Check if the element supports TextPattern
            if (focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out object patternObj))
            {
                var textPattern = (TextPattern)patternObj;
                var selection = textPattern.GetSelection();

                if (selection != null && selection.Length > 0)
                {
                    var selectedRange = selection[0];
                    var rects = selectedRange.GetBoundingRectangles();

                    if (rects != null && rects.Length > 0)
                    {
                        // Combine all bounding rectangles to get the full selection bounds
                        var combinedRect = CombineBoundingRectangles(rects);
                        if (combinedRect.HasValue)
                        {
                            return combinedRect;
                        }
                    }
                }
            }

            return null;
        }
        catch (ElementNotAvailableException)
        {
            Log.Debug("SelectionPositionHelper: Element no longer available");
            return null;
        }
        catch (Exception ex)
        {
            Log.Debug($"SelectionPositionHelper: UI Automation error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Attempts to find the focused element that supports text selection.
    /// </summary>
    private static AutomationElement? GetFocusedTextElement(AutomationElement rootElement)
    {
        try
        {
            // First try the globally focused element
            var focused = AutomationElement.FocusedElement;
            if (focused != null && SupportsTextPattern(focused))
            {
                return focused;
            }

            // If that doesn't work, search within the window
            var condition = new AndCondition(
                new PropertyCondition(AutomationElement.IsTextPatternAvailableProperty, true),
                new PropertyCondition(AutomationElement.HasKeyboardFocusProperty, true)
            );

            var textElement = rootElement.FindFirst(TreeScope.Descendants, condition);
            if (textElement != null)
            {
                return textElement;
            }

            // As a fallback, just find any text element with IsTextPatternAvailable
            var textCondition = new PropertyCondition(AutomationElement.IsTextPatternAvailableProperty, true);
            return rootElement.FindFirst(TreeScope.Descendants, textCondition);
        }
        catch
        {
            return null;
        }
    }

    private static bool SupportsTextPattern(AutomationElement element)
    {
        try
        {
            return element.TryGetCurrentPattern(TextPattern.Pattern, out _);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Combines multiple bounding rectangles into a single rectangle that encompasses them all.
    /// </summary>
    private static Rectangle? CombineBoundingRectangles(Rect[] rects)
    {
        if (rects == null || rects.Length == 0)
            return null;

        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;

        foreach (var rect in rects)
        {
            if (rect.IsEmpty || double.IsInfinity(rect.X) || double.IsInfinity(rect.Y))
                continue;

            minX = Math.Min(minX, rect.X);
            minY = Math.Min(minY, rect.Y);
            maxX = Math.Max(maxX, rect.X + rect.Width);
            maxY = Math.Max(maxY, rect.Y + rect.Height);
        }

        if (minX == double.MaxValue || minY == double.MaxValue)
            return null;

        return new Rectangle(
            (int)minX,
            (int)minY,
            (int)(maxX - minX),
            (int)(maxY - minY)
        );
    }

    /// <summary>
    /// Calculates the optimal popup position based on selection bounds, mouse position, and screen constraints.
    /// </summary>
    /// <param name="selectionBounds">The bounding rectangle of the selected text (if available).</param>
    /// <param name="mousePosition">The current mouse position.</param>
    /// <param name="popupWidth">The width of the popup window.</param>
    /// <param name="popupHeight">The height of the popup window.</param>
    /// <param name="screenBounds">The working area of the screen (excluding taskbar).</param>
    /// <param name="margin">Margin between popup and anchor point.</param>
    /// <returns>The optimal position for the popup window.</returns>
    public static Point CalculatePopupPosition(
        Rectangle? selectionBounds,
        Point mousePosition,
        double popupWidth,
        double popupHeight,
        Rectangle screenBounds,
        int margin = 12)
    {
        double desiredX, desiredY;

        if (selectionBounds.HasValue && selectionBounds.Value.Width > 0 && selectionBounds.Value.Height > 0)
        {
            var sel = selectionBounds.Value;
            Log.Debug($"Positioning popup near selection: {sel}");

            // Primary: Try to position below the selection
            desiredX = sel.Left;
            desiredY = sel.Bottom + margin;

            // Check if popup would go below screen bottom
            if (desiredY + popupHeight > screenBounds.Bottom)
            {
                // Try positioning above the selection
                desiredY = sel.Top - popupHeight - margin;

                // If that would go above screen top, position to the right of selection
                if (desiredY < screenBounds.Top)
                {
                    desiredX = sel.Right + margin;
                    desiredY = sel.Top;

                    // If right side doesn't fit, try left side
                    if (desiredX + popupWidth > screenBounds.Right)
                    {
                        desiredX = sel.Left - popupWidth - margin;
                    }
                }
            }
        }
        else
        {
            // Fallback: Position near mouse cursor
            Log.Debug("Positioning popup near mouse cursor (no selection bounds available)");
            desiredX = mousePosition.X + margin;
            desiredY = mousePosition.Y + margin;
        }

        // Clamp position to ensure popup stays within screen bounds
        desiredX = ClampToRange(desiredX, screenBounds.Left, screenBounds.Right - popupWidth);
        desiredY = ClampToRange(desiredY, screenBounds.Top, screenBounds.Bottom - popupHeight);

        return new Point((int)desiredX, (int)desiredY);
    }

    /// <summary>
    /// Clamps a value to the specified range.
    /// </summary>
    private static double ClampToRange(double value, double min, double max)
    {
        if (max < min) max = min; // Handle edge case where popup is larger than screen
        return Math.Max(min, Math.Min(value, max));
    }
}
