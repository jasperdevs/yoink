using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Yoink.Native;

namespace Yoink.Capture;

/// <summary>
/// Fullscreen transparent overlay that follows the cursor with a magnified pixel grid,
/// showing RGB/Hex values. Click to copy hex to clipboard. ESC to cancel.
/// </summary>
public sealed class ColorPickerForm : Form
{
    private readonly Bitmap _screenshot;
    private readonly Rectangle _virtualBounds;
    private readonly System.Windows.Forms.Timer _trackTimer;

    private Point _cursorPos;
    private Color _pickedColor = Color.Black;

    private const int GridSize = 11; // odd number so center pixel is clear
    private const int CellSize = 14;
    private const int MagSize = GridSize * CellSize; // 154px
    private const int InfoHeight = 52;
    private const int PanelWidth = MagSize + 2; // +2 for border
    private const int PanelHeight = MagSize + InfoHeight + 2;
    private const int PanelOffset = 24; // offset from cursor

    /// <summary>Fires with the hex color string when user clicks.</summary>
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

        _trackTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60fps
        _trackTimer.Tick += (_, _) =>
        {
            User32.GetCursorPos(out var pt);
            var newPos = new Point(pt.X - _virtualBounds.X, pt.Y - _virtualBounds.Y);
            if (newPos != _cursorPos)
            {
                _cursorPos = newPos;
                Invalidate();
            }
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
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.CompositingMode = CompositingMode.SourceCopy;

        // Draw the full screenshot as background (dimmed)
        g.DrawImage(_screenshot, 0, 0);
        g.CompositingMode = CompositingMode.SourceOver;
        using (var dim = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
            g.FillRectangle(dim, 0, 0, ClientSize.Width, ClientSize.Height);

        // Get the color under cursor
        int cx = Math.Clamp(_cursorPos.X, 0, _screenshot.Width - 1);
        int cy = Math.Clamp(_cursorPos.Y, 0, _screenshot.Height - 1);
        _pickedColor = _screenshot.GetPixel(cx, cy);

        // Calculate magnifier panel position (offset from cursor, stay on screen)
        int panelX = _cursorPos.X + PanelOffset;
        int panelY = _cursorPos.Y + PanelOffset;
        if (panelX + PanelWidth > ClientSize.Width) panelX = _cursorPos.X - PanelOffset - PanelWidth;
        if (panelY + PanelHeight > ClientSize.Height) panelY = _cursorPos.Y - PanelOffset - PanelHeight;
        panelX = Math.Max(4, panelX);
        panelY = Math.Max(4, panelY);

        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Panel background
        var panelRect = new Rectangle(panelX, panelY, PanelWidth, PanelHeight);
        using (var path = RoundRect(panelRect, 10))
        {
            using var bg = new SolidBrush(Color.FromArgb(230, 24, 24, 24));
            g.FillPath(bg, path);
            using var border = new Pen(Color.FromArgb(50, 255, 255, 255), 1f);
            g.DrawPath(border, path);
        }

        g.SmoothingMode = SmoothingMode.Default;

        // Draw magnified pixel grid
        int magX = panelX + 1;
        int magY = panelY + 1;
        int half = GridSize / 2;

        for (int gy = 0; gy < GridSize; gy++)
        {
            for (int gx = 0; gx < GridSize; gx++)
            {
                int sx = cx - half + gx;
                int sy = cy - half + gy;

                Color pixelColor;
                if (sx >= 0 && sx < _screenshot.Width && sy >= 0 && sy < _screenshot.Height)
                    pixelColor = _screenshot.GetPixel(sx, sy);
                else
                    pixelColor = Color.Black;

                using var brush = new SolidBrush(pixelColor);
                g.FillRectangle(brush, magX + gx * CellSize, magY + gy * CellSize, CellSize, CellSize);
            }
        }

        // Grid lines (very subtle)
        using (var gridPen = new Pen(Color.FromArgb(30, 255, 255, 255), 1f))
        {
            for (int i = 0; i <= GridSize; i++)
            {
                g.DrawLine(gridPen, magX + i * CellSize, magY, magX + i * CellSize, magY + MagSize);
                g.DrawLine(gridPen, magX, magY + i * CellSize, magX + MagSize, magY + i * CellSize);
            }
        }

        // Center pixel highlight (white border)
        var centerRect = new Rectangle(magX + half * CellSize - 1, magY + half * CellSize - 1,
            CellSize + 2, CellSize + 2);
        using (var highlightPen = new Pen(Color.White, 2f))
            g.DrawRectangle(highlightPen, centerRect);

        // Info panel below magnifier
        int infoX = panelX + 10;
        int infoY = panelY + MagSize + 6;

        // Color swatch
        var swatchRect = new Rectangle(infoX, infoY + 2, 32, 32);
        using (var swatchBrush = new SolidBrush(_pickedColor))
            g.FillRectangle(swatchBrush, swatchRect);
        using (var swatchBorder = new Pen(Color.FromArgb(60, 255, 255, 255), 1f))
            g.DrawRectangle(swatchBorder, swatchRect);

        // Text info
        string hex = $"#{_pickedColor.R:X2}{_pickedColor.G:X2}{_pickedColor.B:X2}";
        string rgb = $"RGB: {_pickedColor.R}, {_pickedColor.G}, {_pickedColor.B}";
        string pos = $"X: {cx}  Y: {cy}";

        using var fontBold = new Font("Segoe UI", 10f, FontStyle.Bold);
        using var fontNormal = new Font("Segoe UI", 9f);
        using var white = new SolidBrush(Color.White);
        using var muted = new SolidBrush(Color.FromArgb(140, 255, 255, 255));

        g.DrawString(hex, fontBold, white, infoX + 40, infoY);
        g.DrawString(rgb, fontNormal, muted, infoX + 40, infoY + 16);
        g.DrawString(pos, fontNormal, muted, infoX + 40, infoY + 30);

        // Crosshair on the actual cursor position (subtle)
        using (var crossPen = new Pen(Color.FromArgb(120, 255, 255, 255), 1f))
        {
            g.DrawLine(crossPen, _cursorPos.X - 12, _cursorPos.Y, _cursorPos.X - 4, _cursorPos.Y);
            g.DrawLine(crossPen, _cursorPos.X + 4, _cursorPos.Y, _cursorPos.X + 12, _cursorPos.Y);
            g.DrawLine(crossPen, _cursorPos.X, _cursorPos.Y - 12, _cursorPos.X, _cursorPos.Y - 4);
            g.DrawLine(crossPen, _cursorPos.X, _cursorPos.Y + 4, _cursorPos.X, _cursorPos.Y + 12);
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
        {
            Cancelled?.Invoke();
        }
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
