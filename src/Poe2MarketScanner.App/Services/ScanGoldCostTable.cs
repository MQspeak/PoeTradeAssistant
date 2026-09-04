using System;
using System.Collections.Generic;
using System.Globalization;
using PoeTradeAssistant.ScannerIntegration;

namespace Poe2MarketScanner.App.Services;

/// <summary>Gold costs already recorded in the current game workspace or this scan.</summary>
public sealed class ScanGoldCostTable
{
    private readonly Dictionary<string, string> _costs = new(StringComparer.Ordinal);

    public bool TryGet(string currencyName, out string goldCost) =>
        _costs.TryGetValue(PriceScanDocumentAdapter.NormalizeCurrency(currencyName), out goldCost!);

    public void Record(string currencyName, string? goldCost)
    {
        var name = PriceScanDocumentAdapter.NormalizeCurrency(currencyName);
        if (name.Length == 0 || !int.TryParse(goldCost, NumberStyles.Integer | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out var value) || value < 0) return;
        _costs[name] = value.ToString(CultureInfo.InvariantCulture);
    }
}
