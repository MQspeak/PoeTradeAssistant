namespace Poe2MarketScanner.App.Services;

public sealed class SellQueryOcrResult
{
    public string GoldCostRaw { get; init; } = string.Empty;
    public string GoldCostNormalized { get; init; } = string.Empty;
    public string RatioRaw { get; init; } = string.Empty;
    public string RatioNormalized { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
}
