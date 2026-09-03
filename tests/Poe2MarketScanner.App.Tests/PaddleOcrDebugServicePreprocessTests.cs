using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using Poe2MarketScanner.Core.Configuration;
using Xunit;

namespace Poe2MarketScanner.App.Tests;

public sealed class PaddleOcrDebugServicePreprocessTests
{
    [Fact]
    public void Preprocess_ShouldPreserveVisiblePixels_ForGoldToneDigits()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"Poe2MarketScannerTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var sourcePath = Path.Combine(tempDirectory, "gold-raw.png");
            var outputPath = Path.Combine(tempDirectory, "gold-processed.png");
            CreateGoldToneDigitsImage(sourcePath);

            var method = typeof(Services.PaddleOcrDebugService).GetMethod(
                "Preprocess",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);

            var settings = new OcrRegionSettings
            {
                Enabled = true,
                Scale = 2.0,
                Threshold = 160,
                EnableGrayscale = true,
                EnableBinarization = true,
                EnableOtsuFallback = true
            };
            method!.Invoke(null, new object[] { sourcePath, outputPath, settings });

            using var bitmap = new Bitmap(outputPath);
            var nonBlackPixels = CountNonBlackPixels(bitmap);

            Assert.True(nonBlackPixels > 0, "Expected preprocess output to keep visible foreground pixels for gold-tone digits.");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static void CreateGoldToneDigitsImage(string path)
    {
        using var bitmap = new Bitmap(140, 44);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(12, 12, 12));

        using var backgroundBrush = new SolidBrush(Color.FromArgb(32, 32, 32));
        graphics.FillRectangle(backgroundBrush, 0, 8, 140, 28);

        using var shadowBrush = new SolidBrush(Color.FromArgb(48, 38, 18));
        graphics.DrawString("8,670", new Font("Segoe UI", 18, FontStyle.Bold, GraphicsUnit.Pixel), shadowBrush, new PointF(37, 10));

        using var goldBrush = new SolidBrush(Color.FromArgb(160, 122, 48));
        graphics.DrawString("8,670", new Font("Segoe UI", 18, FontStyle.Bold, GraphicsUnit.Pixel), goldBrush, new PointF(35, 8));

        bitmap.Save(path, ImageFormat.Png);
    }

    private static int CountNonBlackPixels(Bitmap bitmap)
    {
        return Enumerable.Range(0, bitmap.Height)
            .SelectMany(y => Enumerable.Range(0, bitmap.Width).Select(x => bitmap.GetPixel(x, y)))
            .Count(pixel => pixel.R != 0 || pixel.G != 0 || pixel.B != 0);
    }
}
