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

        var outputPath = Path.Combine(outputDirectory, $"{_now():yyyyMMdd-HHmmss}.v2.json");
        var document = PriceScanDocumentAdapter.FromScannerBatch(result);
        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        File.WriteAllText(outputPath, json);
        return outputPath;
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
