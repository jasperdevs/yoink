using System.Runtime.InteropServices;

namespace OddSnap.Native;

internal static class Shcore
{
    public enum MonitorDpiType
    {
        EffectiveDpi = 0
    }

    [DllImport("shcore.dll")]
    public static extern int GetDpiForMonitor(
        IntPtr hmonitor,
        MonitorDpiType dpiType,
        out uint dpiX,
        out uint dpiY);
}
