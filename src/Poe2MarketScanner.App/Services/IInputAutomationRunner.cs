using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Poe2MarketScanner.Core.Configuration;

namespace Poe2MarketScanner.App.Services;

public interface IInputAutomationRunner
{
    Task ClickAsync(double x, double y, CancellationToken cancellationToken);
    Task ClickWithModifiersAsync(double x, double y, IReadOnlyCollection<InputModifierKey> modifierKeys, CancellationToken cancellationToken);
    Task CtrlClickAsync(double x, double y, CancellationToken cancellationToken);
    Task SendSelectAllAsync(CancellationToken cancellationToken);
    Task SendBackspaceAsync(CancellationToken cancellationToken);
    Task PasteTextAsync(string text, CancellationToken cancellationToken);
    Task WaitAsync(int milliseconds, CancellationToken cancellationToken);
}

