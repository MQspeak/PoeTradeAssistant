namespace Poe2MarketScanner.Core.Ocr;

public interface IOcrEngine
{
    string Name { get; }
    OcrParseResult ParseGoldCost(string rawText);
    OcrParseResult ParseRatio(string rawText);
}
