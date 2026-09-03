using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Poe2MarketScanner.App.Services;
using Poe2MarketScanner.Core.Automation;
using PoeTradeAssistant.Contracts.MarketScan;
using Xunit;

namespace Poe2MarketScanner.App.Tests;

public sealed class ProjectOutputJsonWriterTests
{
    [Fact]
    public void WriteSellQueryBatch_ShouldCreateJsonUnderOutputDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"poe2-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var writer = new ProjectOutputJsonWriter(
                root,
                () => new DateTimeOffset(2026, 6, 25, 1, 0, 30, TimeSpan.FromHours(8)));

            var file = writer.WriteSellQueryBatch(new SellQueryBatchResult());

            Assert.StartsWith(Path.Combine(root, "output"), file);
            Assert.EndsWith("20260625-010030.json", file);
            Assert.True(File.Exists(file));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void WriteSellQueryBatch_ShouldSerializeTradePairHeaderAndItemList()
    {
        var root = Path.Combine(Path.GetTempPath(), $"poe2-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var writer = new ProjectOutputJsonWriter(
                root,
                () => new DateTimeOffset(2026, 6, 25, 1, 0, 30, TimeSpan.FromHours(8)));

            var file = writer.WriteSellQueryBatch(new SellQueryBatchResult
            {
                Items =
                {
                    new SellQueryItemResult
                    {
                        CurrencyName = "崇高石",
                        BuyCurrencyName = "混沌石",
                        SellCurrencyName = "神圣石",
                        CurrentPairRatioNormalized = "1:120",
                        GoldCostNormalized = "250",
                        BuyRatioNormalized = "1:572",
                        SellRatioNormalized = "1:220",
                        Status = "ok",
                        CapturedAt = new DateTimeOffset(2026, 6, 25, 1, 0, 31, TimeSpan.FromHours(8))
                    }
                }
            });

            var json = File.ReadAllText(file);
            using var document = JsonDocument.Parse(json);

            Assert.Equal("混沌石", document.RootElement.GetProperty("购买用通货").GetString());
            Assert.Equal("神圣石", document.RootElement.GetProperty("出售目标通货").GetString());
            Assert.Equal("1:120", document.RootElement.GetProperty("当前交易对比例").GetString());

            var items = document.RootElement.GetProperty("条目");
            Assert.Equal(JsonValueKind.Array, items.ValueKind);
            Assert.Single(items.EnumerateArray());

            var first = items[0];
            Assert.Equal("崇高石", first.GetProperty("名字").GetString());
            Assert.Equal("250", first.GetProperty("金币消耗").GetString());
            Assert.Equal("1:572", first.GetProperty("买入比例").GetString());
            Assert.Equal("1:220", first.GetProperty("卖出比例").GetString());
            Assert.Equal(4, first.EnumerateObject().Count());
            Assert.DoesNotContain("\\u540D\\u5B57", json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void WriteSellQueryBatch_ShouldHonorCustomOutputDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"poe2-output-{Guid.NewGuid():N}");
        var customDirectory = Path.Combine(root, "exports");
        Directory.CreateDirectory(root);

        try
        {
            var writer = new ProjectOutputJsonWriter(
                root,
                customDirectory,
                () => new DateTimeOffset(2026, 6, 25, 1, 0, 30, TimeSpan.FromHours(8)));

            var file = writer.WriteSellQueryBatch(new SellQueryBatchResult());

            Assert.StartsWith(customDirectory, file);
            Assert.True(File.Exists(file));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void WriteSellQueryBatch_ShouldAlsoWriteVersionedV2Document()
    {
        var root = Path.Combine(Path.GetTempPath(), $"poe2-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var writer = new ProjectOutputJsonWriter(
                root,
                () => new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.FromHours(8)));

            var legacyFile = writer.WriteSellQueryBatch(new SellQueryBatchResult
            {
                Mode = "exalted-to-divine",
                FinishedAt = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.FromHours(8)),
                Items =
                {
                    new SellQueryItemResult
                    {
                        CurrencyName = "混沌石",
                        BuyCurrencyName = "崇高石",
                        SellCurrencyName = "神聖石",
                        CurrentPairRatioNormalized = "675:1",
                        Status = "ok"
                    }
                }
            });

            var version2File = Path.ChangeExtension(legacyFile, null) + ".v2.json";
            using var document = JsonDocument.Parse(File.ReadAllText(version2File));

            Assert.Equal(PriceScanDocument.CurrentSchemaVersion, document.RootElement.GetProperty("schemaVersion").GetString());
            Assert.Equal("E", document.RootElement.GetProperty("pair").GetProperty("buyCurrency").GetString());
            Assert.Equal("D", document.RootElement.GetProperty("pair").GetProperty("sellCurrency").GetString());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
