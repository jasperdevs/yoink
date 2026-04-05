using System.Drawing;
using Yoink.Models;
using Yoink.Native;

namespace Yoink.Capture;

/// <summary>
/// Provides point lookup for window-only snapping.
/// Pre-snapshots all visible window rects at overlay startup for instant detection.
/// </summary>
public static class WindowDetector
{
    private static readonly HashSet<IntPtr> IgnoredHandles = new();
    private static readonly object IgnoredHandleLock = new();

    // Pre-computed snapshot of all visible window rects, sorted front-to-back (Z-order).
    private static Rectangle[]? _snapshotRects;
    private static Rectangle _snapshotVirtualBounds;

    public static Rectangle GetDetectionRectAtPoint(
        Point screenPoint,
        Rectangle virtualBounds,
        WindowDetectionMode mode)
    {
        if (mode == WindowDetectionMode.Off)
            return Rectangle.Empty;

        // Use pre-computed snapshot for instant lookup.
        var rects = _snapshotRects;
        if (rects != null && virtualBounds == _snapshotVirtualBounds)
        {
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].Contains(screenPoint))
                    return rects[i];
            }
            return Rectangle.Empty;
        }

        return GetTopLevelWindowRectFallback(screenPoint, virtualBounds);
    }

    public static Rectangle GetWindowRectAtPoint(Point screenPoint, Rectangle virtualBounds)
        => GetDetectionRectAtPoint(screenPoint, virtualBounds, WindowDetectionMode.WindowOnly);

    /// <summary>
    /// Enumerate all visible top-level windows and cache their rects in Z-order.
    /// Call this once when the overlay opens for instant detection during mouse moves.
    /// </summary>
    public static void SnapshotWindows(Rectangle virtualBounds)
    {
        var rects = new List<Rectangle>();
        User32.EnumWindows((hwnd, _) =>
        {
            if (IsIgnoredWindowHandle(hwnd) || !User32.IsWindowVisible(hwnd) || Dwm.IsWindowCloaked(hwnd))
                return true;

            var screenRect = Dwm.GetExtendedFrameBounds(hwnd);
            if (screenRect.Width <= 2 || screenRect.Height <= 2)
                return true;

            var rect = new Rectangle(
                screenRect.Left - virtualBounds.X,
                screenRect.Top - virtualBounds.Y,
                screenRect.Width,
                screenRect.Height);

            if (rect.Width > 2 && rect.Height > 2)
                rects.Add(rect);

            return true;
        }, IntPtr.Zero);

        _snapshotRects = rects.ToArray();
        _snapshotVirtualBounds = virtualBounds;
    }

    /// <summary>Clear the pre-computed snapshot (call on overlay close).</summary>
    public static void ClearSnapshot()
    {
        _snapshotRects = null;
    }

    public static void RegisterIgnoredWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        lock (IgnoredHandleLock)
            IgnoredHandles.Add(hwnd);
    }

    public static void UnregisterIgnoredWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        lock (IgnoredHandleLock)
            IgnoredHandles.Remove(hwnd);
    }

    /// <summary>Fallback for when no snapshot is available.</summary>
    private static Rectangle GetTopLevelWindowRectFallback(Point screenPoint, Rectangle virtualBounds)
    {
        var pt = new User32.POINT(screenPoint.X + virtualBounds.X, screenPoint.Y + virtualBounds.Y);
        IntPtr rawHwnd = User32.WindowFromPoint(pt);

        Span<IntPtr> visited = stackalloc IntPtr[32];
        int visitedCount = 0;
        IntPtr hwnd = rawHwnd;

        for (int depth = 0; depth < 32 && hwnd != IntPtr.Zero; depth++)
        {
            IntPtr candidate = NormalizeTopLevelWindow(hwnd);
            if (candidate == IntPtr.Zero || visited[..visitedCount].Contains(candidate))
                break;
            visited[visitedCount++] = candidate;

            if (TryGetWindowRect(candidate, pt, virtualBounds, out var rect))
                return rect;

            hwnd = User32.GetWindow(candidate, User32.GW_HWNDNEXT);
        }

        return Rectangle.Empty;
    }

    private static bool TryGetWindowRect(IntPtr hwnd, User32.POINT point, Rectangle virtualBounds, out Rectangle rect)
    {
        rect = Rectangle.Empty;

        if (hwnd == IntPtr.Zero || IsIgnoredWindowHandle(hwnd) || !User32.IsWindowVisible(hwnd) || Dwm.IsWindowCloaked(hwnd))
            return false;

        var screenRect = Dwm.GetExtendedFrameBounds(hwnd);
        if (screenRect.Width <= 2 || screenRect.Height <= 2)
            return false;
        if (!screenRect.Contains(point.X, point.Y))
            return false;

        rect = new Rectangle(
            screenRect.Left - virtualBounds.X,
            screenRect.Top - virtualBounds.Y,
            screenRect.Width,
            screenRect.Height);
        return rect.Width > 2 && rect.Height > 2;
    }

    private static IntPtr NormalizeTopLevelWindow(IntPtr hwnd)
    {
        IntPtr root = User32.GetAncestor(hwnd, User32.GA_ROOT);
        return root != IntPtr.Zero ? root : hwnd;
    }

    private static bool IsIgnoredWindowHandle(nint hwnd)
    {
        if (hwnd == 0) return false;
        lock (IgnoredHandleLock)
            return IgnoredHandles.Contains(hwnd);
    }
}
