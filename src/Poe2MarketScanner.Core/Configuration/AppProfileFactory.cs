using System.Collections.Generic;

namespace Poe2MarketScanner.Core.Configuration;

public static class AppProfileFactory
{
    public static AppProfile CreateDefault()
    {
        return new AppProfile
        {
            AnchorCoordinateMode = AnchorVisualGeometry.MarkerCenterMode,
            UseTraditionalChinese = false,
            SelectedTradeModeKey = TradeModeCatalog.All[0].Key,
            Regions = new Dictionary<string, ScreenRegion>
            {
                ["goldCost"] = new()
                {
                    Name = "金币消耗提取框",
                    X = 860,
                    Y = 470,
                    Width = 100,
                    Height = 100,
                    Color = "#FFB347"
                },
                ["ratio"] = new()
                {
                    Name = "比例提取框",
                    X = 960,
                    Y = 370,
                    Width = 100,
                    Height = 100,
                    Color = "#6EE7B7"
                }
            },
            Anchors = new Dictionary<string, AnchorPoint>
            {
                ["leftCurrency"] = new() { Name = "左边通货位置", X = 620, Y = 410 },
                ["rightCurrency"] = new() { Name = "右边通货位置", X = 1290, Y = 410 },
                ["allTab"] = new() { Name = "全部标签位置", X = 1110, Y = 260 },
                ["search"] = new() { Name = "搜索位置", X = 1180, Y = 320 },
                ["searchTarget"] = new() { Name = "搜索目标位置", X = 980, Y = 510 },
                ["leftInput"] = new() { Name = "左边输入位置", X = 700, Y = 470 },
                ["rightInput"] = new() { Name = "右边输入位置", X = 1380, Y = 470 },
                ["idle"] = new() { Name = "滞空锚点", X = 1500, Y = 950 }
            },
            Ocr = new OcrSettings
            {
                Engine = "paddleocr",
                DetectDigitsOnly = true,
                Scale = 2.0,
                Threshold = 160,
                EnableGrayscale = true,
                EnableBinarization = true,
                EnableOtsuFallback = true,
                RegionOverrides = new Dictionary<string, OcrRegionSettings>
                {
                    ["goldCost"] = new()
                    {
                        Enabled = true,
                        Scale = 3.0,
                        Threshold = 120,
                        EnableGrayscale = true,
                        EnableBinarization = true,
                        EnableOtsuFallback = true
                    },
                    ["ratio"] = new()
                    {
                        Enabled = false,
                        Scale = 2.0,
                        Threshold = 160,
                        EnableGrayscale = true,
                        EnableBinarization = true,
                        EnableOtsuFallback = true
                    }
                }
            },
            Automation = new AutomationSettings
            {
                Reserved = true,
                RecognizeGoldCost = true,
                CommonDelayMs = 500,
                ClickDelayMs = 120,
                InputDelayMs = 80
            }
        };
    }
}
