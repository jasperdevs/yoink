using System.Drawing;
using System.Drawing.Drawing2D;

namespace OddSnap.Capture;

internal static class SelectionFrameRenderer
{
    private static readonly Color FillTint = Color.FromArgb(34, 0, 0, 0);
    private static readonly Color Stroke = Color.FromArgb(248, 255, 255, 255);

    public static void DrawRectangle(Graphics g, Rectangle rect, bool fill = true)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        var oldSmoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (fill)
        {
            using var tint = new SolidBrush(FillTint);
            g.FillRectangle(tint, rect);
        }

        var outline = rect;
        outline.Width = Math.Max(1, outline.Width - 1);
        outline.Height = Math.Max(1, outline.Height - 1);

        using var stroke = new Pen(Stroke, 2f) { LineJoin = LineJoin.Miter };
        g.DrawRectangle(stroke, outline);

        g.SmoothingMode = oldSmoothing;
    }

    public static void DrawPath(Graphics g, IReadOnlyList<Point> points, bool closed, bool fill = true)
    {
        if (points.Count < 2)
            return;

        using var path = new GraphicsPath();
        path.AddLines(points.ToArray());
        if (closed && points.Count >= 3)
            path.CloseFigure();

        DrawPath(g, path, fill && closed);
    }

    public static void DrawPath(Graphics g, GraphicsPath path, bool fill = true)
    {
        var oldSmoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (fill)
        {
            using var tint = new SolidBrush(FillTint);
            g.FillPath(tint, path);
        }

        using var stroke = new Pen(Stroke, 2f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawPath(stroke, path);

        g.SmoothingMode = oldSmoothing;
    }
}
