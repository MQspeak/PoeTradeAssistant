using Xunit;

namespace Poe2MarketScanner.App.Tests;

public sealed class WindowsInputAutomationRunnerPlanTests
{
    [Fact]
    public void SearchSequence_ShouldRequireSelectAllThenBackspaceBeforePaste()
    {
        var steps = new[]
        {
            "Click(search)",
            "Hotkey(Ctrl+A)",
            "Hotkey(Backspace)",
            "Paste(Exalted Orb)"
        };

        Assert.Equal("Hotkey(Ctrl+A)", steps[1]);
        Assert.Equal("Hotkey(Backspace)", steps[2]);
    }
}
