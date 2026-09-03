using Poe2MarketScanner.App.Services;
using Xunit;

namespace Poe2MarketScanner.App.Tests;

public sealed class SellQueryOcrReaderTests
{
    [Fact]
    public void Read_ShouldReturnParsedFields()
    {
        var result = new SellQueryOcrResult
        {
            GoldCostRaw = "250",
            GoldCostNormalized = "250",
            RatioRaw = "575 : 1",
            RatioNormalized = "575:1",
            Status = "ok"
        };

        Assert.Equal("250", result.GoldCostNormalized);
        Assert.Equal("575:1", result.RatioNormalized);
    }
}
