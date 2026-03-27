using System.Drawing;
using System.Windows.Forms;
using Yoink.Models;

namespace Yoink.Capture;

public sealed partial class RegionOverlayForm
{
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right) { Cancel(); return; }
        if (e.Button != MouseButtons.Left) return;

        int btn = GetToolbarButtonAt(e.Location);
        if (btn >= 0)
        {
            if (btn == BtnCount - 1) { Cancel(); return; }     // close
            if (btn == BtnCount - 2) { SettingsRequested?.Invoke(); Cancel(); return; } // gear
            if (btn == BtnCount - 3) { ToggleColorPicker(); return; } // color dot
            var modeMap = new[] {
                CaptureMode.Rectangle, CaptureMode.Freeform,
                CaptureMode.Ocr, CaptureMode.ColorPicker,
                CaptureMode.Draw, CaptureMode.Arrow, CaptureMode.CurvedArrow,
                CaptureMode.Text, CaptureMode.Blur, CaptureMode.Eraser };
            if (btn < modeMap.Length) SetMode(modeMap[btn]);
            return;
        }

        // Color picker popup: check if clicked a swatch
        if (_colorPickerOpen)
        {
            if (HandleColorPickerClick(e.Location))
                return;
            _colorPickerOpen = false;
            Invalidate();
        }

        // If typing text, commit current text on click elsewhere
        if (_isTyping)
        {
            CommitText();
            return;
        }

        if (_mode == CaptureMode.ColorPicker)
        {
            ColorPicked?.Invoke(_hexStr);
            return;
        }

        if (_mode == CaptureMode.Eraser)
        {
            // Smart eraser: sample color at click point, start dragging a rect
            int cx = Math.Clamp(e.Location.X, 0, _bmpW - 1);
            int cy = Math.Clamp(e.Location.Y, 0, _bmpH - 1);
            _eraserColor = Color.FromArgb(_pixelData[cy * _bmpW + cx]);
            _eraserStart = e.Location;
            _isEraserDragging = true;
            return;
        }

        _hasDragged = false;
        switch (_mode)
        {
            case CaptureMode.Rectangle:
            case CaptureMode.Ocr:
                _isSelecting = true;
                _selectionStart = _selectionEnd = e.Location;
                _hasSelection = false;
                break;
            case CaptureMode.Freeform:
                _isSelecting = true;
                _freeformPoints.Clear();
                _freeformPoints.Add(e.Location);
                break;
            case CaptureMode.Text:
                _isTyping = true;
                _textPos = e.Location;
                _textBuffer = "";
                Invalidate();
                break;
            case CaptureMode.Draw:
                _isSelecting = true;
                _currentStroke = new List<Point> { e.Location };
                _drawStrokes.Add(_currentStroke);
                break;
            case CaptureMode.Arrow:
                _isArrowDragging = true;
                _arrowStart = e.Location;
                break;
            case CaptureMode.CurvedArrow:
                _isCurvedArrowDragging = true;
                _currentCurvedArrow = new List<Point> { e.Location };
                _curvedArrows.Add(_currentCurvedArrow);
                break;
            case CaptureMode.Blur:
                _isBlurring = true;
                _blurStart = e.Location;
                break;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int btn = GetToolbarButtonAt(e.Location);
        if (btn != _hoveredButton) { _hoveredButton = btn; Invalidate(); }

        if (btn >= 0)
            { if (!Cursor.Equals(Cursors.Hand)) Cursor = Cursors.Hand; }
        else if (_mode == CaptureMode.ColorPicker)
            { if (Cursor != _blankCursor) Cursor = _blankCursor; }
        else
            { if (!Cursor.Equals(Cursors.Cross)) Cursor = Cursors.Cross; }

        switch (_mode)
        {
            case CaptureMode.Rectangle when _isSelecting:
            case CaptureMode.Ocr when _isSelecting:
                _selectionEnd = e.Location;
                _selectionRect = NormRect(_selectionStart, _selectionEnd);
                if (_selectionRect.Width > 3 || _selectionRect.Height > 3) _hasDragged = true;
                _hasSelection = _selectionRect.Width > 2 && _selectionRect.Height > 2;
                Invalidate();
                break;
            case CaptureMode.Freeform when _isSelecting:
                _freeformPoints.Add(e.Location);
                _hasDragged = true;
                Invalidate();
                break;
            case CaptureMode.Draw when _isSelecting:
                _currentStroke?.Add(e.Location);
                Invalidate();
                break;
            case CaptureMode.Arrow when _isArrowDragging:
                Invalidate();
                break;
            case CaptureMode.CurvedArrow when _isCurvedArrowDragging:
                _currentCurvedArrow?.Add(e.Location);
                Invalidate();
                break;
            case CaptureMode.Blur when _isBlurring:
                Invalidate();
                break;
            case CaptureMode.Eraser when _isEraserDragging:
                Invalidate();
                break;

        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        switch (_mode)
        {
            case CaptureMode.Draw when _isSelecting:
                _isSelecting = false;
                _currentStroke = null;
                _undoStack.Add("draw");
                break;
            case CaptureMode.Arrow when _isArrowDragging:
                _isArrowDragging = false;
                var end = e.Location;
                float dx = end.X - _arrowStart.X;
                float dy = end.Y - _arrowStart.Y;
                if (MathF.Sqrt(dx * dx + dy * dy) > 5)
                {
                    _arrows.Add((_arrowStart, end));
                    _undoStack.Add("arrow");
                }
                Invalidate();
                break;
            case CaptureMode.CurvedArrow when _isCurvedArrowDragging:
                _isCurvedArrowDragging = false;
                if (_currentCurvedArrow is { Count: >= 2 })
                    _undoStack.Add("curvedArrow");
                else if (_currentCurvedArrow != null)
                    _curvedArrows.Remove(_currentCurvedArrow);
                _currentCurvedArrow = null;
                Invalidate();
                break;
            case CaptureMode.Blur when _isBlurring:
                _isBlurring = false;
                var blurRect = NormRect(_blurStart, e.Location);
                if (blurRect.Width > 3 && blurRect.Height > 3)
                {
                    _blurRects.Add(blurRect);
                    _undoStack.Add("blur");
                }
                Invalidate();
                break;
            case CaptureMode.Eraser when _isEraserDragging:
                _isEraserDragging = false;
                var eraserRect = NormRect(_eraserStart, e.Location);
                if (eraserRect.Width > 1 && eraserRect.Height > 1)
                {
                    _eraserFills.Add((eraserRect, _eraserColor));
                    _undoStack.Add("eraser");
                }
                Invalidate();
                break;
            case CaptureMode.Rectangle when _isSelecting:
            case CaptureMode.Ocr when _isSelecting:
                _isSelecting = false;
                bool isOcr = _mode == CaptureMode.Ocr;
                if (!_hasDragged)
                {
                    var fullRect = new Rectangle(0, 0, _screenshot.Width, _screenshot.Height);
                    if (isOcr) OcrRegionSelected?.Invoke(fullRect);
                    else RegionSelected?.Invoke(fullRect);
                }
                else if (_selectionRect.Width > 2 && _selectionRect.Height > 2)
                {
                    if (isOcr) OcrRegionSelected?.Invoke(_selectionRect);
                    else RegionSelected?.Invoke(_selectionRect);
                }
                else { _hasSelection = false; Invalidate(); }
                break;
            case CaptureMode.Freeform when _isSelecting:
                _isSelecting = false;
                if (!_hasDragged)
                    RegionSelected?.Invoke(new Rectangle(0, 0, _screenshot.Width, _screenshot.Height));
                else if (_freeformPoints.Count > 2) CompleteFreeform();
                break;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Text input mode
        if (_isTyping)
        {
            if (e.KeyCode == Keys.Escape) { _isTyping = false; _textBuffer = ""; Invalidate(); return; }
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return) { CommitText(); return; }
            if (e.KeyCode == Keys.Back && _textBuffer.Length > 0)
            {
                _textBuffer = _textBuffer[..^1]; Invalidate(); return;
            }
            return; // Don't process other keys while typing
        }

        if (e.KeyCode == Keys.Escape) Cancel();
        if (e.KeyCode == Keys.D1) SetMode(CaptureMode.Rectangle);
        if (e.KeyCode == Keys.D2) SetMode(CaptureMode.Freeform);
        if (e.KeyCode == Keys.D3) SetMode(CaptureMode.Ocr);
        if (e.KeyCode == Keys.D4) SetMode(CaptureMode.ColorPicker);
        if (e.KeyCode == Keys.D5) SetMode(CaptureMode.Draw);
        if (e.KeyCode == Keys.D6) SetMode(CaptureMode.Arrow);
        if (e.KeyCode == Keys.D7) SetMode(CaptureMode.CurvedArrow);
        if (e.KeyCode == Keys.D8) SetMode(CaptureMode.Text);
        if (e.KeyCode == Keys.D9) SetMode(CaptureMode.Blur);
        if (e.KeyCode == Keys.D0) SetMode(CaptureMode.Eraser);

        if (e.KeyCode == Keys.Z && e.Control && _undoStack.Count > 0)
        {
            var last = _undoStack[^1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            if (last == "draw" && _drawStrokes.Count > 0)
                _drawStrokes.RemoveAt(_drawStrokes.Count - 1);
            else if (last == "blur" && _blurRects.Count > 0)
                _blurRects.RemoveAt(_blurRects.Count - 1);
            else if (last == "arrow" && _arrows.Count > 0)
                _arrows.RemoveAt(_arrows.Count - 1);
            else if (last == "curvedArrow" && _curvedArrows.Count > 0)
                _curvedArrows.RemoveAt(_curvedArrows.Count - 1);
            else if (last == "eraser" && _eraserFills.Count > 0)
                _eraserFills.RemoveAt(_eraserFills.Count - 1);
            else if (last == "text" && _textAnnotations.Count > 0)
                _textAnnotations.RemoveAt(_textAnnotations.Count - 1);
            Invalidate();
        }
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (_isTyping && !char.IsControl(e.KeyChar))
        {
            _textBuffer += e.KeyChar;
            e.Handled = true;
            Invalidate();
        }
        base.OnKeyPress(e);
    }

    private void CommitText()
    {
        if (_isTyping && _textBuffer.Length > 0)
        {
            _textAnnotations.Add((_textPos, _textBuffer, _textFontSize, _toolColor));
            _undoStack.Add("text");
        }
        _isTyping = false;
        _textBuffer = "";
        Invalidate();
    }

    private void ToggleColorPicker()
    {
        _colorPickerOpen = !_colorPickerOpen;
        Invalidate();
    }

    private bool HandleColorPickerClick(Point p)
    {
        if (!_colorPickerRect.Contains(p)) return false;

        int cols = 6, swatchSize = 28, pad = 4;
        int relX = p.X - _colorPickerRect.X - pad;
        int relY = p.Y - _colorPickerRect.Y - pad;
        int col = relX / (swatchSize + pad);
        if (col >= 0 && col < ToolColors.Length && relY >= 0 && relY < swatchSize + pad)
        {
            _toolColor = ToolColors[col];
            _toolColorIndex = col;
            _colorPickerOpen = false;
            Invalidate();
            return true;
        }
        return false;
    }

    private int GetToolbarButtonAt(Point p)
    {
        for (int i = 0; i < _toolbarButtons.Length; i++)
            if (_toolbarButtons[i].Contains(p)) return i;
        return -1;
    }

    private void SetMode(CaptureMode m)
    {
        if (_isTyping) CommitText();
        _colorPickerOpen = false;
        _mode = m;
        _hasSelection = false;
        _hasDragged = false;
        _freeformPoints.Clear();
        _isSelecting = false;
        _isBlurring = false;
        _isArrowDragging = false;
        _isCurvedArrowDragging = false;
        _isEraserDragging = false;

        if (m == CaptureMode.ColorPicker)
            _pickerTimer.Start();
        else
            _pickerTimer.Stop();

        Invalidate();
    }
}
