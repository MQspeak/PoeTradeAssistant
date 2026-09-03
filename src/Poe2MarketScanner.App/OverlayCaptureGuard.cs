namespace Poe2MarketScanner.App;

public interface IOverlayCaptureHost
{
    bool IsOverlayVisible { get; }
    bool IsOverlayEditing { get; }
    void ShowOverlay();
    void HideOverlay();
    void SetOverlayEditing(bool editing);
}

public sealed class OverlayCaptureGuard
{
    private readonly IOverlayCaptureHost _host;
    private bool _restoreVisible;
    private bool _restoreEditing;

    public OverlayCaptureGuard(IOverlayCaptureHost host)
    {
        _host = host;
    }

    public void BeginCapture()
    {
        _restoreVisible = _host.IsOverlayVisible;
        _restoreEditing = _host.IsOverlayEditing;

        if (_restoreVisible)
        {
            _host.HideOverlay();
        }
    }

    public void Restore()
    {
        if (!_restoreVisible)
        {
            return;
        }

        _host.ShowOverlay();
        _host.SetOverlayEditing(_restoreEditing);
    }
}
