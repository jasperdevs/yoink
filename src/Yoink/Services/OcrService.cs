using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Yoink.Services;

public static class OcrService
{
    public static async Task<string> RecognizeAsync(Bitmap bitmap)
    {
        try
        {
            // Use file-based approach for most reliable WinRT interop
            var tmpPath = Path.Combine(Path.GetTempPath(), $"yoink_ocr_{Guid.NewGuid():N}.png");
            bitmap.Save(tmpPath, ImageFormat.Png);

            try
            {
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(tmpPath);
                using var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);

                var decoder = await BitmapDecoder.CreateAsync(stream);
                var softwareBmp = await decoder.GetSoftwareBitmapAsync(
                    Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);

                var engine = OcrEngine.TryCreateFromUserProfileLanguages()
                    ?? OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));

                if (engine is null) return "";

                var result = await engine.RecognizeAsync(softwareBmp);
                return result.Text ?? "";
            }
            finally
            {
                try { File.Delete(tmpPath); } catch { }
            }
        }
        catch
        {
            return "";
        }
    }
}
