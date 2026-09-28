using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace IntentInk.Desktop.Infrastructure;

/// <summary>
/// Generates crisp, high-DPI tray icons at runtime.
/// Avoids missing asset crashes and ensures smooth rendering.
/// </summary>
public static class IconFactory
{
    private static Icon? _activeIcon;
    private static Icon? _pausedIcon;

    public static Icon GetActiveIcon() => _activeIcon ??= CreateIcon(isPaused: false);
    public static Icon GetPausedIcon() => _pausedIcon ??= CreateIcon(isPaused: true);

    public static Icon CreateIcon(bool isPaused)
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            // Background badge circle
            var badgeColor = isPaused ? Color.FromArgb(0x4A, 0x55, 0x68) : Color.FromArgb(0x6C, 0x63, 0xFF);
            using var bgBrush = new SolidBrush(badgeColor);
            g.FillEllipse(bgBrush, 2, 2, size - 4, size - 4);

            // Icon graphics: Pen Nib / Ink Droplet
            var inkColor = isPaused ? Color.FromArgb(0xA0, 0xAE, 0xC0) : Color.White;
            using var inkBrush = new SolidBrush(inkColor);

            if (isPaused)
            {
                // Pause bars
                g.FillRectangle(inkBrush, 11, 10, 3, 12);
                g.FillRectangle(inkBrush, 18, 10, 3, 12);
            }
            else
            {
                // Pen nib polygon
                PointF[] nibPoints =
                [
                    new(16, 7),   // Tip
                    new(22, 16),  // Right shoulder
                    new(19, 23),  // Right base
                    new(13, 23),  // Left base
                    new(10, 16)   // Left shoulder
                ];
                g.FillPolygon(inkBrush, nibPoints);

                // Nib center line
                using var slitPen = new Pen(badgeColor, 1.5f);
                g.DrawLine(slitPen, 16, 7, 16, 17);
                g.FillEllipse(bgBrush, 15, 16, 2, 2);
            }
        }

        var hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }
}
