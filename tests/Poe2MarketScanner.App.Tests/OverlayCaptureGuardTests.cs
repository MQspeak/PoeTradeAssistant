using Poe2MarketScanner.App;
using Xunit;

namespace Poe2MarketScanner.App.Tests;

public sealed class OverlayCaptureGuardTests
{
    [Fact]
    public void BeginCapture_ShouldHideVisibleOverlay_AndRestorePreviousEditingState()
    {
        var host = new FakeOverlayCaptureHost
        {
            IsOverlayVisible = true,
            IsOverlayEditing = true
        };
        var guard = new OverlayCaptureGuard(host);

        guard.BeginCapture();

        Assert.False(host.IsOverlayVisible);
        Assert.Equal(1, host.HideCalls);

        guard.Restore();

        Assert.True(host.IsOverlayVisible);
        Assert.True(host.IsOverlayEditing);
        Assert.Equal(1, host.ShowCalls);
        Assert.Equal(1, host.SetEditingCalls);
    }

    [Fact]
    public void BeginCapture_ShouldDoNothing_WhenOverlayIsAlreadyHidden()
    {
        var host = new FakeOverlayCaptureHost
        {
            IsOverlayVisible = false,
            IsOverlayEditing = false
        };
        var guard = new OverlayCaptureGuard(host);

        guard.BeginCapture();
        guard.Restore();

        Assert.Equal(0, host.HideCalls);
        Assert.Equal(0, host.ShowCalls);
        Assert.Equal(0, host.SetEditingCalls);
    }

    private sealed class FakeOverlayCaptureHost : IOverlayCaptureHost
    {
        public bool IsOverlayVisible { get; set; }
        public bool IsOverlayEditing { get; set; }
        public int HideCalls { get; private set; }
        public int ShowCalls { get; private set; }
        public int SetEditingCalls { get; private set; }

        public void HideOverlay()
        {
            HideCalls += 1;
            IsOverlayVisible = false;
        }

        public void ShowOverlay()
        {
            ShowCalls += 1;
            IsOverlayVisible = true;
        }

        public void SetOverlayEditing(bool editing)
        {
            SetEditingCalls += 1;
            IsOverlayEditing = editing;
        }
    }
}
