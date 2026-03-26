using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Yoink.Capture;

public sealed partial class RegionOverlayForm
{
    private void CompleteFreeform()
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var p in _freeformPoints)
        {
            minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y);
        }
        var bb = new Rectangle(minX, minY, maxX - minX, maxY - minY);
        if (bb.Width < 3 || bb.Height < 3) return;

        var annotated = RenderAnnotatedBitmap();
        var r = new Bitmap(bb.Width, bb.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(r))
        {
            var pts = _freeformPoints.Select(p => new Point(p.X - minX, p.Y - minY)).ToArray();
            using var cp = new GraphicsPath();
            cp.AddPolygon(pts);
            g.SetClip(cp);
            g.DrawImage(annotated, new Rectangle(0, 0, bb.Width, bb.Height), bb, GraphicsUnit.Pixel);
        }
        annotated.Dispose();

        if (_mode == Models.CaptureMode.Ocr)
            OcrFreeformSelected?.Invoke(r);
        else
            FreeformSelected?.Invoke(r);
    }

    public Bitmap RenderAnnotatedBitmap()
    {
        var result = new Bitmap(_screenshot);
        using var g = Graphics.FromImage(result);

        foreach (var br in _blurRects)
            PaintBlurRect(g, br);

        if (_drawStrokes.Count > 0)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.Red, 3f) { LineJoin = LineJoin.Round };
            foreach (var stroke in _drawStrokes)
                if (stroke.Count >= 2)
                    g.DrawLines(pen, stroke.ToArray());
            g.SmoothingMode = SmoothingMode.Default;
        }

        foreach (var arrow in _arrows)
            PaintArrow(g, arrow.from, arrow.to);

        return result;
    }

    private void EraseAtPoint(Point click)
    {
        const float threshold = 15f;
        float bestDist = threshold;
        int bestIndex = -1;
        string bestType = "";

        // Check draw strokes
        for (int i = 0; i < _drawStrokes.Count; i++)
        {
            var stroke = _drawStrokes[i];
            for (int j = 0; j < stroke.Count - 1; j++)
            {
                float d = DistPointToSegment(click, stroke[j], stroke[j + 1]);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestIndex = i;
                    bestType = "draw";
                }
            }
        }

        // Check arrows
        for (int i = 0; i < _arrows.Count; i++)
        {
            float d = DistPointToSegment(click, _arrows[i].from, _arrows[i].to);
            if (d < bestDist)
            {
                bestDist = d;
                bestIndex = i;
                bestType = "arrow";
            }
        }

        if (bestIndex < 0) return;

        if (bestType == "draw")
        {
            _drawStrokes.RemoveAt(bestIndex);
            // Remove matching undo entries
            RemoveNthUndoEntry("draw", bestIndex);
        }
        else if (bestType == "arrow")
        {
            _arrows.RemoveAt(bestIndex);
            RemoveNthUndoEntry("arrow", bestIndex);
        }

        Invalidate();
    }

    private void RemoveNthUndoEntry(string type, int n)
    {
        int count = 0;
        for (int i = 0; i < _undoStack.Count; i++)
        {
            if (_undoStack[i] == type)
            {
                if (count == n) { _undoStack.RemoveAt(i); return; }
                count++;
            }
        }
    }

    private static float DistPointToSegment(Point p, Point a, Point b)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y;
        float lenSq = dx * dx + dy * dy;
        if (lenSq < 0.001f)
            return MathF.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));

        float t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0f, 1f);
        float projX = a.X + t * dx, projY = a.Y + t * dy;
        float ex = p.X - projX, ey = p.Y - projY;
        return MathF.Sqrt(ex * ex + ey * ey);
    }
}
