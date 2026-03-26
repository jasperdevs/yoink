namespace Yoink.Models;

public enum AfterCaptureAction
{
    CopyToClipboard,
    ShowPreview
}

public sealed class AppSettings
{
    public uint HotkeyModifiers { get; set; } = Native.User32.MOD_ALT;
    public uint HotkeyKey { get; set; } = 0xC0; // VK_OEM_3 = backtick/tilde key
    public AfterCaptureAction AfterCapture { get; set; } = AfterCaptureAction.CopyToClipboard;
    public bool SaveToFile { get; set; }
    public string SaveDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    public bool StartWithWindows { get; set; }
    public CaptureMode LastCaptureMode { get; set; } = CaptureMode.Rectangle;
    public bool SaveHistory { get; set; } = true;
}
