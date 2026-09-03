namespace Poe2MarketScanner.Core.Automation;

public interface IQueryResultWriter
{
    string WriteSellQueryBatch(SellQueryBatchResult result);
}
