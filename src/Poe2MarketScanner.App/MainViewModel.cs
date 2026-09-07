using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Poe2MarketScanner.App.Services;
using Poe2MarketScanner.Core.Configuration;
using Poe2MarketScanner.Core.Ocr;

namespace Poe2MarketScanner.App;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IProfileStorageService _storageService;
    private readonly string _profilesRoot;
    private readonly string _legacyProfilePath;
    private GameMode _selectedGameMode = GameMode.Poe2;
    private NativeCalculatorViewModel _calculator;
    private readonly Dictionary<GameMode, (AppProfile Profile, NativeCalculatorViewModel Calculator)> _workspaces = new();
    private string SettingsPath => Path.Combine(_profilesRoot, "application-settings.json");
    private string _querySourceFile = string.Empty;
    private string _goldCostRawText = "81,000";
    private string _ratioRawText = "675 : 1";
    private string _ocrPreviewSummary = "运行 OCR 调试后可在此查看识别结果。";
    private string _latestDebugDirectory = string.Empty;
    public MainViewModel(IProfileStorageService storageService, string? applicationDataPath = null)
    {
        _storageService = storageService;
        var dataRoot = applicationDataPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PoeTradeAssistant");
        _profilesRoot = Path.Combine(dataRoot, "profiles");
        _legacyProfilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Poe2MarketScanner",
            "profile.json");
        _calculator = new NativeCalculatorViewModel(GetCalculatorWorkspacePath(_selectedGameMode));
        RefreshTradeModes();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public Func<bool>? CanSwitchGame { get; set; }
    public event EventHandler? LayoutChanged;

    public string ProfilePath => GetProfilePath(SelectedGameMode);

    public ObservableCollection<RegionEntry> RegionEntries { get; private set; } = new();
    public ObservableCollection<AnchorEntry> AnchorEntries { get; private set; } = new();
    public ObservableCollection<OcrRegionSettingsEntry> OcrRegionEntries { get; private set; } = new();
    public ObservableCollection<string> QueryItems { get; } = new();
    public ObservableCollection<QueryItemEntry> QueryItemEntries { get; } = new();
    public int EnabledQueryItemCount => QueryItemEntries.Count(item => item.IsEnabled);
    public ObservableCollection<OcrDebugRegionResult> DebugRegions { get; } = new();
    public ObservableCollection<TradeModeOption> TradeModes { get; } = new();
    public AppProfile Profile { get; private set; } = AppProfileFactory.CreateDefault();
    public NativeCalculatorViewModel Calculator => _calculator;
    public IReadOnlyList<GameModeOption> GameModes { get; } = new[]
    {
        new GameModeOption(GameMode.Poe1, "POE1"),
        new GameModeOption(GameMode.Poe2, "POE2")
    };

    public GameMode SelectedGameMode
    {
        get => _selectedGameMode;
        set
        {
            if (_selectedGameMode == value)
            {
                return;
            }

            if (CanSwitchGame is not null && !CanSwitchGame())
            {
                OnPropertyChanged(nameof(IsPoe1Mode));
                OnPropertyChanged(nameof(IsPoe2Mode));
                return;
            }

            SaveCurrentGameState();
            _selectedGameMode = value;
            LoadGameState();
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPoe1Mode));
            OnPropertyChanged(nameof(IsPoe2Mode));
            OnPropertyChanged(nameof(ActiveGameDisplayName));
            OnPropertyChanged(nameof(ProfilePath));
            OnPropertyChanged(nameof(Calculator));
        }
    }

    public bool IsPoe1Mode
    {
        get => SelectedGameMode == GameMode.Poe1;
        set { if (value) SelectedGameMode = GameMode.Poe1; }
    }

    public bool IsPoe2Mode
    {
        get => SelectedGameMode == GameMode.Poe2;
        set { if (value) SelectedGameMode = GameMode.Poe2; }
    }

    public string ActiveGameDisplayName => SelectedGameMode == GameMode.Poe1 ? "POE1" : "POE2";

    public string QuerySourceFile
    {
        get => _querySourceFile;
        set => SetProperty(ref _querySourceFile, value);
    }

    public string GoldCostRawText
    {
        get => _goldCostRawText;
        set => SetProperty(ref _goldCostRawText, value);
    }

    public string RatioRawText
    {
        get => _ratioRawText;
        set => SetProperty(ref _ratioRawText, value);
    }

    public string OcrPreviewSummary
    {
        get => _ocrPreviewSummary;
        private set => SetProperty(ref _ocrPreviewSummary, value);
    }

    public string LatestDebugDirectory
    {
        get => _latestDebugDirectory;
        private set => SetProperty(ref _latestDebugDirectory, value);
    }

    public bool UseTraditionalChinese
    {
        get => Profile.UseTraditionalChinese;
        set
        {
            if (Profile.UseTraditionalChinese == value)
            {
                return;
            }

            Profile.UseTraditionalChinese = value;
            RefreshTradeModes();
            OnPropertyChanged();
        }
    }

    public string SelectedTradeModeKey
    {
        get => Profile.SelectedTradeModeKey;
        set
        {
            var resolved = TradeModeCatalog.Resolve(value).Key;
            if (Profile.SelectedTradeModeKey == resolved)
            {
                return;
            }

            Profile.SelectedTradeModeKey = resolved;
            OnPropertyChanged();
        }
    }

    public string OutputDirectory
    {
        get => Profile.OutputDirectory;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (Profile.OutputDirectory == normalized)
            {
                return;
            }

            Profile.OutputDirectory = normalized;
            OnPropertyChanged();
        }
    }

    public bool RecognizeGoldCost
    {
        get => Profile.Automation.RecognizeGoldCost;
        set
        {
            if (Profile.Automation.RecognizeGoldCost == value)
            {
                return;
            }

            Profile.Automation.RecognizeGoldCost = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsGoldCostInputEnabled));
        }
    }

    public bool IsGoldCostInputEnabled => RecognizeGoldCost;

    public void Load()
    {
        if (File.Exists(SettingsPath))
        {
            var settings = JsonSerializer.Deserialize<ApplicationSettings>(File.ReadAllText(SettingsPath));
            if (settings is not null)
            {
                _selectedGameMode = Enum.IsDefined(settings.SelectedGameMode) ? settings.SelectedGameMode : GameMode.Poe2;
                GoldCostRawText = settings.GoldCostRawText;
                RatioRawText = settings.RatioRawText;
            }
        }
        LoadGameState();
    }

    public async Task SaveAllWorkspacesAsync()
    {
        SynchronizeProfile();
        _workspaces[SelectedGameMode] = (Profile, Calculator);
        var saves = _workspaces.Select(entry => (
            Path: GetProfilePath(entry.Key),
            Profile: JsonSerializer.Deserialize<AppProfile>(JsonSerializer.Serialize(entry.Value.Profile))!,
            SaveCalculator: entry.Value.Calculator.CreateSaveAction())).ToArray();
        var settings = JsonSerializer.Serialize(new ApplicationSettings
        {
            SelectedGameMode = SelectedGameMode,
            GoldCostRawText = GoldCostRawText,
            RatioRawText = RatioRawText
        });
        await Task.Run(() =>
        {
            foreach (var save in saves)
            {
                _storageService.Save(save.Path, save.Profile);
                save.SaveCalculator();
            }
            AtomicFile.WriteAllText(SettingsPath, settings);
        });
    }

    private sealed class ApplicationSettings
    {
        public GameMode SelectedGameMode { get; set; } = GameMode.Poe2;
        public string GoldCostRawText { get; set; } = "81,000";
        public string RatioRawText { get; set; } = "675 : 1";
    }

    public void Save()
    {
        if (!TrySave(out var errorMessage))
        {
            throw new InvalidOperationException(errorMessage ?? "Failed to save profile.");
        }
    }

    public bool TrySave(out string? errorMessage)
    {
        SynchronizeProfile();
        errorMessage = null;
        try
        {
            _storageService.Save(ProfilePath, Profile);
            return true;
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
            return false;
        }
    }

    private void SynchronizeProfile()
    {
        Profile.QueryList.SourceFile = QuerySourceFile;
        Profile.QueryList.Items = QueryItems.ToList();
        Profile.QueryList.DisabledItems = QueryItemEntries
            .Where(item => !item.IsEnabled)
            .Select(item => item.Name)
            .ToList();
        Profile.Regions = RegionEntries.ToDictionary(item => item.Key, item => item.Region);
        Profile.Anchors = AnchorEntries.ToDictionary(item => item.Key, item => item.Anchor);
        Profile.Ocr.RegionOverrides = OcrRegionEntries.ToDictionary(item => item.Key, item => item.Settings);
        Profile.SelectedTradeModeKey = TradeModeCatalog.Resolve(Profile.SelectedTradeModeKey).Key;
    }

    public void ImportQueryFile(string filePath)
    {
        var items = File.ReadAllLines(filePath)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        QueryItems.Clear();
        QueryItemEntries.Clear();
        foreach (var item in items)
        {
            QueryItems.Add(item);
            QueryItemEntries.Add(CreateQueryItemEntry(item, true));
        }

        QuerySourceFile = filePath;
    }

    public void LoadProfileFromFile(string filePath)
    {
        var importedProfile = _storageService.Load(filePath);
        _storageService.Save(ProfilePath, importedProfile);
        ApplyProfile(importedProfile);
    }

    public void ResetToDefaultProfile()
    {
        var defaultProfile = AppProfileNormalizer.Normalize(AppProfileFactory.CreateDefault());
        ApplyProfile(defaultProfile);
    }

    public void ParseOcrPreview()
    {
        var goldResult = OcrTextParser.ParseGoldCost(GoldCostRawText);
        var ratioResult = OcrTextParser.ParseRatio(RatioRawText);

        OcrPreviewSummary =
            $"金币：{(goldResult.Success ? goldResult.NormalizedText : "解析失败")} | " +
            $"比例：{(ratioResult.Success ? ratioResult.NormalizedText : "解析失败")}";
    }

    public void ApplyOcrDebugResult(OcrDebugRunResult result)
    {
        DebugRegions.Clear();

        if (result.GoldCostResult is not null)
        {
            GoldCostRawText = result.GoldCostResult.RawText;
            DebugRegions.Add(result.GoldCostResult);
        }

        if (result.RatioResult is not null)
        {
            RatioRawText = result.RatioResult.RawText;
            DebugRegions.Add(result.RatioResult);
        }

        LatestDebugDirectory = result.DebugDirectory;
        OcrPreviewSummary = result.Summary;
    }

    public void NotifyLayoutChanged()
    {
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveQueryItem(QueryItemEntry? item)
    {
        if (item is null)
        {
            return;
        }

        QueryItemEntries.Remove(item);
        QueryItems.Remove(item.Name);
        OnPropertyChanged(nameof(EnabledQueryItemCount));
    }

    public void ToggleAllQueryItems()
    {
        var enableAll = QueryItemEntries.Any(item => !item.IsEnabled);
        foreach (var item in QueryItemEntries)
        {
            item.IsEnabled = enableAll;
        }
    }

    private void RefreshTradeModes()
    {
        var selectedKey = Profile.SelectedTradeModeKey;
        TradeModes.Clear();

        foreach (var mode in TradeModeCatalog.All)
        {
            TradeModes.Add(new TradeModeOption(
                mode.Key,
                TradeModeCatalog.LocalizeTradeModeDisplayName(mode, Profile.UseTraditionalChinese)));
        }

        Profile.SelectedTradeModeKey = TradeModeCatalog.Resolve(selectedKey).Key;
        OnPropertyChanged(nameof(TradeModes));
        OnPropertyChanged(nameof(SelectedTradeModeKey));
    }

    private void LoadGameState()
    {
        if (_workspaces.TryGetValue(SelectedGameMode, out var workspace))
        {
            ApplyProfile(workspace.Profile);
            _calculator = workspace.Calculator;
            OnPropertyChanged(nameof(Calculator));
            return;
        }
        MigratePoe2DataIfNeeded();
        ApplyProfile(_storageService.Load(ProfilePath));
        _calculator = new NativeCalculatorViewModel(GetCalculatorWorkspacePath(SelectedGameMode));
        _calculator.Load();
        _workspaces[SelectedGameMode] = (Profile, Calculator);
        OnPropertyChanged(nameof(Calculator));
    }

    private void SaveCurrentGameState()
    {
        SynchronizeProfile();
        _workspaces[SelectedGameMode] = (Profile, Calculator);
        TrySave(out _);
        try
        {
            Calculator.Save();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The user can retry saving from the calculator panel; switching games must remain available.
        }
    }

    private string GetProfilePath(GameMode gameMode) => Path.Combine(_profilesRoot, GetGameModeFolderName(gameMode), "profile.json");

    private string GetCalculatorWorkspacePath(GameMode gameMode) => Path.Combine(_profilesRoot, GetGameModeFolderName(gameMode), "calculator-workspace.json");

    private static string GetGameModeFolderName(GameMode gameMode) => gameMode == GameMode.Poe1 ? "poe1" : "poe2";

    private void MigratePoe2DataIfNeeded()
    {
        if (SelectedGameMode != GameMode.Poe2)
        {
            return;
        }

        var targetProfilePath = GetProfilePath(GameMode.Poe2);
        if (!File.Exists(targetProfilePath) && File.Exists(_legacyProfilePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetProfilePath)!);
            File.Copy(_legacyProfilePath, targetProfilePath);
        }

        var legacyCalculatorPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PoeTradeAssistant",
            "calculator-workspace.json");
        var targetCalculatorPath = GetCalculatorWorkspacePath(GameMode.Poe2);
        if (!File.Exists(targetCalculatorPath) && File.Exists(legacyCalculatorPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetCalculatorPath)!);
            File.Copy(legacyCalculatorPath, targetCalculatorPath);
        }
    }

    private void ApplyProfile(AppProfile profile)
    {
        Profile = AppProfileNormalizer.Normalize(profile);
        QuerySourceFile = Profile.QueryList.SourceFile;

        RegionEntries = new ObservableCollection<RegionEntry>(
            Profile.Regions.Select(item => new RegionEntry(item.Key, item.Value)));
        AnchorEntries = new ObservableCollection<AnchorEntry>(
            Profile.Anchors.Select(item => new AnchorEntry(item.Key, item.Value)));
        OcrRegionEntries = new ObservableCollection<OcrRegionSettingsEntry>(
            Profile.Regions.Select(item => new OcrRegionSettingsEntry(
                item.Key,
                item.Value.Name,
                GetOrCreateRegionOverride(item.Key))));

        QueryItems.Clear();
        QueryItemEntries.Clear();
        var disabledItems = Profile.QueryList.DisabledItems.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Profile.QueryList.Items)
        {
            QueryItems.Add(item);
            QueryItemEntries.Add(CreateQueryItemEntry(item, !disabledItems.Contains(item)));
        }

        SubscribeLayoutEvents();
        RefreshTradeModes();

        OnPropertyChanged(nameof(RegionEntries));
        OnPropertyChanged(nameof(AnchorEntries));
        OnPropertyChanged(nameof(OcrRegionEntries));
        OnPropertyChanged(nameof(Profile));
        OnPropertyChanged(nameof(UseTraditionalChinese));
        OnPropertyChanged(nameof(SelectedTradeModeKey));
        OnPropertyChanged(nameof(OutputDirectory));
        OnPropertyChanged(nameof(RecognizeGoldCost));
        OnPropertyChanged(nameof(IsGoldCostInputEnabled));
        OnPropertyChanged(nameof(QueryItemEntries));
        OnPropertyChanged(nameof(EnabledQueryItemCount));
        ParseOcrPreview();
    }

    private QueryItemEntry CreateQueryItemEntry(string name, bool isEnabled)
    {
        var entry = new QueryItemEntry(name, isEnabled);
        entry.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(QueryItemEntry.IsEnabled))
            {
                OnPropertyChanged(nameof(EnabledQueryItemCount));
            }
        };
        return entry;
    }

    private OcrRegionSettings GetOrCreateRegionOverride(string key)
    {
        if (!Profile.Ocr.RegionOverrides.TryGetValue(key, out var settings))
        {
            settings = new OcrRegionSettings();
            Profile.Ocr.RegionOverrides[key] = settings;
        }

        return settings;
    }

    private void SubscribeLayoutEvents()
    {
        foreach (var entry in RegionEntries)
        {
            entry.Region.PropertyChanged += HandleLayoutItemChanged;
        }

        foreach (var entry in AnchorEntries)
        {
            entry.Anchor.PropertyChanged += HandleLayoutItemChanged;
        }
    }

    private void HandleLayoutItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public sealed record RegionEntry(string Key, ScreenRegion Region);

public sealed record AnchorEntry(string Key, AnchorPoint Anchor);

public sealed record OcrRegionSettingsEntry(string Key, string Name, OcrRegionSettings Settings);

public sealed record TradeModeOption(string Key, string DisplayName);

public sealed class QueryItemEntry : ObservableModel
{
    private bool _isEnabled;

    public QueryItemEntry(string name, bool isEnabled)
    {
        Name = name;
        _isEnabled = isEnabled;
    }

    public string Name { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}

public enum GameMode { Poe1, Poe2 }

public sealed record GameModeOption(GameMode Mode, string DisplayName);


