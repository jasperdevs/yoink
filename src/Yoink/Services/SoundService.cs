using System.IO;
using System.Media;

namespace Yoink.Services;

public static class SoundService
{
    private static byte[]? _captureWav;
    private static byte[]? _colorWav;
    private static byte[]? _textWav;

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

    public static void PlayColorSound()
    {
        try
        {
            _colorWav ??= GenerateColorWav();
            using var ms = new MemoryStream(_colorWav);
            using var player = new SoundPlayer(ms);
            player.Play();
        }
        catch { }
    }

    public static void PlayTextSound()
    {
        try
        {
            _textWav ??= GenerateTextWav();
            using var ms = new MemoryStream(_textWav);
            using var player = new SoundPlayer(ms);
            player.Play();
        }
        catch { }
    }

    /// <summary>
    /// Short 30ms soft click/pop sound for screenshot capture.
    /// </summary>
    private static byte[] GenerateClickWav()
    {
        const int sampleRate = 22050;
        const int durationMs = 30;
        int numSamples = sampleRate * durationMs / 1000;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        WriteWavHeader(bw, numSamples, sampleRate);

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;
            double envelope = Math.Exp(-t * 120);
            double sample = Math.Sin(2 * Math.PI * 800 * t) * envelope * 0.3;
            bw.Write((short)(sample * short.MaxValue));
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Short bright "bling" for color pick: two ascending tones.
    /// </summary>
    private static byte[] GenerateColorWav()
    {
        const int sampleRate = 22050;
        const int durationMs = 80;
        int numSamples = sampleRate * durationMs / 1000;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        WriteWavHeader(bw, numSamples, sampleRate);

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;
            double envelope = Math.Exp(-t * 50);
            // Two-tone ascending: 1200Hz then 1600Hz
            double freq = t < 0.035 ? 1200 : 1600;
            double sample = Math.Sin(2 * Math.PI * freq * t) * envelope * 0.25;
            bw.Write((short)(sample * short.MaxValue));
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Soft sweep for text/OCR capture: descending tone with harmonics.
    /// </summary>
    private static byte[] GenerateTextWav()
    {
        const int sampleRate = 22050;
        const int durationMs = 60;
        int numSamples = sampleRate * durationMs / 1000;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        WriteWavHeader(bw, numSamples, sampleRate);

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;
            double envelope = Math.Exp(-t * 70);
            // Descending sweep from 1000Hz to 600Hz
            double freq = 1000 - (400 * t / 0.06);
            double sample = (Math.Sin(2 * Math.PI * freq * t) * 0.7
                           + Math.Sin(2 * Math.PI * freq * 2 * t) * 0.3)
                           * envelope * 0.25;
            bw.Write((short)(sample * short.MaxValue));
        }

        return ms.ToArray();
    }

    private static void WriteWavHeader(BinaryWriter bw, int numSamples, int sampleRate)
    {
        int dataSize = numSamples * 2; // 16-bit mono
        bw.Write("RIFF"u8);
        bw.Write(36 + dataSize);
        bw.Write("WAVE"u8);
        bw.Write("fmt "u8);
        bw.Write(16);
        bw.Write((short)1); // PCM
        bw.Write((short)1); // mono
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2); // byte rate
        bw.Write((short)2); // block align
        bw.Write((short)16); // bits per sample
        bw.Write("data"u8);
        bw.Write(dataSize);
    }
}
