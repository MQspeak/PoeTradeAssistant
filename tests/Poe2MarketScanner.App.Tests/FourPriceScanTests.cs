using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Poe2MarketScanner.App.Services;
using Poe2MarketScanner.Core.Configuration;
using PoeTradeAssistant.Contracts.MarketScan;
using Xunit;

namespace Poe2MarketScanner.App.Tests;

public sealed class FourPriceScanTests
{
    [Fact]
    public async Task ScanExportsExactComponentsAndCalculatorUsesAllFourModes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "four-price-" + Guid.NewGuid().ToString("N"));
        try
        {
            var input = new FakeInputAutomationRunner();
            var reader = new DirectionReader(input);
            var runner = new SellQueryAutomationRunner(input, reader, new ProjectOutputJsonWriter(directory));
            var result = await runner.RunAsync(Profile(), CancellationToken.None);
            var item = Assert.Single(result.Items);
            Assert.Equal(10m, item.HighestBuyPrice);
            Assert.Equal(8m, item.LowestBuyPrice);
            Assert.Equal(4m, item.HighestSellPrice);
            Assert.Equal(3m, item.LowestSellPrice);
            Assert.Equal(new[] { 0, 0, 1, 2, 3 }, reader.SwapsAtRead);
            Assert.Equal(4, input.Steps.Count(step => step.StartsWith("CtrlClick")));

            var v2 = result.OutputPath;
            var document = JsonSerializer.Deserialize<PriceScanDocument>(File.ReadAllText(v2))!;
            Assert.Equal(4, document.Items[0].PriceObservations.Count);
            Assert.All(document.Items[0].PriceObservations.Values, observation => Assert.Equal("ok", observation.Status));
            using var exported = JsonDocument.Parse(File.ReadAllText(result.OutputPath));
            Assert.Equal(10m, exported.RootElement.GetProperty("items")[0].GetProperty("highestBuyPrice").GetDecimal());
            var calculator = new NativeCalculatorViewModel(Path.Combine(directory, "workspace.json"));
            calculator.ImportScanDocument(document);
            foreach (var (mode, roi) in new[] { ("高买低卖", "+20.00%"), ("高买高卖", "+60.00%"), ("低买高卖", "+100.00%"), ("低买低卖", "+50.00%") })
            {
                calculator.ProfitMode = mode;
                Assert.Equal(roi, calculator.Targets[0].RoiText);
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task FailedSwappedReadRestoresSidesAndDoesNotFillMissingPriceFromLegacyRatio()
    {
        var directory = Path.Combine(Path.GetTempPath(), "four-price-failed-" + Guid.NewGuid().ToString("N"));
        try
        {
            var input = new FakeInputAutomationRunner();
            var runner = new SellQueryAutomationRunner(input, new DirectionReader(input, failLowBuy: true), new ProjectOutputJsonWriter(directory));
            var result = await runner.RunAsync(Profile(), CancellationToken.None);
            Assert.Null(result.Items[0].LowestBuyPrice);
            Assert.Equal("parse_failed", result.Items[0].PriceObservations["lowestBuyPrice"].Status);
            Assert.Equal(4, input.Steps.Count(step => step.StartsWith("CtrlClick")));
            Assert.Equal(3m, result.Items[0].LowestSellPrice);
            var calculator = new NativeCalculatorViewModel(Path.Combine(directory, "workspace.json"));
            var document = JsonSerializer.Deserialize<PriceScanDocument>(File.ReadAllText(result.OutputPath))!;
            calculator.ImportScanDocument(document);
            calculator.ProfitMode = "高买低卖";
            Assert.Equal("+20.00%", calculator.Targets[0].RoiText);
            calculator.ProfitMode = "低买低卖";
            Assert.Equal("", calculator.Targets[0].LowestBuyPriceText);
            Assert.Equal("待计算", calculator.Targets[0].RoiText);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CancellationAfterSwapRestoresSidesThenStops()
    {
        using var cancellation = new CancellationTokenSource();
        var input = new FakeInputAutomationRunner();
        var reader = new DirectionReader(input, cancel: cancellation);
        var runner = new SellQueryAutomationRunner(input, reader, new FakeQueryResultWriter());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(Profile(), cancellation.Token));
        Assert.Equal(2, input.Steps.Count(step => step.StartsWith("CtrlClick")));
    }

    private static AppProfile Profile()
    {
        var profile = AppProfileFactory.CreateDefault();
        profile.Automation.RecognizeGoldCost = false;
        profile.QueryList.Items.Add("Example");
        return profile;
    }

    private sealed class DirectionReader(FakeInputAutomationRunner input, bool failLowBuy = false, CancellationTokenSource? cancel = null) : ISellQueryOcrReader
    {
        public List<int> SwapsAtRead { get; } = new();
        public SellQueryOcrResult Read(AppProfile profile) => throw new InvalidOperationException();
        public SellQueryOcrResult Read(AppProfile profile, bool recognizeGoldCost, bool recognizeRatio)
        {
            Assert.False(recognizeGoldCost);
            Assert.True(recognizeRatio);
            var swaps = input.Steps.Count(step => step.StartsWith("CtrlClick"));
            SwapsAtRead.Add(swaps);
            if (swaps == 1 && cancel is not null) { cancel.Cancel(); cancel.Token.ThrowIfCancellationRequested(); }
            if (swaps == 1 && failLowBuy) return new SellQueryOcrResult { Status = "parse_failed", ErrorMessage = "test_failure" };
            var ratio = SwapsAtRead.Count == 1 ? "1:4" : swaps switch { 0 => "2:10", 1 => "8:3", 2 => "7:4", _ => "3:9" };
            return new SellQueryOcrResult { Status = "ok", RatioRaw = ratio, RatioNormalized = ratio };
        }
    }
}
