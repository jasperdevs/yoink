using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Yoink.Models;

namespace Yoink.Capture;

public sealed class RegionOverlayForm : Form
{
    private readonly Bitmap _screenshot;
    private readonly Bitmap _dimmedScreenshot;
    private readonly Bitmap _extraDimmedScreenshot;
    private readonly Rectangle _virtualBounds;

    private CaptureMode _mode = CaptureMode.Rectangle;

    private bool _isSelecting;
    private Point _selectionStart;
    private Point _selectionEnd;
    private Rectangle _selectionRect;
    private Rectangle _prevSelectionRect; // for dirty-rect tracking
    private bool _hasSelection;
    private bool _hasDragged;

    private readonly List<Point> _freeformPoints = new();
    private Rectangle _hoveredWindowRect;

    // Toolbar
    private readonly Rectangle[] _toolbarButtons = new Rectangle[5];
    private int _hoveredButton = -1;
    private Rectangle _toolbarRect;
    private const int ToolbarHeight = 44;
    private const int ButtonSize = 36;
    private const int ButtonSpacing = 4;
    private const int ToolbarTopMargin = 16;

    // Toolbar slide-in
    private float _toolbarAnimProgress;
    private readonly System.Windows.Forms.Timer _animTimer;
    private readonly DateTime _showTime;

    public event Action<Rectangle>? RegionSelected;
    public event Action<Bitmap>? FreeformSelected;
    public event Action? SelectionCancelled;

    public RegionOverlayForm(Bitmap screenshot, Rectangle virtualBounds,
        CaptureMode initialMode = CaptureMode.Rectangle)
    {
        _screenshot = screenshot;
        _virtualBounds = virtualBounds;
        _mode = initialMode;
        _dimmedScreenshot = CreateDimmedBitmap(screenshot, 102);
        _extraDimmedScreenshot = CreateDimmedBitmap(screenshot, 140);
        _showTime = DateTime.UtcNow;
        _toolbarAnimProgress = 0f;

        SetupForm();
        CalculateToolbarLayout();

        _animTimer = new System.Windows.Forms.Timer { Interval = 12 };
        _animTimer.Tick += (_, _) =>
        {
            float elapsed = (float)(DateTime.UtcNow - _showTime).TotalMilliseconds;
            _toolbarAnimProgress = Math.Min(1f, elapsed / 180f);
            if (_toolbarAnimProgress >= 1f) _animTimer.Stop();
            InvalidateToolbar();
        };
        _animTimer.Start();
    }

    private void SetupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Normal;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(_virtualBounds.X, _virtualBounds.Y,
            _virtualBounds.Width, _virtualBounds.Height);
        Cursor = Cursors.Cross;
        BackColor = Color.Black;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        KeyPreview = true;
    }

    private void CalculateToolbarLayout()
    {
        int totalW = ButtonSize * 5 + ButtonSpacing * 4;
        int toolbarW = totalW + 16;
        int toolbarX = (ClientSize.Width - toolbarW) / 2;
        _toolbarRect = new Rectangle(toolbarX, ToolbarTopMargin, toolbarW, ToolbarHeight);

        for (int i = 0; i < 5; i++)
            _toolbarButtons[i] = new Rectangle(
                _toolbarRect.X + 8 + i * (ButtonSize + ButtonSpacing),
                _toolbarRect.Y + (ToolbarHeight - ButtonSize) / 2,
                ButtonSize, ButtonSize);
    }

    /// <summary>Pre-renders screenshot with a dark overlay baked in.</summary>
    private static Bitmap CreateDimmedBitmap(Bitmap original, int alpha)
    {
        var dimmed = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(dimmed);
        g.DrawImage(original, 0, 0);
        using var overlay = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));
        g.FillRectangle(overlay, 0, 0, dimmed.Width, dimmed.Height);
        return dimmed;
    }

    // ─── Paint ─────────────────────────────────────────────────────

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var clip = e.ClipRectangle;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;

        // Decide which base to draw: extra-dimmed when selection active, normal otherwise
        bool selectionActive = _hasSelection ||
            (_hoveredWindowRect.Width > 0 && _mode == CaptureMode.Window);

        var baseBmp = selectionActive ? _extraDimmedScreenshot : _dimmedScreenshot;

        // Blit only the clip region for performance
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImage(baseBmp, clip, clip, GraphicsUnit.Pixel);
        g.CompositingMode = CompositingMode.SourceOver;

        switch (_mode)
        {
            case CaptureMode.Rectangle: PaintRectSelection(g); break;
            case CaptureMode.Freeform: PaintFreeform(g); break;
            case CaptureMode.Window: PaintWindowHighlight(g); break;
        }

        PaintToolbar(g);
    }

    private void PaintRectSelection(Graphics g)
    {
        if (!_hasSelection) return;
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImage(_screenshot, _selectionRect, _selectionRect, GraphicsUnit.Pixel);
        g.CompositingMode = CompositingMode.SourceOver;
        using var pen = new Pen(Color.White, 2f);
        g.DrawRectangle(pen, _selectionRect);
        DrawDimensionLabel(g, _selectionRect);
    }

    private void PaintFreeform(Graphics g)
    {
        if (_freeformPoints.Count < 2) return;
        using var pen = new Pen(Color.White, 2f);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawLines(pen, _freeformPoints.ToArray());
        if (!_isSelecting && _freeformPoints.Count > 2)
            g.DrawLine(pen, _freeformPoints[^1], _freeformPoints[0]);
        g.SmoothingMode = SmoothingMode.Default;
    }

    private void PaintWindowHighlight(Graphics g)
    {
        if (_hoveredWindowRect.Width <= 0) return;
        var clipped = Rectangle.Intersect(_hoveredWindowRect,
            new Rectangle(0, 0, _screenshot.Width, _screenshot.Height));
        if (clipped.Width > 0 && clipped.Height > 0)
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(_screenshot, clipped, clipped, GraphicsUnit.Pixel);
            g.CompositingMode = CompositingMode.SourceOver;
        }
        using var pen = new Pen(Color.FromArgb(200, 0, 120, 215), 3f);
        g.DrawRectangle(pen, _hoveredWindowRect);
    }

    private void PaintToolbar(Graphics g)
    {
        float t = EaseOutCubic(_toolbarAnimProgress);
        int offsetY = (int)((1f - t) * -30);
        int alpha = (int)(t * 200);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        var animRect = new Rectangle(_toolbarRect.X, _toolbarRect.Y + offsetY,
            _toolbarRect.Width, _toolbarRect.Height);

        using var bgPath = RoundedRect(animRect, 10);
        using var bgBrush = new SolidBrush(Color.FromArgb(alpha, 32, 32, 32));
        g.FillPath(bgBrush, bgPath);
        using var borderPen = new Pen(Color.FromArgb((int)(t * 50), 255, 255, 255), 1f);
        g.DrawPath(borderPen, bgPath);

        string[] icons = { "rect", "free", "win", "full", "close" };
        for (int i = 0; i < 5; i++)
        {
            var btn = new Rectangle(_toolbarButtons[i].X, _toolbarButtons[i].Y + offsetY,
                _toolbarButtons[i].Width, _toolbarButtons[i].Height);
            bool isActive = i < 4 && (int)_mode == i;
            bool isHovered = _hoveredButton == i;

            if (isActive || isHovered)
            {
                using var btnPath = RoundedRect(btn, 6);
                using var btnBrush = new SolidBrush(
                    isActive ? Color.FromArgb((int)(t * 80), 255, 255, 255) :
                    Color.FromArgb((int)(t * 40), 255, 255, 255));
                g.FillPath(btnBrush, btnPath);
            }

            int ia = (int)(t * 255);
            DrawIcon(g, icons[i], btn,
                i == 4 ? Color.FromArgb(ia * 200 / 255, 255, 255, 255)
                       : Color.FromArgb(ia, 255, 255, 255));
        }
        g.SmoothingMode = SmoothingMode.Default;
    }

    private static float EaseOutCubic(float x) => 1f - MathF.Pow(1f - x, 3f);

    private static void DrawIcon(Graphics g, string icon, Rectangle b, Color c)
    {
        using var pen = new Pen(c, 1.6f);
        int cx = b.X + b.Width / 2, cy = b.Y + b.Height / 2, s = 8;
        switch (icon)
        {
            case "rect": g.DrawRectangle(pen, cx - s, cy - s + 2, s * 2, s * 2 - 4); break;
            case "free": g.DrawBezier(pen, cx - s, cy + s - 4, cx - s + 4, cy - s,
                             cx + s - 4, cy + s - 2, cx + s, cy - s + 4); break;
            case "win":
                g.DrawRectangle(pen, cx - s, cy - s + 2, s * 2, s * 2 - 4);
                g.DrawLine(pen, cx - s, cy - s + 7, cx + s, cy - s + 7); break;
            case "full":
                g.DrawRectangle(pen, cx - s, cy - s + 1, s * 2, s * 2 - 5);
                g.DrawLine(pen, cx - 4, cy + s - 2, cx + 4, cy + s - 2); break;
            case "close":
                g.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
                g.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5); break;
        }
    }

    private void DrawDimensionLabel(Graphics g, Rectangle rect)
    {
        string text = $"{rect.Width} x {rect.Height}";
        using var font = new Font("Segoe UI", 11f);
        var sz = g.MeasureString(text, font);
        float lx = rect.X, ly = rect.Bottom + 8;
        if (ly + sz.Height > ClientSize.Height) ly = rect.Y - sz.Height - 8;
        var lr = new RectangleF(lx - 6, ly - 3, sz.Width + 12, sz.Height + 6);
        using var bg = new SolidBrush(Color.FromArgb(210, 24, 24, 24));
        using var fg = new SolidBrush(Color.White);
        using var p = RoundedRect(lr, 6);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillPath(bg, p);
        g.SmoothingMode = SmoothingMode.Default;
        g.DrawString(text, font, fg, lx, ly);
    }

    private static GraphicsPath RoundedRect(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // ─── Mouse ─────────────────────────────────────────────────────

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right) { Cancel(); return; }
        if (e.Button != MouseButtons.Left) { base.OnMouseDown(e); return; }

        int btnIdx = GetToolbarButtonAt(e.Location);
        if (btnIdx >= 0) { HandleToolbarClick(btnIdx); base.OnMouseDown(e); return; }

        _hasDragged = false;

        switch (_mode)
        {
            case CaptureMode.Rectangle:
                _isSelecting = true;
                _selectionStart = e.Location;
                _selectionEnd = e.Location;
                _hasSelection = false;
                _prevSelectionRect = Rectangle.Empty;
                break;
            case CaptureMode.Freeform:
                _isSelecting = true;
                _freeformPoints.Clear();
                _freeformPoints.Add(e.Location);
                break;
            case CaptureMode.Window:
                if (_hoveredWindowRect.Width > 0)
                    RegionSelected?.Invoke(_hoveredWindowRect);
                break;
            case CaptureMode.Fullscreen:
                RegionSelected?.Invoke(new Rectangle(0, 0, _screenshot.Width, _screenshot.Height));
                break;
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int btnIdx = GetToolbarButtonAt(e.Location);
        if (btnIdx != _hoveredButton) { _hoveredButton = btnIdx; InvalidateToolbar(); }
        Cursor = btnIdx >= 0 ? Cursors.Hand : Cursors.Cross;

        switch (_mode)
        {
            case CaptureMode.Rectangle when _isSelecting:
                _selectionEnd = e.Location;
                var newRect = GetNormalizedRect(_selectionStart, _selectionEnd);
                if (newRect.Width > 3 || newRect.Height > 3) _hasDragged = true;
                _hasSelection = newRect.Width > 2 && newRect.Height > 2;

                // Dirty-rect: invalidate union of old and new selection (with padding for stroke + label)
                var dirty = _prevSelectionRect.IsEmpty ? newRect :
                    Rectangle.Union(_prevSelectionRect, newRect);
                dirty.Inflate(10, 40); // pad for stroke width + dimension label
                _selectionRect = newRect;
                _prevSelectionRect = newRect;
                Invalidate(dirty);
                break;

            case CaptureMode.Freeform when _isSelecting:
                _freeformPoints.Add(e.Location);
                _hasDragged = true;
                // Invalidate just around the last two points
                if (_freeformPoints.Count >= 2)
                {
                    var p1 = _freeformPoints[^2];
                    var p2 = _freeformPoints[^1];
                    var fr = Rectangle.FromLTRB(
                        Math.Min(p1.X, p2.X) - 4, Math.Min(p1.Y, p2.Y) - 4,
                        Math.Max(p1.X, p2.X) + 4, Math.Max(p1.Y, p2.Y) + 4);
                    Invalidate(fr);
                }
                break;

            case CaptureMode.Window:
                var wr = WindowDetector.GetWindowRectAtPoint(e.Location, _virtualBounds);
                if (wr != _hoveredWindowRect)
                {
                    var oldWr = _hoveredWindowRect;
                    _hoveredWindowRect = wr;
                    if (!oldWr.IsEmpty) { oldWr.Inflate(4, 4); Invalidate(oldWr); }
                    if (!wr.IsEmpty) { var nr = wr; nr.Inflate(4, 4); Invalidate(nr); }
                    else Invalidate();
                }
                break;
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) { base.OnMouseUp(e); return; }

        switch (_mode)
        {
            case CaptureMode.Rectangle when _isSelecting:
                _isSelecting = false;
                if (!_hasDragged)
                    RegionSelected?.Invoke(new Rectangle(0, 0, _screenshot.Width, _screenshot.Height));
                else
                {
                    _selectionEnd = e.Location;
                    _selectionRect = GetNormalizedRect(_selectionStart, _selectionEnd);
                    if (_selectionRect.Width > 2 && _selectionRect.Height > 2)
                        RegionSelected?.Invoke(_selectionRect);
                    else { _hasSelection = false; Invalidate(); }
                }
                break;
            case CaptureMode.Freeform when _isSelecting:
                _isSelecting = false;
                if (!_hasDragged)
                    RegionSelected?.Invoke(new Rectangle(0, 0, _screenshot.Width, _screenshot.Height));
                else if (_freeformPoints.Count > 2)
                    CompleteFreeform();
                break;
        }
        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Cancel();
        if (e.KeyCode == Keys.D1) SetMode(CaptureMode.Rectangle);
        if (e.KeyCode == Keys.D2) SetMode(CaptureMode.Freeform);
        if (e.KeyCode == Keys.D3) SetMode(CaptureMode.Window);
        if (e.KeyCode == Keys.D4) SetMode(CaptureMode.Fullscreen);
        base.OnKeyDown(e);
    }

    // ─── Toolbar ───────────────────────────────────────────────────

    private int GetToolbarButtonAt(Point p)
    {
        for (int i = 0; i < _toolbarButtons.Length; i++)
            if (_toolbarButtons[i].Contains(p)) return i;
        return -1;
    }

    private void HandleToolbarClick(int idx)
    {
        if (idx == 4) { Cancel(); return; }
        SetMode((CaptureMode)idx);
    }

    private void SetMode(CaptureMode mode)
    {
        _mode = mode;
        _hasSelection = false;
        _hasDragged = false;
        _freeformPoints.Clear();
        _hoveredWindowRect = Rectangle.Empty;
        _prevSelectionRect = Rectangle.Empty;
        _isSelecting = false;
        Invalidate();
    }

    private void InvalidateToolbar()
    {
        Invalidate(new Rectangle(_toolbarRect.X - 2, _toolbarRect.Y - 32,
            _toolbarRect.Width + 4, _toolbarRect.Height + 36));
    }

    // ─── Freeform ──────────────────────────────────────────────────

    private void CompleteFreeform()
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        foreach (var p in _freeformPoints)
        {
            minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y);
        }
        var bbox = new Rectangle(minX, minY, maxX - minX, maxY - minY);
        if (bbox.Width < 3 || bbox.Height < 3) return;

        var result = new Bitmap(bbox.Width, bbox.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(result))
        {
            var pts = _freeformPoints.Select(p => new Point(p.X - minX, p.Y - minY)).ToArray();
            using var clip = new GraphicsPath();
            clip.AddPolygon(pts);
            g.SetClip(clip);
            g.DrawImage(_screenshot, new Rectangle(0, 0, bbox.Width, bbox.Height), bbox, GraphicsUnit.Pixel);
        }
        FreeformSelected?.Invoke(result);
    }

    private void Cancel() => SelectionCancelled?.Invoke();

    private static Rectangle GetNormalizedRect(Point s, Point e)
    {
        return new Rectangle(Math.Min(s.X, e.X), Math.Min(s.Y, e.Y),
            Math.Abs(e.X - s.X), Math.Abs(e.Y - s.Y));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dimmedScreenshot.Dispose();
            _extraDimmedScreenshot.Dispose();
            _animTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x80; return cp; }
    }
}
