using System.Collections.Generic;
using System.Linq;

namespace Poe2MarketScanner.Core.Configuration;

public static class AppProfileNormalizer
{
    public static AppProfile Normalize(AppProfile? profile)
    {
        var defaults = AppProfileFactory.CreateDefault();
        if (profile is null)
        {
            return defaults;
        }

        profile.ProfileName = string.IsNullOrWhiteSpace(profile.ProfileName) ? defaults.ProfileName : profile.ProfileName;
        profile.PriceMode = string.IsNullOrWhiteSpace(profile.PriceMode) ? defaults.PriceMode : profile.PriceMode;
        profile.AnchorCoordinateMode = NormalizeAnchorCoordinateMode(profile.AnchorCoordinateMode);
        profile.OutputDirectory ??= defaults.OutputDirectory;
        profile.SelectedTradeModeKey = TradeModeCatalog.Resolve(profile.SelectedTradeModeKey).Key;
        profile.QueryList ??= defaults.QueryList;
        profile.Automation ??= defaults.Automation;
        profile.Ocr ??= defaults.Ocr;
        profile.Regions ??= new Dictionary<string, ScreenRegion>();
        profile.Anchors ??= new Dictionary<string, AnchorPoint>();

        foreach (var entry in defaults.Regions)
        {
            if (!profile.Regions.ContainsKey(entry.Key))
            {
                profile.Regions[entry.Key] = Clone(entry.Value);
            }
        }

        foreach (var entry in defaults.Anchors)
        {
            if (!profile.Anchors.ContainsKey(entry.Key))
            {
                profile.Anchors[entry.Key] = Clone(entry.Value);
            }
        }

        if (profile.AnchorCoordinateMode == AnchorVisualGeometry.LegacyPanelTopLeftMode)
        {
            foreach (var anchor in profile.Anchors.Values)
            {
                AnchorVisualGeometry.MigrateLegacyTopLeftToMarkerCenter(anchor);
            }

            profile.AnchorCoordinateMode = AnchorVisualGeometry.MarkerCenterMode;
        }

        profile.Ocr.RegionOverrides ??= new Dictionary<string, OcrRegionSettings>();
        foreach (var entry in defaults.Ocr.RegionOverrides)
        {
            if (!profile.Ocr.RegionOverrides.ContainsKey(entry.Key))
            {
                profile.Ocr.RegionOverrides[entry.Key] = Clone(entry.Value);
            }
        }

        return profile;
    }

    public static OcrRegionSettings ResolveOcrRegionSettings(AppProfile profile, string regionKey)
    {
        if (profile.Ocr.RegionOverrides.TryGetValue(regionKey, out var overrideSettings) && overrideSettings.Enabled)
        {
            return Clone(overrideSettings);
        }

        return new OcrRegionSettings
        {
            Enabled = false,
            Scale = profile.Ocr.Scale,
            Threshold = profile.Ocr.Threshold,
            EnableGrayscale = profile.Ocr.EnableGrayscale,
            EnableBinarization = profile.Ocr.EnableBinarization,
            EnableOtsuFallback = profile.Ocr.EnableOtsuFallback
        };
    }

    private static ScreenRegion Clone(ScreenRegion region)
    {
        return new ScreenRegion
        {
            Name = region.Name,
            X = region.X,
            Y = region.Y,
            Width = region.Width,
            Height = region.Height,
            Color = region.Color
        };
    }

    private static AnchorPoint Clone(AnchorPoint anchor)
    {
        return new AnchorPoint
        {
            Name = anchor.Name,
            X = anchor.X,
            Y = anchor.Y
        };
    }

    private static string NormalizeAnchorCoordinateMode(string? anchorCoordinateMode)
    {
        return string.Equals(anchorCoordinateMode, AnchorVisualGeometry.MarkerCenterMode, System.StringComparison.OrdinalIgnoreCase)
            ? AnchorVisualGeometry.MarkerCenterMode
            : AnchorVisualGeometry.LegacyPanelTopLeftMode;
    }

    private static OcrRegionSettings Clone(OcrRegionSettings settings)
    {
        return new OcrRegionSettings
        {
            Enabled = settings.Enabled,
            Scale = settings.Scale,
            Threshold = settings.Threshold,
            EnableGrayscale = settings.EnableGrayscale,
            EnableBinarization = settings.EnableBinarization,
            EnableOtsuFallback = settings.EnableOtsuFallback
        };
    }

}
