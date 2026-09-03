using Poe2MarketScanner.Core.Configuration;

namespace Poe2MarketScanner.App.Services;

public interface ISellQueryOcrReader
{
    SellQueryOcrResult Read(AppProfile profile);
}
