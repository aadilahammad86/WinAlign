using WinAlign.Core.Engine;
using WinAlign.Core.Models;
using WinAlign.Core.Native;
using Xunit;

namespace WinAlign.Tests;

public class WindowBoundsCalculatorTests
{
    [Fact]
    public void CalculateTargetPosition_CenterMiddle_CalculatesExactCenter()
    {
        // 1920x1080 display with 40px taskbar at bottom (work area height = 1040)
        var workArea = new Win32Api.RECT(0, 0, 1920, 1040);
        var windowRect = new Win32Api.RECT(100, 100, 900, 700); // 800 x 600

        var (x, y) = WindowBoundsCalculator.CalculateTargetPosition(
            windowRect,
            workArea,
            AlignmentAnchor.CenterMiddle,
            margin: 0);

        // Expected X: (1920 - 800) / 2 = 560
        // Expected Y: (1040 - 600) / 2 = 220
        Assert.Equal(560, x);
        Assert.Equal(220, y);
    }

    [Theory]
    [InlineData(AlignmentAnchor.TopLeft, 10, 10)]
    [InlineData(AlignmentAnchor.CenterTop, 560, 10)]
    [InlineData(AlignmentAnchor.TopRight, 1110, 10)]
    [InlineData(AlignmentAnchor.CenterLeft, 10, 220)]
    [InlineData(AlignmentAnchor.CenterMiddle, 560, 220)]
    [InlineData(AlignmentAnchor.CenterRight, 1110, 220)]
    [InlineData(AlignmentAnchor.BottomLeft, 10, 430)]
    [InlineData(AlignmentAnchor.CenterBottom, 560, 430)]
    [InlineData(AlignmentAnchor.BottomRight, 1110, 430)]
    public void CalculateTargetPosition_AllNineAnchors_WorkAreaWithMargin(
        AlignmentAnchor anchor,
        int expectedX,
        int expectedY)
    {
        // 1920x1040 work area, 800x600 window, 10px margin
        var workArea = new Win32Api.RECT(0, 0, 1920, 1040);
        var windowRect = new Win32Api.RECT(0, 0, 800, 600);

        var (x, y) = WindowBoundsCalculator.CalculateTargetPosition(
            windowRect,
            workArea,
            anchor,
            margin: 10);

        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
    }

    [Fact]
    public void CalculateTargetPosition_WithDwmShadowPadding_CompensatesOffset()
    {
        // Typical Windows 11 invisible border: 7px left, 0px top, 7px right, 7px bottom
        var workArea = new Win32Api.RECT(0, 0, 1920, 1040);
        var windowRect = new Win32Api.RECT(0, 0, 814, 607); // 800x600 visible frame + 14px horizontal, 7px vertical
        var shadow = new WindowBoundsCalculator.ShadowMargins(7, 0, 7, 7);

        var (x, y) = WindowBoundsCalculator.CalculateTargetPosition(
            windowRect,
            workArea,
            AlignmentAnchor.TopLeft,
            margin: 10,
            shadow: shadow);

        // Visible frame target is (10, 10).
        // Actual window rect left must be: 10 - 7 = 3
        // Actual window rect top must be: 10 - 0 = 10
        Assert.Equal(3, x);
        Assert.Equal(10, y);
    }

    [Fact]
    public void CalculateTargetPosition_SecondaryMonitor_CalculatesRelativeCoordinates()
    {
        // Secondary 2560x1440 display placed to the right of primary (Left = 1920, Right = 4480, Top = 0, Bottom = 1400)
        var workArea = new Win32Api.RECT(1920, 0, 4480, 1400);
        var windowRect = new Win32Api.RECT(0, 0, 1000, 800);

        var (x, y) = WindowBoundsCalculator.CalculateTargetPosition(
            windowRect,
            workArea,
            AlignmentAnchor.CenterMiddle,
            margin: 0);

        // Expected X: 1920 + (2560 - 1000) / 2 = 1920 + 780 = 2700
        // Expected Y: 0 + (1400 - 800) / 2 = 300
        Assert.Equal(2700, x);
        Assert.Equal(300, y);
    }
}
