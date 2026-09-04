using System.Text.Json.Serialization;

namespace PoeTradeAssistant.Contracts.MarketScan;

/// <summary>
/// The stable, versioned result produced by a completed market scan.
/// Ratio semantics are explicit: <see cref="NormalizedRatio.RightPerLeft"/> is right / left.
/// </summary>
public sealed record PriceScanDocument
{
    public const string CurrentSchemaVersion = "poe-trade-scan/v2";

    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = CurrentSchemaVersion;

    [JsonPropertyName("capturedAt")]
    public DateTimeOffset CapturedAt { get; init; }

    [JsonPropertyName("tradeMode")]
    public string TradeMode { get; init; } = string.Empty;

    [JsonPropertyName("pair")]
    public TradePairSnapshot Pair { get; init; } = new();

    [JsonPropertyName("items")]
    public IReadOnlyList<PriceScanItem> Items { get; init; } = Array.Empty<PriceScanItem>();
}

public sealed record TradePairSnapshot
{
    [JsonPropertyName("buyCurrency")]
    public string BuyCurrency { get; init; } = string.Empty;

    [JsonPropertyName("sellCurrency")]
    public string SellCurrency { get; init; } = string.Empty;

    [JsonPropertyName("currentRatio")]
    public NormalizedRatio CurrentRatio { get; init; } = new();
}

public sealed record PriceScanItem
{
    [JsonPropertyName("priceObservations")]
    public IReadOnlyDictionary<string, PriceObservation> PriceObservations { get; init; } = new Dictionary<string, PriceObservation>();
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("goldCost")]
    public string GoldCost { get; init; } = string.Empty;

    [JsonPropertyName("highestBuyPrice")]
    public decimal? HighestBuyPrice { get; init; }

    [JsonPropertyName("lowestBuyPrice")]
    public decimal? LowestBuyPrice { get; init; }

    [JsonPropertyName("highestSellPrice")]
    public decimal? HighestSellPrice { get; init; }

    [JsonPropertyName("lowestSellPrice")]
    public decimal? LowestSellPrice { get; init; }

    [JsonPropertyName("buyRatio")]
    public NormalizedRatio BuyRatio { get; init; } = new();

    [JsonPropertyName("sellRatio")]
    public NormalizedRatio SellRatio { get; init; } = new();

    [JsonPropertyName("status")]
    public string Status { get; init; } = "unknown";

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }

    [JsonPropertyName("capturedAt")]
    public DateTimeOffset CapturedAt { get; init; }
}

public sealed record PriceObservation
{
    [JsonPropertyName("raw")]
    public string Raw { get; init; } = string.Empty;
    [JsonPropertyName("normalized")]
    public string Normalized { get; init; } = string.Empty;
    [JsonPropertyName("status")]
    public string Status { get; init; } = "pending";
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
}

public sealed record NormalizedRatio
{
    [JsonPropertyName("raw")]
    public string Raw { get; init; } = string.Empty;

    [JsonPropertyName("left")]
    public decimal? Left { get; init; }

    [JsonPropertyName("right")]
    public decimal? Right { get; init; }

    [JsonPropertyName("rightPerLeft")]
    public decimal? RightPerLeft { get; init; }
}
