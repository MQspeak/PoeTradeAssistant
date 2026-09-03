using System.Text.Json.Serialization;

namespace PoeTradeAssistant.Contracts.MarketScan;

/// <summary>Legacy file shape already consumed by the standalone calculator.</summary>
public sealed record LegacyPriceTableV1
{
    [JsonPropertyName("购买用通货")]
    public string BuyCurrency { get; init; } = string.Empty;

    [JsonPropertyName("出售目标通货")]
    public string SellCurrency { get; init; } = string.Empty;

    [JsonPropertyName("当前交易对比例")]
    public string CurrentPairRatio { get; init; } = string.Empty;

    [JsonPropertyName("条目")]
    public IReadOnlyList<LegacyPriceTableItemV1> Items { get; init; } = Array.Empty<LegacyPriceTableItemV1>();
}

public sealed record LegacyPriceTableItemV1
{
    [JsonPropertyName("名字")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("金币消耗")]
    public string GoldCost { get; init; } = string.Empty;

    [JsonPropertyName("买入比例")]
    public string BuyRatio { get; init; } = string.Empty;

    [JsonPropertyName("卖出比例")]
    public string SellRatio { get; init; } = string.Empty;
}
