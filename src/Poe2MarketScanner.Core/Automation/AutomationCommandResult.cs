namespace Poe2MarketScanner.Core.Automation;

public sealed class AutomationCommandResult
{
    public bool Success { get; init; }
    public string Step { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? ScreenshotPath { get; init; }
}
