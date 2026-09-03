using System;
using System.IO;
using System.Linq;
using Poe2MarketScanner.App;
using Poe2MarketScanner.App.Services;
using Poe2MarketScanner.Core.Configuration;
using Xunit;

namespace Poe2MarketScanner.App.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void Load_ShouldExposeEditableOcrRegionEntries()
    {
        var viewModel = new MainViewModel(new InMemoryProfileStorageService());

        viewModel.Load();

        Assert.Contains(viewModel.OcrRegionEntries, entry => entry.Key == "goldCost");
        Assert.Contains(viewModel.OcrRegionEntries, entry => entry.Key == "ratio");
    }

    [Fact]
    public void UseTraditionalChinese_ShouldRefreshTradeModeDisplayNames()
    {
        var viewModel = new MainViewModel(new InMemoryProfileStorageService());
        var originalMode = TradeModeCatalog.All[1];
        var selectedMode = TradeModeCatalog.All[^1];

        viewModel.Load();
        viewModel.SelectedTradeModeKey = selectedMode.Key;
        Assert.Contains(viewModel.TradeModes, item => item.DisplayName == originalMode.DisplayName);

        viewModel.UseTraditionalChinese = true;

        var traditionalDisplayName = TradeModeCatalog.LocalizeTradeModeDisplayName(originalMode, true);
        Assert.Contains(viewModel.TradeModes, item => item.DisplayName == traditionalDisplayName);
        Assert.DoesNotContain(viewModel.TradeModes, item => item.DisplayName == originalMode.DisplayName);
        Assert.Equal(selectedMode.Key, viewModel.SelectedTradeModeKey);
    }

    [Fact]
    public void ApplyOcrDebugResult_ShouldExposeDetailedRegionDebugCards()
    {
        var viewModel = new MainViewModel(new InMemoryProfileStorageService());

        viewModel.ApplyOcrDebugResult(new OcrDebugRunResult
        {
            DebugDirectory = @"C:\debug\run-1",
            Summary = "gold=81000(ok) | ratio=675:1(ok)",
            GoldCostResult = new OcrDebugRegionResult
            {
                RegionKey = "goldCost",
                RegionName = "gold",
                OriginalImagePath = @"C:\debug\run-1\gold-raw.png",
                ProcessedImagePath = @"C:\debug\run-1\gold-processed.png",
                RawText = "81,000",
                NormalizedText = "81000",
                Status = "ok"
            },
            RatioResult = new OcrDebugRegionResult
            {
                RegionKey = "ratio",
                RegionName = "ratio",
                OriginalImagePath = @"C:\debug\run-1\ratio-raw.png",
                ProcessedImagePath = @"C:\debug\run-1\ratio-processed.png",
                RawText = "675 : 1",
                NormalizedText = "675:1",
                Status = "ok"
            }
        });

        Assert.Equal(@"C:\debug\run-1", viewModel.LatestDebugDirectory);
        Assert.Equal(2, viewModel.DebugRegions.Count);
        Assert.Equal("gold", viewModel.DebugRegions[0].RegionName);
        Assert.Equal("81,000", viewModel.DebugRegions[0].RawText);
        Assert.Equal("81000", viewModel.DebugRegions[0].NormalizedText);
        Assert.Equal(@"C:\debug\run-1\gold-raw.png", viewModel.DebugRegions[0].OriginalImagePath);
        Assert.Equal(@"C:\debug\run-1\gold-processed.png", viewModel.DebugRegions[0].ProcessedImagePath);
    }

    [Fact]
    public void TrySave_ShouldPersistCurrentProfileState()
    {
        var storage = new InMemoryProfileStorageService();
        var viewModel = new MainViewModel(storage);

        viewModel.Load();
        viewModel.QuerySourceFile = @"C:\temp\currencies.txt";
        viewModel.Profile.Automation.CommonDelayMs = 900;
        viewModel.UseTraditionalChinese = true;

        var saved = viewModel.TrySave(out var errorMessage);

        Assert.True(saved);
        Assert.Null(errorMessage);
        Assert.Equal(@"C:\temp\currencies.txt", storage.StoredProfile.QueryList.SourceFile);
        Assert.Equal(900, storage.StoredProfile.Automation.CommonDelayMs);
        Assert.True(storage.StoredProfile.UseTraditionalChinese);
    }

    [Fact]
    public void TrySave_ShouldPersistOutputDirectory()
    {
        var storage = new InMemoryProfileStorageService();
        var viewModel = new MainViewModel(storage);

        viewModel.Load();
        viewModel.OutputDirectory = @"D:\exports";

        var saved = viewModel.TrySave(out var errorMessage);

        Assert.True(saved);
        Assert.Null(errorMessage);
        Assert.Equal(@"D:\exports", storage.StoredProfile.OutputDirectory);
    }

    [Fact]
    public void RecognizeGoldCost_ShouldControlGoldCostInputState_AndPersist()
    {
        var storage = new InMemoryProfileStorageService();
        var viewModel = new MainViewModel(storage);

        viewModel.Load();

        Assert.True(viewModel.RecognizeGoldCost);
        Assert.True(viewModel.IsGoldCostInputEnabled);

        viewModel.RecognizeGoldCost = false;
        var saved = viewModel.TrySave(out var errorMessage);

        Assert.True(saved);
        Assert.Null(errorMessage);
        Assert.False(viewModel.IsGoldCostInputEnabled);
        Assert.False(storage.StoredProfile.Automation.RecognizeGoldCost);
    }

    [Fact]
    public void LoadProfileFromFile_ShouldReplaceCurrentState()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            var storage = new InMemoryProfileStorageService();
            var profile = AppProfileFactory.CreateDefault();
            profile.OutputDirectory = @"E:\exports";
            profile.QueryList.SourceFile = @"C:\imports\currencies.txt";
            profile.QueryList.Items = new() { "Divine Orb", "Exalted Orb" };
            profile.UseTraditionalChinese = true;

            var jsonStorage = new JsonProfileStorageService();
            jsonStorage.Save(tempFile, profile);
            storage.RegisterExternalProfile(tempFile, jsonStorage.Load(tempFile));

            var viewModel = new MainViewModel(storage);
            viewModel.Load();
            viewModel.OutputDirectory = @"D:\stale";

            viewModel.LoadProfileFromFile(tempFile);

            Assert.Equal(@"E:\exports", viewModel.OutputDirectory);
            Assert.Equal(@"C:\imports\currencies.txt", viewModel.QuerySourceFile);
            Assert.Equal(new[] { "Divine Orb", "Exalted Orb" }, viewModel.QueryItems.ToArray());
            Assert.True(viewModel.UseTraditionalChinese);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ResetToDefaultProfile_ShouldRestoreDefaultState()
    {
        var storage = new InMemoryProfileStorageService();
        var viewModel = new MainViewModel(storage);
        var queryFile = CreateTempQueryFile("Divine Orb", "Exalted Orb");

        try
        {
            viewModel.Load();
            viewModel.OutputDirectory = @"D:\exports";
            viewModel.ImportQueryFile(queryFile);

            viewModel.ResetToDefaultProfile();

            var defaults = AppProfileNormalizer.Normalize(AppProfileFactory.CreateDefault());
            Assert.Equal(defaults.OutputDirectory, viewModel.OutputDirectory);
            Assert.Equal(defaults.QueryList.SourceFile, viewModel.QuerySourceFile);
            Assert.Empty(viewModel.QueryItems);
        }
        finally
        {
            File.Delete(queryFile);
        }
    }

    [Fact]
    public void TrySave_ShouldReturnFalseWhenStorageFails()
    {
        var viewModel = new MainViewModel(new ThrowingProfileStorageService());

        viewModel.Load();

        var saved = viewModel.TrySave(out var errorMessage);

        Assert.False(saved);
        Assert.Equal("save failed", errorMessage);
    }

    [Fact]
    public void SwitchingGameModes_ShouldKeepProfilesAndCalculatorWorkspacesIndependent()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), $"poe-game-modes-{Guid.NewGuid():N}");

        try
        {
            var viewModel = new MainViewModel(new JsonProfileStorageService(), dataDirectory);
            viewModel.Load();
            viewModel.OutputDirectory = @"D:\poe2-output";
            viewModel.Calculator.AddCurrency();
            viewModel.Calculator.Items.Single().Name = "POE2 通货";

            viewModel.SelectedGameMode = GameMode.Poe1;
            viewModel.OutputDirectory = @"D:\poe1-output";
            viewModel.Calculator.AddCurrency();
            viewModel.Calculator.Items.Single().Name = "POE1 通货";

            viewModel.SelectedGameMode = GameMode.Poe2;

            Assert.Equal(@"D:\poe2-output", viewModel.OutputDirectory);
            Assert.Equal("POE2 通货", viewModel.Calculator.Items.Single().Name);
            Assert.Contains($"profiles{Path.DirectorySeparatorChar}poe2", viewModel.ProfilePath, StringComparison.OrdinalIgnoreCase);

            viewModel.SelectedGameMode = GameMode.Poe1;

            Assert.Equal(@"D:\poe1-output", viewModel.OutputDirectory);
            Assert.Equal("POE1 通货", viewModel.Calculator.Items.Single().Name);
            Assert.Contains($"profiles{Path.DirectorySeparatorChar}poe1", viewModel.ProfilePath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(dataDirectory))
            {
                Directory.Delete(dataDirectory, recursive: true);
            }
        }
    }

    private static string CreateTempQueryFile(params string[] items)
    {
        var path = Path.GetTempFileName();
        File.WriteAllLines(path, items);
        return path;
    }
}

internal sealed class InMemoryProfileStorageService : IProfileStorageService
{
    private AppProfile _profile = AppProfileNormalizer.Normalize(AppProfileFactory.CreateDefault());
    private readonly System.Collections.Generic.Dictionary<string, AppProfile> _externalProfiles = new(StringComparer.OrdinalIgnoreCase);

    public AppProfile Load(string path)
    {
        if (_externalProfiles.TryGetValue(path, out var profile))
        {
            return profile;
        }

        return _profile;
    }

    public void Save(string path, AppProfile profile) => _profile = profile;

    public AppProfile StoredProfile => _profile;

    public void RegisterExternalProfile(string path, AppProfile profile) => _externalProfiles[path] = profile;
}

internal sealed class ThrowingProfileStorageService : IProfileStorageService
{
    public AppProfile Load(string path) => AppProfileNormalizer.Normalize(AppProfileFactory.CreateDefault());

    public void Save(string path, AppProfile profile) => throw new InvalidOperationException("save failed");
}

