using Poe2MarketScanner.Core.Configuration;

namespace Poe2MarketScanner.App.Services;

public interface ISellQueryOcrReader
{
    SellQueryOcrResult Read(AppProfile profile);
    SellQueryOcrResult Read(AppProfile profile, bool recognizeGoldCost, bool recognizeRatio) => Read(profile);
}
