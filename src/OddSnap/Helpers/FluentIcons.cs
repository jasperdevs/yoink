using System.Drawing;
using System.Windows.Media.Imaging;

namespace OddSnap.Helpers;

/// <summary>
/// Compatibility facade for call sites that request Fluent icons.
/// Rendering stays backed by bundled SVG path data, not OS icon fonts, so icons
/// are stable across Windows 10 and Windows 11.
/// </summary>
public static class FluentIcons
{
    public static void Preload() => StreamlineIcons.Preload();

    public static Bitmap? GetIcon(string id, bool active = false)
        => StreamlineIcons.GetIcon(id, active);

    public static Bitmap? RenderBitmap(string id, Color color, int size, bool active = false)
        => StreamlineIcons.RenderBitmap(id, color, size, active);

    public static bool HasIcon(string id) => StreamlineIcons.HasIcon(id);

    public static void DrawIcon(Graphics g, string id, RectangleF bounds, Color color, float iconInset = 7f, bool active = false)
        => StreamlineIcons.DrawIcon(g, id, bounds, color, iconInset, active);

    public static BitmapSource? RenderWpf(string id, Color color, int size, bool active = false)
        => StreamlineIcons.RenderWpf(id, color, size, active);
}
