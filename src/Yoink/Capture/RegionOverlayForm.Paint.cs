using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Linq;
using Yoink.Helpers;
using Yoink.Models;

// Unified dash/border constants for consistency across all selection borders
// Every dashed border in the app uses these same values.

namespace Yoink.Capture;

public sealed partial class RegionOverlayForm
{
    private static void ApplyUiGraphics(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.CompositingMode = CompositingMode.SourceOver;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.None;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.SmoothingMode = SmoothingMode.None;

        // Blit the cached screenshot + committed annotations layer.
        var clip = e.ClipRectangle;
        var committed = GetCommittedAnnotationsBitmap();
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImage(committed, clip, clip, GraphicsUnit.Pixel);
        g.CompositingMode = CompositingMode.SourceOver;

        bool isOcr = _mode == CaptureMode.Ocr;
        bool isScan = _mode == CaptureMode.Scan;
        bool isSelectionMode = _mode is CaptureMode.Rectangle or CaptureMode.Ocr or CaptureMode.Scan or CaptureMode.Sticker;

        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Live tool previews (active drawing in progress)
        PaintAnnotations(g);

        // Select tool: draw selection highlight and handles
        if (_mode == CaptureMode.Select && _selectedAnnotationIndex >= 0 && _selectedAnnotationIndex < _undoStack.Count)
        {
            var bounds = GetAnnotationBounds(_undoStack[_selectedAnnotationIndex]);
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                var selRect = Rectangle.Inflate(bounds, 4, 4);
                var selPen = _selectDashPen ??= new Pen(Color.FromArgb(200, 100, 149, 237), 1.5f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                g.DrawRectangle(selPen, selRect);

                // Corner handles
                int hs = 6;
                var hBrush = _selectHandleBrush ??= new SolidBrush(UiChrome.SurfaceTextPrimary);
                var hPen = _selectHandlePen ??= new Pen(Color.FromArgb(200, 100, 149, 237), 1f);
                var corners = new[] {
                    new Rectangle(selRect.X - hs / 2, selRect.Y - hs / 2, hs, hs),
                    new Rectangle(selRect.Right - hs / 2, selRect.Y - hs / 2, hs, hs),
                    new Rectangle(selRect.X - hs / 2, selRect.Bottom - hs / 2, hs, hs),
                    new Rectangle(selRect.Right - hs / 2, selRect.Bottom - hs / 2, hs, hs),
                };
                foreach (var c in corners) { g.FillRectangle(hBrush, c); g.DrawRectangle(hPen, c); }
            }
        }

        if (_mode == CaptureMode.ColorPicker)
            return; // magnifier is its own layered window, overlay stays static

        if (isSelectionMode && !_isSelecting && _autoDetectActive && _autoDetectRect.Width > 0)
        {
            // Clamp the rect so dashes stay within the visible client area
            var drawRect = ClampRectToClient(_autoDetectRect);
            if (drawRect.Width > 0 && drawRect.Height > 0)
            {
                g.DrawRectangle(ShadowPen(30), drawRect.X + 1, drawRect.Y + 1, drawRect.Width, drawRect.Height);
                g.DrawRectangle(DashedPen(180), drawRect);
            }
            _lastAutoDetectRect = _autoDetectRect;
        }
        else if (isSelectionMode && !_hasSelection && !_isSelecting)
        {
            // Full-screen fallback border: use dashed pattern to match auto-detect style
            g.DrawRectangle(DashedPen(120), 2, 2, ClientSize.Width - 5, ClientSize.Height - 5);
            _lastAutoDetectRect = Rectangle.Empty;
        }

        // Selection borders (on top of everything)
        switch (_mode)
        {
            case CaptureMode.Rectangle when _hasSelection:
            case CaptureMode.Ocr when _hasSelection:
            case CaptureMode.Scan when _hasSelection:
            case CaptureMode.Sticker when _hasSelection:
                // Subtle outer shadow
                {
                    var sr = _selectionRect;
                    sr.Inflate(1, 1);
                    g.DrawRectangle(ShadowPen(40), sr);
                }
                // Static dashed border
                g.DrawRectangle(DashedPen(255), _selectionRect);
                DrawLabel(g, _selectionRect, isOcr, isScan);
                _lastSelectionRect = _selectionRect;
                break;

            case CaptureMode.Freeform when _freeformPoints.Count >= 2:
                var pts = _freeformPoints.ToArray();

                // Find the most recent closed loop anywhere in the path.
                // The old logic only checked "latest point near earlier point", which breaks
                // if the user keeps drawing after closing a loop. We instead scan segment
                // intersections across the whole stroke and use the latest valid one.
                int loopStart = -1;
                int loopEnd = -1;
                if (pts.Length > 6)
                {
                    for (int i = 0; i < pts.Length - 3; i++)
                    {
                        var a1 = pts[i];
                        var a2 = pts[i + 1];
                        for (int j = i + 2; j < pts.Length - 1; j++)
                        {
                            // Skip adjacent segments that share endpoints
                            if (j == i || j == i + 1) continue;
                            var b1 = pts[j];
                            var b2 = pts[j + 1];
                            if (SegmentsIntersect(a1, a2, b1, b2))
                            {
                                loopStart = i + 1;
                                loopEnd = j;
                            }
                        }
                    }

                    // Fallback: if the current tail returns near any earlier point, keep dimming
                    // while the user continues drawing after a closed loop.
                    if (loopStart < 0 && _isSelecting)
                    {
                        var last = pts[^1];
                        for (int ci = 0; ci < pts.Length - 15; ci++)
                        {
                            if (Math.Abs(last.X - pts[ci].X) + Math.Abs(last.Y - pts[ci].Y) < 30)
                            {
                                loopStart = ci;
                                loopEnd = pts.Length - 1;
                            }
                        }
                    }
                }

                // Dim outside closed loop
                bool hasClosed = loopStart >= 0 || !_isSelecting;
                Point[]? closedLoopPts = null;
                if (hasClosed && pts.Length > 2)
                {
                    int from = loopStart >= 0 ? loopStart : 0;
                    int to = loopEnd >= from ? loopEnd + 1 : pts.Length;
                    int count = to - from;
                    if (count >= 3)
                    {
                        var loopPts = new Point[count];
                        Array.Copy(pts, from, loopPts, 0, loopPts.Length);

                        // Remove adjacent duplicates and collapse degenerate runs.
                        loopPts = DeduplicateAdjacent(loopPts);

                        // Guard against pathological loops (e.g. degenerate/near-collinear triangles)
                        if (loopPts.Length >= 3 && PolygonArea(loopPts) >= 4f)
                        {
                            closedLoopPts = loopPts;
                            try
                            {
                                using var path = new GraphicsPath();
                                path.AddPolygon(loopPts);
                                using var region = new Region(new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
                                region.Exclude(path);
                                var dimBrush = _freeformDimBrush ??= new SolidBrush(Color.FromArgb(100, 0, 0, 0));
                                g.FillRegion(dimBrush, region);
                            }
                            catch (ArgumentException)
                            {
                                hasClosed = false;
                                closedLoopPts = null;
                            }
                        }
                        else
                        {
                            hasClosed = false;
                        }
                    }
                    else
                    {
                        hasClosed = false;
                    }
                }

                // Dashed border matching rectangle selection style
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawLines(ShadowPen(30), pts);
                var freeformPen = DashedPen(220);
                g.DrawLines(freeformPen, pts);
                if (hasClosed && closedLoopPts is { Length: >= 3 })
                {
                    g.DrawLine(freeformPen, closedLoopPts[^1], closedLoopPts[0]);
                }
                g.SmoothingMode = SmoothingMode.Default;
                break;
        }

        if (!_hasSelection)
            _lastSelectionRect = Rectangle.Empty;

        g.SmoothingMode = SmoothingMode.Default;
    }

    /// <summary>Clamp a rectangle so it stays 2px inside the client area (prevents dashes from being cut off at screen edges).</summary>
    private Rectangle ClampRectToClient(Rectangle rect)
    {
        const int pad = 2;
        int x = Math.Max(pad, rect.X);
        int y = Math.Max(pad, rect.Y);
        int right = Math.Min(ClientSize.Width - pad - 1, rect.Right);
        int bottom = Math.Min(ClientSize.Height - pad - 1, rect.Bottom);
        return new Rectangle(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
    }

    // Cached GDI objects for hot-path rendering (avoid allocation per frame).
    private static Pen? _cachedDash180, _cachedDash255, _cachedDash120, _cachedDash220;
    private static Pen? _cachedShadow30, _cachedShadow40;
    private static SolidBrush? _freeformDimBrush;
    private static Pen? _selectDashPen, _selectHandlePen;
    private static SolidBrush? _selectHandleBrush;

    /// <summary>Cached dashed pen for selection borders. Reuses instances for common alpha values.</summary>
    private static Pen DashedPen(int alpha, float width = 2f)
    {
        // Fast path: return cached instance for the common alpha values used every frame.
        ref Pen? slot = ref _cachedDash180; // dummy init
        if (width == 2f)
        {
            switch (alpha)
            {
                case 120: slot = ref _cachedDash120; break;
                case 180: slot = ref _cachedDash180; break;
                case 220: slot = ref _cachedDash220; break;
                case 255: slot = ref _cachedDash255; break;
                default: slot = ref _cachedDash180; goto create; // uncommon alpha, always create
            }
            if (slot != null) return slot;
        }
        else goto create;

        create:
        var pen = new Pen(Color.FromArgb(alpha, UiChrome.SurfaceTextPrimary.R, UiChrome.SurfaceTextPrimary.G, UiChrome.SurfaceTextPrimary.B), width)
        {
            DashStyle = DashStyle.Dash,
            DashPattern = new[] { 6f, 4f }
        };
        if (width == 2f && (alpha is 120 or 180 or 220 or 255))
            slot = pen;
        return pen;
    }

    private static Pen ShadowPen(int alpha)
    {
        ref Pen? slot = ref alpha == 30 ? ref _cachedShadow30 : ref _cachedShadow40;
        return slot ??= new Pen(Color.FromArgb(alpha, 0, 0, 0), 4f);
    }

    private static bool SegmentsIntersect(Point p1, Point p2, Point q1, Point q2)
    {
        static long Cross(Point a, Point b, Point c)
            => (long)(b.X - a.X) * (c.Y - a.Y) - (long)(b.Y - a.Y) * (c.X - a.X);

        static bool OnSegment(Point a, Point b, Point p)
            => Math.Min(a.X, b.X) <= p.X && p.X <= Math.Max(a.X, b.X)
            && Math.Min(a.Y, b.Y) <= p.Y && p.Y <= Math.Max(a.Y, b.Y);

        long d1 = Cross(p1, p2, q1);
        long d2 = Cross(p1, p2, q2);
        long d3 = Cross(q1, q2, p1);
        long d4 = Cross(q1, q2, p2);

        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
            ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;

        if (d1 == 0 && OnSegment(p1, p2, q1)) return true;
        if (d2 == 0 && OnSegment(p1, p2, q2)) return true;
        if (d3 == 0 && OnSegment(q1, q2, p1)) return true;
        if (d4 == 0 && OnSegment(q1, q2, p2)) return true;

        return false;
    }

    private static float PolygonArea(Point[] pts)
    {
        if (pts.Length < 3) return 0f;
        long sum = 0;
        for (int i = 0; i < pts.Length; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Length];
            sum += (long)a.X * b.Y - (long)b.X * a.Y;
        }
        return Math.Abs(sum) * 0.5f;
    }

    private static Point[] DeduplicateAdjacent(Point[] pts)
    {
        if (pts.Length <= 1) return pts;
        var list = new List<Point>(pts.Length) { pts[0] };
        for (int i = 1; i < pts.Length; i++)
        {
            if (pts[i] != list[^1])
                list.Add(pts[i]);
        }
        if (list.Count > 1 && list[0] == list[^1])
            list.RemoveAt(list.Count - 1);
        return list.ToArray();
    }

    private void DrawLabel(Graphics g, Rectangle rect, bool isOcr, bool isScan = false)
    {
        string text = GetSelectionLabelText(rect, isOcr, isScan);
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        var font = UiChrome.ChromeFont(10f);
        var lr = GetLabelBounds(rect, isOcr, isScan, text, font, out float lx, out float ly);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = RRect(lr, 8))
        {
            using var lblBg = new SolidBrush(UiChrome.SurfacePill);
            g.FillPath(lblBg, p);
            using var border = new Pen(UiChrome.SurfaceBorderSubtle, 1.4f);
            g.DrawPath(border, p);
        }
        g.SmoothingMode = SmoothingMode.Default;
        using var fg = new SolidBrush(UiChrome.SurfaceTextPrimary);
        g.DrawString(text, font, fg, lx, ly);
        g.TextRenderingHint = TextRenderingHint.SystemDefault;
    }

    private static string GetSelectionLabelText(Rectangle rect, bool isOcr, bool isScan)
        => isOcr ? $"OCR  {rect.Width} x {rect.Height}"
        : isScan ? $"SCAN  {rect.Width} x {rect.Height}"
        : $"{rect.Width} x {rect.Height}";

    private RectangleF GetLabelBounds(Rectangle rect, bool isOcr, bool isScan)
    {
        string text = GetSelectionLabelText(rect, isOcr, isScan);
        var font = UiChrome.ChromeFont(10f);
        return GetLabelBounds(rect, isOcr, isScan, text, font, out _, out _);
    }

    private RectangleF GetLabelBounds(Rectangle rect, bool isOcr, bool isScan, string text, Font font, out float lx, out float ly)
    {
        var sz = TextRenderer.MeasureText(text, font, Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        lx = rect.X;
        ly = rect.Bottom + 8;
        if (ly + sz.Height > ClientSize.Height) ly = rect.Y - sz.Height - 8;
        return new RectangleF(lx - 8, ly - 3, sz.Width + 16, sz.Height + 6);
    }

    private static void PaintShadow(Graphics g, RectangleF rect, float radius, int alpha = 52, float yOffset = 1f)
    {
        var oldSmooth = g.SmoothingMode;
        var oldComp = g.CompositingMode;
        var oldCompQual = g.CompositingQuality;
        var oldPix = g.PixelOffsetMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.CompositingMode = CompositingMode.SourceOver;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var baseRect = rect;
        baseRect.Inflate(2f, 2f);
        var offsets = new (float dx, float dy, int a)[]
        {
            (5f, yOffset + 5f, alpha / 6),
            (3f, yOffset + 3f, alpha / 4),
            (1.5f, yOffset + 1.5f, alpha / 2),
            (0f, yOffset, alpha),
        };
        foreach (var (dx, dy, a) in offsets)
        {
            using var path = RRect(new RectangleF(baseRect.X + dx, baseRect.Y + dy, baseRect.Width, baseRect.Height), radius + 2f);
            using var brush = new SolidBrush(Color.FromArgb(Math.Clamp(a, 1, 255), 0, 0, 0));
            g.FillPath(brush, path);
        }

        g.SmoothingMode = oldSmooth;
        g.CompositingMode = oldComp;
        g.CompositingQuality = oldCompQual;
        g.PixelOffsetMode = oldPix;
    }

    private void PaintRuler(Graphics g, Point from, Point to)
    {
        float dx = to.X - from.X;
        float dy = to.Y - from.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        float angle = MathF.Atan2(dy, dx) * 180f / MathF.PI;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        float nx = 0, ny = 0;
        if (dist > 1) { nx = -dy / dist; ny = dx / dist; }
        const float tickHalf = 6f;

        using var shadowPen = new Pen(Color.FromArgb(70, 0, 0, 0), 3f)
            { StartCap = LineCap.Flat, EndCap = LineCap.Flat };
        g.DrawLine(shadowPen, from.X + 1, from.Y + 1, to.X + 1, to.Y + 1);

        using var pen = new Pen(UiChrome.SurfaceTextPrimary, 1.8f)
            { StartCap = LineCap.Flat, EndCap = LineCap.Flat };
        g.DrawLine(pen, from, to);

        using var tickPen = new Pen(UiChrome.SurfaceTextPrimary, 1.8f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(tickPen, from.X - nx * tickHalf, from.Y - ny * tickHalf,
                            from.X + nx * tickHalf, from.Y + ny * tickHalf);
        g.DrawLine(tickPen, to.X - nx * tickHalf, to.Y - ny * tickHalf,
                            to.X + nx * tickHalf, to.Y + ny * tickHalf);

        string text = $"{(int)dist}px  \u00b7  {Math.Abs(dx):0} \u00d7 {Math.Abs(dy):0}  \u00b7  {angle:0.0}\u00b0";
        var font = UiChrome.ChromeFont(9.5f);
        var sz = g.MeasureString(text, font);
        var mid = new PointF((from.X + to.X) / 2f, (from.Y + to.Y) / 2f);
        var label = new RectangleF(mid.X - sz.Width / 2f - 10, mid.Y - sz.Height - 14, sz.Width + 20, sz.Height + 10);
        PaintShadow(g, label, 8f, 48, 1f);
        using var path = RRect(label, 8f);
        using var bg = new SolidBrush(UiChrome.SurfacePill);
        using var border = new Pen(UiChrome.SurfaceBorderSubtle, 1.4f);
        g.FillPath(bg, path);
        g.DrawPath(border, path);

        using var fg = new SolidBrush(UiChrome.SurfaceTextPrimary);
        g.DrawString(text, font, fg, label.X + 10, label.Y + 5);

        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SystemDefault;
        g.SmoothingMode = SmoothingMode.Default;
    }

    private Graphics GetBlurPreviewGraphics(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));

        if (_blurPreviewBitmap == null || _blurPreviewSize != size)
        {
            _blurPreviewGraphics?.Dispose();
            _blurPreviewBitmap?.Dispose();
            _blurPreviewBitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            _blurPreviewGraphics = Graphics.FromImage(_blurPreviewBitmap);
            _blurPreviewSize = size;
        }

        return _blurPreviewGraphics!;
    }

    private void PaintBlurRect(Graphics g, Rectangle rect)
    {
        int blockSize = Math.Max(6, Math.Min(rect.Width, rect.Height) / 8);
        if (rect.Width < 3 || rect.Height < 3) return;
        var clamped = Rectangle.Intersect(rect, new Rectangle(0, 0, _bmpW, _bmpH));
        if (clamped.Width < 1 || clamped.Height < 1) return;
        int sw = Math.Max(1, clamped.Width / blockSize);
        int sh = Math.Max(1, clamped.Height / blockSize);
        var small = GetBlurPreviewGraphics(new Size(sw, sh));
        small.Clear(Color.Transparent);
        small.InterpolationMode = InterpolationMode.Bilinear;
        small.DrawImage(_screenshot, new Rectangle(0, 0, sw, sh), clamped, GraphicsUnit.Pixel);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(_blurPreviewBitmap!, clamped);
        g.InterpolationMode = InterpolationMode.Default;
        g.PixelOffsetMode = PixelOffsetMode.Default;
    }
}
