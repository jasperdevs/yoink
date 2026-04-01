using System.Windows;
using System.Windows.Interop;
using Yoink.Models;

namespace Yoink.UI;

internal static class PopupWindowHelper
{
    public static void ApplyNoActivateChrome(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        int exStyle = Native.User32.GetWindowLongA(hwnd, Native.User32.GWL_EXSTYLE);
        exStyle |= 0x80; // WS_EX_TOOLWINDOW
        exStyle |= 0x08000000; // WS_EX_NOACTIVATE
        Native.User32.SetWindowLongA(hwnd, Native.User32.GWL_EXSTYLE, exStyle);
        Native.Dwm.DisableBackdrop(hwnd);
    }

    public static (double targetLeft, double targetTop, double startLeft, double startTop, bool animateLeft) GetPlacement(
        ToastPosition position,
        double actualWidth,
        double actualHeight,
        Rect workArea,
        double edge = 8,
        double offScreenDistance = 10)
    {
        return position switch
        {
            ToastPosition.Left =>
                (edge, workArea.Bottom - actualHeight - edge, -actualWidth - offScreenDistance, workArea.Bottom - actualHeight - edge, true),
            ToastPosition.TopLeft =>
                (edge, edge, edge, -actualHeight - offScreenDistance, false),
            ToastPosition.TopRight =>
                (workArea.Right - actualWidth - edge, edge, workArea.Right - actualWidth - edge, -actualHeight - offScreenDistance, false),
            _ =>
                (workArea.Right - actualWidth - edge, workArea.Bottom - actualHeight - edge, workArea.Right + offScreenDistance, workArea.Bottom - actualHeight - edge, true),
        };
    }

    public static (double exitLeft, double exitTop, bool animateLeft) GetDismissPlacement(
        ToastPosition position,
        double actualWidth,
        double actualHeight,
        Rect workArea,
        double edge = 8,
        double exitDistance = 20)
    {
        return position switch
        {
            ToastPosition.Left =>
                (-actualWidth - exitDistance, workArea.Bottom - actualHeight - edge, true),
            ToastPosition.TopLeft =>
                (edge, -actualHeight - exitDistance, false),
            ToastPosition.TopRight =>
                (workArea.Right - actualWidth - edge, -actualHeight - exitDistance, false),
            _ =>
                (workArea.Right + exitDistance, workArea.Bottom - actualHeight - edge, true),
        };
    }
}
