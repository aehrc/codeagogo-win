using System.Drawing;

namespace SNOMEDLookup.Tests;

public class SelectionPositionHelperTests
{
    private readonly Rectangle _defaultScreenBounds = new(0, 0, 1920, 1080);
    private const int DefaultPopupWidth = 440;
    private const int DefaultPopupHeight = 280;
    private const int DefaultMargin = 12;

    [Fact]
    public void CalculatePopupPosition_WithSelectionBounds_PositionsBelowSelection()
    {
        // Selection in the middle of the screen
        var selectionBounds = new Rectangle(400, 300, 100, 20);
        var mousePosition = new Point(450, 310);

        var position = SelectionPositionHelper.CalculatePopupPosition(
            selectionBounds,
            mousePosition,
            DefaultPopupWidth,
            DefaultPopupHeight,
            _defaultScreenBounds,
            DefaultMargin);

        // Popup should be positioned below the selection
        Assert.Equal(selectionBounds.Left, position.X);
        Assert.Equal(selectionBounds.Bottom + DefaultMargin, position.Y);
    }

    [Fact]
    public void CalculatePopupPosition_WithoutSelectionBounds_PositionsNearMouse()
    {
        var mousePosition = new Point(500, 400);

        var position = SelectionPositionHelper.CalculatePopupPosition(
            null, // No selection bounds
            mousePosition,
            DefaultPopupWidth,
            DefaultPopupHeight,
            _defaultScreenBounds,
            DefaultMargin);

        // Popup should be positioned near the mouse cursor
        Assert.Equal(mousePosition.X + DefaultMargin, position.X);
        Assert.Equal(mousePosition.Y + DefaultMargin, position.Y);
    }

    [Fact]
    public void CalculatePopupPosition_SelectionNearBottomEdge_PositionsAboveSelection()
    {
        // Selection near the bottom of the screen
        var selectionBounds = new Rectangle(400, 900, 100, 20);
        var mousePosition = new Point(450, 910);

        var position = SelectionPositionHelper.CalculatePopupPosition(
            selectionBounds,
            mousePosition,
            DefaultPopupWidth,
            DefaultPopupHeight,
            _defaultScreenBounds,
            DefaultMargin);

        // Popup should be positioned above the selection (since below would go off-screen)
        Assert.Equal(selectionBounds.Left, position.X);
        Assert.Equal(selectionBounds.Top - DefaultPopupHeight - DefaultMargin, position.Y);
    }

    [Fact]
    public void CalculatePopupPosition_SelectionNearRightEdge_ClampsToScreenBounds()
    {
        // Selection near the right edge of the screen
        var selectionBounds = new Rectangle(1700, 300, 100, 20);
        var mousePosition = new Point(1750, 310);

        var position = SelectionPositionHelper.CalculatePopupPosition(
            selectionBounds,
            mousePosition,
            DefaultPopupWidth,
            DefaultPopupHeight,
            _defaultScreenBounds,
            DefaultMargin);

        // Popup X should be clamped to fit within screen (1920 - 440 = 1480)
        Assert.Equal(_defaultScreenBounds.Width - DefaultPopupWidth, position.X);
        Assert.Equal(selectionBounds.Bottom + DefaultMargin, position.Y);
    }

    [Fact]
    public void CalculatePopupPosition_MouseNearRightEdge_ClampsToScreenBounds()
    {
        var mousePosition = new Point(1800, 400);

        var position = SelectionPositionHelper.CalculatePopupPosition(
            null, // No selection bounds
            mousePosition,
            DefaultPopupWidth,
            DefaultPopupHeight,
            _defaultScreenBounds,
            DefaultMargin);

        // Popup X should be clamped to fit within screen
        Assert.Equal(_defaultScreenBounds.Width - DefaultPopupWidth, position.X);
        Assert.Equal(mousePosition.Y + DefaultMargin, position.Y);
    }

    [Fact]
    public void CalculatePopupPosition_MouseNearBottomEdge_ClampsToScreenBounds()
    {
        var mousePosition = new Point(500, 900);

        var position = SelectionPositionHelper.CalculatePopupPosition(
            null, // No selection bounds
            mousePosition,
            DefaultPopupWidth,
            DefaultPopupHeight,
            _defaultScreenBounds,
            DefaultMargin);

        // Popup Y should be clamped to fit within screen
        Assert.Equal(mousePosition.X + DefaultMargin, position.X);
        Assert.Equal(_defaultScreenBounds.Height - DefaultPopupHeight, position.Y);
    }

    [Fact]
    public void CalculatePopupPosition_MouseNearTopLeftCorner_ClampsToMinBounds()
    {
        var mousePosition = new Point(-50, -50);

        var position = SelectionPositionHelper.CalculatePopupPosition(
            null, // No selection bounds
            mousePosition,
            DefaultPopupWidth,
            DefaultPopupHeight,
            _defaultScreenBounds,
            DefaultMargin);

        // Popup position should be clamped to 0,0
        Assert.Equal(0, position.X);
        Assert.Equal(0, position.Y);
    }

    [Fact]
    public void CalculatePopupPosition_SelectionNearTopLeftCorner_ClampsToMinBounds()
    {
        // Selection at the top-left corner
        var selectionBounds = new Rectangle(10, 10, 50, 20);
        var mousePosition = new Point(35, 20);

        var position = SelectionPositionHelper.CalculatePopupPosition(
            selectionBounds,
            mousePosition,
            DefaultPopupWidth,
            DefaultPopupHeight,
            _defaultScreenBounds,
            DefaultMargin);

        // X should be clamped to 0 (since selection.Left is 10 but that's fine)
        // Y should be below the selection
        Assert.Equal(selectionBounds.Left, position.X);
        Assert.Equal(selectionBounds.Bottom + DefaultMargin, position.Y);
    }

    [Fact]
    public void CalculatePopupPosition_SelectionVerticallyConstrainedToTop_PositionsToRight()
    {
        // Selection at the very bottom where neither above nor below works
        // This tests the "position to the right" fallback
        var selectionBounds = new Rectangle(100, 900, 50, 20);
        var mousePosition = new Point(125, 910);
        var tallPopup = 950; // Popup taller than available space above

        var position = SelectionPositionHelper.CalculatePopupPosition(
            selectionBounds,
            mousePosition,
            DefaultPopupWidth,
            tallPopup,
            _defaultScreenBounds,
            DefaultMargin);

        // Should try to position to the right of selection
        Assert.Equal(selectionBounds.Right + DefaultMargin, position.X);
        // Y gets clamped to fit
        Assert.True(position.Y >= 0);
        Assert.True(position.Y + tallPopup <= _defaultScreenBounds.Height);
    }

    [Fact]
    public void CalculatePopupPosition_EmptySelectionBounds_FallsBackToMouse()
    {
        // Empty rectangle (width = 0, height = 0) should be treated as no selection
        var emptySelection = new Rectangle(400, 300, 0, 0);
        var mousePosition = new Point(500, 400);

        var position = SelectionPositionHelper.CalculatePopupPosition(
            emptySelection,
            mousePosition,
            DefaultPopupWidth,
            DefaultPopupHeight,
            _defaultScreenBounds,
            DefaultMargin);

        // Should fall back to mouse position
        Assert.Equal(mousePosition.X + DefaultMargin, position.X);
        Assert.Equal(mousePosition.Y + DefaultMargin, position.Y);
    }

    [Fact]
    public void CalculatePopupPosition_MultiMonitorOffset_HandlesNonZeroScreenOrigin()
    {
        // Simulate second monitor with negative X origin
        var secondMonitorBounds = new Rectangle(-1920, 0, 1920, 1080);
        var mousePosition = new Point(-960, 540);

        var position = SelectionPositionHelper.CalculatePopupPosition(
            null,
            mousePosition,
            DefaultPopupWidth,
            DefaultPopupHeight,
            secondMonitorBounds,
            DefaultMargin);

        // Should position near mouse, clamped to the second monitor bounds
        Assert.True(position.X >= secondMonitorBounds.Left);
        Assert.True(position.X + DefaultPopupWidth <= secondMonitorBounds.Right);
        Assert.True(position.Y >= secondMonitorBounds.Top);
        Assert.True(position.Y + DefaultPopupHeight <= secondMonitorBounds.Bottom);
    }

    [Fact]
    public void CalculatePopupPosition_VeryLongSelection_StillPositionsCorrectly()
    {
        // Very long horizontal selection (like a whole paragraph)
        var selectionBounds = new Rectangle(50, 300, 1800, 20);
        var mousePosition = new Point(1000, 310);

        var position = SelectionPositionHelper.CalculatePopupPosition(
            selectionBounds,
            mousePosition,
            DefaultPopupWidth,
            DefaultPopupHeight,
            _defaultScreenBounds,
            DefaultMargin);

        // Should still position at left of selection, below it
        Assert.Equal(selectionBounds.Left, position.X);
        Assert.Equal(selectionBounds.Bottom + DefaultMargin, position.Y);
    }

    [Fact]
    public void CalculatePopupPosition_MultiLineSelection_UsesTopLeft()
    {
        // Multi-line selection (taller than single line)
        var selectionBounds = new Rectangle(100, 200, 400, 80);
        var mousePosition = new Point(300, 280);

        var position = SelectionPositionHelper.CalculatePopupPosition(
            selectionBounds,
            mousePosition,
            DefaultPopupWidth,
            DefaultPopupHeight,
            _defaultScreenBounds,
            DefaultMargin);

        // Should position at left of selection, below it
        Assert.Equal(selectionBounds.Left, position.X);
        Assert.Equal(selectionBounds.Bottom + DefaultMargin, position.Y);
    }
}
