using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace IntentInk.Desktop.Infrastructure;

/// <summary>
/// Provides crisp, high-DPI tray and window icons.
/// Loads pre-rendered multi-resolution icon assets or falls back to runtime drawing.
/// </summary>
public static class IconFactory
{
    private static Icon? _activeIcon;
    private static Icon? _pausedIcon;

    public static Icon GetActiveIcon()
    {
        if (_activeIcon != null) return _activeIcon;

        string assetPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(assetPath))
        {
            try
            {
                return _activeIcon = new Icon(assetPath, new Size(32, 32));
            }
            catch
            {
                // Fallback to runtime rendering if icon parsing fails
            }
        }

        return _activeIcon = CreateIcon(isPaused: false);
    }

    public static Icon GetPausedIcon()
    {
        if (_pausedIcon != null) return _pausedIcon;

        string assetPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app_paused.ico");
        if (File.Exists(assetPath))
        {
            try
            {
                return _pausedIcon = new Icon(assetPath, new Size(32, 32));
            }
            catch
            {
                // Fallback to runtime rendering
            }
        }

        return _pausedIcon = CreateIcon(isPaused: true);
    }

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

            // Background badge squircle
            var badgeColor = isPaused ? Color.FromArgb(0x33, 0x41, 0x55) : Color.FromArgb(0x4F, 0x46, 0xE5);
            using var bgBrush = new SolidBrush(badgeColor);
            using var path = new GraphicsPath();
            float r = 7f;
            float d = r * 2;
            path.AddArc(1, 1, d, d, 180, 90);
            path.AddArc(size - 1 - d, 1, d, d, 270, 90);
            path.AddArc(size - 1 - d, size - 1 - d, d, d, 0, 90);
            path.AddArc(1, size - 1 - d, d, d, 90, 90);
            path.CloseFigure();
            g.FillPath(bgBrush, path);

            // Border ring
            var borderColor = isPaused ? Color.FromArgb(0x64, 0x74, 0x8B) : Color.FromArgb(0x38, 0xBD, 0xF8);
            using var borderPen = new Pen(borderColor, 1.2f);
            g.DrawPath(borderPen, path);

            // Icon graphics
            var inkColor = isPaused ? Color.FromArgb(0x94, 0xA3, 0xB8) : Color.White;
            using var inkBrush = new SolidBrush(inkColor);

            if (isPaused)
            {
                // Pause bars
                g.FillRectangle(inkBrush, 11, 9, 3, 14);
                g.FillRectangle(inkBrush, 18, 9, 3, 14);
            }
            else
            {
                // Stylized modern pen nib
                PointF[] nibPoints =
                [
                    new(16, 23),  // Tip pointing downwards
                    new(18, 20),
                    new(23, 15),  // Right shoulder
                    new(20, 9),   // Right base
                    new(12, 9),   // Left base
                    new(9, 15),   // Left shoulder
                    new(14, 20)
                ];
                g.FillPolygon(inkBrush, nibPoints);

                // Slit & breather spark
                using var slitPen = new Pen(badgeColor, 1.2f);
                g.DrawLine(slitPen, 16, 12, 16, 21);

                // Diamond breather hole
                PointF[] spark =
                [
                    new(16, 11),
                    new(17.5f, 13),
                    new(16, 15),
                    new(14.5f, 13)
                ];
                using var sparkBrush = new SolidBrush(Color.FromArgb(0x38, 0xBD, 0xF8));
                g.FillPolygon(sparkBrush, spark);
            }
        }

        var hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }
}
