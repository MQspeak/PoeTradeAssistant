using System;
using System.IO;
using System.Linq;
using Poe2MarketScanner.App;
using Poe2MarketScanner.Core.Configuration;
using Xunit;

namespace Poe2MarketScanner.App.Tests;

// Keep WPF collection views on their creating thread; the save worker only writes snapshots.
#pragma warning disable xUnit1031
public sealed class WorkspaceShutdownSaveTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"poe-shutdown-{Guid.NewGuid():N}");

    [Fact]
    public void SaveAll_RestoresBothWorkspacesAndLastSelectedGame()
    {
        var model = new MainViewModel(new JsonProfileStorageService(), _root);
        model.Load();
        model.OutputDirectory = "poe2-output";
        model.QueryItems.Add("POE2 查询");
        model.UseTraditionalChinese = true;
        model.Profile.Automation.ClickDelayMs = 321;
        model.Calculator.AddCurrency();
        model.Calculator.Items.Single().Name = "POE2 通货";
        model.Calculator.Items.Single().GoldCostText = "123";
        model.Calculator.ProfitMode = "低买高卖";
        model.SelectedGameMode = GameMode.Poe1;
        model.OutputDirectory = "poe1-output";
        model.Calculator.AddTargetItem();
        model.Calculator.Items.Single().Name = "POE1 标的";
        model.GoldCostRawText = "456";
        model.SaveAllWorkspacesAsync().GetAwaiter().GetResult();

        var restored = new MainViewModel(new JsonProfileStorageService(), _root);
        restored.Load();
        Assert.Equal(GameMode.Poe1, restored.SelectedGameMode);
        Assert.Equal("poe1-output", restored.OutputDirectory);
        Assert.Equal("POE1 标的", restored.Calculator.Items.Single().Name);
        Assert.Equal("456", restored.GoldCostRawText);
        restored.SelectedGameMode = GameMode.Poe2;
        Assert.Equal("poe2-output", restored.OutputDirectory);
        Assert.Contains("POE2 查询", restored.QueryItems);
        Assert.True(restored.UseTraditionalChinese);
        Assert.Equal(321, restored.Profile.Automation.ClickDelayMs);
        Assert.Equal("POE2 通货", restored.Calculator.Items.Single().Name);
        Assert.Equal("123", restored.Calculator.Items.Single().GoldCostText);
        Assert.Equal("低买高卖", restored.Calculator.ProfitMode);
    }

    [Fact]
    public void FailedSwitchSave_KeepsWorkspaceInMemoryAndRetriesOnExit()
    {
        var storage = new RetryStorage();
        var model = new MainViewModel(storage, _root);
        model.Load();
        model.OutputDirectory = "unsaved-poe2";
        model.SelectedGameMode = GameMode.Poe1;
        model.OutputDirectory = "unsaved-poe1";
        Assert.Throws<IOException>(() => model.SaveAllWorkspacesAsync().GetAwaiter().GetResult());
        model.SelectedGameMode = GameMode.Poe2;
        Assert.Equal("unsaved-poe2", model.OutputDirectory);
        storage.Fail = false;
        model.SaveAllWorkspacesAsync().GetAwaiter().GetResult();
        var reader = new JsonProfileStorageService();
        Assert.Equal("unsaved-poe2", reader.Load(Path.Combine(_root, "profiles", "poe2", "profile.json")).OutputDirectory);
        Assert.Equal("unsaved-poe1", reader.Load(Path.Combine(_root, "profiles", "poe1", "profile.json")).OutputDirectory);
    }

    [Fact]
    public void CalculatorWriteFailure_IsReportedAndCanBeRetriedWithoutLosingEdits()
    {
        var model = new MainViewModel(new JsonProfileStorageService(), _root);
        model.Load();
        model.Calculator.AddCurrency();
        model.Calculator.Items.Single().Name = "保留的编辑";
        var path = Path.Combine(_root, "profiles", "poe2", "calculator-workspace.json");
        AtomicFile.WriteAllText(path, "previous contents");
        using (var lockedFile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = Record.Exception(() => model.SaveAllWorkspacesAsync().GetAwaiter().GetResult());
            Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString() ?? "Expected a write failure.");
            Assert.Equal("previous contents", File.ReadAllText(path));
        }
        Assert.False(File.Exists(Path.Combine(_root, "profiles", "application-settings.json")));
        model.SaveAllWorkspacesAsync().GetAwaiter().GetResult();
        var restored = new MainViewModel(new JsonProfileStorageService(), _root);
        restored.Load();
        Assert.Equal("保留的编辑", restored.Calculator.Items.Single().Name);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class RetryStorage : IProfileStorageService
    {
        private readonly JsonProfileStorageService _inner = new();
        public bool Fail { get; set; } = true;
        public AppProfile Load(string path) => _inner.Load(path);
        public void Save(string path, AppProfile profile)
        {
            if (Fail) throw new IOException("test write failure");
            _inner.Save(path, profile);
        }
    }
}
