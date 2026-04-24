using System.Windows;
using System.Windows.Shell;

namespace OddSnap.UI;

public static class OddSnapWindowChrome
{
    public static void Apply(Window window)
    {
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = 0,
            CornerRadius = new CornerRadius(12),
            GlassFrameThickness = new Thickness(0),
            ResizeBorderThickness = new Thickness(8),
            UseAeroCaptionButtons = false
        });
    }
}
