using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Poe2MarketScanner.Core.Automation;
using PoeTradeAssistant.ScannerIntegration;

namespace Poe2MarketScanner.App.Services;

public sealed class ProjectOutputJsonWriter : IQueryResultWriter
{
    private readonly string _projectRoot;
    private readonly string? _outputDirectory;
    private readonly Func<DateTimeOffset> _now;

    public ProjectOutputJsonWriter(string projectRoot, Func<DateTimeOffset>? now = null)
        : this(projectRoot, null, now)
    {
    }

    public ProjectOutputJsonWriter(string projectRoot, string? outputDirectory, Func<DateTimeOffset>? now = null)
    {
        _projectRoot = projectRoot;
        _outputDirectory = outputDirectory;
        _now = now ?? (() => DateTimeOffset.Now);
    }

    public string WriteSellQueryBatch(SellQueryBatchResult result)
    {
        var outputDirectory = ResolveOutputDirectory();
        Directory.CreateDirectory(outputDirectory);

        var fileName = $"{_now():yyyyMMdd-HHmmss}.json";
        var outputPath = Path.Combine(outputDirectory, fileName);
        var firstItem = result.Items.FirstOrDefault();
        var payload = new
        {
            购买用通货 = firstItem?.BuyCurrencyName ?? string.Empty,
            出售目标通货 = firstItem?.SellCurrencyName ?? string.Empty,
            当前交易对比例 = firstItem?.CurrentPairRatioNormalized ?? string.Empty,
            条目 = result.Items.Select(item => new
            {
                名字 = item.CurrencyName,
                金币消耗 = item.GoldCostNormalized,
                买入比例 = item.BuyRatioNormalized,
                卖出比例 = item.SellRatioNormalized
            })
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        File.WriteAllText(outputPath, json);
        WriteVersion2Document(result, outputDirectory, fileName);
        return outputPath;
    }

    private static void WriteVersion2Document(SellQueryBatchResult result, string outputDirectory, string legacyFileName)
    {
        var version2FileName = Path.GetFileNameWithoutExtension(legacyFileName) + ".v2.json";
        var version2Path = Path.Combine(outputDirectory, version2FileName);
        var document = PriceScanDocumentAdapter.FromScannerBatch(result);
        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        File.WriteAllText(version2Path, json);
    }

    private string ResolveOutputDirectory()
    {
        if (string.IsNullOrWhiteSpace(_outputDirectory))
        {
            return Path.Combine(_projectRoot, "output");
        }

        return Path.IsPathRooted(_outputDirectory)
            ? _outputDirectory
            : Path.GetFullPath(Path.Combine(_projectRoot, _outputDirectory));
    }

    public static string DiscoverProjectRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            var hasDesktop = Directory.Exists(Path.Combine(current, "desktop"));
            var hasDocs = Directory.Exists(Path.Combine(current, "docs"));
            if (hasDesktop && hasDocs)
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }

        return Directory.GetCurrentDirectory();
    }
}
