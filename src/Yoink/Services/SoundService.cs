using System.Media;

namespace Yoink.Services;

/// <summary>
/// Plays a subtle capture sound using Windows system sounds.
/// </summary>
public static class SoundService
{
    public static void PlayCaptureSound()
    {
        // Use the system "device connect" sound which is a subtle click
        // Falls back silently if sound is disabled
        try
        {
            SystemSounds.Asterisk.Play();
        }
        catch { }
    }
}
