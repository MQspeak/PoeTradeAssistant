namespace Poe2MarketScanner.Core.Configuration;

public interface IProfileStorageService
{
    AppProfile Load(string path);
    void Save(string path, AppProfile profile);
}
