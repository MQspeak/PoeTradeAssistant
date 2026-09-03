using Poe2MarketScanner.Core.Configuration;
using Xunit;

namespace Poe2MarketScanner.Core.Tests;

public sealed class AnchorVisualGeometryTests
{
    [Fact]
    public void GetVisualOriginFromMarkerCenter_ShouldPlaceMarkerCenterBackOntoAnchorPoint()
    {
        Assert.Equal(613, AnchorVisualGeometry.GetVisualLeft(620));
        Assert.Equal(395, AnchorVisualGeometry.GetVisualTop(410));
    }
}
