using System;
using System.Linq;
using Poe2MarketScanner.Core.Automation;
using PoeTradeAssistant.Contracts.MarketScan;
using PoeTradeAssistant.ScannerIntegration;
using Xunit;

namespace PoeTradeAssistant.ScannerIntegration.Tests;

public sealed class PriceScanDocumentAdapterTests
{
    [Fact]
    public void FromScannerBatch_ShouldPreserveStatusesAndNormalizeCurrencies()
    {
        var capturedAt = new DateTimeOffset(2026, 9, 2, 10, 30, 0, TimeSpan.FromHours(8));
        var batch = new SellQueryBatchResult
        {
            Mode = "exalted-to-divine",
            StartedAt = capturedAt.AddMinutes(-1),
            FinishedAt = capturedAt,
            Items =
            {
                new SellQueryItemResult
                {
                    CurrencyName = "混沌石",
                    BuyCurrencyName = "崇高石",
                    SellCurrencyName = "神聖石",
                    CurrentPairRatioRaw = "675 : 1",
                    CurrentPairRatioNormalized = "675:1",
                    GoldCostNormalized = "81000",
                    BuyRatioNormalized = "2:1",
                    SellRatioNormalized = "1:3",
                    Status = "ok",
                    CapturedAt = capturedAt
                }
            }
        };

        var document = PriceScanDocumentAdapter.FromScannerBatch(batch);

        Assert.Equal(PriceScanDocument.CurrentSchemaVersion, document.SchemaVersion);
        Assert.Equal("E", document.Pair.BuyCurrency);
        Assert.Equal("D", document.Pair.SellCurrency);
        Assert.Equal(1m / 675m, document.Pair.CurrentRatio.RightPerLeft);
        Assert.Equal("ok", document.Items.Single().Status);
        Assert.Equal(3m, document.Items.Single().SellRatio.RightPerLeft);
    }

    [Fact]
    public void FromLegacyTable_ShouldReadCurrentCalculatorFileShape()
    {
        var capturedAt = new DateTimeOffset(2026, 9, 2, 11, 0, 0, TimeSpan.FromHours(8));
        var table = new LegacyPriceTableV1
        {
            BuyCurrency = "神圣石",
            SellCurrency = "混沌石",
            CurrentPairRatio = "1:120",
            Items = new[]
            {
                new LegacyPriceTableItemV1 { Name = "崇高石", GoldCost = "25", BuyRatio = "3:1", SellRatio = "1:2" }
            }
        };

        var document = PriceScanDocumentAdapter.FromLegacyTable(table, capturedAt);

        Assert.Equal("D", document.Pair.BuyCurrency);
        Assert.Equal("C", document.Pair.SellCurrency);
        Assert.Equal(120m, document.Pair.CurrentRatio.RightPerLeft);
        Assert.Equal("imported-v1", document.Items.Single().Status);
        Assert.Equal(1m / 3m, document.Items.Single().BuyRatio.RightPerLeft);
    }
}
