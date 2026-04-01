using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Media.Imaging;

namespace Yoink.Helpers;

/// <summary>
/// Shared rendering helpers for semantic tool icons.
/// These are used for icons that should look the same everywhere instead of relying on font glyph availability.
/// </summary>
public static class ToolIcons
{
    public static BitmapSource RenderToolIconWpf(string toolId, char glyph, Color color, int size)
    {
        if (toolId == "sticker")
            return RenderStickerWpf(color, size);

        if (toolId == "_record")
            return RenderRecordWpf(color, size);

        return IconFont.RenderGlyphWpf(glyph, color, size);
    }

    public static BitmapSource RenderStickerWpf(Color color, int size) =>
        RenderShapeWpf(size, g => DrawSticker(g, size, color));

    public static BitmapSource RenderRecordWpf(Color color, int size) =>
        RenderShapeWpf(size, g => DrawRecord(g, size, color));

    private static BitmapSource RenderShapeWpf(int size, Action<Graphics> draw)
    {
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            draw(g);
        }

        var hBmp = bmp.GetHbitmap();
        try
        {
            var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBmp, IntPtr.Zero, System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        finally
        {
            Native.User32.DeleteObject(hBmp);
        }
    }

    private static void DrawSticker(Graphics g, int size, Color color)
    {
        var stroke = Math.Max(1.6f, size * 0.09f);
        var corner = Math.Max(3.2f, size * 0.22f);
        var insetX = size * 0.24f;
        var insetY = size * 0.20f;
        var body = new RectangleF(insetX, insetY, size - insetX * 2f, size - insetY * 2f);

        using var pen = new Pen(color, stroke)
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        using var path = new GraphicsPath();
        path.AddArc(body.X, body.Y, corner, corner, 180, 90);
        path.AddLine(body.X + corner / 2f, body.Y, body.Right - corner, body.Y);
        path.AddLine(body.Right - corner, body.Y, body.Right, body.Y + corner / 2f);
        path.AddLine(body.Right, body.Y + corner / 2f, body.Right, body.Bottom - corner / 2f);
        path.AddArc(body.Right - corner, body.Bottom - corner, corner, corner, 0, 90);
        path.AddArc(body.X, body.Bottom - corner, corner, corner, 90, 90);
        path.CloseFigure();
        g.DrawPath(pen, path);

        g.DrawLine(pen, body.Right - corner, body.Y, body.Right - corner, body.Y + corner / 2f);
        g.DrawLine(pen, body.Right - corner, body.Y + corner / 2f, body.Right, body.Y + corner / 2f);
    }

    private static void DrawRecord(Graphics g, int size, Color color)
    {
        var stroke = Math.Max(1.5f, size * 0.08f);
        var ring = new RectangleF(size * 0.19f, size * 0.19f, size * 0.62f, size * 0.62f);
        var dot = new RectangleF(size * 0.41f, size * 0.41f, size * 0.18f, size * 0.18f);

        using var pen = new Pen(color, stroke)
        {
            LineJoin = LineJoin.Round
        };
        using var brush = new SolidBrush(color);

        g.DrawEllipse(pen, ring);
        g.FillEllipse(brush, dot);
    }
}
