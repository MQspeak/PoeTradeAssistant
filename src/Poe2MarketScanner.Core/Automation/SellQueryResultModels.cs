using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Poe2MarketScanner.Core.Automation;

public sealed class SellQueryBatchResult
{
    public string Mode { get; set; } = "sell";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public List<SellQueryItemResult> Items { get; init; } = new();

    [JsonIgnore]
    public string OutputPath { get; set; } = string.Empty;
}

public sealed class SellQueryItemResult
{
    public decimal? HighestBuyPrice { get; set; }
    public decimal? LowestBuyPrice { get; set; }
    public decimal? HighestSellPrice { get; set; }
    public decimal? LowestSellPrice { get; set; }
    public Dictionary<string, ScanPriceObservation> PriceObservations { get; } = new();
    public string CurrencyName { get; set; } = string.Empty;
    public string BuyCurrencyName { get; set; } = string.Empty;
    public string SellCurrencyName { get; set; } = string.Empty;
    public string CurrentPairRatioRaw { get; set; } = string.Empty;
    public string CurrentPairRatioNormalized { get; set; } = string.Empty;
    public string GoldCostRaw { get; set; } = string.Empty;
    public string GoldCostNormalized { get; set; } = string.Empty;
    public string BuyRatioRaw { get; set; } = string.Empty;
    public string BuyRatioNormalized { get; set; } = string.Empty;
    public string SellRatioRaw { get; set; } = string.Empty;
    public string SellRatioNormalized { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
}

public sealed class ScanPriceObservation
{
    public string Raw { get; init; } = string.Empty;
    public string Normalized { get; init; } = string.Empty;
    public string Status { get; init; } = "pending";
    public string? ErrorMessage { get; init; }
}
