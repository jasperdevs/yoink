using System.Windows.Interop;
using Yoink.Native;

namespace Yoink.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HOTKEY_CAPTURE = 9001;
    private const int HOTKEY_OCR = 9002;
    private const int HOTKEY_PICKER = 9003;
    private bool _captureRegistered;
    private bool _ocrRegistered;
    private bool _pickerRegistered;

    public event Action? HotkeyPressed;
    public event Action? OcrHotkeyPressed;
    public event Action? PickerHotkeyPressed;

    public bool Register(uint modifiers, uint key)
    {
        ComponentDispatcher.ThreadPreprocessMessage += OnMsg;
        _captureRegistered = User32.RegisterHotKey(
            IntPtr.Zero, HOTKEY_CAPTURE, modifiers | User32.MOD_NOREPEAT, key);
        return _captureRegistered;
    }

    public bool RegisterOcr(uint modifiers, uint key)
    {
        _ocrRegistered = User32.RegisterHotKey(
            IntPtr.Zero, HOTKEY_OCR, modifiers | User32.MOD_NOREPEAT, key);
        return _ocrRegistered;
    }

    public bool RegisterPicker(uint modifiers, uint key)
    {
        _pickerRegistered = User32.RegisterHotKey(
            IntPtr.Zero, HOTKEY_PICKER, modifiers | User32.MOD_NOREPEAT, key);
        return _pickerRegistered;
    }

    public void Unregister()
    {
        if (_captureRegistered) { User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_CAPTURE); _captureRegistered = false; }
        if (_ocrRegistered) { User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_OCR); _ocrRegistered = false; }
        if (_pickerRegistered) { User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_PICKER); _pickerRegistered = false; }
        ComponentDispatcher.ThreadPreprocessMessage -= OnMsg;
    }

    private void OnMsg(ref MSG msg, ref bool handled)
    {
        if (msg.message != User32.WM_HOTKEY) return;
        int id = (int)msg.wParam;
        if (id == HOTKEY_CAPTURE) { HotkeyPressed?.Invoke(); handled = true; }
        else if (id == HOTKEY_OCR) { OcrHotkeyPressed?.Invoke(); handled = true; }
        else if (id == HOTKEY_PICKER) { PickerHotkeyPressed?.Invoke(); handled = true; }
    }

    public void Dispose() => Unregister();
}
