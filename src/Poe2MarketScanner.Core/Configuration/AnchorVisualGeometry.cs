namespace Poe2MarketScanner.Core.Configuration;

public static class AnchorVisualGeometry
{
    public const string LegacyPanelTopLeftMode = "panelTopLeft";
    public const string MarkerCenterMode = "markerCenter";

    public const double MarkerCenterOffsetX = 7;
    public const double MarkerCenterOffsetY = 15;

    public static double GetVisualLeft(double anchorCenterX)
    {
        return anchorCenterX - MarkerCenterOffsetX;
    }

    public static double GetVisualTop(double anchorCenterY)
    {
        return anchorCenterY - MarkerCenterOffsetY;
    }

    public static void MigrateLegacyTopLeftToMarkerCenter(AnchorPoint anchor)
    {
        anchor.X += MarkerCenterOffsetX;
        anchor.Y += MarkerCenterOffsetY;
    }
}
