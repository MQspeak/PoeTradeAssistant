using System.Globalization;
using Poe2MarketScanner.Core.Automation;
using PoeTradeAssistant.Contracts.MarketScan;

namespace PoeTradeAssistant.ScannerIntegration;

/// <summary>Converts both scanner runtime results and the legacy calculator file into the V2 contract.</summary>
public static class PriceScanDocumentAdapter
{
    private static readonly IReadOnlyDictionary<string, string> CurrencyAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["混沌石"] = "C",
            ["Chaos Orb"] = "C",
            ["C"] = "C",
            ["神圣石"] = "D",
            ["神聖石"] = "D",
            ["Divine Orb"] = "D",
            ["D"] = "D",
            ["崇高石"] = "E",
            ["Exalted Orb"] = "E",
            ["E"] = "E"
        };

    public static PriceScanDocument FromScannerBatch(SellQueryBatchResult batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var first = batch.Items.FirstOrDefault();
        var capturedAt = batch.FinishedAt != default ? batch.FinishedAt : batch.StartedAt;

        return new PriceScanDocument
        {
            CapturedAt = capturedAt,
            TradeMode = batch.Mode,
            Pair = new TradePairSnapshot
            {
                BuyCurrency = NormalizeCurrency(first?.BuyCurrencyName),
                SellCurrency = NormalizeCurrency(first?.SellCurrencyName),
                CurrentRatio = ParseRatio(first?.CurrentPairRatioRaw, first?.CurrentPairRatioNormalized)
            },
            Items = batch.Items.Select(item => new PriceScanItem
            {
                Name = item.CurrencyName.Trim(),
                GoldCost = item.GoldCostNormalized,
                BuyRatio = ParseRatio(item.BuyRatioRaw, item.BuyRatioNormalized),
                SellRatio = ParseRatio(item.SellRatioRaw, item.SellRatioNormalized),
                Status = string.IsNullOrWhiteSpace(item.Status) ? "unknown" : item.Status,
                ErrorMessage = item.ErrorMessage,
                CapturedAt = item.CapturedAt
            }).ToArray()
        };
    }

    public static PriceScanDocument FromLegacyTable(LegacyPriceTableV1 table, DateTimeOffset capturedAt)
    {
        ArgumentNullException.ThrowIfNull(table);

        return new PriceScanDocument
        {
            CapturedAt = capturedAt,
            TradeMode = "legacy-import",
            Pair = new TradePairSnapshot
            {
                BuyCurrency = NormalizeCurrency(table.BuyCurrency),
                SellCurrency = NormalizeCurrency(table.SellCurrency),
                CurrentRatio = ParseRatio(table.CurrentPairRatio, table.CurrentPairRatio)
            },
            Items = table.Items.Select(item => new PriceScanItem
            {
                Name = item.Name.Trim(),
                GoldCost = item.GoldCost,
                BuyRatio = ParseRatio(item.BuyRatio, item.BuyRatio),
                SellRatio = ParseRatio(item.SellRatio, item.SellRatio),
                Status = "imported-v1",
                CapturedAt = capturedAt
            }).ToArray()
        };
    }

    public static string NormalizeCurrency(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return CurrencyAliases.TryGetValue(normalized, out var canonical) ? canonical : normalized;
    }

    public static NormalizedRatio ParseRatio(string? raw, string? normalized)
    {
        var source = string.IsNullOrWhiteSpace(normalized) ? raw?.Trim() ?? string.Empty : normalized.Trim();
        var separator = source.Contains(':') ? ':' : source.Contains('：') ? '：' : source.Contains('/') ? '/' : '\0';
        if (separator == '\0')
        {
            return new NormalizedRatio { Raw = raw?.Trim() ?? string.Empty };
        }

        var parts = source.Split(separator, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var left) ||
            !decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var right) ||
            left <= 0 || right <= 0)
        {
            return new NormalizedRatio { Raw = raw?.Trim() ?? string.Empty };
        }

        return new NormalizedRatio
        {
            Raw = raw?.Trim() ?? string.Empty,
            Left = left,
            Right = right,
            RightPerLeft = right / left
        };
    }
}
