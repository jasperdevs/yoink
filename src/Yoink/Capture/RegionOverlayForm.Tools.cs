using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Yoink.Models;

namespace Yoink.Capture;

public sealed partial class RegionOverlayForm
{
    private void CompleteFreeform()
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var p in _freeformPoints)
        { minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y); maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); }
        var bb = new Rectangle(minX, minY, maxX - minX, maxY - minY);
        if (bb.Width < 3 || bb.Height < 3) return;

        // Render annotated screenshot, then crop freeform
        var annotated = RenderAnnotatedBitmap();
        var r = new Bitmap(bb.Width, bb.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(r))
        {
            var pts = _freeformPoints.Select(p => new Point(p.X - minX, p.Y - minY)).ToArray();
            using var cp = new GraphicsPath(); cp.AddPolygon(pts); g.SetClip(cp);
            g.DrawImage(annotated, new Rectangle(0, 0, bb.Width, bb.Height), bb, GraphicsUnit.Pixel);
        }
        annotated.Dispose();
        FreeformSelected?.Invoke(r);
    }

    /// <summary>
    /// Renders the screenshot with all committed annotations in creation order.
    /// </summary>
    public Bitmap RenderAnnotatedBitmap()
    {
        var result = new Bitmap(_screenshot);
        using var g = Graphics.FromImage(result);

        int iDraw = 0, iBlur = 0, iArrow = 0, iCurved = 0;
        int iEraser = 0, iText = 0, iStep = 0, iHighlight = 0;

        foreach (var entry in _undoStack)
        {
            switch (entry)
            {
                case "eraser" when iEraser < _eraserFills.Count:
                    var (er, ec) = _eraserFills[iEraser++];
                    using (var brush = new SolidBrush(ec))
                        g.FillRectangle(brush, er);
                    break;

                case "blur" when iBlur < _blurRects.Count:
                    PaintBlurRect(g, _blurRects[iBlur++]);
                    break;

                case "draw" when iDraw < _drawStrokes.Count:
                    var stroke = _drawStrokes[iDraw++];
                    if (stroke.Count >= 2)
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        using var dp = new Pen(_toolColor, 3f) { LineJoin = LineJoin.Round };
                        g.DrawLines(dp, stroke.ToArray());
                        g.SmoothingMode = SmoothingMode.Default;
                    }
                    break;

                case "highlight" when iHighlight < _highlightRects.Count:
                    var (hr, hc) = _highlightRects[iHighlight++];
                    using (var hBrush = new SolidBrush(Color.FromArgb(90, hc.R, hc.G, hc.B)))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        using var hp = RRect(hr, 3);
                        g.FillPath(hBrush, hp);
                        g.SmoothingMode = SmoothingMode.Default;
                    }
                    break;

                case "arrow" when iArrow < _arrows.Count:
                    PaintArrow(g, _arrows[iArrow++].from, _arrows[iArrow - 1].to);
                    break;

                case "curvedArrow" when iCurved < _curvedArrows.Count:
                    PaintCurvedArrow(g, _curvedArrows[iCurved++]);
                    break;

                case "step" when iStep < _stepNumbers.Count:
                    var (sp, sn, sc) = _stepNumbers[iStep++];
                    PaintStepNumber(g, sp, sn, sc);
                    break;

                case "text" when iText < _textAnnotations.Count:
                    var (tp, tt, tf, tc) = _textAnnotations[iText++];
                    using (var font = new Font("Segoe UI", tf, FontStyle.Bold))
                    {
                        using var shadow = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
                        g.DrawString(tt, font, shadow, tp.X + 1, tp.Y + 1);
                        using var brush = new SolidBrush(tc);
                        g.DrawString(tt, font, brush, tp.X, tp.Y);
                    }
                    break;
                // Magnifiers are not rendered into the final image (they're a viewing aid)
            }
        }

        return result;
    }
}
