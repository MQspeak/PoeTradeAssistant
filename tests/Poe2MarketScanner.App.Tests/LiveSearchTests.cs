using System;
using System.IO;
using PoeTradeAssistant.LiveSearch;
using Xunit;

namespace Poe2MarketScanner.App.Tests;

public sealed class LiveSearchTests
{
    [Theory]
    [InlineData("https://evil.example/trade/search/Standard/abc")]
    [InlineData("http://www.pathofexile.com/trade/search/Standard/abc")]
    [InlineData("https://www.pathofexile.com/trade2/search/poe2/Standard/abc")]
    [InlineData("https://www.pathofexile.com/trade/search/Standard")]
    [InlineData("https://www.pathofexile.com:8888/trade/search/Standard/abc")]
    [InlineData("https://user@www.pathofexile.com/trade/search/Standard/abc")]
    public void UrlMustMatchOfficialEnvironment(string url) =>
        Assert.Throws<ArgumentException>(() => new TradeEnvironment("poe1", "international").ValidateUrl(url));

    [Theory]
    [InlineData("poe1", "international", "https://www.pathofexile.com/trade/search/Standard/AbC/live")]
    [InlineData("poe1", "china", "https://poe.game.qq.com/trade/search/S29/AbC")]
    [InlineData("poe2", "international", "https://www.pathofexile.com/trade2/search/poe2/Standard/AbC")]
    [InlineData("poe2", "china", "https://poe.game.qq.com/trade2/search/poe2/Standard/AbC")]
    public void FourEnvironmentsRetainCaseSensitiveSearchIds(string game, string region, string url) =>
        Assert.Equal(url, new TradeEnvironment(game, region).ValidateUrl(url));

    [Fact]
    public void StorageKeepsEnvironmentsSeparateAndPreservesInvalidOriginal()
    {
        var root = Path.Combine(Path.GetTempPath(), "poe-live-test-" + Guid.NewGuid().ToString("N"));
        var storage = new SearchStorage(root);
        var environment = new TradeEnvironment("poe1", "international");
        try
        {
            storage.Save(environment, [new("1", "Test", environment.HomeUrl + "/AbC")]);
            Assert.Single(storage.Load(environment).Links);
            Assert.Empty(storage.Load(new("poe2", "international")).Links);
            Assert.Empty(storage.Load(new("poe1", "china")).Links);
            var file = Path.Combine(storage.DirectoryFor(environment), "workspace.json");
            File.WriteAllText(file, "invalid-json");
            Assert.ThrowsAny<Exception>(() => storage.Load(environment));
            Assert.Equal("invalid-json", File.ReadAllText(file));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void GameSwitchGuardPreventsChangingWorkspaceWhileBrowserIsOpen()
    {
        var root = Path.Combine(Path.GetTempPath(), "poe-game-guard-" + Guid.NewGuid().ToString("N"));
        try
        {
            var vm = new Poe2MarketScanner.App.MainViewModel(new Poe2MarketScanner.Core.Configuration.JsonProfileStorageService(), root);
            var initial = vm.SelectedGameMode;
            vm.CanSwitchGame = () => false;
            vm.IsPoe1Mode = true;
            Assert.Equal(initial, vm.SelectedGameMode);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void BrowserLocatorPrefersChromeAndFallsBackToEdge()
    {
        Assert.Equal("chrome", SystemBrowserLocator.Resolve(null, path => path.EndsWith("chrome.exe", StringComparison.OrdinalIgnoreCase))!.Channel);
        Assert.Equal("msedge", SystemBrowserLocator.Resolve(null, path => path.EndsWith("msedge.exe", StringComparison.OrdinalIgnoreCase))!.Channel);
        Assert.Equal("msedge", SystemBrowserLocator.Resolve("msedge", _ => true)!.Channel);
        Assert.Null(SystemBrowserLocator.Resolve(null, _ => false));
    }
}
