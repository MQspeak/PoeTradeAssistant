using Poe2MarketScanner.Core.Configuration;
using Xunit;

namespace Poe2MarketScanner.Core.Tests;

public sealed class ProfileDefaultsTests
{
    [Fact]
    public void CreateDefault_ShouldCreateTradeModeAndEightAnchors()
    {
        var profile = AppProfileFactory.CreateDefault();

        Assert.Equal("default", profile.ProfileName);
        Assert.False(profile.UseTraditionalChinese);
        Assert.Equal(TradeModeCatalog.All[0].Key, profile.SelectedTradeModeKey);
        Assert.Equal(2, profile.Regions.Count);
        Assert.Equal(8, profile.Anchors.Count);
        Assert.Equal(AnchorVisualGeometry.MarkerCenterMode, profile.AnchorCoordinateMode);
        Assert.Equal(string.Empty, profile.OutputDirectory);
        Assert.Equal(500, profile.Automation.CommonDelayMs);
        Assert.Contains("goldCost", profile.Regions.Keys);
        Assert.Contains("ratio", profile.Regions.Keys);
        Assert.Contains("leftCurrency", profile.Anchors.Keys);
        Assert.Contains("leftInput", profile.Anchors.Keys);
        Assert.Contains("rightInput", profile.Anchors.Keys);
        Assert.Contains("idle", profile.Anchors.Keys);
        Assert.True(profile.Ocr.RegionOverrides["goldCost"].Enabled);
    }
}
