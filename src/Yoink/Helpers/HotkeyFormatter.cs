using System.Windows.Input;

namespace Yoink.Helpers;

public static class HotkeyFormatter
{
    public static string Format(uint mod, uint key)
    {
        var parts = new List<string>();
        if ((mod & Native.User32.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((mod & Native.User32.MOD_ALT) != 0) parts.Add("Alt");
        if ((mod & Native.User32.MOD_SHIFT) != 0) parts.Add("Shift");
        var k = KeyInterop.KeyFromVirtualKey((int)key);
        parts.Add(k == Key.Oem3 ? "`" : k.ToString());
        return string.Join("+", parts);
    }
}
