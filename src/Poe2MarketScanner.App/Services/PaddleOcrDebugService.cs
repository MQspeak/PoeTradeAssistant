using System;
using System.IO;
using OpenCvSharp;
using Poe2MarketScanner.Core.Configuration;
using Poe2MarketScanner.Core.Ocr;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Local;

namespace Poe2MarketScanner.App.Services;

public sealed class PaddleOcrDebugService : IDisposable
{
    private readonly ScreenCaptureService _screenCaptureService = new();
    private readonly PaddleOcrAll _ocr;

    public PaddleOcrDebugService()
    {
        FullOcrModel model = LocalFullModels.ChineseV5;
        _ocr = new PaddleOcrAll(model, PaddleDevice.Mkldnn())
        {
            AllowRotateDetection = false,
            Enable180Classification = false
        };
    }

    public OcrDebugRunResult Run(AppProfile profile)
    {
        var debugDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Poe2MarketScanner",
            "debug",
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(debugDirectory);

        var goldResult = RunForRegion(profile, "goldCost", profile.Regions["goldCost"], debugDirectory, OcrTextParser.ParseGoldCost);
        var ratioResult = RunForRegion(profile, "ratio", profile.Regions["ratio"], debugDirectory, OcrTextParser.ParseRatio);

        return new OcrDebugRunResult
        {
            Success = goldResult.Status == "ok" || ratioResult.Status == "ok",
            DebugDirectory = debugDirectory,
            GoldCostResult = goldResult,
            RatioResult = ratioResult,
            Summary = $"gold={goldResult.NormalizedText}({goldResult.Status}) | ratio={ratioResult.NormalizedText}({ratioResult.Status})"
        };
    }

    public void Dispose()
    {
        _ocr.Dispose();
    }

    private OcrDebugRegionResult RunForRegion(
        AppProfile profile,
        string regionKey,
        ScreenRegion region,
        string debugDirectory,
        Func<string?, OcrParseResult> parser)
    {
        var originalPath = Path.Combine(debugDirectory, $"{regionKey}-raw.png");
        var processedPath = Path.Combine(debugDirectory, $"{regionKey}-processed.png");

        _screenCaptureService.CaptureRegion(region, originalPath);
        var settings = AppProfileNormalizer.ResolveOcrRegionSettings(profile, regionKey);
        Preprocess(originalPath, processedPath, settings);

        using Mat src = Cv2.ImRead(processedPath, ImreadModes.Color);
        PaddleOcrResult result = _ocr.Run(src);
        var rawText = result.Text?.Trim() ?? string.Empty;
        var parsed = parser(rawText);

        return new OcrDebugRegionResult
        {
            RegionKey = regionKey,
            RegionName = region.Name,
            OriginalImagePath = originalPath,
            ProcessedImagePath = processedPath,
            RawText = rawText,
            NormalizedText = parsed.NormalizedText,
            Status = parsed.Status
        };
    }

    private static void Preprocess(string originalPath, string processedPath, OcrRegionSettings settings)
    {
        using Mat source = Cv2.ImRead(originalPath, ImreadModes.Color);
        using Mat working = settings.EnableGrayscale
            ? source.CvtColor(ColorConversionCodes.BGR2GRAY)
            : source.Clone();

        using Mat resized = new();
        var scale = settings.Scale <= 0 ? 1.0 : settings.Scale;
        Cv2.Resize(
            working,
            resized,
            new OpenCvSharp.Size(
                Math.Max(1, (int)Math.Round(working.Width * scale)),
                Math.Max(1, (int)Math.Round(working.Height * scale))),
            0,
            0,
            InterpolationFlags.Nearest);

        using Mat output = new();
        if (settings.EnableBinarization)
        {
            Cv2.Threshold(resized, output, settings.Threshold, 255, ThresholdTypes.Binary);
            if (settings.EnableOtsuFallback && CountForegroundPixels(output) == 0)
            {
                Cv2.Threshold(resized, output, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            }
        }
        else
        {
            resized.CopyTo(output);
        }

        Cv2.ImWrite(processedPath, output);
    }

    private static int CountForegroundPixels(Mat image)
    {
        return Cv2.CountNonZero(image);
    }
}
