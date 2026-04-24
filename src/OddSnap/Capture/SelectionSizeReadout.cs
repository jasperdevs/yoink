using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace OddSnap.Capture;

internal static class SelectionSizeReadout
{
    private const int CursorOffsetX = 11;
    private const int CursorOffsetY = 7;
    private const int Gap = 3;

    public static Rectangle GetBounds(Point cursor, Rectangle selection, Font font, Rectangle clientBounds, IReadOnlyList<string>? details = null)
    {
        if (selection.Width <= 2 || selection.Height <= 2)
            return Rectangle.Empty;

        return GetTextBounds(cursor, Measure(BuildLines(selection, details), font), clientBounds);
    }

    public static void Draw(Graphics g, Point cursor, Rectangle selection, Font font, Rectangle clientBounds, IReadOnlyList<string>? details = null)
    {
        if (selection.Width <= 2 || selection.Height <= 2)
            return;

        var oldTextHint = g.TextRenderingHint;
        var oldSmoothing = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        var lines = BuildLines(selection, details);
        var size = Measure(lines, font);
        var textRect = GetTextBounds(cursor, size, clientBounds);
        using var haloBrush = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
        using var textBrush = new SolidBrush(Color.FromArgb(245, 255, 255, 255));

        int lineHeight = GetLineHeight(font);
        for (int i = 0; i < lines.Length; i++)
        {
            var point = new PointF(textRect.X, textRect.Y + (lineHeight - 1) * i);
            DrawReadableLine(g, lines[i], font, haloBrush, textBrush, point);
        }

        g.TextRenderingHint = oldTextHint;
        g.SmoothingMode = oldSmoothing;
    }

    private static string[] BuildLines(Rectangle selection, IReadOnlyList<string>? details)
    {
        if (details is null || details.Count == 0)
            return [selection.Width.ToString(), selection.Height.ToString()];

        var lines = new List<string>(details.Count + 2);
        foreach (var detail in details)
        {
            if (!string.IsNullOrWhiteSpace(detail))
                lines.Add(detail);
        }
        lines.Add(selection.Width.ToString());
        lines.Add(selection.Height.ToString());
        return lines.ToArray();
    }

    private static Size Measure(IReadOnlyList<string> lines, Font font)
    {
        int width = 1;
        foreach (var line in lines)
        {
            var size = TextRenderer.MeasureText(line, font, Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            width = Math.Max(width, size.Width);
        }

        return new Size(width, GetLineHeight(font) * lines.Count - 1);
    }

    private static Rectangle GetTextBounds(Point cursor, Size size, Rectangle clientBounds)
    {
        int x = cursor.X + CursorOffsetX;
        int y = cursor.Y + CursorOffsetY;

        if (x + size.Width > clientBounds.Right - Gap)
            x = cursor.X - CursorOffsetX - size.Width;
        if (y + size.Height > clientBounds.Bottom - Gap)
            y = cursor.Y - CursorOffsetY - size.Height;

        x = Math.Clamp(x, clientBounds.Left + Gap, Math.Max(clientBounds.Left + Gap, clientBounds.Right - size.Width - Gap));
        y = Math.Clamp(y, clientBounds.Top + Gap, Math.Max(clientBounds.Top + Gap, clientBounds.Bottom - size.Height - Gap));
        return new Rectangle(x, y, size.Width, size.Height);
    }

    private static int GetLineHeight(Font font)
        => Math.Max(1, TextRenderer.MeasureText("0", font, Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Height - 1);

    private static void DrawReadableLine(Graphics g, string text, Font font, Brush haloBrush, Brush textBrush, PointF point)
    {
        g.DrawString(text, font, haloBrush, point.X + 1, point.Y);
        g.DrawString(text, font, haloBrush, point.X - 1, point.Y);
        g.DrawString(text, font, haloBrush, point.X, point.Y + 1);
        g.DrawString(text, font, haloBrush, point.X, point.Y - 1);
        g.DrawString(text, font, textBrush, point);
    }
}
