using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Yoink.Native;

namespace Yoink.Capture;

public sealed class ColorPickerForm : Form
{
    private readonly Bitmap _screenshot;
    private readonly Rectangle _virtualBounds;
    private readonly System.Windows.Forms.Timer _trackTimer;

    private Point _cursorPos;
    private Color _pickedColor = Color.Black;

    private const int GridSize = 7;
    private const int CellSize = 12;
    private const int MagSize = GridSize * CellSize;
    private const int InfoHeight = 44;
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

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(virtualBounds.X, virtualBounds.Y,
            virtualBounds.Width, virtualBounds.Height);
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
            var newPos = new Point(pt.X - _virtualBounds.X, pt.Y - _virtualBounds.Y);
            if (newPos != _cursorPos) { _cursorPos = newPos; Invalidate(); }
        };
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

        // Draw screenshot dimmed
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImage(_screenshot, 0, 0);
        g.CompositingMode = CompositingMode.SourceOver;
        using (var dim = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
            g.FillRectangle(dim, 0, 0, ClientSize.Width, ClientSize.Height);

        int cx = Math.Clamp(_cursorPos.X, 0, _screenshot.Width - 1);
        int cy = Math.Clamp(_cursorPos.Y, 0, _screenshot.Height - 1);
        _pickedColor = _screenshot.GetPixel(cx, cy);

        // Panel position - stay tight to cursor
        int px = _cursorPos.X + CursorOffset;
        int py = _cursorPos.Y + CursorOffset;
        if (px + PanelW > ClientSize.Width) px = _cursorPos.X - CursorOffset - PanelW;
        if (py + PanelH > ClientSize.Height) py = _cursorPos.Y - CursorOffset - PanelH;
        px = Math.Max(2, px);
        py = Math.Max(2, py);

        var panelRect = new Rectangle(px, py, PanelW, PanelH);

        // Draw panel with rounded clip
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var panelPath = RoundRect(panelRect, 8);

        // Background
        using (var bg = new SolidBrush(Color.FromArgb(235, 28, 28, 28)))
            g.FillPath(bg, panelPath);
        using (var border = new Pen(Color.FromArgb(40, 255, 255, 255)))
            g.DrawPath(border, panelPath);

        g.SmoothingMode = SmoothingMode.Default;

        // Magnified pixels - clip to rounded rect inside panel
        int magX = px + Pad;
        int magY = py + Pad;
        var magRect = new Rectangle(magX, magY, MagSize, MagSize);

        // Save state, clip to rounded mag area
        var oldClip = g.Clip;
        using var magClip = new Region(RoundRect(magRect, 6));
        g.Clip = magClip;

        int half = GridSize / 2;
        for (int gy = 0; gy < GridSize; gy++)
        {
            for (int gx = 0; gx < GridSize; gx++)
            {
                int sx = cx - half + gx;
                int sy = cy - half + gy;
                Color pc = (sx >= 0 && sx < _screenshot.Width && sy >= 0 && sy < _screenshot.Height)
                    ? _screenshot.GetPixel(sx, sy) : Color.Black;
                using var brush = new SolidBrush(pc);
                g.FillRectangle(brush, magX + gx * CellSize, magY + gy * CellSize, CellSize, CellSize);
            }
        }

        // Grid lines
        using (var gp = new Pen(Color.FromArgb(25, 255, 255, 255)))
        {
            for (int i = 0; i <= GridSize; i++)
            {
                g.DrawLine(gp, magX + i * CellSize, magY, magX + i * CellSize, magY + MagSize);
                g.DrawLine(gp, magX, magY + i * CellSize, magX + MagSize, magY + i * CellSize);
            }
        }

        // Restore clip
        g.Clip = oldClip;

        // Center pixel highlight
        var centerR = new Rectangle(magX + half * CellSize - 1, magY + half * CellSize - 1,
            CellSize + 1, CellSize + 1);
        using (var hp = new Pen(Color.White, 1.5f))
            g.DrawRectangle(hp, centerR);

        // Info area
        int infoY = magY + MagSize + 6;
        string hex = $"#{_pickedColor.R:X2}{_pickedColor.G:X2}{_pickedColor.B:X2}";
        string rgb = $"{_pickedColor.R}, {_pickedColor.G}, {_pickedColor.B}";

        // Swatch
        using (var sb = new SolidBrush(_pickedColor))
            g.FillRectangle(sb, magX, infoY, 24, 24);

        using var fBold = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        using var fSmall = new Font("Segoe UI", 8f);
        using var white = new SolidBrush(Color.White);
        using var muted = new SolidBrush(Color.FromArgb(120, 255, 255, 255));

        g.DrawString(hex, fBold, white, magX + 30, infoY - 1);
        g.DrawString(rgb, fSmall, muted, magX + 30, infoY + 14);

        // Crosshair at cursor
        using (var cp = new Pen(Color.FromArgb(150, 255, 255, 255), 1f))
        {
            g.DrawLine(cp, _cursorPos.X - 8, _cursorPos.Y, _cursorPos.X - 3, _cursorPos.Y);
            g.DrawLine(cp, _cursorPos.X + 3, _cursorPos.Y, _cursorPos.X + 8, _cursorPos.Y);
            g.DrawLine(cp, _cursorPos.X, _cursorPos.Y - 8, _cursorPos.X, _cursorPos.Y - 3);
            g.DrawLine(cp, _cursorPos.X, _cursorPos.Y + 3, _cursorPos.X, _cursorPos.Y + 8);
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            string hex = $"#{_pickedColor.R:X2}{_pickedColor.G:X2}{_pickedColor.B:X2}";
            ColorPicked?.Invoke(hex);
        }
        else if (e.Button == MouseButtons.Right)
            Cancelled?.Invoke();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Cancelled?.Invoke();
    }

    private static GraphicsPath RoundRect(Rectangle r, int rad)
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
        if (disposing) _trackTimer.Dispose();
        base.Dispose(disposing);
    }

    protected override CreateParams CreateParams
    { get { var cp = base.CreateParams; cp.ExStyle |= 0x80; return cp; } }
}
