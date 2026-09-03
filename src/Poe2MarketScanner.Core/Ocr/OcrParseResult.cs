namespace Poe2MarketScanner.Core.Ocr;

public sealed class OcrParseResult
{
    public bool Success { get; init; }
    public string NormalizedText { get; init; } = string.Empty;
    public int? GoldCostValue { get; init; }
    public decimal? RatioLeft { get; init; }
    public decimal? RatioRight { get; init; }
    public string Status { get; init; } = "parse_failed";
}
