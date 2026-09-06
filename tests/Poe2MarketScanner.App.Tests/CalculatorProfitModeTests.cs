using System;
using System.IO;
using System.Linq;
using Poe2MarketScanner.App.Services;
using PoeTradeAssistant.Contracts.MarketScan;
using Xunit;

namespace Poe2MarketScanner.App.Tests;

public sealed class CalculatorProfitModeTests
{
    [Theory]
    [InlineData("高买低卖", "+20.00%", "+2 C")]
    [InlineData("高买高卖", "+60.00%", "+6 C")]
    [InlineData("低买高卖", "+100.00%", "+8 C")]
    [InlineData("低买低卖", "+50.00%", "+4 C")]
    public void ModeChangeRecalculatesAndSurvivesReload(string mode, string roi, string profit)
    {
        var path = Path.Combine(Path.GetTempPath(), $"calculator-modes-{Guid.NewGuid():N}.json");
        try
        {
            var calculator = new NativeCalculatorViewModel(path);
            calculator.ImportScanDocument(Scan());
            calculator.ProfitMode = mode;
            Assert.Equal(roi, calculator.Targets[0].RoiText);
            Assert.Equal(profit, calculator.Targets[0].NetProfitText);
            calculator.Save();
            var restored = new NativeCalculatorViewModel(path);
            restored.Load();
            Assert.Equal(mode, restored.ProfitMode);
            Assert.Equal(roi, restored.Targets[0].RoiText);
            Assert.Equal("10", restored.Targets[0].HighestBuyPriceText);
            Assert.Equal("8", restored.Targets[0].LowestBuyPriceText);
            Assert.Equal("4", restored.Targets[0].HighestSellPriceText);
            Assert.Equal("3", restored.Targets[0].LowestSellPriceText);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ReimportSamePairRefreshesAllPricesAndMissingModePriceDoesNotCalculate()
    {
        var path = Path.Combine(Path.GetTempPath(), $"calculator-import-{Guid.NewGuid():N}.json");
        try
        {
            var calculator = new NativeCalculatorViewModel(path);
            calculator.ImportScanDocument(Scan());
            calculator.Targets[0].LowestBuyPriceText = "1";
            calculator.ImportScanDocument(Scan());
            calculator.ProfitMode = "低买高卖";
            Assert.Equal("+100.00%", calculator.Targets[0].RoiText);
            calculator.Targets[0].HighestSellPriceText = "";
            Assert.Equal("待计算", calculator.Targets[0].RoiText);
            calculator.Targets[0].HighestSellPriceText = "5";
            Assert.Equal("+150.00%", calculator.Targets[0].RoiText);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void VisiblePriceEditsStayWithTheirModeAndSelectedPair()
    {
        var path = Path.Combine(Path.GetTempPath(), $"calculator-selection-{Guid.NewGuid():N}.json");
        try
        {
            var calculator = new NativeCalculatorViewModel(path);
            calculator.ImportScanDocument(Scan());
            var firstPair = calculator.SelectedPair!;
            var target = calculator.Targets[0];
            Assert.Equal("10", target.ActiveBuyPriceText);
            Assert.Equal("3", target.ActiveSellPriceText);
            calculator.ProfitMode = "低买高卖";
            Assert.Equal("8", target.ActiveBuyPriceText);
            Assert.Equal("4", target.ActiveSellPriceText);
            target.ActiveBuyPriceText = "5";
            target.ActiveSellPriceText = "5";
            Assert.Equal("+300.00%", target.RoiText);
            calculator.ProfitMode = "高买低卖";
            Assert.Equal("10", target.ActiveBuyPriceText);
            Assert.Equal("3", target.ActiveSellPriceText);
            Assert.Equal("+20.00%", target.RoiText);

            var otherBuy = new CalculatorItemRow("other-buy", "E", CalculatorItemCategory.Currency, "0");
            var otherSell = new CalculatorItemRow("other-sell", "F", CalculatorItemCategory.Currency, "0");
            calculator.Items.Add(otherBuy);
            calculator.Items.Add(otherSell);
            var otherPair = new CalculatorPairRow { BaseItem = otherSell, QuoteItem = otherBuy, RateText = "2" };
            calculator.Pairs.Add(otherPair);
            calculator.SelectedPair = otherPair;
            Assert.Equal("", target.ActiveBuyPriceText);
            Assert.Equal("", target.ActiveSellPriceText);
            target.ActiveBuyPriceText = "2";
            target.ActiveSellPriceText = "3";
            Assert.Equal("+200.00%", target.RoiText);
            calculator.SelectedPair = firstPair;
            calculator.ProfitMode = "低买高卖";
            Assert.Equal("5", target.ActiveBuyPriceText);
            Assert.Equal("5", target.ActiveSellPriceText);
            Assert.Equal("+300.00%", target.RoiText);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CalculatesSortableRoiAndGoldEfficiencyValues()
    {
        var calculator = new NativeCalculatorViewModel(Path.Combine(Path.GetTempPath(), $"calculator-gold-{Guid.NewGuid():N}.json"));
        calculator.ImportScanDocument(Scan());
        calculator.Items.Single(item => item.Name == "测试标的").GoldCostText = "100";
        calculator.Items.Single(item => item.Name == "C").GoldCostText = "10";
        calculator.Items.Single(item => item.Name == "D").GoldCostText = "50";

        var target = calculator.Targets[0];
        Assert.Equal(20m, target.RoiValue);
        Assert.True(target.IsPositiveRoi);
        Assert.Equal(700m, target.GoldEfficiencyValue);
        Assert.Equal("700.00 金/D", target.GoldEfficiencyText);

        target.ActiveSellPriceText = "2";
        Assert.Equal(-20m, target.RoiValue);
        Assert.True(target.IsNegativeRoi);
        Assert.Null(target.GoldEfficiencyValue);
        Assert.Equal("--", target.GoldEfficiencyText);
    }

    private static PriceScanDocument Scan() => new()
    {
        Pair = new TradePairSnapshot
        {
            BuyCurrency = "C", SellCurrency = "D",
            CurrentRatio = new NormalizedRatio { RightPerLeft = 4m }
        },
        Items = new[]
        {
            new PriceScanItem
            {
                Name = "测试标的", Status = "ok", HighestBuyPrice = 10m,
                LowestBuyPrice = 8m, HighestSellPrice = 4m, LowestSellPrice = 3m
            }
        }
    };
}
