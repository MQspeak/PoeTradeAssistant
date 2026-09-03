using Poe2MarketScanner.Core.Automation;
using Xunit;

namespace Poe2MarketScanner.Core.Tests;

public sealed class SellQueryResultWriterContractTests
{
    [Fact]
    public void SellQueryBatchResult_ShouldDefaultToSellMode()
    {
        var result = new SellQueryBatchResult();

        Assert.Equal("sell", result.Mode);
        Assert.NotNull(result.Items);
    }
}
