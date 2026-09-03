using System.IO;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Poe2MarketScanner.Core.Configuration;

public sealed class JsonProfileStorageService : IProfileStorageService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly Func<ProfileScreenMetrics> _screenMetricsProvider;

    public JsonProfileStorageService(Func<ProfileScreenMetrics>? screenMetricsProvider = null)
    {
        _screenMetricsProvider = screenMetricsProvider ?? (() => ProfileScreenMetrics.Default);
    }

    public AppProfile Load(string path)
    {
        if (!File.Exists(path))
        {
            return AppProfileNormalizer.Normalize(AppProfileFactory.CreateDefault());
        }

        var json = File.ReadAllText(path);
        var persisted = JsonSerializer.Deserialize<PersistedAppProfile>(json, Options);
        var profile = PersistedAppProfileMapper.ToRuntimeProfile(
            persisted,
            _screenMetricsProvider());
        return AppProfileNormalizer.Normalize(profile);
    }

    public void Save(string path, AppProfile profile)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var persisted = PersistedAppProfileMapper.ToPersistedProfile(
            AppProfileNormalizer.Normalize(profile),
            _screenMetricsProvider());
        File.WriteAllText(path, JsonSerializer.Serialize(persisted, Options));
    }

    private sealed class PersistedAppProfile
    {
        public string ProfileName { get; set; } = "default";
        public string PriceMode { get; set; } = "sell";
        public string AnchorCoordinateMode { get; set; } = string.Empty;
        public string CoordinateSpaceMode { get; set; } = "absolute";
        public ProfileScreenMetrics? ReferenceScreen { get; set; }
        public bool UseTraditionalChinese { get; set; }
        public string OutputDirectory { get; set; } = string.Empty;
        public string SelectedTradeModeKey { get; set; } = TradeModeCatalog.All[0].Key;
        public Dictionary<string, PersistedScreenRegion> Regions { get; set; } = new();
        public Dictionary<string, PersistedAnchorPoint> Anchors { get; set; } = new();
        public OcrSettings Ocr { get; set; } = new();
        public QueryListSettings QueryList { get; set; } = new();
        public AutomationSettings Automation { get; set; } = new();
    }

    private sealed class PersistedScreenRegion
    {
        public string Name { get; set; } = string.Empty;
        public double? X { get; set; }
        public double? Y { get; set; }
        public double? Width { get; set; }
        public double? Height { get; set; }
        public double? RelativeX { get; set; }
        public double? RelativeY { get; set; }
        public double? RelativeWidth { get; set; }
        public double? RelativeHeight { get; set; }
        public string Color { get; set; } = "#FFFFFF";
    }

    private sealed class PersistedAnchorPoint
    {
        public string Name { get; set; } = string.Empty;
        public double? X { get; set; }
        public double? Y { get; set; }
        public double? RelativeX { get; set; }
        public double? RelativeY { get; set; }
    }

    private static class PersistedAppProfileMapper
    {
        private const string ScreenRelativeMode = "screenRelative";

        public static PersistedAppProfile ToPersistedProfile(AppProfile profile, ProfileScreenMetrics screen)
        {
            var safeWidth = screen.SafeWidth;
            var safeHeight = screen.SafeHeight;

            return new PersistedAppProfile
            {
                ProfileName = profile.ProfileName,
                PriceMode = profile.PriceMode,
                AnchorCoordinateMode = profile.AnchorCoordinateMode,
                CoordinateSpaceMode = ScreenRelativeMode,
                ReferenceScreen = screen,
                UseTraditionalChinese = profile.UseTraditionalChinese,
                OutputDirectory = profile.OutputDirectory,
                SelectedTradeModeKey = profile.SelectedTradeModeKey,
                Regions = profile.Regions.ToDictionary(
                    static item => item.Key,
                    item => new PersistedScreenRegion
                    {
                        Name = item.Value.Name,
                        RelativeX = (item.Value.X - screen.Left) / safeWidth,
                        RelativeY = (item.Value.Y - screen.Top) / safeHeight,
                        RelativeWidth = item.Value.Width / safeWidth,
                        RelativeHeight = item.Value.Height / safeHeight,
                        Color = item.Value.Color
                    }),
                Anchors = profile.Anchors.ToDictionary(
                    static item => item.Key,
                    item => new PersistedAnchorPoint
                    {
                        Name = item.Value.Name,
                        RelativeX = (item.Value.X - screen.Left) / safeWidth,
                        RelativeY = (item.Value.Y - screen.Top) / safeHeight
                    }),
                Ocr = profile.Ocr,
                QueryList = profile.QueryList,
                Automation = profile.Automation
            };
        }

        public static AppProfile ToRuntimeProfile(PersistedAppProfile? persisted, ProfileScreenMetrics screen)
        {
            if (persisted is null)
            {
                return AppProfileFactory.CreateDefault();
            }

            var safeWidth = screen.SafeWidth;
            var safeHeight = screen.SafeHeight;
            var useRelativeCoordinates = string.Equals(
                persisted.CoordinateSpaceMode,
                ScreenRelativeMode,
                System.StringComparison.OrdinalIgnoreCase);

            return new AppProfile
            {
                ProfileName = persisted.ProfileName,
                PriceMode = persisted.PriceMode,
                AnchorCoordinateMode = persisted.AnchorCoordinateMode,
                UseTraditionalChinese = persisted.UseTraditionalChinese,
                OutputDirectory = persisted.OutputDirectory,
                SelectedTradeModeKey = persisted.SelectedTradeModeKey,
                Regions = persisted.Regions.ToDictionary(
                    static item => item.Key,
                    item => new ScreenRegion
                    {
                        Name = item.Value.Name,
                        X = ResolveRegionX(item.Value, useRelativeCoordinates, screen, safeWidth),
                        Y = ResolveRegionY(item.Value, useRelativeCoordinates, screen, safeHeight),
                        Width = ResolveRegionWidth(item.Value, useRelativeCoordinates, safeWidth),
                        Height = ResolveRegionHeight(item.Value, useRelativeCoordinates, safeHeight),
                        Color = item.Value.Color
                    }),
                Anchors = persisted.Anchors.ToDictionary(
                    static item => item.Key,
                    item => new AnchorPoint
                    {
                        Name = item.Value.Name,
                        X = ResolveAnchorX(item.Value, useRelativeCoordinates, screen, safeWidth),
                        Y = ResolveAnchorY(item.Value, useRelativeCoordinates, screen, safeHeight)
                    }),
                Ocr = persisted.Ocr,
                QueryList = persisted.QueryList,
                Automation = persisted.Automation
            };
        }

        private static double ResolveRegionX(
            PersistedScreenRegion region,
            bool useRelativeCoordinates,
            ProfileScreenMetrics screen,
            double safeWidth)
        {
            if (useRelativeCoordinates && region.RelativeX.HasValue)
            {
                return screen.Left + (region.RelativeX.Value * safeWidth);
            }

            return region.X ?? 0;
        }

        private static double ResolveRegionY(
            PersistedScreenRegion region,
            bool useRelativeCoordinates,
            ProfileScreenMetrics screen,
            double safeHeight)
        {
            if (useRelativeCoordinates && region.RelativeY.HasValue)
            {
                return screen.Top + (region.RelativeY.Value * safeHeight);
            }

            return region.Y ?? 0;
        }

        private static double ResolveRegionWidth(PersistedScreenRegion region, bool useRelativeCoordinates, double safeWidth)
        {
            if (useRelativeCoordinates && region.RelativeWidth.HasValue)
            {
                return region.RelativeWidth.Value * safeWidth;
            }

            return region.Width ?? 0;
        }

        private static double ResolveRegionHeight(PersistedScreenRegion region, bool useRelativeCoordinates, double safeHeight)
        {
            if (useRelativeCoordinates && region.RelativeHeight.HasValue)
            {
                return region.RelativeHeight.Value * safeHeight;
            }

            return region.Height ?? 0;
        }

        private static double ResolveAnchorX(
            PersistedAnchorPoint anchor,
            bool useRelativeCoordinates,
            ProfileScreenMetrics screen,
            double safeWidth)
        {
            if (useRelativeCoordinates && anchor.RelativeX.HasValue)
            {
                return screen.Left + (anchor.RelativeX.Value * safeWidth);
            }

            return anchor.X ?? 0;
        }

        private static double ResolveAnchorY(
            PersistedAnchorPoint anchor,
            bool useRelativeCoordinates,
            ProfileScreenMetrics screen,
            double safeHeight)
        {
            if (useRelativeCoordinates && anchor.RelativeY.HasValue)
            {
                return screen.Top + (anchor.RelativeY.Value * safeHeight);
            }

            return anchor.Y ?? 0;
        }
    }
}
