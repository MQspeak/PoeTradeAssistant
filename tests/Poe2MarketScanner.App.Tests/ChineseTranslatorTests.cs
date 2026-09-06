using PoeTradeAssistant.LiveSearch;
using Xunit;

namespace Poe2MarketScanner.App.Tests;

public class ChineseTranslatorTests
{
    [Fact]
    public void TranslatesCurrencyWithQuantityAndKeepsUnknownText()
    {
        var t = ChineseTranslator.Instance;
        Assert.Equal("神圣石", t.Translate("Divine Orb"));
        Assert.Equal("1×神圣石", t.Translate("1×Divine Orb"));
        Assert.Equal("Unknown Item XYZ", t.Translate("Unknown Item XYZ"));
    }

    [Fact]
    public void PreservesRolledNumbersAndLineBreaks()
    {
        var t = ChineseTranslator.Instance;
        Assert.Equal("+89 生命上限", t.Translate("+89 to maximum Life"));
        Assert.Equal("+(10-20) 生命上限", t.Translate("+(10-20) to maximum Life"));
        Assert.Equal("神圣石\nUnknown", t.Translate("Divine Orb\nUnknown"));
        // This upstream Chinese template has fewer placeholders. Never lose a value.
        Assert.Equal("Adds 10 to 20 Physical Damage", t.Translate("Adds 10 to 20 Physical Damage"));
    }

    [Fact]
    public void TranslatesPoe1ItemsCurrencyAndRolledStats()
    {
        var t = ChineseTranslator.Instance;
        Assert.Equal("奇塔弗之渴望", t.Translate("Kitava's Thirst", "poe1"));
        Assert.Equal("1×神圣石", t.Translate("1×Divine Orb", "poe1"));
        Assert.Equal("89 最大生命", t.Translate("+89 to maximum Life", "poe1"));
        Assert.Equal("Unknown Item XYZ", t.Translate("Unknown Item XYZ", "poe1"));
    }
}
