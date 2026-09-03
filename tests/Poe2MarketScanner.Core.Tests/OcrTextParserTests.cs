using Poe2MarketScanner.Core.Ocr;
using Xunit;

namespace Poe2MarketScanner.Core.Tests;

public sealed class OcrTextParserTests
{
    [Theory]
    [InlineData("81,000", 81000)]
    [InlineData(" 8 1 0 0 0 ", 81000)]
    public void ParseGoldCost_ShouldKeepDigitsOnly(string input, int expected)
    {
        var result = OcrTextParser.ParseGoldCost(input);

        Assert.True(result.Success);
        Assert.Equal(expected, result.GoldCostValue);
        Assert.Equal(expected.ToString(), result.NormalizedText);
    }

    [Theory]
    [InlineData("675:1", "675:1")]
    [InlineData("675 / 1", "675:1")]
    [InlineData("675\uFF1A1", "675:1")]
    [InlineData("1:409.09", "1:409.09")]
    public void ParseRatio_ShouldNormalizeCommonSeparators(
        string input,
        string expected)
    {
        var result = OcrTextParser.ParseRatio(input);

        Assert.True(result.Success);
        Assert.Equal(expected, result.NormalizedText);
    }
}
