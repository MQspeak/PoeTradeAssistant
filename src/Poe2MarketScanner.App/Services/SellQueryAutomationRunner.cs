using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Poe2MarketScanner.Core.Automation;
using Poe2MarketScanner.Core.Configuration;
using Poe2MarketScanner.Core.Ocr;

namespace Poe2MarketScanner.App.Services;

public sealed class SellQueryAutomationRunner
{
    private const int MaxOcrAttempts = 8;

    private readonly IInputAutomationRunner _inputRunner;
    private readonly ISellQueryOcrReader _ocrReader;
    private readonly IQueryResultWriter _resultWriter;
    private readonly Func<DateTimeOffset> _now;

    public SellQueryAutomationRunner(
        IInputAutomationRunner inputRunner,
        ISellQueryOcrReader ocrReader,
        IQueryResultWriter resultWriter,
        Func<DateTimeOffset>? now = null)
    {
        _inputRunner = inputRunner;
        _ocrReader = ocrReader;
        _resultWriter = resultWriter;
        _now = now ?? (() => DateTimeOffset.Now);
    }

    public async Task<SellQueryBatchResult> RunAsync(AppProfile profile, CancellationToken cancellationToken,
        ScanGoldCostTable? goldCosts = null)
    {
        goldCosts ??= new ScanGoldCostTable();
        var disabledItems = profile.QueryList.DisabledItems.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var queryItems = profile.QueryList.Items
            .Where(item => !string.IsNullOrWhiteSpace(item) && !disabledItems.Contains(item))
            .ToList();

        if (queryItems.Count == 0)
        {
            throw new InvalidOperationException("No query items are available for automation.");
        }

        var baseMode = TradeModeCatalog.Resolve(profile.SelectedTradeModeKey);
        var mode = CreateEffectiveMode(baseMode, profile.UseTraditionalChinese);
        var result = new SellQueryBatchResult
        {
            Mode = mode.Key,
            StartedAt = _now()
        };

        foreach (var currencyName in queryItems)
        {
            result.Items.Add(new SellQueryItemResult
            {
                CurrencyName = currencyName,
                BuyCurrencyName = mode.BuyCurrencyName,
                SellCurrencyName = mode.SellCurrencyName,
                Status = "pending",
                CapturedAt = _now()
            });
        }

        if (!profile.Automation.SkipCurrentPairRatioRecognition)
        {
            await CaptureCurrentPairRatioAsync(profile, mode, result, cancellationToken);
        }
        foreach (var item in result.Items)
            foreach (var key in new[] { "highestBuyPrice", "lowestBuyPrice", "highestSellPrice", "lowestSellPrice" })
                item.PriceObservations[key] = new ScanPriceObservation();
        await PrimeBuySideAsync(profile, mode, cancellationToken);
        foreach (var item in result.Items)
        {
            await CaptureBuyAsync(profile, item, goldCosts, cancellationToken);
        }

        await PrimeSellSideAsync(profile, mode, cancellationToken);
        foreach (var item in result.Items)
        {
            await CaptureSellAsync(profile, item, cancellationToken);
        }

        result.FinishedAt = _now();
        result.OutputPath = _resultWriter.WriteSellQueryBatch(result);
        return result;
    }

    private async Task CaptureCurrentPairRatioAsync(
        AppProfile profile,
        TradeModeDefinition mode,
        SellQueryBatchResult result,
        CancellationToken cancellationToken)
    {
        await PrimeCurrencySideAsync(profile, "rightCurrency", mode.BuyCurrencyName, cancellationToken);
        await PrimeCurrencySideAsync(profile, "leftCurrency", mode.SellCurrencyName, cancellationToken);
        await SetQuantityAsync(profile, "leftInput", cancellationToken);

        var readResult = await ReadUntilStableAsync(profile, cancellationToken);
        foreach (var item in result.Items)
        {
            item.CurrentPairRatioRaw = readResult.RatioRaw;
            item.CurrentPairRatioNormalized = readResult.RatioNormalized;
            if (!string.Equals(readResult.Status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                item.Status = readResult.Status;
                item.ErrorMessage = readResult.ErrorMessage;
            }
        }
    }

    private Task PrimeBuySideAsync(AppProfile profile, TradeModeDefinition mode, CancellationToken cancellationToken)
    {
        return PrimeCurrencySideAsync(profile, "rightCurrency", mode.BuyCurrencyName, cancellationToken);
    }

    private Task PrimeSellSideAsync(AppProfile profile, TradeModeDefinition mode, CancellationToken cancellationToken)
    {
        return PrimeCurrencySideAsync(profile, "rightCurrency", mode.SellCurrencyName, cancellationToken);
    }

    private async Task PrimeCurrencySideAsync(
        AppProfile profile,
        string currencyAnchorKey,
        string currencyName,
        CancellationToken cancellationToken)
    {
        await ClickAnchorAsync(profile, currencyAnchorKey, cancellationToken);
        await WaitAfterActionAsync(profile.Automation.ClickDelayMs, profile, cancellationToken);
        await ClickAnchorAsync(profile, "allTab", cancellationToken);
        await WaitAfterActionAsync(profile.Automation.ClickDelayMs, profile, cancellationToken);
        await EnterSearchTextAsync(profile, currencyName, cancellationToken);
        await ClickAnchorAsync(profile, "searchTarget", cancellationToken);
        await WaitAfterActionAsync(profile.Automation.ClickDelayMs * 2, profile, cancellationToken);
    }

    private async Task CaptureBuyAsync(AppProfile profile, SellQueryItemResult item, ScanGoldCostTable goldCosts, CancellationToken cancellationToken)
    {
        try
        {
            await SearchCurrencyAsync(profile, "leftCurrency", item.CurrencyName, cancellationToken);
            await SetQuantityAsync(profile, "leftInput", cancellationToken);
            SellQueryOcrResult goldReadResult;
            if (goldCosts.TryGet(item.CurrencyName, out var recordedGoldCost))
            {
                goldReadResult = new SellQueryOcrResult { Status = "ok" };
                item.GoldCostNormalized = recordedGoldCost;
            }
            else if (profile.Automation.RecognizeGoldCost)
            {
                goldReadResult = await ReadUntilStableAsync(profile, cancellationToken, recognizeGoldCost: true);
                item.GoldCostRaw = goldReadResult.GoldCostRaw;
                item.GoldCostNormalized = goldReadResult.GoldCostNormalized;
                if (string.Equals(goldReadResult.Status, "ok", StringComparison.OrdinalIgnoreCase))
                    goldCosts.Record(item.CurrencyName, item.GoldCostNormalized);
            }
            else
            {
                goldReadResult = new SellQueryOcrResult
                {
                    Status = "ok"
                };
                item.GoldCostRaw = string.Empty;
                item.GoldCostNormalized = string.Empty;
            }

            var highestRead = await ReadUntilStableAsync(profile, cancellationToken);
            item.HighestBuyPrice = RecordPrice(item, "highestBuyPrice", highestRead, takeLeft: false);
            var ratioReadResult = await ReadSwappedRatioAsync(profile, cancellationToken);
            item.LowestBuyPrice = RecordPrice(item, "lowestBuyPrice", ratioReadResult, takeLeft: true);

            item.BuyRatioRaw = ratioReadResult.RatioRaw;
            item.BuyRatioNormalized = ratioReadResult.RatioNormalized;
            item.CapturedAt = _now();
            item.Status = string.Equals(goldReadResult.Status, "ok", StringComparison.OrdinalIgnoreCase) &&
                item.HighestBuyPrice.HasValue && item.LowestBuyPrice.HasValue
                ? "ok"
                : !string.Equals(goldReadResult.Status, "ok", StringComparison.OrdinalIgnoreCase)
                    ? goldReadResult.Status
                    : "parse_failed";
            item.ErrorMessage = !string.IsNullOrWhiteSpace(goldReadResult.ErrorMessage)
                ? goldReadResult.ErrorMessage
                : highestRead.ErrorMessage ?? ratioReadResult.ErrorMessage;
        }
        catch (Exception exception) when (exception is not OperationCanceledException && exception is not CurrencySwapException)
        {
            item.Status = "failed";
            item.ErrorMessage = exception.Message;
            item.CapturedAt = _now();
        }
    }

    private async Task CaptureSellAsync(AppProfile profile, SellQueryItemResult item, CancellationToken cancellationToken)
    {
        try
        {
            await SearchCurrencyAsync(profile, "leftCurrency", item.CurrencyName, cancellationToken);
            await SetQuantityAsync(profile, "leftInput", cancellationToken);
            var readResult = await ReadUntilStableAsync(profile, cancellationToken);
            item.HighestSellPrice = RecordPrice(item, "highestSellPrice", readResult, takeLeft: false, calculateUnitPrice: true);
            var lowestRead = await ReadSwappedRatioAsync(profile, cancellationToken);
            item.LowestSellPrice = RecordPrice(item, "lowestSellPrice", lowestRead, takeLeft: true, calculateUnitPrice: true);

            item.SellRatioRaw = readResult.RatioRaw;
            item.SellRatioNormalized = readResult.RatioNormalized;
            item.CapturedAt = _now();

            if (!item.HighestSellPrice.HasValue || !item.LowestSellPrice.HasValue)
            {
                item.Status = "parse_failed";
                item.ErrorMessage = readResult.ErrorMessage ?? lowestRead.ErrorMessage;
                return;
            }

            if (!string.Equals(item.Status, "ok", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(item.Status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            item.Status = "ok";
            item.ErrorMessage = null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException && exception is not CurrencySwapException)
        {
            item.Status = "failed";
            item.ErrorMessage = exception.Message;
            item.CapturedAt = _now();
        }
    }

    private static decimal? RecordPrice(
        SellQueryItemResult item,
        string key,
        SellQueryOcrResult read,
        bool takeLeft,
        bool calculateUnitPrice = false)
    {
        var parsed = OcrTextParser.ParseRatio(read.RatioNormalized);
        var success = read.Status == "ok" && parsed.Success && parsed.RatioLeft > 0 && parsed.RatioRight > 0;
        item.PriceObservations[key] = new ScanPriceObservation
        {
            Raw = read.RatioRaw,
            Normalized = read.RatioNormalized,
            Status = success ? "ok" : read.Status == "ok" ? "parse_failed" : read.Status,
            ErrorMessage = success ? null : read.ErrorMessage ?? "invalid_price_ratio"
        };
        if (!success)
        {
            return null;
        }

        if (calculateUnitPrice)
        {
            // Normal: target M is on the left and sell currency N is on the right => N / M.
            // Swapped: sell currency M is on the left and target N is on the right => M / N.
            return takeLeft
                ? parsed.RatioLeft / parsed.RatioRight
                : parsed.RatioRight / parsed.RatioLeft;
        }

        return takeLeft ? parsed.RatioLeft : parsed.RatioRight;
    }

    private async Task<SellQueryOcrResult> ReadSwappedRatioAsync(AppProfile profile, CancellationToken cancellationToken)
    {
        await SwapCurrenciesAsync(profile, cancellationToken);
        try
        {
            await WaitAfterActionAsync(profile.Automation.ClickDelayMs, profile, cancellationToken);
            await ClickAnchorAsync(profile, "idle", cancellationToken);
            await WaitAfterActionAsync(profile.Automation.ClickDelayMs, profile, cancellationToken);
            return await ReadUntilStableAsync(profile, cancellationToken);
        }
        finally
        {
            // A completed swap is always undone, even when OCR fails or the user stops.
            await SwapCurrenciesAsync(profile, CancellationToken.None);
            await WaitAfterActionAsync(profile.Automation.ClickDelayMs, profile, CancellationToken.None);
        }
    }

    private async Task SwapCurrenciesAsync(AppProfile profile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // Do not interrupt a key-down/mouse-click/key-up sequence halfway through.
            await CtrlClickAnchorAsync(profile, "leftCurrency", CancellationToken.None);
        }
        catch (Exception exception)
        {
            throw new CurrencySwapException("通货对调操作失败，已停止扫描，请检查两侧通货位置。", exception);
        }
    }

    private sealed class CurrencySwapException : Exception
    {
        public CurrencySwapException(string message, Exception inner) : base(message, inner) { }
    }

    private async Task SearchCurrencyAsync(
        AppProfile profile,
        string currencyAnchorKey,
        string currencyName,
        CancellationToken cancellationToken)
    {
        await ClickAnchorAsync(profile, currencyAnchorKey, cancellationToken);
        await WaitAfterActionAsync(profile.Automation.ClickDelayMs, profile, cancellationToken);
        await ClickAnchorAsync(profile, "allTab", cancellationToken);
        await WaitAfterActionAsync(profile.Automation.ClickDelayMs, profile, cancellationToken);
        await EnterSearchTextAsync(profile, currencyName, cancellationToken);
        await ClickAnchorAsync(profile, "searchTarget", cancellationToken);
        await WaitAfterActionAsync(profile.Automation.ClickDelayMs * 2, profile, cancellationToken);
    }

    private async Task EnterSearchTextAsync(AppProfile profile, string text, CancellationToken cancellationToken)
    {
        await ClickAnchorAsync(profile, "search", cancellationToken);
        await WaitAfterActionAsync(profile.Automation.InputDelayMs, profile, cancellationToken);
        await _inputRunner.SendSelectAllAsync(cancellationToken);
        await WaitAfterActionAsync(profile.Automation.InputDelayMs, profile, cancellationToken);
        await _inputRunner.SendBackspaceAsync(cancellationToken);
        await WaitAfterActionAsync(profile.Automation.InputDelayMs, profile, cancellationToken);
        await _inputRunner.PasteTextAsync(text, cancellationToken);
        await WaitAfterActionAsync(profile.Automation.InputDelayMs, profile, cancellationToken);
    }

    private async Task SetQuantityAsync(AppProfile profile, string inputAnchorKey, CancellationToken cancellationToken)
    {
        await ClickAnchorAsync(profile, inputAnchorKey, cancellationToken);
        await WaitAfterActionAsync(profile.Automation.ClickDelayMs, profile, cancellationToken);
        await _inputRunner.SendSelectAllAsync(cancellationToken);
        await WaitAfterActionAsync(profile.Automation.InputDelayMs, profile, cancellationToken);
        await _inputRunner.SendBackspaceAsync(cancellationToken);
        await WaitAfterActionAsync(profile.Automation.InputDelayMs, profile, cancellationToken);
        await _inputRunner.PasteTextAsync("1", cancellationToken);
        await WaitAfterActionAsync(profile.Automation.InputDelayMs, profile, cancellationToken);
        await ClickAnchorAsync(profile, "idle", cancellationToken);
        await WaitAfterActionAsync(profile.Automation.ClickDelayMs, profile, cancellationToken);
    }

    private async Task<SellQueryOcrResult> ReadUntilStableAsync(AppProfile profile, CancellationToken cancellationToken,
        bool recognizeGoldCost = false)
    {
        SellQueryOcrResult? lastResult = null;

        for (var attempt = 0; attempt < MaxOcrAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                lastResult = _ocrReader.Read(profile, recognizeGoldCost, recognizeRatio: !recognizeGoldCost);
            }
            catch (Exception exception)
            {
                lastResult = new SellQueryOcrResult
                {
                    Status = "read_failed",
                    ErrorMessage = exception.Message
                };
            }

            if (string.Equals(lastResult.Status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return lastResult;
            }

            await _inputRunner.WaitAsync(Math.Max(200, profile.Automation.InputDelayMs), cancellationToken);
        }

        if (lastResult is not null)
        {
            return new SellQueryOcrResult
            {
                GoldCostRaw = lastResult.GoldCostRaw,
                GoldCostNormalized = lastResult.GoldCostNormalized,
                RatioRaw = lastResult.RatioRaw,
                RatioNormalized = lastResult.RatioNormalized,
                Status = lastResult.Status,
                ErrorMessage = string.IsNullOrWhiteSpace(lastResult.ErrorMessage)
                    ? "ocr_timeout"
                    : $"{lastResult.ErrorMessage}; timeout=ocr"
            };
        }

        return new SellQueryOcrResult
        {
            Status = "failed",
            ErrorMessage = "ocr_timeout"
        };
    }

    private Task WaitAfterActionAsync(int operationDelayMs, AppProfile profile, CancellationToken cancellationToken)
    {
        var totalDelay = Math.Max(0, profile.Automation.CommonDelayMs) + Math.Max(0, operationDelayMs);
        return _inputRunner.WaitAsync(totalDelay, cancellationToken);
    }

    private Task ClickAnchorAsync(AppProfile profile, string anchorKey, CancellationToken cancellationToken)
    {
        if (!profile.Anchors.TryGetValue(anchorKey, out var anchor))
        {
            throw new InvalidOperationException($"Missing anchor configuration: {anchorKey}");
        }

        return _inputRunner.ClickAsync(anchor.X, anchor.Y, cancellationToken);
    }

    private Task CtrlClickAnchorAsync(AppProfile profile, string anchorKey, CancellationToken cancellationToken)
    {
        if (!profile.Anchors.TryGetValue(anchorKey, out var anchor))
        {
            throw new InvalidOperationException($"Missing anchor configuration: {anchorKey}");
        }

        return _inputRunner.CtrlClickAsync(anchor.X, anchor.Y, cancellationToken);
    }

    private static TradeModeDefinition CreateEffectiveMode(TradeModeDefinition mode, bool useTraditionalChinese)
    {
        if (!useTraditionalChinese)
        {
            return mode;
        }

        return new TradeModeDefinition
        {
            Key = mode.Key,
            DisplayName = TradeModeCatalog.LocalizeTradeModeDisplayName(mode, true),
            BuyCurrencyName = TradeModeCatalog.LocalizeCurrencyName(mode.BuyCurrencyName, true),
            SellCurrencyName = TradeModeCatalog.LocalizeCurrencyName(mode.SellCurrencyName, true)
        };
    }
}
