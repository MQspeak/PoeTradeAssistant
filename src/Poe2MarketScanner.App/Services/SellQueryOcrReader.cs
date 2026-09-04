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

public sealed class SellQueryOcrReader : ISellQueryOcrReader, IDisposable
{
    private readonly ScreenCaptureService _screenCaptureService = new();
    private readonly PaddleOcrAll _ocr;

    public SellQueryOcrReader()
    {
        FullOcrModel model = LocalFullModels.ChineseV5;
        _ocr = new PaddleOcrAll(model, PaddleDevice.Mkldnn())
        {
            AllowRotateDetection = false,
            Enable180Classification = false
        };
    }

    public SellQueryOcrResult Read(AppProfile profile) => Read(profile, true, true);

    public SellQueryOcrResult Read(AppProfile profile, bool recognizeGoldCost, bool recognizeRatio)
    {
        var debugDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Poe2MarketScanner",
            "debug",
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(debugDirectory);

        var goldRead = recognizeGoldCost
            ? ReadRegion(profile, "goldCost", profile.Regions["goldCost"], debugDirectory) : new RegionReadResult();
        var ratioRead = recognizeRatio
            ? ReadRegion(profile, "ratio", profile.Regions["ratio"], debugDirectory) : new RegionReadResult();

        var goldParsed = OcrTextParser.ParseGoldCost(goldRead.RawText);
        var ratioParsed = OcrTextParser.ParseRatio(ratioRead.RawText);
        var success = (!recognizeGoldCost || goldParsed.Success) && (!recognizeRatio || ratioParsed.Success);

        return new SellQueryOcrResult
        {
            GoldCostRaw = goldRead.RawText,
            GoldCostNormalized = goldParsed.NormalizedText,
            RatioRaw = ratioRead.RawText,
            RatioNormalized = ratioParsed.NormalizedText,
            Status = success ? "ok" : "parse_failed",
            ErrorMessage = success
                ? null
                : $"gold={goldParsed.Status}; ratio={ratioParsed.Status}"
        };
    }

    public void Dispose()
    {
        _ocr.Dispose();
    }

    private RegionReadResult ReadRegion(AppProfile profile, string regionKey, ScreenRegion region, string debugDirectory)
    {
        var originalPath = Path.Combine(debugDirectory, $"{regionKey}-raw.png");
        var processedPath = Path.Combine(debugDirectory, $"{regionKey}-processed.png");

        _screenCaptureService.CaptureRegion(region, originalPath);
        var settings = AppProfileNormalizer.ResolveOcrRegionSettings(profile, regionKey);
        Preprocess(originalPath, processedPath, settings);

        using Mat src = Cv2.ImRead(processedPath, ImreadModes.Color);
        var result = _ocr.Run(src);

        return new RegionReadResult
        {
            RawText = result.Text?.Trim() ?? string.Empty
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
            new Size(
                Math.Max(1, (int)Math.Round(working.Width * scale)),
                Math.Max(1, (int)Math.Round(working.Height * scale))),
            0,
            0,
            InterpolationFlags.Nearest);

        using Mat output = new();
        if (settings.EnableBinarization)
        {
            Cv2.Threshold(resized, output, settings.Threshold, 255, ThresholdTypes.Binary);
            if (settings.EnableOtsuFallback && Cv2.CountNonZero(output) == 0)
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

    private sealed class RegionReadResult
    {
        public string RawText { get; init; } = string.Empty;
    }
}
