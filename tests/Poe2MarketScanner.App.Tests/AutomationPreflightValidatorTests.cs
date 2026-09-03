using Poe2MarketScanner.App.Services;
using Poe2MarketScanner.Core.Configuration;
using Xunit;

namespace Poe2MarketScanner.App.Tests;

public sealed class AutomationPreflightValidatorTests
{
    [Fact]
    public void Validate_ShouldWarnWhenNewAnchorsStayAtDefaultsButLegacyAnchorsWereCustomized()
    {
        var profile = AppProfileFactory.CreateDefault();
        profile.Anchors["leftCurrency"].X = 740;
        profile.Anchors["leftCurrency"].Y = 232;
        profile.Anchors["rightCurrency"].X = 1171;
        profile.Anchors["rightCurrency"].Y = 233;

        var message = AutomationPreflightValidator.Validate(profile);

        Assert.Contains("左边输入位置", message);
        Assert.Contains("右边输入位置", message);
        Assert.Contains("滞空锚点", message);
    }

    [Fact]
    public void Validate_ShouldPassWhenNewAnchorsWereConfigured()
    {
        var profile = AppProfileFactory.CreateDefault();
        profile.Anchors["leftCurrency"].X = 740;
        profile.Anchors["leftCurrency"].Y = 232;
        profile.Anchors["leftInput"].X = 822;
        profile.Anchors["rightInput"].X = 1130;
        profile.Anchors["idle"].X = 1510;

        var message = AutomationPreflightValidator.Validate(profile);

        Assert.Null(message);
    }

    [Fact]
    public void ValidateStartRequirements_ShouldWarnWhenQueryListIsEmpty()
    {
        var profile = AppProfileFactory.CreateDefault();
        profile.OutputDirectory = @"D:\exports";

        var message = AutomationPreflightValidator.ValidateStartRequirements(profile, 0);

        Assert.Contains("通货列表", message);
    }

    [Fact]
    public void ValidateStartRequirements_ShouldWarnWhenOutputDirectoryIsMissing()
    {
        var profile = AppProfileFactory.CreateDefault();
        profile.OutputDirectory = string.Empty;

        var message = AutomationPreflightValidator.ValidateStartRequirements(profile, 2);

        Assert.Contains("输出目录", message);
    }
}
