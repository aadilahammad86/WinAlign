using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace WinAlign.UI.Tray;

public static class TrayIconHelper
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    /// <summary>
    /// Creates a crisp, modern vector-style 32x32 application icon representing window alignment.
    /// </summary>
    public static Icon CreateAppIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        // Draw rounded monitor/outer frame
        using (var framePen = new Pen(Color.FromArgb(200, 200, 200), 2f))
        using (var bgBrush = new SolidBrush(Color.FromArgb(32, 32, 32)))
        {
            var rect = new Rectangle(2, 2, 27, 27);
            g.FillRectangle(bgBrush, rect);
            g.DrawRectangle(framePen, rect);
        }

        // Draw inner 3x3 grid dots / alignment markers
        using var inactiveBrush = new SolidBrush(Color.FromArgb(90, 90, 90));
        using var activeBrush = new SolidBrush(Color.FromArgb(0, 150, 255)); // Fluent blue

        int[] coords = { 6, 15, 23 };

        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                // Highlight the center anchor as the emblem
                bool isCenter = (row == 1 && col == 1);
                var brush = isCenter ? activeBrush : inactiveBrush;
                int size = isCenter ? 5 : 3;
                int offset = isCenter ? 2 : 1;

                g.FillEllipse(brush, coords[col] - offset, coords[row] - offset, size, size);
            }
        }

        IntPtr hIcon = bitmap.GetHicon();
        Icon icon = (Icon)Icon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);

        return icon;
    }
}
