using System.Drawing;
using System.Windows.Forms;
using Yoink.Models;

namespace Yoink.Capture;

public sealed partial class RegionOverlayForm
{
    // All text input is handled by off-screen TextBox controls

    private void CommitText()
    {
        // Sync from TextBox before committing
        if (_textBox != null && _textBox.Visible)
            _textBuffer = _textBox.Text;
        if (_isTyping && _textBuffer.Length > 0)
            AddAnnotation(new TextAnnotation(_textPos, _textBuffer, _textFontSize, _toolColor, _textBold, _textItalic, _textStroke, _textShadow, _textFontFamily));
        _isTyping = false;
        _textBuffer = "";
        InvalidateActiveTextLayout();
        _fontPickerOpen = false;
        HideTextBox();
        Invalidate();
    }

    private RectangleF MeasureTextRect(Point pos, string text, float fontSize, string fontFamily, bool bold, bool italic)
    {
        var style = FontStyle.Regular;
        if (bold) style |= FontStyle.Bold;
        if (italic) style |= FontStyle.Italic;
        var font = GetAnnotationFont(fontFamily, fontSize, style);
        string display = text.Length > 0 ? text : "Type here...";
        var size = TextRenderer.MeasureText(display, font, Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        return new RectangleF(pos.X - 6, pos.Y - 4, Math.Max(size.Width + 12, 100), size.Height + 8);
    }

    private RectangleF GetActiveTextRect()
    {
        if (!_isTyping) return RectangleF.Empty;
        if (_activeTextLayoutDirty)
        {
            var style = FontStyle.Regular;
            if (_textBold) style |= FontStyle.Bold;
            if (_textItalic) style |= FontStyle.Italic;
            var font = GetAnnotationFont(_textFontFamily, _textFontSize, style);
            string display = _textBuffer.Length > 0 ? _textBuffer : "Type here...";
            var measured = TextRenderer.MeasureText(display, font, Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            _activeTextRectCache = MeasureTextRect(_textPos, _textBuffer, _textFontSize, _textFontFamily, _textBold, _textItalic);
            _activeTextMeasureWidth = measured.Width;
            const int hs = 10;
            _activeTextHandleCache[0] = new RectangleF(_activeTextRectCache.X - hs / 2f, _activeTextRectCache.Y - hs / 2f, hs, hs);
            _activeTextHandleCache[1] = new RectangleF(_activeTextRectCache.Right - hs / 2f, _activeTextRectCache.Y - hs / 2f, hs, hs);
            _activeTextHandleCache[2] = new RectangleF(_activeTextRectCache.X - hs / 2f, _activeTextRectCache.Bottom - hs / 2f, hs, hs);
            _activeTextHandleCache[3] = new RectangleF(_activeTextRectCache.Right - hs / 2f, _activeTextRectCache.Bottom - hs / 2f, hs, hs);
            _activeTextLayoutDirty = false;
        }
        return _activeTextRectCache;
    }

    private int GetTextHandle(Point p)
    {
        if (!_isTyping) return -1;
        _ = GetActiveTextRect();
        for (int i = 0; i < _activeTextHandleCache.Length; i++)
            if (_activeTextHandleCache[i].Contains(p)) return i;
        return -1;
    }

    private List<TextAnnotation> GetTextAnnotations() =>
        _undoStack.OfType<TextAnnotation>().ToList();

    private int HitTestText(Point p)
    {
        var texts = GetTextAnnotations();
        for (int i = texts.Count - 1; i >= 0; i--)
        {
            var ta = texts[i];
            var rect = MeasureTextRect(ta.Pos, ta.Text, ta.FontSize, ta.FontFamily, ta.Bold, ta.Italic);
            if (rect.Contains(p)) return i;
        }
        return -1;
    }

    private void ToggleColorPicker()
    {
        _emojiPickerOpen = false;
        _fontPickerOpen = false;
        HideEmojiSearchBox();
        HideFontSearchBox();
        _isPlacingEmoji = false;
        _colorPickerOpen = !_colorPickerOpen;
        Invalidate(InflateForRepaint(GetColorPickerBounds(), 12));
        RefreshToolbar();
    }

    private bool HandleColorPickerClick(Point p)
    {
        if (!_colorPickerRect.Contains(p)) return false;

        int swatchSize = 28, pad = 4;
        int relX = p.X - _colorPickerRect.X - pad;
        int relY = p.Y - _colorPickerRect.Y - pad;
        int col = relX / (swatchSize + pad);
        if (col >= 0 && col < ToolColors.Length && relY >= 0 && relY < swatchSize + pad)
        {
            _toolColor = ToolColors[col];
            _toolColorIndex = col;
            _colorPickerOpen = false;
            Invalidate(InflateForRepaint(GetColorPickerBounds(), 12));
            RefreshToolbar();
            return true;
        }
        return false;
    }

    private bool HandleFontPickerClick(Point p)
    {
        if (!_fontPickerRect.Contains(p)) return false;

        int itemH = 28, pad = 6, searchBarH = 28;
        int listY = _fontPickerRect.Y + pad + searchBarH + pad;
        int relY = p.Y - listY;
        int idx = _fontPickerScroll + relY / itemH;
        var fonts = GetFilteredFonts();

        if (relY >= 0 && idx >= 0 && idx < fonts.Length)
        {
            _textFontFamily = fonts[idx];
            _fontPickerOpen = false;
            _fontSearch = ""; _filteredFonts = null;
            InvalidateActiveTextLayout();
            UpdateTextBoxStyle(); SyncTextBoxSize();
            Invalidate(InflateForRepaint(GetFontPickerBounds(), 12));
            RefreshToolbar();
            return true;
        }
        return true; // absorb click inside picker
    }

    private bool HandleEmojiPickerClick(Point p)
    {
        if (!_emojiPickerRect.Contains(p)) return false;

        var filtered = GetFilteredEmojiPalette();

        int cols = 8, emojiSize = 32, pad = 6;
        int searchBarH = 28;
        int gridY = _emojiPickerRect.Y + pad + searchBarH + pad;

        // Check if clicking in search bar area (just keep focus, absorb click)
        if (p.Y < gridY) return true;

        int relX = p.X - _emojiPickerRect.X - pad;
        int relY = p.Y - gridY;
        int col = relX / (emojiSize + pad);
        int row = relY / (emojiSize + pad);
        int idx = (_emojiScrollOffset + row) * cols + col;

        if (col >= 0 && col < cols && row >= 0 && idx < filtered.Length)
        {
            _selectedEmoji = filtered[idx].emoji;
            _isPlacingEmoji = true;
            _emojiPickerOpen = false;
            _fontPickerOpen = false;
            HideEmojiSearchBox();
            Invalidate(InflateForRepaint(GetEmojiPickerBounds(), 12));
            RefreshToolbar();
            return true;
        }
        return true; // absorb click inside picker
    }
}
