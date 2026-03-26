using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Yoink.Native;

namespace Yoink.Capture;

public sealed class ColorPickerForm : Form
{
    private readonly Bitmap _screenshot;
    private readonly Bitmap _dimmed;
    private readonly int[] _pixelData; // pre-cached ARGB pixel data
    private readonly int _bmpW, _bmpH;
    private readonly Rectangle _virtualBounds;
    private readonly System.Windows.Forms.Timer _trackTimer;

    private Point _cursorPos;
    private Color _pickedColor = Color.Black;

    private const int GridSize = 7;
    private const int CellSize = 12;
    private const int MagSize = GridSize * CellSize;
    private const int InfoHeight = 40;
    private const int Pad = 8;
    private const int PanelW = MagSize + Pad * 2;
    private const int PanelH = MagSize + InfoHeight + Pad * 2;
    private const int CursorOffset = 18;

    public event Action<string>? ColorPicked;
    public event Action? Cancelled;

    public ColorPickerForm(Bitmap screenshot, Rectangle virtualBounds)
    {
        _screenshot = screenshot;
        _virtualBounds = virtualBounds;
        _bmpW = screenshot.Width;
        _bmpH = screenshot.Height;

        // Pre-cache all pixels into int array for fast access (no GetPixel)
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
            if (np != _cursorPos) { _cursorPos = np; Invalidate(); }
        };
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
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImage(_dimmed, 0, 0);
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

        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = RRect(panelRect, 8))
        {
            using var bg = new SolidBrush(Color.FromArgb(235, 28, 28, 28));
            g.FillPath(bg, path);
            using var border = new Pen(Color.FromArgb(40, 255, 255, 255));
            g.DrawPath(border, path);
        }
        g.SmoothingMode = SmoothingMode.Default;

        int magX = px + Pad;
        int magY = py + Pad;
        var magRect = new Rectangle(magX, magY, MagSize, MagSize);

        // Clip to rounded mag area
        var oldClip = g.Clip;
        using (var magClip = new Region(RRect(magRect, 6)))
        {
            g.Clip = magClip;
            int half = GridSize / 2;
            for (int gy = 0; gy < GridSize; gy++)
                for (int gx = 0; gx < GridSize; gx++)
                {
                    var pc = GetFastPixel(cx - half + gx, cy - half + gy);
                    using var brush = new SolidBrush(pc);
                    g.FillRectangle(brush, magX + gx * CellSize, magY + gy * CellSize, CellSize, CellSize);
                }

            using var gp = new Pen(Color.FromArgb(20, 255, 255, 255));
            for (int i = 0; i <= GridSize; i++)
            {
                g.DrawLine(gp, magX + i * CellSize, magY, magX + i * CellSize, magY + MagSize);
                g.DrawLine(gp, magX, magY + i * CellSize, magX + MagSize, magY + i * CellSize);
            }
        }
        g.Clip = oldClip;

        // Center pixel
        var cr = new Rectangle(magX + (GridSize / 2) * CellSize - 1, magY + (GridSize / 2) * CellSize - 1,
            CellSize + 1, CellSize + 1);
        using (var hp = new Pen(Color.White, 1.5f))
            g.DrawRectangle(hp, cr);

        // Info
        int iy = magY + MagSize + 6;
        using (var sb = new SolidBrush(_pickedColor))
            g.FillRectangle(sb, magX, iy, 22, 22);

        string hex = $"#{_pickedColor.R:X2}{_pickedColor.G:X2}{_pickedColor.B:X2}";
        using var fb = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        using var fs = new Font("Segoe UI", 8f);
        using var w = new SolidBrush(Color.White);
        using var m = new SolidBrush(Color.FromArgb(120, 255, 255, 255));
        g.DrawString(hex, fb, w, magX + 28, iy - 1);
        g.DrawString($"{_pickedColor.R}, {_pickedColor.G}, {_pickedColor.B}", fs, m, magX + 28, iy + 13);

        // Crosshair
        using var cp = new Pen(Color.FromArgb(150, 255, 255, 255), 1f);
        g.DrawLine(cp, _cursorPos.X - 8, _cursorPos.Y, _cursorPos.X - 3, _cursorPos.Y);
        g.DrawLine(cp, _cursorPos.X + 3, _cursorPos.Y, _cursorPos.X + 8, _cursorPos.Y);
        g.DrawLine(cp, _cursorPos.X, _cursorPos.Y - 8, _cursorPos.X, _cursorPos.Y - 3);
        g.DrawLine(cp, _cursorPos.X, _cursorPos.Y + 3, _cursorPos.X, _cursorPos.Y + 8);
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
        if (disposing) { _trackTimer.Dispose(); _dimmed.Dispose(); }
        base.Dispose(disposing);
    }

    protected override CreateParams CreateParams
    { get { var cp = base.CreateParams; cp.ExStyle |= 0x80; return cp; } }
}
