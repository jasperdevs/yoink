using System.Windows.Forms;
using OddSnap.Native;

namespace OddSnap.Capture;

internal static class CaptureWindowExclusion
{
    public static void Apply(Form form)
    {
        if (form.IsDisposed || !form.IsHandleCreated)
            return;

        Apply(form.Handle);
    }

    public static void Apply(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return;

        try
        {
            User32.SetWindowDisplayAffinity(handle, User32.WDA_EXCLUDEFROMCAPTURE);
        }
        catch
        {
            // Best-effort only; older Windows builds do not support capture exclusion.
        }
    }
}
