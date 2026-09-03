using System.IO;
using System.Text.Json;
using Poe2MarketScanner.Core.Configuration;
using Xunit;

namespace Poe2MarketScanner.Core.Tests;

public sealed class ProfileStorageTests
{
    [Fact]
    public void SaveAndLoad_ShouldRoundTripEditedCoordinates()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            var service = new JsonProfileStorageService();
            var profile = AppProfileFactory.CreateDefault();
            profile.Regions["goldCost"].X = 1001;
            profile.Anchors["searchTarget"].Y = 222;
            profile.UseTraditionalChinese = true;
            profile.OutputDirectory = @"D:\exports";

            service.Save(tempFile, profile);
            var loaded = service.Load(tempFile);

            Assert.Equal(1001, loaded.Regions["goldCost"].X);
            Assert.Equal(222, loaded.Anchors["searchTarget"].Y);
            Assert.True(loaded.UseTraditionalChinese);
            Assert.Equal(@"D:\exports", loaded.OutputDirectory);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_ShouldPreserveSavedCoordinates_WhileAddingMissingAutomationAnchors()
    {
        var tempFile = Path.GetTempFileName();
        var selectedTradeModeKey = TradeModeCatalog.All[^1].Key;

        try
        {
            File.WriteAllText(tempFile, $@"
{{
  ""profileName"": ""default"",
  ""priceMode"": ""sell"",
  ""selectedTradeModeKey"": ""{selectedTradeModeKey}"",
  ""useTraditionalChinese"": true,
  ""outputDirectory"": ""D:\\exports"",
  ""regions"": {{
    ""goldCost"": {{
      ""name"": ""gold"",
      ""x"": 4321,
      ""y"": 5678,
      ""width"": 111,
      ""height"": 222,
      ""color"": ""#FFB347""
    }}
  }},
  ""anchors"": {{
    ""leftCurrency"": {{
      ""name"": ""left"",
      ""x"": 12,
      ""y"": 34
    }}
  }},
  ""ocr"": {{
    ""engine"": ""paddleocr"",
    ""detectDigitsOnly"": true,
    ""scale"": 2.0,
    ""threshold"": 160,
    ""enableGrayscale"": true,
    ""enableBinarization"": true
  }},
  ""queryList"": {{
    ""sourceFile"": ""currencies.txt"",
    ""items"": []
  }},
  ""automation"": {{
    ""reserved"": true,
    ""commonDelayMs"": 650,
    ""clickDelayMs"": 120,
    ""inputDelayMs"": 80
  }}
}}");

            var service = new JsonProfileStorageService();
            var loaded = service.Load(tempFile);

            Assert.Equal(4321, loaded.Regions["goldCost"].X);
            Assert.Equal(5678, loaded.Regions["goldCost"].Y);
            Assert.Equal(19, loaded.Anchors["leftCurrency"].X);
            Assert.Equal(49, loaded.Anchors["leftCurrency"].Y);
            Assert.Equal(650, loaded.Automation.CommonDelayMs);
            Assert.Equal(selectedTradeModeKey, loaded.SelectedTradeModeKey);
            Assert.Equal(AnchorVisualGeometry.MarkerCenterMode, loaded.AnchorCoordinateMode);
            Assert.True(loaded.UseTraditionalChinese);
            Assert.Equal(@"D:\exports", loaded.OutputDirectory);
            Assert.True(loaded.Regions.ContainsKey("ratio"));
            Assert.True(loaded.Anchors.ContainsKey("searchTarget"));
            Assert.True(loaded.Anchors.ContainsKey("leftInput"));
            Assert.True(loaded.Anchors.ContainsKey("rightInput"));
            Assert.True(loaded.Anchors.ContainsKey("idle"));
            Assert.NotNull(loaded.Ocr.RegionOverrides);
            Assert.True(loaded.Ocr.RegionOverrides.ContainsKey("goldCost"));
            Assert.True(loaded.Ocr.RegionOverrides.ContainsKey("ratio"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_ShouldThrowJsonException_WhenProfileJsonIsInvalid()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            File.WriteAllText(tempFile, "{ invalid json");
            var service = new JsonProfileStorageService();

            Assert.Throws<JsonException>(() => service.Load(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }


}

