using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Poe2MarketScanner.Core.Ocr;

public static class OcrTextParser
{
    public static OcrParseResult ParseGoldCost(string? input)
    {
        var digits = new string((input ?? string.Empty).Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits) || !int.TryParse(digits, out var value))
        {
            return new OcrParseResult
            {
                Success = false,
                Status = "parse_failed"
            };
        }

        return new OcrParseResult
        {
            Success = true,
            Status = "ok",
            NormalizedText = value.ToString(CultureInfo.InvariantCulture),
            GoldCostValue = value
        };
    }

    public static OcrParseResult ParseRatio(string? input)
    {
        var normalized = (input ?? string.Empty)
            .Replace('\uFF1A', ':')
            .Replace('/', ':')
            .Replace('\uFF0E', '.')
            .Replace('\u3002', '.')
            .Replace('\u00B7', '.');

        normalized = Regex.Replace(normalized, @"\s+", string.Empty);
        normalized = Regex.Replace(normalized, @"[^0-9:.]", string.Empty);

        var parts = normalized.Split(':', System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 ||
            !TryParseRatioPart(parts[0], out var left) ||
            !TryParseRatioPart(parts[1], out var right))
        {
            return new OcrParseResult
            {
                Success = false,
                Status = "parse_failed"
            };
        }

        return new OcrParseResult
        {
            Success = true,
            Status = "ok",
            NormalizedText = $"{FormatRatioPart(left)}:{FormatRatioPart(right)}",
            RatioLeft = left,
            RatioRight = right
        };
    }

    private static bool TryParseRatioPart(string text, out decimal value)
    {
        return decimal.TryParse(
            text,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static string FormatRatioPart(decimal value)
    {
        return value.ToString("G29", CultureInfo.InvariantCulture);
    }
}
