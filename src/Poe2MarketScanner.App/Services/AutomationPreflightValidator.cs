using System;
using System.Collections.Generic;
using Poe2MarketScanner.Core.Configuration;

namespace Poe2MarketScanner.App.Services;

public static class AutomationPreflightValidator
{
    private static readonly string[] RequiredAnchorKeys =
    {
        "leftCurrency",
        "rightCurrency",
        "allTab",
        "search",
        "searchTarget",
        "leftInput",
        "rightInput",
        "idle"
    };

    private static readonly string[] NewAnchorKeys =
    {
        "leftInput",
        "rightInput",
        "idle"
    };

    public static string? Validate(AppProfile profile)
    {
        foreach (var key in RequiredAnchorKeys)
        {
            if (!profile.Anchors.ContainsKey(key))
            {
                return $"缺少锚点配置：{key}";
            }
        }

        var defaults = AppProfileFactory.CreateDefault();
        var hasCustomizedLegacyAnchors = HasCustomizedLegacyAnchors(profile, defaults);
        if (!hasCustomizedLegacyAnchors)
        {
            return null;
        }

        var suspiciousAnchors = new List<string>();
        foreach (var key in NewAnchorKeys)
        {
            var current = profile.Anchors[key];
            var fallback = defaults.Anchors[key];
            if (Math.Abs(current.X - fallback.X) < 0.01 && Math.Abs(current.Y - fallback.Y) < 0.01)
            {
                suspiciousAnchors.Add(current.Name);
            }
        }

        if (suspiciousAnchors.Count == 0)
        {
            return null;
        }

        return $"检测到新锚点仍是默认位置，请先标定后再开始自动查询：{string.Join("、", suspiciousAnchors)}";
    }

    public static string? ValidateStartRequirements(AppProfile profile, int queryItemCount)
    {
        if (queryItemCount <= 0)
        {
            return "当前通货列表为空，请先导入通货列表后再开始自动查询。";
        }

        if (string.IsNullOrWhiteSpace(profile.OutputDirectory))
        {
            return "尚未设置输出目录，请先选择输出目录后再开始自动查询。";
        }

        return null;
    }

    private static bool HasCustomizedLegacyAnchors(AppProfile profile, AppProfile defaults)
    {
        foreach (var pair in defaults.Anchors)
        {
            if (Array.IndexOf(NewAnchorKeys, pair.Key) >= 0)
            {
                continue;
            }

            if (!profile.Anchors.TryGetValue(pair.Key, out var current))
            {
                continue;
            }

            if (Math.Abs(current.X - pair.Value.X) > 0.01 || Math.Abs(current.Y - pair.Value.Y) > 0.01)
            {
                return true;
            }
        }

        return false;
    }
}
