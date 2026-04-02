using System.Drawing;
using Yoink.Models;
using Yoink.Native;

namespace Yoink.Capture;

/// <summary>
/// Provides point lookup for window-only snapping.
/// </summary>
public static class WindowDetector
{
    private static readonly HashSet<IntPtr> IgnoredHandles = new();
    private static readonly object IgnoredHandleLock = new();

    public static Rectangle GetDetectionRectAtPoint(
        Point screenPoint,
        Rectangle virtualBounds,
        WindowDetectionMode mode)
    {
        if (mode == WindowDetectionMode.Off)
            return Rectangle.Empty;

        return GetTopLevelWindowRectAtPoint(screenPoint, virtualBounds);
    }

    public static Rectangle GetWindowRectAtPoint(Point screenPoint, Rectangle virtualBounds)
        => GetTopLevelWindowRectAtPoint(screenPoint, virtualBounds);

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

    public static Rectangle GetTopLevelWindowRectAtPoint(Point screenPoint, Rectangle virtualBounds)
    {
        var pt = new User32.POINT(screenPoint.X + virtualBounds.X, screenPoint.Y + virtualBounds.Y);

        foreach (var hwnd in EnumerateTopLevelWindows())
        {
            if (IsIgnoredWindowHandle(hwnd))
                continue;

            if (!User32.GetWindowRect(hwnd, out var rect))
                continue;

            if (pt.X < rect.Left || pt.X >= rect.Right || pt.Y < rect.Top || pt.Y >= rect.Bottom)
                continue;

            var hwndRect = GetWindowRectFromHandle(hwnd, virtualBounds);
            if (IsUsableRect(hwndRect))
                return hwndRect;
        }

        return Rectangle.Empty;
    }

    private static Rectangle GetWindowRectFromHandle(IntPtr hwnd, Rectangle virtualBounds)
    {
        IntPtr root = User32.GetAncestor(hwnd, User32.GA_ROOT);
        if (root != IntPtr.Zero)
            hwnd = root;

        if (!User32.GetWindowRect(hwnd, out var rect))
            return Rectangle.Empty;

        return new Rectangle(
            rect.Left - virtualBounds.X,
            rect.Top - virtualBounds.Y,
            rect.Width,
            rect.Height);
    }

    private static IEnumerable<IntPtr> EnumerateTopLevelWindows()
    {
        var windows = new List<IntPtr>();
        User32.EnumWindows((hWnd, _) =>
        {
            if (hWnd != IntPtr.Zero && User32.IsWindowVisible(hWnd))
                windows.Add(hWnd);
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    private static bool IsUsableRect(Rectangle rect)
        => rect.Width > 2 && rect.Height > 2;

    private static bool IsIgnoredWindowHandle(nint hwnd)
    {
        if (hwnd == 0) return false;
        lock (IgnoredHandleLock)
            return IgnoredHandles.Contains(hwnd);
    }
}
