using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Poe2MarketScanner.App.Services;
using Poe2MarketScanner.Core.Automation;
using Poe2MarketScanner.Core.Configuration;
using Xunit;

namespace Poe2MarketScanner.App.Tests;

public sealed class SellQueryAutomationRunnerTests
{
    private const string ChaosBuyDivineSellMode = "\u6DF7\u6C8C\u77F3\u4E70\u795E\u5723\u77F3\u5356";
    private const string ChaosOrb = "\u6DF7\u6C8C\u77F3";
    private const string DivineOrb = "\u795E\u5723\u77F3";
    private const string TraditionalDivineOrb = "\u795E\u8056\u77F3";
    private const string ExaltedOrb = "\u5D07\u9AD8\u77F3";

    [Fact]
    public async Task RunAsync_ShouldUseUpdatedBuyAndSellSequence()
    {
        var profile = AppProfileFactory.CreateDefault();
        profile.SelectedTradeModeKey = ChaosBuyDivineSellMode;
        profile.QueryList.Items.Add(ExaltedOrb);

        var inputRunner = new FakeInputAutomationRunner();
        var runner = new SellQueryAutomationRunner(
            inputRunner,
            new SequencedFakeSellQueryOcrReader(),
            new FakeQueryResultWriter(),
            () => new DateTimeOffset(2026, 6, 25, 15, 0, 0, TimeSpan.FromHours(8)));

        var result = await runner.RunAsync(profile, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(ExaltedOrb, result.Items[0].CurrencyName);
        Assert.Equal(ChaosOrb, result.Items[0].BuyCurrencyName);
        Assert.Equal(DivineOrb, result.Items[0].SellCurrencyName);
        Assert.Equal("1:120", result.Items[0].CurrentPairRatioNormalized);
        Assert.Equal("250", result.Items[0].GoldCostNormalized);
        Assert.Equal("1:572", result.Items[0].BuyRatioNormalized);
        Assert.Equal("1:220", result.Items[0].SellRatioNormalized);

        Assert.Equal("Click(1290,410)", inputRunner.Steps[0]);
        var buyPrimeIndex = inputRunner.Steps.IndexOf($"Paste({ChaosOrb})");
        var pairSellPrimeIndex = inputRunner.Steps.IndexOf($"Paste({DivineOrb})");
        var pairQuantityIndex = inputRunner.Steps.IndexOf("Click(700,470)");
        var buyLoopIndex = inputRunner.Steps.IndexOf($"Paste({ExaltedOrb})", pairQuantityIndex + 1);
        var firstCtrlClickIndex = inputRunner.Steps.IndexOf("CtrlClick(620,410)");
        var buyRatioIdleIndex = inputRunner.Steps.IndexOf("Click(1500,950)", firstCtrlClickIndex + 1);
        var secondCtrlClickIndex = inputRunner.Steps.IndexOf("CtrlClick(620,410)", firstCtrlClickIndex + 1);
        var sellPrimeIndex = inputRunner.Steps.LastIndexOf($"Paste({DivineOrb})");
        var sellLoopIndex = inputRunner.Steps.LastIndexOf($"Paste({ExaltedOrb})");
        var sellQuantityIndex = inputRunner.Steps.LastIndexOf("Click(700,470)");

        Assert.True(buyPrimeIndex >= 0);
        Assert.True(pairSellPrimeIndex > buyPrimeIndex);
        Assert.True(pairQuantityIndex > pairSellPrimeIndex);
        Assert.True(buyLoopIndex > pairQuantityIndex);
        Assert.True(firstCtrlClickIndex > buyLoopIndex);
        Assert.True(buyRatioIdleIndex > firstCtrlClickIndex);
        Assert.True(secondCtrlClickIndex > buyRatioIdleIndex);
        Assert.True(sellPrimeIndex > secondCtrlClickIndex);
        Assert.True(sellLoopIndex > sellPrimeIndex);
        Assert.True(sellQuantityIndex > sellLoopIndex);
    }

    [Fact]
    public async Task RunAsync_ShouldContinueAfterSingleItemFailure()
    {
        var profile = AppProfileFactory.CreateDefault();
        profile.QueryList.Items.Add("Broken Item");
        profile.QueryList.Items.Add("Good Item");

        var runner = new SellQueryAutomationRunner(
            new FakeInputAutomationRunner(),
            new MixedFakeSellQueryOcrReader(),
            new FakeQueryResultWriter(),
            () => new DateTimeOffset(2026, 6, 25, 15, 5, 0, TimeSpan.FromHours(8)));

        var result = await runner.RunAsync(profile, CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal("parse_failed", result.Items[0].Status);
        Assert.Equal("ok", result.Items[1].Status);
    }

    [Fact]
    public async Task RunAsync_ShouldWaitForSuccessfulOcrBeforeMovingToNextPhase()
    {
        var profile = AppProfileFactory.CreateDefault();
        profile.QueryList.Items.Add(ExaltedOrb);

        var inputRunner = new FakeInputAutomationRunner();
        var retryingReader = new RetryingFakeSellQueryOcrReader();
        var runner = new SellQueryAutomationRunner(
            inputRunner,
            retryingReader,
            new FakeQueryResultWriter(),
            () => new DateTimeOffset(2026, 6, 25, 15, 10, 0, TimeSpan.FromHours(8)));

        var result = await runner.RunAsync(profile, CancellationToken.None);

        Assert.Equal("ok", result.Items[0].Status);
        Assert.Equal("1:220", result.Items[0].SellRatioNormalized);
        Assert.True(retryingReader.ReadCount >= 5);
        Assert.Contains("Wait(200)", inputRunner.Steps);
    }

    [Fact]
    public async Task RunAsync_ShouldPasteTraditionalTradeCurrencyNames_WhenEnabled()
    {
        var profile = AppProfileFactory.CreateDefault();
        profile.SelectedTradeModeKey = ChaosBuyDivineSellMode;
        profile.UseTraditionalChinese = true;
        profile.QueryList.Items.Add(ExaltedOrb);

        var inputRunner = new FakeInputAutomationRunner();
        var runner = new SellQueryAutomationRunner(
            inputRunner,
            new SequencedFakeSellQueryOcrReader(),
            new FakeQueryResultWriter(),
            () => new DateTimeOffset(2026, 6, 26, 10, 0, 0, TimeSpan.FromHours(8)));

        var result = await runner.RunAsync(profile, CancellationToken.None);

        Assert.Equal(TraditionalDivineOrb, result.Items[0].SellCurrencyName);
        Assert.Contains($"Paste({TraditionalDivineOrb})", inputRunner.Steps);
        Assert.DoesNotContain($"Paste({DivineOrb})", inputRunner.Steps);
    }

    [Fact]
    public async Task RunAsync_ShouldSkipGoldRecognition_WhenDisabled()
    {
        var profile = AppProfileFactory.CreateDefault();
        profile.Automation.RecognizeGoldCost = false;
        profile.QueryList.Items.Add(ExaltedOrb);

        var inputRunner = new FakeInputAutomationRunner();
        var reader = new GoldRecognitionDisabledSellQueryOcrReader();
        var runner = new SellQueryAutomationRunner(
            inputRunner,
            reader,
            new FakeQueryResultWriter(),
            () => new DateTimeOffset(2026, 6, 30, 10, 0, 0, TimeSpan.FromHours(8)));

        var result = await runner.RunAsync(profile, CancellationToken.None);

        Assert.Equal("ok", result.Items[0].Status);
        Assert.Equal(string.Empty, result.Items[0].GoldCostNormalized);
        Assert.Equal("1:572", result.Items[0].BuyRatioNormalized);
        Assert.Equal("1:220", result.Items[0].SellRatioNormalized);
        Assert.Equal(3, reader.ReadCount);
        Assert.DoesNotContain("Wait(200)", inputRunner.Steps);
    }
}

internal sealed class FakeInputAutomationRunner : IInputAutomationRunner
{
    public List<string> Steps { get; } = new();

    public Task ClickAsync(double x, double y, CancellationToken cancellationToken)
    {
        Steps.Add($"Click({Math.Round(x)},{Math.Round(y)})");
        return Task.CompletedTask;
    }

    public Task ClickWithModifiersAsync(double x, double y, IReadOnlyCollection<InputModifierKey> modifierKeys, CancellationToken cancellationToken)
    {
        var suffix = modifierKeys.Count == 0 ? "None" : string.Join("+", modifierKeys);
        Steps.Add($"ClickWithModifiers({Math.Round(x)},{Math.Round(y)},{suffix})");
        return Task.CompletedTask;
    }

    public Task CtrlClickAsync(double x, double y, CancellationToken cancellationToken)
    {
        Steps.Add($"CtrlClick({Math.Round(x)},{Math.Round(y)})");
        return Task.CompletedTask;
    }

    public Task SendSelectAllAsync(CancellationToken cancellationToken)
    {
        Steps.Add("SelectAll");
        return Task.CompletedTask;
    }

    public Task SendBackspaceAsync(CancellationToken cancellationToken)
    {
        Steps.Add("Backspace");
        return Task.CompletedTask;
    }

    public Task PasteTextAsync(string text, CancellationToken cancellationToken)
    {
        Steps.Add($"Paste({text})");
        return Task.CompletedTask;
    }

    public Task WaitAsync(int milliseconds, CancellationToken cancellationToken)
    {
        Steps.Add($"Wait({milliseconds})");
        return Task.CompletedTask;
    }
}

internal sealed class SequencedFakeSellQueryOcrReader : ISellQueryOcrReader
{
    private int _count;

    public SellQueryOcrResult Read(AppProfile profile)
    {
        _count++;
        return _count switch
        {
            1 => new SellQueryOcrResult
            {
                GoldCostRaw = string.Empty,
                GoldCostNormalized = string.Empty,
                RatioRaw = "1 : 120",
                RatioNormalized = "1:120",
                Status = "ok"
            },
            2 => new SellQueryOcrResult
            {
                GoldCostRaw = "250",
                GoldCostNormalized = "250",
                RatioRaw = string.Empty,
                RatioNormalized = string.Empty,
                Status = "ok"
            },
            3 => new SellQueryOcrResult
            {
                RatioRaw = "1 : 572",
                RatioNormalized = "1:572",
                Status = "ok"
            },
            _ => new SellQueryOcrResult
            {
                RatioRaw = "1 : 220",
                RatioNormalized = "1:220",
                Status = "ok"
            }
        };
    }
}

internal sealed class MixedFakeSellQueryOcrReader : ISellQueryOcrReader
{
    private int _count;

    public SellQueryOcrResult Read(AppProfile profile)
    {
        _count++;
        if (_count >= 2 && _count <= 9)
        {
            return new SellQueryOcrResult
            {
                GoldCostRaw = string.Empty,
                GoldCostNormalized = string.Empty,
                RatioRaw = string.Empty,
                RatioNormalized = string.Empty,
                Status = "parse_failed",
                ErrorMessage = "simulated OCR failure"
            };
        }

        return new SellQueryOcrResult
        {
            GoldCostRaw = "250",
            GoldCostNormalized = "250",
            RatioRaw = _count % 2 == 0 ? "1 : 572" : "1 : 220",
            RatioNormalized = _count % 2 == 0 ? "1:572" : "1:220",
            Status = "ok"
        };
    }
}

internal sealed class FakeQueryResultWriter : IQueryResultWriter
{
    public string WriteSellQueryBatch(SellQueryBatchResult result)
    {
        return @"K:\output\fake.json";
    }
}

internal sealed class RetryingFakeSellQueryOcrReader : ISellQueryOcrReader
{
    public int ReadCount { get; private set; }

    public SellQueryOcrResult Read(AppProfile profile)
    {
        ReadCount++;
        if (ReadCount == 1 || ReadCount == 3 || ReadCount == 5)
        {
            return new SellQueryOcrResult
            {
                GoldCostRaw = string.Empty,
                GoldCostNormalized = string.Empty,
                RatioRaw = string.Empty,
                RatioNormalized = string.Empty,
                Status = "parse_failed",
                ErrorMessage = "retry"
            };
        }

        return new SellQueryOcrResult
        {
            GoldCostRaw = "250",
            GoldCostNormalized = "250",
            RatioRaw = ReadCount switch
            {
                2 => "1 : 120",
                4 => "1 : 572",
                _ => "1 : 220"
            },
            RatioNormalized = ReadCount switch
            {
                2 => "1:120",
                4 => "1:572",
                _ => "1:220"
            },
            Status = "ok"
        };
    }
}

internal sealed class GoldRecognitionDisabledSellQueryOcrReader : ISellQueryOcrReader
{
    public int ReadCount { get; private set; }

    public SellQueryOcrResult Read(AppProfile profile)
    {
        ReadCount++;
        return ReadCount switch
        {
            1 => new SellQueryOcrResult
            {
                RatioRaw = "1 : 120",
                RatioNormalized = "1:120",
                Status = "ok"
            },
            2 => new SellQueryOcrResult
            {
                RatioRaw = "1 : 572",
                RatioNormalized = "1:572",
                Status = "ok"
            },
            3 => new SellQueryOcrResult
            {
                RatioRaw = "1 : 220",
                RatioNormalized = "1:220",
                Status = "ok"
            },
            _ => throw new InvalidOperationException("Gold recognition should be skipped when disabled.")
        };
    }
}


