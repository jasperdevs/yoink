using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Yoink.Native;

namespace Yoink.Capture;

public sealed class ColorPickerForm : Form
{
    private readonly Bitmap _screenshot;
    private readonly Bitmap _dimmed;
    private readonly int[] _pixelData;
    private readonly int _bmpW, _bmpH;
    private readonly Rectangle _virtualBounds;
    private readonly System.Windows.Forms.Timer _trackTimer;

    // Reusable GDI objects to avoid per-frame allocation
    private readonly SolidBrush _bgBrush = new(Color.FromArgb(240, 24, 24, 24));
    private readonly Pen _borderPen = new(Color.FromArgb(50, 255, 255, 255));
    private readonly Pen _gridPen = new(Color.FromArgb(25, 255, 255, 255));
    private readonly Pen _centerPen = new(Color.White, 2f);
    private readonly Pen _crossPen = new(Color.FromArgb(180, 255, 255, 255), 1f);
    private readonly SolidBrush _whiteBrush = new(Color.White);
    private readonly SolidBrush _mutedBrush = new(Color.FromArgb(140, 255, 255, 255));
    private readonly SolidBrush _pixelBrush = new(Color.Black);
    private readonly Font _hexFont = new("Segoe UI", 11f, FontStyle.Bold);
    private readonly Font _rgbFont = new("Segoe UI", 9f);

    private Point _cursorPos;
    private Point _prevCursorPos;
    private Rectangle _prevPanelRect;
    private Color _pickedColor = Color.Black;

    private const int GridSize = 9;
    private const int CellSize = 14;
    private const int MagSize = GridSize * CellSize; // 126px
    private const int InfoHeight = 48;
    private const int Pad = 10;
    private const int PanelW = MagSize + Pad * 2;
    private const int PanelH = MagSize + InfoHeight + Pad * 2;
    private const int CursorOffset = 20;

    public event Action<string>? ColorPicked;
    public event Action? Cancelled;

    public ColorPickerForm(Bitmap screenshot, Rectangle virtualBounds)
    {
        _screenshot = screenshot;
        _virtualBounds = virtualBounds;
        _bmpW = screenshot.Width;
        _bmpH = screenshot.Height;

        // Pre-cache all pixels into int array for fast access
        _pixelData = new int[_bmpW * _bmpH];
        var bits = screenshot.LockBits(new Rectangle(0, 0, _bmpW, _bmpH),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(bits.Scan0, _pixelData, 0, _pixelData.Length);
        screenshot.UnlockBits(bits);

        // Pre-render dimmed screenshot
        _dimmed = new Bitmap(_bmpW, _bmpH, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(_dimmed))
        {
            g.DrawImage(screenshot, 0, 0);
            using var dim = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
            g.FillRectangle(dim, 0, 0, _bmpW, _bmpH);
        }

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(virtualBounds.X, virtualBounds.Y, _bmpW, _bmpH);
        BackColor = Color.Black;
        Cursor = Cursors.Cross;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        KeyPreview = true;

        _trackTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _trackTimer.Tick += (_, _) =>
        {
            User32.GetCursorPos(out var pt);
            var np = new Point(pt.X - _virtualBounds.X, pt.Y - _virtualBounds.Y);
            if (np != _cursorPos)
            {
                _prevCursorPos = _cursorPos;
                _cursorPos = np;

                var oldCross = new Rectangle(_prevCursorPos.X - 12, _prevCursorPos.Y - 12, 24, 24);
                var newCross = new Rectangle(_cursorPos.X - 12, _cursorPos.Y - 12, 24, 24);
                var newPanel = CalcPanelRect(_cursorPos);

                Invalidate(Rectangle.Union(oldCross, newCross));
                if (!_prevPanelRect.IsEmpty) Invalidate(_prevPanelRect);
                Invalidate(newPanel);
                _prevPanelRect = newPanel;
            }
        };
    }

    private Rectangle CalcPanelRect(Point cursor)
    {
        int px = cursor.X + CursorOffset;
        int py = cursor.Y + CursorOffset;
        if (px + PanelW > ClientSize.Width) px = cursor.X - CursorOffset - PanelW;
        if (py + PanelH > ClientSize.Height) py = cursor.Y - CursorOffset - PanelH;
        px = Math.Max(2, px);
        py = Math.Max(2, py);
        return new Rectangle(px - 3, py - 3, PanelW + 6, PanelH + 6);
    }

    private Color GetFastPixel(int x, int y)
    {
        if (x < 0 || x >= _bmpW || y < 0 || y >= _bmpH) return Color.Black;
        return Color.FromArgb(_pixelData[y * _bmpW + x]);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        User32.SetWindowPos(Handle, User32.HWND_TOPMOST, 0, 0, 0, 0,
            User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_SHOWWINDOW);
        User32.SetForegroundWindow(Handle);
        _trackTimer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var clip = e.ClipRectangle;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImage(_dimmed, clip, clip, GraphicsUnit.Pixel);
        g.CompositingMode = CompositingMode.SourceOver;

        int cx = Math.Clamp(_cursorPos.X, 0, _bmpW - 1);
        int cy = Math.Clamp(_cursorPos.Y, 0, _bmpH - 1);
        _pickedColor = GetFastPixel(cx, cy);

        int px = _cursorPos.X + CursorOffset;
        int py = _cursorPos.Y + CursorOffset;
        if (px + PanelW > ClientSize.Width) px = _cursorPos.X - CursorOffset - PanelW;
        if (py + PanelH > ClientSize.Height) py = _cursorPos.Y - CursorOffset - PanelH;
        px = Math.Max(2, px);
        py = Math.Max(2, py);

        var panelRect = new Rectangle(px, py, PanelW, PanelH);

        // Panel background
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = RRect(panelRect, 10))
        {
            g.FillPath(_bgBrush, path);
            g.DrawPath(_borderPen, path);
        }
        g.SmoothingMode = SmoothingMode.Default;

        int magX = px + Pad;
        int magY = py + Pad;
        var magRect = new Rectangle(magX, magY, MagSize, MagSize);

        // Clip to rounded mag area
        var oldClip = g.Clip;
        using (var magClip = new Region(RRect(magRect, 7)))
        {
            g.Clip = magClip;
            int half = GridSize / 2;
            for (int gy = 0; gy < GridSize; gy++)
                for (int gx = 0; gx < GridSize; gx++)
                {
                    var pc = GetFastPixel(cx - half + gx, cy - half + gy);
                    _pixelBrush.Color = pc;
                    g.FillRectangle(_pixelBrush, magX + gx * CellSize, magY + gy * CellSize, CellSize, CellSize);
                }

            // Grid lines
            for (int i = 0; i <= GridSize; i++)
            {
                g.DrawLine(_gridPen, magX + i * CellSize, magY, magX + i * CellSize, magY + MagSize);
                g.DrawLine(_gridPen, magX, magY + i * CellSize, magX + MagSize, magY + i * CellSize);
            }
        }
        g.Clip = oldClip;

        // Center pixel highlight
        var cr = new Rectangle(magX + (GridSize / 2) * CellSize, magY + (GridSize / 2) * CellSize,
            CellSize - 1, CellSize - 1);
        g.DrawRectangle(_centerPen, cr);

        // Info section
        int iy = magY + MagSize + 8;

        // Color swatch with rounded corners
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var swatchRect = new Rectangle(magX, iy, 26, 26);
        using (var swatchPath = RRect(swatchRect, 4))
        {
            _pixelBrush.Color = _pickedColor;
            g.FillPath(_pixelBrush, swatchPath);
        }
        g.SmoothingMode = SmoothingMode.Default;

        string hex = $"#{_pickedColor.R:X2}{_pickedColor.G:X2}{_pickedColor.B:X2}";
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.DrawString(hex, _hexFont, _whiteBrush, magX + 32, iy - 2);
        g.DrawString($"{_pickedColor.R}, {_pickedColor.G}, {_pickedColor.B}", _rgbFont, _mutedBrush, magX + 32, iy + 15);

        // Crosshair
        g.DrawLine(_crossPen, _cursorPos.X - 10, _cursorPos.Y, _cursorPos.X - 3, _cursorPos.Y);
        g.DrawLine(_crossPen, _cursorPos.X + 3, _cursorPos.Y, _cursorPos.X + 10, _cursorPos.Y);
        g.DrawLine(_crossPen, _cursorPos.X, _cursorPos.Y - 10, _cursorPos.X, _cursorPos.Y - 3);
        g.DrawLine(_crossPen, _cursorPos.X, _cursorPos.Y + 3, _cursorPos.X, _cursorPos.Y + 10);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            ColorPicked?.Invoke($"#{_pickedColor.R:X2}{_pickedColor.G:X2}{_pickedColor.B:X2}");
        else if (e.Button == MouseButtons.Right)
            Cancelled?.Invoke();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Cancelled?.Invoke();
    }

    private static GraphicsPath RRect(Rectangle r, int rad)
    {
        var p = new GraphicsPath();
        int d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trackTimer.Dispose();
            _dimmed.Dispose();
            _bgBrush.Dispose();
            _borderPen.Dispose();
            _gridPen.Dispose();
            _centerPen.Dispose();
            _crossPen.Dispose();
            _whiteBrush.Dispose();
            _mutedBrush.Dispose();
            _pixelBrush.Dispose();
            _hexFont.Dispose();
            _rgbFont.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override CreateParams CreateParams
    { get { var cp = base.CreateParams; cp.ExStyle |= 0x80; return cp; } }
}
