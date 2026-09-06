using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Data;
using PoeTradeAssistant.Contracts.MarketScan;
using PoeTradeAssistant.ScannerIntegration;

namespace Poe2MarketScanner.App.Services;

/// <summary>Pure WPF calculator state. Item names are maintained once, then selected from lists everywhere else.</summary>
public sealed class NativeCalculatorViewModel : INotifyPropertyChanged
{
    private const string WorkspaceSchemaVersion = "poe-trade-assistant/calculator-workspace/v2";
    private readonly string _workspacePath;
    private readonly ListCollectionView _currencyItems;
    private readonly ListCollectionView _targetItems;
    private CalculatorPairRow? _selectedPair;
    private string _profitMode = "高买低卖";
    public IReadOnlyList<string> ProfitModes { get; } = new[] { "高买低卖", "高买高卖", "低买高卖", "低买低卖" };
    public string ProfitMode
    {
        get => _profitMode;
        set { if (!ProfitModes.Contains(value)) return; SetProperty(ref _profitMode, value); RefreshCalculations(); }
    }
    private string _statusMessage = "先在物品列表维护币种和标的物；交易对、套利标的均通过下拉选择。";

    public NativeCalculatorViewModel(string? workspacePath = null)
    {
        _workspacePath = workspacePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PoeTradeAssistant", "calculator-workspace.json");
        _currencyItems = new ListCollectionView(Items) { Filter = item => item is CalculatorItemRow { Category: CalculatorItemCategory.Currency } };
        _targetItems = new ListCollectionView(Items) { Filter = item => item is CalculatorItemRow { Category: CalculatorItemCategory.Target } };
        Items.CollectionChanged += Items_CollectionChanged;
        Pairs.CollectionChanged += (_, _) => RefreshCalculations();
        Targets.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null) foreach (CalculatorTargetRow row in e.OldItems) row.PropertyChanged -= TargetPriceChanged;
            if (e.NewItems is not null) foreach (CalculatorTargetRow row in e.NewItems) row.PropertyChanged += TargetPriceChanged;
            RefreshCalculations();
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<CalculatorItemRow> Items { get; } = new();
    public ObservableCollection<CalculatorPairRow> Pairs { get; } = new();
    public ObservableCollection<CalculatorTargetRow> Targets { get; } = new();
    public ICollectionView CurrencyItems => _currencyItems;
    public ICollectionView TargetItems => _targetItems;

    public CalculatorPairRow? SelectedPair
    {
        get => _selectedPair;
        set
        {
            if (ReferenceEquals(_selectedPair, value)) return;
            CacheVisiblePrices(_selectedPair);
            _selectedPair = value;
            LoadVisiblePrices(_selectedPair);
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedPairSummary));
            RefreshCalculations();
        }
    }

    public string SelectedPairSummary => SelectedPair?.IsComplete == true
        ? $"当前交易对：用 {SelectedPair.QuoteItem!.Name} 买入、用 {SelectedPair.BaseItem!.Name} 卖出；1 个 {SelectedPair.BaseItem.Name} = {SelectedPair.RateText} 个 {SelectedPair.QuoteItem.Name}。价格会按币种保存，切换交易对后自动恢复。"
        : "请选择完整基础交易对。买入和卖出币种必须来自“物品列表”中的币种。";

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public void Load()
    {
        if (!File.Exists(_workspacePath)) return;
        try
        {
            var document = JsonSerializer.Deserialize<CalculatorWorkspaceDocument>(File.ReadAllText(_workspacePath));
            if (document?.SchemaVersion == WorkspaceSchemaVersion)
            {
                ApplyWorkspace(document);
                StatusMessage = "已加载原生计算器工作区。";
                return;
            }

            var legacyDocument = JsonSerializer.Deserialize<LegacyCalculatorWorkspaceDocument>(File.ReadAllText(_workspacePath));
            if (legacyDocument?.SchemaVersion == "poe-trade-assistant/calculator-workspace/v1")
            {
                MigrateLegacyWorkspace(legacyDocument);
                Save();
                StatusMessage = "已将旧版计算器工作区迁移为下拉关联结构。";
                return;
            }

            StatusMessage = "未识别的计算器工作区版本；请重新导入扫描结果。";
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            StatusMessage = $"读取计算器工作区失败：{exception.Message}";
        }
    }

    public void Save()
    {
        CreateSaveAction()();
        StatusMessage = $"工作区已保存：{_workspacePath}";
    }

    // Capture UI-bound collections before moving file IO to a worker thread.
    public Action CreateSaveAction()
    {
        CacheVisiblePrices(SelectedPair);
        var document = new CalculatorWorkspaceDocument
        {
            SchemaVersion = WorkspaceSchemaVersion,
            SelectedPairKey = SelectedPair?.Key,
            ProfitMode = ProfitMode,
            Items = Items.Select(item => new CalculatorItemDocument(item.Id, item.Name, item.Category, item.GoldCostText)).ToArray(),
            Pairs = Pairs.Select(pair => new CalculatorPairDocument(pair.BaseItem?.Id, pair.QuoteItem?.Id, pair.RateText)).ToArray(),
            Targets = Targets.Select(target => new CalculatorTargetDocument(target.TargetItem?.Id, target.PricesByCurrencyId)).ToArray()
        };
        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        return () => Poe2MarketScanner.Core.Configuration.AtomicFile.WriteAllText(_workspacePath, json);
    }

    public void AddCurrency() => Items.Add(CreateItem(CalculatorItemCategory.Currency));
    public void AddTargetItem() => Items.Add(CreateItem(CalculatorItemCategory.Target));
    public void AddPair()
    {
        var currencies = Items.Where(item => item.Category == CalculatorItemCategory.Currency).ToArray();
        var pair = new CalculatorPairRow { BaseItem = currencies.FirstOrDefault(), QuoteItem = currencies.Skip(1).FirstOrDefault() };
        Pairs.Add(pair);
        SelectedPair ??= pair;
    }
    public void AddTarget() => Targets.Add(new CalculatorTargetRow { TargetItem = Items.FirstOrDefault(item => item.Category == CalculatorItemCategory.Target) });

    public void RemoveItem(CalculatorItemRow? item)
    {
        if (item is null) return;
        foreach (var pair in Pairs.Where(pair => ReferenceEquals(pair.BaseItem, item) || ReferenceEquals(pair.QuoteItem, item)).ToArray()) RemovePair(pair);
        foreach (var target in Targets.Where(target => ReferenceEquals(target.TargetItem, item)).ToArray()) Targets.Remove(target);
        Items.Remove(item);
    }
    public void RemovePair(CalculatorPairRow? row)
    {
        if (row is null) return;
        var wasSelected = ReferenceEquals(SelectedPair, row);
        Pairs.Remove(row);
        if (wasSelected) SelectedPair = Pairs.FirstOrDefault();
    }
    public void RemoveTarget(CalculatorTargetRow? row) { if (row is not null) Targets.Remove(row); }

    public string ImportScanDocument(PriceScanDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion != PriceScanDocument.CurrentSchemaVersion) throw new InvalidOperationException("仅支持 poe-trade-scan/v2 扫描结果。");
        var buyCurrency = PriceScanDocumentAdapter.NormalizeCurrency(document.Pair.BuyCurrency);
        var sellCurrency = PriceScanDocumentAdapter.NormalizeCurrency(document.Pair.SellCurrency);
        var pairRate = document.Pair.CurrentRatio.RightPerLeft;
        if (string.IsNullOrWhiteSpace(buyCurrency) || string.IsNullOrWhiteSpace(sellCurrency) || pairRate is null or <= 0) throw new InvalidOperationException("V2 扫描结果缺少有效交易对或当前比例。");

        var buyItem = EnsureItem(buyCurrency, CalculatorItemCategory.Currency, "0");
        var sellItem = EnsureItem(sellCurrency, CalculatorItemCategory.Currency, "0");
        var pair = Pairs.FirstOrDefault(row => ReferenceEquals(row.BaseItem, sellItem) && ReferenceEquals(row.QuoteItem, buyItem));
        if (pair is null)
        {
            pair = new CalculatorPairRow { BaseItem = sellItem, QuoteItem = buyItem };
            Pairs.Add(pair);
        }
        pair.RateText = FormatDecimal(pairRate.Value);

        CacheVisiblePrices(SelectedPair);
        var importedCount = 0;
        foreach (var item in document.Items.Where(item => item.Status is "ok" or "imported-v1" || item.PriceObservations.Count > 0))
        {
            if (string.IsNullOrWhiteSpace(item.Name)) continue;
            var targetItem = EnsureItem(item.Name.Trim(), CalculatorItemCategory.Target, item.GoldCost);
            if (!string.IsNullOrWhiteSpace(item.GoldCost)) targetItem.GoldCostText = item.GoldCost;
            var target = Targets.FirstOrDefault(row => ReferenceEquals(row.TargetItem, targetItem));
            if (target is null)
            {
                target = new CalculatorTargetRow { TargetItem = targetItem };
                Targets.Add(target);
            }
            var hasFourPriceScan = item.PriceObservations.Count > 0;
            target.SetPrice(buyItem.Id, hasFourPriceScan ? string.Empty : FormatBuyPrice(item.BuyRatio));
            target.SetPrice(sellItem.Id, hasFourPriceScan ? string.Empty : FormatSellPrice(item.SellRatio));
            target.SetPriceBounds(buyItem.Id, item.HighestBuyPrice, item.LowestBuyPrice, true);
            target.SetPriceBounds(sellItem.Id, item.HighestSellPrice, item.LowestSellPrice, false);
            importedCount++;
        }

        _selectedPair = pair;
        LoadVisiblePrices(pair);
        OnPropertyChanged(nameof(SelectedPair));
        RefreshCalculations();
        Save();
        StatusMessage = $"已导入 {importedCount} 条扫描价格；已恢复为下拉可选的币种、交易对和标的物。";
        return StatusMessage;
    }

    private void TargetPriceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CalculatorTargetRow.HighestBuyPriceText) or nameof(CalculatorTargetRow.LowestBuyPriceText)
            or nameof(CalculatorTargetRow.HighestSellPriceText) or nameof(CalculatorTargetRow.LowestSellPriceText)
            or nameof(CalculatorTargetRow.BuyPriceText) or nameof(CalculatorTargetRow.SellPriceText) or nameof(CalculatorTargetRow.TargetItem)) RefreshCalculations();
    }

    public void RefreshCalculations()
    {
        foreach (var target in Targets) target.SetProfitMode(ProfitMode);
        var pair = SelectedPair;
        if (pair?.IsComplete != true || !TryPositive(pair.RateText, out var sellToBuyRate))
        {
            foreach (var target in Targets) target.SetResult("待计算", "--", "--", "请先选择有效基础交易对。", false);
            OnPropertyChanged(nameof(SelectedPairSummary));
            return;
        }

        foreach (var target in Targets)
        {
            if (target.TargetItem is null)
            {
                target.SetResult("待计算", "--", "--", "请选择标的物。", false);
                continue;
            }
            var selectedBuy = target.ActiveBuyPriceText;
            var selectedSell = target.ActiveSellPriceText;
            if (!TryPositive(selectedBuy, out var buyPrice) || !TryTradeValue(selectedSell, out var sellPrice))
            {
                target.SetResult("待计算", "--", "--", $"请填写{ProfitMode}模式对应的有效买入价和卖出价。", false);
                continue;
            }

            var revenueInBuy = sellPrice * sellToBuyRate;
            var profitInBuy = revenueInBuy - buyPrice;
            var roi = profitInBuy / buyPrice * 100m;
            var gold = ParseNonNegative(target.TargetItem.GoldCostText) + sellPrice * ParseNonNegative(pair.BaseItem!.GoldCostText) + buyPrice * ParseNonNegative(pair.QuoteItem!.GoldCostText);
            var profitable = profitInBuy > 0;
            var goldEfficiency = profitable ? gold / (profitInBuy / sellToBuyRate) : (decimal?)null;
            var efficiency = goldEfficiency is { } value ? $"每赚 1 {pair.BaseItem.Name} 约消耗 {value.ToString("0.00", CultureInfo.InvariantCulture)} 金" : "当前净收益不为正，无法计算金币转化率。";
            target.SetResult($"{(roi >= 0 ? "+" : string.Empty)}{roi.ToString("0.00", CultureInfo.InvariantCulture)}%", $"{(profitInBuy >= 0 ? "+" : string.Empty)}{FormatDecimal(profitInBuy)} {pair.QuoteItem.Name}", $"{FormatDecimal(gold)} 金", efficiency, profitable, roi, goldEfficiency, pair.BaseItem.Name);
        }
        OnPropertyChanged(nameof(SelectedPairSummary));
    }

    private void ApplyWorkspace(CalculatorWorkspaceDocument document)
    {
        ProfitMode = document.ProfitMode ?? "高买低卖";
        Items.Clear(); Pairs.Clear(); Targets.Clear();
        foreach (var item in document.Items ?? Array.Empty<CalculatorItemDocument>()) Items.Add(new CalculatorItemRow(item.Id, item.Name, item.Category, item.GoldCost));
        var itemById = Items.ToDictionary(item => item.Id);
        foreach (var pair in document.Pairs ?? Array.Empty<CalculatorPairDocument>())
        {
            itemById.TryGetValue(pair.BaseItemId ?? string.Empty, out var baseItem);
            itemById.TryGetValue(pair.QuoteItemId ?? string.Empty, out var quoteItem);
            Pairs.Add(new CalculatorPairRow { BaseItem = baseItem, QuoteItem = quoteItem, RateText = pair.Rate });
        }
        foreach (var target in document.Targets ?? Array.Empty<CalculatorTargetDocument>())
        {
            itemById.TryGetValue(target.TargetItemId ?? string.Empty, out var targetItem);
            Targets.Add(new CalculatorTargetRow(target.PricesByCurrencyId) { TargetItem = targetItem });
        }
        SelectedPair = Pairs.FirstOrDefault(pair => pair.Key == document.SelectedPairKey) ?? Pairs.FirstOrDefault();
        RefreshItemViews();
        RefreshCalculations();
    }

    private void MigrateLegacyWorkspace(LegacyCalculatorWorkspaceDocument legacy)
    {
        Items.Clear(); Pairs.Clear(); Targets.Clear();
        foreach (var currency in legacy.Currencies ?? Array.Empty<LegacyCalculatorCurrencyDocument>())
        {
            EnsureItem(currency.Name, CalculatorItemCategory.Currency, currency.GoldCost);
        }

        foreach (var pair in legacy.Pairs ?? Array.Empty<LegacyCalculatorPairDocument>())
        {
            var baseItem = Items.FirstOrDefault(item => item.Category == CalculatorItemCategory.Currency && string.Equals(item.Name, pair.BaseCurrency, StringComparison.OrdinalIgnoreCase));
            var quoteItem = Items.FirstOrDefault(item => item.Category == CalculatorItemCategory.Currency && string.Equals(item.Name, pair.QuoteCurrency, StringComparison.OrdinalIgnoreCase));
            Pairs.Add(new CalculatorPairRow { BaseItem = baseItem, QuoteItem = quoteItem, RateText = pair.Rate });
        }

        var selectedPair = Pairs.FirstOrDefault(pair => string.Equals($"{pair.BaseItem?.Name}|{pair.QuoteItem?.Name}", legacy.SelectedPairKey, StringComparison.OrdinalIgnoreCase)) ?? Pairs.FirstOrDefault();
        foreach (var target in legacy.Targets ?? Array.Empty<LegacyCalculatorTargetDocument>())
        {
            var item = EnsureItem(target.Name, CalculatorItemCategory.Target, target.GoldCost);
            var row = new CalculatorTargetRow { TargetItem = item };
            if (selectedPair?.IsComplete == true)
            {
                row.SetPrice(selectedPair.QuoteItem!.Id, target.BuyPrice);
                row.SetPrice(selectedPair.BaseItem!.Id, target.SellPrice);
            }
            Targets.Add(row);
        }

        SelectedPair = selectedPair;
        RefreshItemViews();
        RefreshCalculations();
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null) foreach (CalculatorItemRow item in e.OldItems) item.PropertyChanged -= Item_PropertyChanged;
        if (e.NewItems is not null) foreach (CalculatorItemRow item in e.NewItems) item.PropertyChanged += Item_PropertyChanged;
        RefreshItemViews();
        RefreshCalculations();
    }
    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e) { RefreshItemViews(); RefreshCalculations(); }
    private void RefreshItemViews() { _currencyItems.Refresh(); _targetItems.Refresh(); OnPropertyChanged(nameof(CurrencyItems)); OnPropertyChanged(nameof(TargetItems)); }
    private void CacheVisiblePrices(CalculatorPairRow? pair) { if (pair?.IsComplete == true) foreach (var target in Targets) target.CacheVisiblePrices(pair); }
    private void LoadVisiblePrices(CalculatorPairRow? pair) { if (pair?.IsComplete == true) foreach (var target in Targets) target.LoadVisiblePrices(pair); }
    private CalculatorItemRow EnsureItem(string name, CalculatorItemCategory category, string goldCost)
    {
        var existing = Items.FirstOrDefault(item => item.Category == category && string.Equals(item.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;
        var created = new CalculatorItemRow(Guid.NewGuid().ToString("N"), name, category, goldCost);
        Items.Add(created);
        return created;
    }
    private static CalculatorItemRow CreateItem(CalculatorItemCategory category) => new(Guid.NewGuid().ToString("N"), string.Empty, category, "0");
    private static string FormatBuyPrice(NormalizedRatio ratio) => ratio.RightPerLeft is > 0 ? FormatDecimal(1m / ratio.RightPerLeft.Value) : string.Empty;
    private static string FormatSellPrice(NormalizedRatio ratio) => ratio.Left is > 0 && ratio.Right is > 0 ? $"{FormatDecimal(ratio.Left.Value)}/{FormatDecimal(ratio.Right.Value)}" : string.Empty;
    private static bool TryPositive(string? text, out decimal value) => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value > 0;
    private static bool TryTradeValue(string? text, out decimal value)
    {
        var source = text?.Trim() ?? string.Empty;
        if (source.Contains('/'))
        {
            var parts = source.Split('/', StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && TryPositive(parts[0], out var itemCount) && TryPositive(parts[1], out var currencyCount)) { value = currencyCount / itemCount; return true; }
        }
        return TryPositive(source, out value);
    }
    private static decimal ParseNonNegative(string? text) => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : 0m;
    private static string FormatDecimal(decimal value) => value.ToString("0.########", CultureInfo.InvariantCulture);
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return; field = value; OnPropertyChanged(propertyName); }

    private sealed class CalculatorWorkspaceDocument
    {
        public string SchemaVersion { get; init; } = string.Empty;
        public string? SelectedPairKey { get; init; }
        public string? ProfitMode { get; init; }
        public IReadOnlyList<CalculatorItemDocument>? Items { get; init; }
        public IReadOnlyList<CalculatorPairDocument>? Pairs { get; init; }
        public IReadOnlyList<CalculatorTargetDocument>? Targets { get; init; }
    }
    private sealed record CalculatorItemDocument(string Id, string Name, CalculatorItemCategory Category, string GoldCost);
    private sealed record CalculatorPairDocument(string? BaseItemId, string? QuoteItemId, string Rate);
    private sealed record CalculatorTargetDocument(string? TargetItemId, IReadOnlyDictionary<string, string>? PricesByCurrencyId);
    private sealed class LegacyCalculatorWorkspaceDocument
    {
        public string SchemaVersion { get; init; } = string.Empty;
        public string? SelectedPairKey { get; init; }
        public IReadOnlyList<LegacyCalculatorCurrencyDocument>? Currencies { get; init; }
        public IReadOnlyList<LegacyCalculatorPairDocument>? Pairs { get; init; }
        public IReadOnlyList<LegacyCalculatorTargetDocument>? Targets { get; init; }
    }
    private sealed record LegacyCalculatorCurrencyDocument(string Name, string GoldCost);
    private sealed record LegacyCalculatorPairDocument(string BaseCurrency, string QuoteCurrency, string Rate);
    private sealed record LegacyCalculatorTargetDocument(string Name, string GoldCost, string BuyPrice, string SellPrice);
}

public enum CalculatorItemCategory { Currency, Target }

public sealed class CalculatorItemRow : NotifyRow
{
    private string _name; private CalculatorItemCategory _category; private string _goldCostText;
    public CalculatorItemRow(string id, string name, CalculatorItemCategory category, string goldCostText) { Id = id; _name = name; _category = category; _goldCostText = goldCostText; }
    public string Id { get; }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public CalculatorItemCategory Category { get => _category; set => SetProperty(ref _category, value); }
    public string GoldCostText { get => _goldCostText; set => SetProperty(ref _goldCostText, value); }
}

public sealed class CalculatorPairRow : NotifyRow
{
    private CalculatorItemRow? _baseItem; private CalculatorItemRow? _quoteItem; private string _rateText = string.Empty;
    public CalculatorItemRow? BaseItem { get => _baseItem; set { SetProperty(ref _baseItem, value); NotifyPairChanged(); } }
    public CalculatorItemRow? QuoteItem { get => _quoteItem; set { SetProperty(ref _quoteItem, value); NotifyPairChanged(); } }
    public string RateText { get => _rateText; set { SetProperty(ref _rateText, value); OnPropertyChanged(nameof(DisplayName)); } }
    public bool IsComplete => BaseItem is not null && QuoteItem is not null && !ReferenceEquals(BaseItem, QuoteItem);
    public string Key => IsComplete ? $"{BaseItem!.Id}|{QuoteItem!.Id}" : string.Empty;
    public string DisplayName => IsComplete ? $"{QuoteItem!.Name} 买 {BaseItem!.Name} 卖（1 {BaseItem.Name} = {RateText} {QuoteItem.Name}）" : "未配置交易对";
    private void NotifyPairChanged() { OnPropertyChanged(nameof(IsComplete)); OnPropertyChanged(nameof(Key)); OnPropertyChanged(nameof(DisplayName)); }
}

public sealed class CalculatorTargetRow : NotifyRow
{
    private CalculatorItemRow? _targetItem; private string _buyPriceText = string.Empty, _sellPriceText = string.Empty, _roiText = "待计算", _netProfitText = "--", _totalGoldCostText = "--", _goldEfficiencyText = "--", _hint = string.Empty; private bool _isProfitable; private decimal? _roiValue, _goldEfficiencyValue;
    public CalculatorTargetRow(IReadOnlyDictionary<string, string>? pricesByCurrencyId = null)
    {
        PricesByCurrencyId = pricesByCurrencyId is null ? new Dictionary<string, string>() : new Dictionary<string, string>(pricesByCurrencyId);
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(BuyPriceText) or nameof(LowestBuyPriceText)) OnPropertyChanged(nameof(ActiveBuyPriceText));
            if (e.PropertyName is nameof(SellPriceText) or nameof(HighestSellPriceText)) OnPropertyChanged(nameof(ActiveSellPriceText));
        };
    }
    private bool _useHighestBuy = true, _useHighestSell;
    public string ActiveBuyPriceText
    {
        get => _useHighestBuy ? HighestBuyPriceText : LowestBuyPriceText;
        set { if (_useHighestBuy) HighestBuyPriceText = value; else LowestBuyPriceText = value; }
    }
    public string ActiveSellPriceText
    {
        get => _useHighestSell ? HighestSellPriceText : LowestSellPriceText;
        set { if (_useHighestSell) HighestSellPriceText = value; else LowestSellPriceText = value; }
    }
    public void SetProfitMode(string mode)
    {
        var highestBuy = mode.StartsWith("高买", StringComparison.Ordinal);
        var highestSell = mode.EndsWith("高卖", StringComparison.Ordinal);
        if (_useHighestBuy != highestBuy) { _useHighestBuy = highestBuy; OnPropertyChanged(nameof(ActiveBuyPriceText)); }
        if (_useHighestSell != highestSell) { _useHighestSell = highestSell; OnPropertyChanged(nameof(ActiveSellPriceText)); }
    }
    public Dictionary<string, string> PricesByCurrencyId { get; }
    public CalculatorItemRow? TargetItem { get => _targetItem; set => SetProperty(ref _targetItem, value); }
    public string BuyPriceText { get => _buyPriceText; set => SetProperty(ref _buyPriceText, value); }
    public string SellPriceText { get => _sellPriceText; set => SetProperty(ref _sellPriceText, value); }
    private string _lowestBuyPriceText = string.Empty, _highestSellPriceText = string.Empty;
    public string HighestBuyPriceText { get => BuyPriceText; set { BuyPriceText = value; OnPropertyChanged(); } }
    public string LowestBuyPriceText { get => _lowestBuyPriceText; set => SetProperty(ref _lowestBuyPriceText, value); }
    public string HighestSellPriceText { get => _highestSellPriceText; set => SetProperty(ref _highestSellPriceText, value); }
    public string LowestSellPriceText { get => SellPriceText; set { SellPriceText = value; OnPropertyChanged(); } }
    public string RoiText { get => _roiText; private set => SetProperty(ref _roiText, value); }
    public string NetProfitText { get => _netProfitText; private set => SetProperty(ref _netProfitText, value); }
    public string TotalGoldCostText { get => _totalGoldCostText; private set => SetProperty(ref _totalGoldCostText, value); }
    public string GoldEfficiencyText { get => _goldEfficiencyText; private set => SetProperty(ref _goldEfficiencyText, value); }
    public decimal? RoiValue { get => _roiValue; private set { if (SetProperty(ref _roiValue, value)) { OnPropertyChanged(nameof(IsPositiveRoi)); OnPropertyChanged(nameof(IsNegativeRoi)); } } }
    public decimal? GoldEfficiencyValue { get => _goldEfficiencyValue; private set => SetProperty(ref _goldEfficiencyValue, value); }
    public bool IsPositiveRoi => RoiValue > 0;
    public bool IsNegativeRoi => RoiValue < 0;
    public string Hint { get => _hint; private set => SetProperty(ref _hint, value); }
    public bool IsProfitable { get => _isProfitable; private set => SetProperty(ref _isProfitable, value); }
    public void SetPrice(string currencyId, string price) => PricesByCurrencyId[currencyId] = price;
    // Bounds are unit prices in the corresponding currency. Scanner callers can write these without UI coupling.
    public void SetPriceBounds(string currencyId, decimal? highest, decimal? lowest, bool isBuy)
    {
        var side = isBuy ? "buy" : "sell";
        PricesByCurrencyId[$"{currencyId}:{side}:high"] = highest?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        PricesByCurrencyId[$"{currencyId}:{side}:low"] = lowest?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }
    public void CacheVisiblePrices(CalculatorPairRow pair)
    {
        PricesByCurrencyId[pair.QuoteItem!.Id] = BuyPriceText;
        PricesByCurrencyId[pair.BaseItem!.Id] = SellPriceText;
        PricesByCurrencyId[$"{pair.QuoteItem.Id}:buy:high"] = HighestBuyPriceText;
        PricesByCurrencyId[$"{pair.QuoteItem.Id}:buy:low"] = LowestBuyPriceText;
        PricesByCurrencyId[$"{pair.BaseItem.Id}:sell:high"] = HighestSellPriceText;
        PricesByCurrencyId[$"{pair.BaseItem.Id}:sell:low"] = LowestSellPriceText;
    }
    public void LoadVisiblePrices(CalculatorPairRow pair)
    {
        string Read(string currency, string side, string bound, bool legacy) =>
            PricesByCurrencyId.TryGetValue($"{currency}:{side}:{bound}", out var value) && !string.IsNullOrEmpty(value) ? value :
            legacy && PricesByCurrencyId.TryGetValue(currency, out var price) ? price : string.Empty;
        HighestBuyPriceText = Read(pair.QuoteItem!.Id, "buy", "high", true);
        LowestBuyPriceText = Read(pair.QuoteItem.Id, "buy", "low", false);
        HighestSellPriceText = Read(pair.BaseItem!.Id, "sell", "high", false);
        LowestSellPriceText = Read(pair.BaseItem.Id, "sell", "low", true);
    }
    public void SetResult(string roi, string netProfit, string totalGoldCost, string hint, bool isProfitable, decimal? roiValue = null, decimal? goldEfficiencyValue = null, string? sellCurrency = null) { RoiText = roi; NetProfitText = netProfit; TotalGoldCostText = totalGoldCost; Hint = hint; IsProfitable = isProfitable; RoiValue = roiValue; GoldEfficiencyValue = goldEfficiencyValue; GoldEfficiencyText = goldEfficiencyValue is { } value ? $"{value.ToString("0.00", CultureInfo.InvariantCulture)} 金/{sellCurrency}" : "--"; }
}

public abstract class NotifyRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; OnPropertyChanged(propertyName); return true; }
}
