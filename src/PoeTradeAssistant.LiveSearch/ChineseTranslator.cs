using System.Text.Json;
using System.Text.RegularExpressions;

namespace PoeTradeAssistant.LiveSearch;

/// <summary>Display-only PoE2 dictionary translation; original hits remain untouched.</summary>
public sealed class ChineseTranslator
{
    public static ChineseTranslator Instance { get; } = new();
    private readonly Dictionary<string, string> _entries = new(StringComparer.Ordinal);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.CultureInvariant);
    private static readonly Regex Numbers = new(@"\(\s*[+-]?\d+(?:\.\d+)?\s*[—–-]\s*[+-]?\d+(?:\.\d+)?\s*\)|\d+(?:\.\d+)?\s*[—–-]\s*\d+(?:\.\d+)?|\d+(?:\.\d+)?", RegexOptions.CultureInvariant);

    private ChineseTranslator()
    {
        using var stream = typeof(ChineseTranslator).Assembly.GetManifestResourceStream("PoeTradeAssistant.LiveSearch.Data.poe2-en-cn.json")!;
        using var json = JsonDocument.Parse(stream);
        foreach (var entry in json.RootElement.EnumerateObject()) _entries.TryAdd(entry.Name, entry.Value.GetString()!);
    }

    public string Translate(string text) => string.Join("\n", text.Replace("\r\n", "\n").Split('\n').Select(TranslateLine));

    private string TranslateLine(string original)
    {
        var line = Spaces.Replace(original, " ").Trim();
        if (_entries.TryGetValue(line, out var exact) && !exact.Contains('#')) return exact;
        var values = Numbers.Matches(line).Select(x => x.Value).ToArray();
        var template = Numbers.Replace(line, "#");
        if (_entries.TryGetValue(template, out var translated) && translated.Count(x => x == '#') == values.Length)
        {
            var index = 0;
            return Regex.Replace(translated, "#", _ => values[index++]);
        }
        // Price quantities and property labels are translated without altering their values.
        var price = Regex.Match(line, @"^(\d+(?:\.\d+)?\s*[×x]?\s*)(.+)$");
        if (price.Success && _entries.TryGetValue(price.Groups[2].Value, out var currency) && !currency.Contains('#'))
            return price.Groups[1].Value + currency;
        var colon = line.IndexOf(':');
        if (colon > 0 && _entries.TryGetValue(line[..colon], out var label) && !label.Contains('#'))
            return label + "：" + line[(colon + 1)..].TrimStart();
        return original;
    }
}
