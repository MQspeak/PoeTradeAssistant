namespace Poe2MarketScanner.Core.Automation;

public interface IAutomationService
{
    AutomationCommandResult MoveMouseTo(string anchorName);
    AutomationCommandResult ClickAnchor(string anchorName);
    AutomationCommandResult DoubleClickAnchor(string anchorName);
    AutomationCommandResult PasteText(string text);
    AutomationCommandResult SendHotkey(params string[] keys);
    AutomationCommandResult CaptureRegion(string regionName);
    AutomationCommandResult Wait(int milliseconds);
    AutomationCommandResult ReadRegionText(string regionName);
}
