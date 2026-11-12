using WinAlign.Core.Models;
using WinAlign.Core.Native;

namespace WinAlign.Core.Engine;

public static class WindowBoundsCalculator
{
    public record ShadowMargins(int Left, int Top, int Right, int Bottom);

    /// <summary>
    /// Calculates the target (X, Y) coordinates for a window to align with the specified anchor
    /// inside the monitor's working area, taking into account DWM drop-shadow margins.
    /// </summary>
    public static (int X, int Y) CalculateTargetPosition(
        Win32Api.RECT windowRect,
        Win32Api.RECT workArea,
        AlignmentAnchor anchor,
        int margin = 0,
        ShadowMargins? shadow = null)
    {
        var margins = shadow ?? new ShadowMargins(0, 0, 0, 0);

        // Effective visible dimensions
        int visibleWidth = Math.Max(1, windowRect.Width - margins.Left - margins.Right);
        int visibleHeight = Math.Max(1, windowRect.Height - margins.Top - margins.Bottom);

        int targetVisibleLeft;
        switch (anchor)
        {
            case AlignmentAnchor.TopLeft:
            case AlignmentAnchor.CenterLeft:
            case AlignmentAnchor.BottomLeft:
                targetVisibleLeft = workArea.Left + margin;
                break;

            case AlignmentAnchor.CenterTop:
            case AlignmentAnchor.CenterMiddle:
            case AlignmentAnchor.CenterBottom:
                targetVisibleLeft = workArea.Left + Math.Max(0, (workArea.Width - visibleWidth) / 2);
                break;

            case AlignmentAnchor.TopRight:
            case AlignmentAnchor.CenterRight:
            case AlignmentAnchor.BottomRight:
                targetVisibleLeft = workArea.Right - visibleWidth - margin;
                break;

            default:
                targetVisibleLeft = workArea.Left + Math.Max(0, (workArea.Width - visibleWidth) / 2);
                break;
        }

        int targetVisibleTop;
        switch (anchor)
        {
            case AlignmentAnchor.TopLeft:
            case AlignmentAnchor.CenterTop:
            case AlignmentAnchor.TopRight:
                targetVisibleTop = workArea.Top + margin;
                break;

            case AlignmentAnchor.CenterLeft:
            case AlignmentAnchor.CenterMiddle:
            case AlignmentAnchor.CenterRight:
                targetVisibleTop = workArea.Top + Math.Max(0, (workArea.Height - visibleHeight) / 2);
                break;

            case AlignmentAnchor.BottomLeft:
            case AlignmentAnchor.CenterBottom:
            case AlignmentAnchor.BottomRight:
                targetVisibleTop = workArea.Bottom - visibleHeight - margin;
                break;

            default:
                targetVisibleTop = workArea.Top + Math.Max(0, (workArea.Height - visibleHeight) / 2);
                break;
        }

        // Adjust for shadow padding so the visible frame matches target coordinates
        int finalX = targetVisibleLeft - margins.Left;
        int finalY = targetVisibleTop - margins.Top;

        return (finalX, finalY);
    }
}
