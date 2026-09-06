using System.Text.Json;
using System.Text.RegularExpressions;

namespace PoeTradeAssistant.LiveSearch;

/// <summary>Display-only PoE dictionary translation; original hits remain untouched.</summary>
public sealed class ChineseTranslator
{
    public static ChineseTranslator Instance { get; } = new();
    private readonly IReadOnlyDictionary<string, Dictionary<string, string>> _entries;
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.CultureInvariant);
    private static readonly Regex Numbers = new(@"\(\s*[+-]?\d+(?:\.\d+)?\s*[—–-]\s*[+-]?\d+(?:\.\d+)?\s*\)|\d+(?:\.\d+)?\s*[—–-]\s*\d+(?:\.\d+)?|\d+(?:\.\d+)?", RegexOptions.CultureInvariant);

    private ChineseTranslator()
    {
        _entries = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["poe1"] = Load("poe1-en-cn.json"),
            ["poe2"] = Load("poe2-en-cn.json")
        };
    }

    private static Dictionary<string, string> Load(string fileName)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        using var stream = typeof(ChineseTranslator).Assembly.GetManifestResourceStream($"PoeTradeAssistant.LiveSearch.Data.{fileName}")!;
        using var json = JsonDocument.Parse(stream);
        foreach (var entry in json.RootElement.EnumerateObject()) entries.TryAdd(entry.Name, entry.Value.GetString()!);
        return entries;
    }

    public string Translate(string text, string game = "poe2")
    {
        if (!_entries.TryGetValue(game, out var entries)) return text;
        return string.Join("\n", text.Replace("\r\n", "\n").Split('\n').Select(line => TranslateLine(line, entries)));
    }

    private static string TranslateLine(string original, IReadOnlyDictionary<string, string> entries)
    {
        var line = Spaces.Replace(original, " ").Trim();
        if (entries.TryGetValue(line, out var exact) && !exact.Contains('#')) return exact;
        var values = Numbers.Matches(line).Select(x => x.Value).ToArray();
        var template = Numbers.Replace(line, "#");
        if (entries.TryGetValue(template, out var translated) && translated.Count(x => x == '#') == values.Length)
        {
            var index = 0;
            return Regex.Replace(translated, "#", _ => values[index++]);
        }
        // Price quantities and property labels are translated without altering their values.
        var price = Regex.Match(line, @"^(\d+(?:\.\d+)?\s*[×x]?\s*)(.+)$");
        if (price.Success && entries.TryGetValue(price.Groups[2].Value, out var currency) && !currency.Contains('#'))
            return price.Groups[1].Value + currency;
        var colon = line.IndexOf(':');
        if (colon > 0 && entries.TryGetValue(line[..colon], out var label) && !label.Contains('#'))
            return label + "：" + line[(colon + 1)..].TrimStart();
        return original;
    }
}
