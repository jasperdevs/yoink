using System.IO;
using System.Media;

namespace Yoink.Services;

public static class SoundService
{
    private static byte[]? _captureWav;

    public static void PlayCaptureSound()
    {
        try
        {
            _captureWav ??= GenerateClickWav();
            using var ms = new MemoryStream(_captureWav);
            using var player = new SoundPlayer(ms);
            player.Play();
        }
        catch { }
    }

    /// <summary>
    /// Generates a tiny WAV file in memory: a short 30ms soft click/pop sound.
    /// </summary>
    private static byte[] GenerateClickWav()
    {
        const int sampleRate = 22050;
        const int durationMs = 30;
        int numSamples = sampleRate * durationMs / 1000;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // WAV header
        int dataSize = numSamples * 2; // 16-bit mono
        bw.Write("RIFF"u8);
        bw.Write(36 + dataSize);
        bw.Write("WAVE"u8);
        bw.Write("fmt "u8);
        bw.Write(16); // chunk size
        bw.Write((short)1); // PCM
        bw.Write((short)1); // mono
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2); // byte rate
        bw.Write((short)2); // block align
        bw.Write((short)16); // bits per sample
        bw.Write("data"u8);
        bw.Write(dataSize);

        // Generate a short pop: sine wave with fast decay
        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;
            double envelope = Math.Exp(-t * 120); // fast decay
            double sample = Math.Sin(2 * Math.PI * 800 * t) * envelope * 0.3;
            short pcm = (short)(sample * short.MaxValue);
            bw.Write(pcm);
        }

        return ms.ToArray();
    }
}
