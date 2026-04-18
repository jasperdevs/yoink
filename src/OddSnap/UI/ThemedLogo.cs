using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaColor = System.Windows.Media.Color;

namespace OddSnap.UI;

public static class ThemedLogo
{
    private const string SquarePath = "pack://application:,,,/Assets/oddsnap_square.png";
    private const string WordmarkPath = "pack://application:,,,/Assets/oddsnap_wordmark.png";

    public static ImageSource Square(int size) => Render(SquarePath, size, size, Theme.TextPrimary);

    public static ImageSource Wordmark(int width, int height) => Render(WordmarkPath, width, height, Theme.TextPrimary);

    private static ImageSource Render(string resourcePath, int width, int height, MediaColor color)
    {
        var source = LoadBitmap(resourcePath);
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        int sourceStride = converted.PixelWidth * 4;
        var sourcePixels = new byte[sourceStride * converted.PixelHeight];
        converted.CopyPixels(sourcePixels, sourceStride, 0);

        for (int i = 0; i < sourcePixels.Length; i += 4)
        {
            byte alpha = sourcePixels[i + 3];
            if (alpha == 0)
                continue;

            sourcePixels[i] = color.B;
            sourcePixels[i + 1] = color.G;
            sourcePixels[i + 2] = color.R;
        }

        var tinted = BitmapSource.Create(
            converted.PixelWidth,
            converted.PixelHeight,
            converted.DpiX,
            converted.DpiY,
            PixelFormats.Bgra32,
            null,
            sourcePixels,
            sourceStride);

        var scaled = new TransformedBitmap(
            tinted,
            new ScaleTransform(
                width / (double)tinted.PixelWidth,
                height / (double)tinted.PixelHeight));
        scaled.Freeze();
        return scaled;
    }

    private static BitmapSource LoadBitmap(string resourcePath)
    {
        var info = Application.GetResourceStream(new Uri(resourcePath, UriKind.Absolute))
            ?? throw new InvalidOperationException($"Logo resource not found: {resourcePath}");
        var decoder = BitmapDecoder.Create(info.Stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }
}
