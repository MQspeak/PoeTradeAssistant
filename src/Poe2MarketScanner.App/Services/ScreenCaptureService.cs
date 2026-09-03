using System;
using System.Drawing;
using System.Drawing.Imaging;
using Poe2MarketScanner.Core.Configuration;

namespace Poe2MarketScanner.App.Services;

public sealed class ScreenCaptureService
{
    public string CaptureRegion(ScreenRegion region, string outputPath)
    {
        var width = Math.Max(1, (int)Math.Round(region.Width));
        var height = Math.Max(1, (int)Math.Round(region.Height));
        var x = (int)Math.Round(region.X);
        var y = (int)Math.Round(region.Y);

        using Bitmap bitmap = new(width, height);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height));
        bitmap.Save(outputPath, ImageFormat.Png);

        return outputPath;
    }
}
