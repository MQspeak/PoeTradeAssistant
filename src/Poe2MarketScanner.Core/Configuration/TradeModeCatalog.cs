using System;
using System.Collections.Generic;
using System.Linq;

namespace Poe2MarketScanner.Core.Configuration;

public sealed class TradeModeDefinition
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string BuyCurrencyName { get; init; } = string.Empty;
    public string SellCurrencyName { get; init; } = string.Empty;
}

public static class TradeModeCatalog
{
    public static IReadOnlyList<TradeModeDefinition> All { get; } = new[]
    {
        new TradeModeDefinition
        {
            Key = "崇高石买神圣石卖",
            DisplayName = "崇高石买神圣石卖",
            BuyCurrencyName = "崇高石",
            SellCurrencyName = "神圣石"
        },
        new TradeModeDefinition
        {
            Key = "混沌石买神圣石卖",
            DisplayName = "混沌石买神圣石卖",
            BuyCurrencyName = "混沌石",
            SellCurrencyName = "神圣石"
        },
        new TradeModeDefinition
        {
            Key = "崇高石买混沌石卖",
            DisplayName = "崇高石买混沌石卖",
            BuyCurrencyName = "崇高石",
            SellCurrencyName = "混沌石"
        },
        new TradeModeDefinition
        {
            Key = "混沌石买崇高石卖",
            DisplayName = "混沌石买崇高石卖",
            BuyCurrencyName = "混沌石",
            SellCurrencyName = "崇高石"
        },
        new TradeModeDefinition
        {
            Key = "神圣石买崇高石卖",
            DisplayName = "神圣石买崇高石卖",
            BuyCurrencyName = "神圣石",
            SellCurrencyName = "崇高石"
        },
        new TradeModeDefinition
        {
            Key = "神圣石买混沌石卖",
            DisplayName = "神圣石买混沌石卖",
            BuyCurrencyName = "神圣石",
            SellCurrencyName = "混沌石"
        }
    };

    public static TradeModeDefinition Resolve(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return All[0];
        }

        return All.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal))
            ?? All[0];
    }

    public static string LocalizeTradeModeDisplayName(TradeModeDefinition mode, bool useTraditionalChinese)
    {
        ArgumentNullException.ThrowIfNull(mode);
        return useTraditionalChinese ? ToTraditional(mode.DisplayName) : mode.DisplayName;
    }

    public static string LocalizeCurrencyName(string currencyName, bool useTraditionalChinese)
    {
        if (!useTraditionalChinese)
        {
            return currencyName;
        }

        return ToTraditional(currencyName);
    }

    private static string ToTraditional(string text)
    {
        return text
            .Replace("神圣石", "神聖石", StringComparison.Ordinal)
            .Replace("神圣", "神聖", StringComparison.Ordinal);
    }
}
