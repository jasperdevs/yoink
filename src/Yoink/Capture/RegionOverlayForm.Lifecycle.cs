using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Yoink.Helpers;
using Yoink.Models;

namespace Yoink.Capture;

public sealed partial class RegionOverlayForm
{
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _toolbarAnim = 1f;
        WindowDetector.RegisterIgnoredWindow(Handle);
        Native.User32.SetWindowPos(Handle, Native.User32.HWND_TOPMOST,
            0, 0, 0, 0,
            Native.User32.SWP_NOMOVE | Native.User32.SWP_NOSIZE | Native.User32.SWP_SHOWWINDOW);
        Native.User32.SetForegroundWindow(Handle);

        _toolbarForm = new ToolbarForm(this);
        PositionToolbarForm();
        var _ = _toolbarForm.Handle;
        WindowDetector.RegisterIgnoredWindow(_toolbarForm.Handle);
        _toolbarForm.UpdateSurface();
        _toolbarForm.Show(this);
        Focus();
        Invalidate(new Rectangle(_toolbarRect.X - 12, _toolbarRect.Y - 48,
            _toolbarRect.Width + 24, _toolbarRect.Height + 96));

        // Pre-snapshot all visible window rects for instant detection during mouse moves.
        WindowDetector.SnapshotWindows(_virtualBounds);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);

        if (_allowDeactivation || IsDisposed || Disposing || !Visible)
            return;

        BeginInvoke(new Action(() =>
        {
            if (_allowDeactivation || IsDisposed || Disposing || !Visible)
                return;

            Activate();
            Focus();
            Native.User32.SetForegroundWindow(Handle);
        }));
    }

    internal void PositionToolbarForm()
    {
        if (_toolbarForm is null) return;
        // Keep enough horizontal room for tooltips, but avoid a giant full-screen layered surface.
        int marginX = 220;
        int marginY = 24;
        int popupH = 320;
        var bounds = new Rectangle(
            _toolbarRect.X - marginX + _virtualBounds.X,
            _toolbarRect.Y - marginY + _virtualBounds.Y,
            _toolbarRect.Width + marginX * 2,
            _toolbarRect.Height + popupH + marginY * 2);
        _toolbarForm.Bounds = bounds;
    }

    private void ShowTextBox()
    {
        if (_textBox == null)
        {
            _textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Multiline = false,
                ScrollBars = ScrollBars.None,
                // Hidden off-screen - we only use it for input handling, not display
                Size = new Size(1, 1),
            };
            _textBox.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CommitText(); }
                if (e.KeyCode == Keys.Escape)
                {
                    e.SuppressKeyPress = true;
                    _textBuffer = "";
                    _isTyping = false;
                    HideTextBox();
                    InvalidateActiveTextLayout();
                    Invalidate();
                }
            };
            _textBox.TextChanged += (_, _) =>
            {
                if (_textBox == null) return;
                _textBuffer = _textBox.Text;
                InvalidateActiveTextLayout();
                Invalidate();
            };
            Controls.Add(_textBox);
        }
        _textBox.Text = _textBuffer;
        // Place off-screen so it's invisible but still receives keyboard input
        _textBox.Location = new Point(-100, -100);
        _textBox.Visible = true;
        _textBox.Focus();
        if (_textBuffer.Length > 0) _textBox.SelectAll();
    }

    private void HideTextBox()
    {
        if (_textBox != null)
        {
            _textBox.Visible = false;
            Focus();
        }
    }

    private void UpdateTextBoxStyle() { }
    private void SyncTextBoxSize() { }

    private void InvalidateActiveTextLayout()
    {
        _activeTextLayoutDirty = true;
    }

    private void ShowEmojiSearchBox()
    {
        if (_emojiSearchBox == null)
        {
            _emojiSearchBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Size = new Size(1, 1),
            };
            _emojiSearchBox.TextChanged += (_, _) =>
            {
                if (_emojiSearchBox == null) return;
                _emojiSearch = _emojiSearchBox.Text;
                _emojiScrollOffset = 0;
                RefreshToolbar();
            };
            _emojiSearchBox.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    e.SuppressKeyPress = true;
                    _emojiPickerOpen = false;
                    HideEmojiSearchBox();
                    Invalidate(InflateForRepaint(GetEmojiPickerBounds(), 12));
                    RefreshToolbar();
                }
            };
            Controls.Add(_emojiSearchBox);
        }
        _emojiSearchBox.Text = _emojiSearch;
        _emojiSearchBox.Location = new Point(-100, -100);
        _emojiSearchBox.Visible = true;
        _emojiSearchBox.Focus();
    }

    private void HideEmojiSearchBox()
    {
        if (_emojiSearchBox != null) { _emojiSearchBox.Visible = false; Focus(); }
    }

    private void WarmEmojiPickerCacheBatch()
    {
        if (!_emojiWarmupPending || !_emojiPickerOpen)
            return;

        var filtered = GetFilteredEmojiPalette();
        if (_emojiWarmupIndex >= filtered.Length)
        {
            _emojiWarmupPending = false;
            return;
        }

        int batchSize = 4;
        int end = Math.Min(filtered.Length, _emojiWarmupIndex + batchSize);
        for (int i = _emojiWarmupIndex; i < end; i++)
            _emojiRenderer.GetEmoji(filtered[i].emoji, 22f);

        _emojiWarmupIndex = end;
        if (_emojiWarmupIndex >= filtered.Length)
            _emojiWarmupPending = false;

        Invalidate(InflateForRepaint(GetEmojiPickerBounds(), 12));
    }

    private void ShowFontSearchBox()
    {
        if (_fontSearchBox == null)
        {
            _fontSearchBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Size = new Size(1, 1),
            };
            _fontSearchBox.TextChanged += (_, _) =>
            {
                if (_fontSearchBox == null) return;
                _fontSearch = _fontSearchBox.Text;
                _filteredFonts = null; _fontPickerScroll = 0;
                RefreshToolbar();
            };
            _fontSearchBox.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    e.SuppressKeyPress = true;
                    _fontPickerOpen = false;
                    _fontSearch = ""; _filteredFonts = null;
                    HideFontSearchBox();
                    Invalidate(InflateForRepaint(GetFontPickerBounds(), 12));
                    RefreshToolbar();
                }
            };
            Controls.Add(_fontSearchBox);
        }
        _fontSearchBox.Text = _fontSearch;
        _fontSearchBox.Location = new Point(-100, -100);
        _fontSearchBox.Visible = true;
        _fontSearchBox.Focus();
    }

    private void HideFontSearchBox()
    {
        if (_fontSearchBox != null) { _fontSearchBox.Visible = false; Focus(); }
    }

    internal void RefreshToolbar()
    {
        CalcToolbar();
        PositionToolbarForm();
        _toolbarForm?.UpdateSurface();
    }

    private void HideToolbarImmediately()
    {
        if (_toolbarForm is null || _toolbarForm.IsDisposed)
            return;

        _animTimer.Stop();
        _toolbarAnim = 1f;
        _toolbarForm.Hide();
    }

    private void HideToolbarForCaptureTool()
    {
        if (ToolDef.IsCaptureTool(_mode))
            HideToolbarImmediately();
    }

    private void EnsureCrosshairForms()
    {
        if (_verticalCrosshairForm != null && _horizontalCrosshairForm != null)
            return;

        var color = Color.FromArgb(72, UiChrome.SurfaceTextPrimary.R, UiChrome.SurfaceTextPrimary.G, UiChrome.SurfaceTextPrimary.B);
        _verticalCrosshairForm ??= new CrosshairGuideForm(color);
        _horizontalCrosshairForm ??= new CrosshairGuideForm(color);
    }

    private void UpdateCrosshairGuides(Point point)
    {
        bool shouldShow = ShowCrosshairGuides
            && _mode != CaptureMode.ColorPicker
            && point != Point.Empty
            && !IsPointInOverlayUi(point);
        if (!shouldShow)
        {
            ClearCrosshairGuides();
            return;
        }

        EnsureCrosshairForms();
        if (_verticalCrosshairForm is null || _horizontalCrosshairForm is null)
            return;

        int screenX = _virtualBounds.X + point.X;
        int screenY = _virtualBounds.Y + point.Y;
        _verticalCrosshairForm.UpdateLine(new Rectangle(screenX - 1, _virtualBounds.Top, 3, _virtualBounds.Height));
        _horizontalCrosshairForm.UpdateLine(new Rectangle(_virtualBounds.Left, screenY - 1, _virtualBounds.Width, 3));
    }

    private void ClearCrosshairGuides()
    {
        _verticalCrosshairForm?.Hide();
        _horizontalCrosshairForm?.Hide();
    }

    private void Cancel()
    {
        _allowDeactivation = true;
        SelectionCancelled?.Invoke();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClearCrosshairGuides();
            _verticalCrosshairForm?.Close();
            _verticalCrosshairForm?.Dispose();
            _horizontalCrosshairForm?.Close();
            _horizontalCrosshairForm?.Dispose();
            WindowDetector.UnregisterIgnoredWindow(Handle);
            WindowDetector.ClearSnapshot();
            if (_toolbarForm != null)
                WindowDetector.UnregisterIgnoredWindow(_toolbarForm.Handle);
            CloseMagWindow();
            CloseCaptureMagnifier();
            _toolbarForm?.Close();
            _toolbarForm?.Dispose();
            _animTimer.Dispose();
            _pickerTimer.Dispose();
            _autoDetectTimer.Dispose();
            _magGfx.Dispose();
            _magBitmap.Dispose();
            _committedAnnotationsBitmap?.Dispose();
            _hexFont.Dispose();
            _rgbFont.Dispose();
            _mutedBrush.Dispose();
            _crossPen.Dispose();
            foreach (var f in _fontCache.Values) f?.Dispose();
            _fontCache.Clear();
            foreach (var f in _annotationFontCache.Values) f?.Dispose();
            _annotationFontCache.Clear();
        }
        base.Dispose(disposing);
    }

    protected override CreateParams CreateParams
    { get { var cp = base.CreateParams; cp.ExStyle |= 0x80; return cp; } }
}
