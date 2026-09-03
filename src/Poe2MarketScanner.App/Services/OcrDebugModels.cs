namespace Poe2MarketScanner.App.Services;

public sealed class OcrDebugRegionResult
{
    public string RegionKey { get; init; } = string.Empty;
    public string RegionName { get; init; } = string.Empty;
    public string OriginalImagePath { get; init; } = string.Empty;
    public string ProcessedImagePath { get; init; } = string.Empty;
    public string RawText { get; init; } = string.Empty;
    public string NormalizedText { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed class OcrDebugRunResult
{
    public bool Success { get; init; }
    public string DebugDirectory { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public OcrDebugRegionResult? GoldCostResult { get; init; }
    public OcrDebugRegionResult? RatioResult { get; init; }
}
