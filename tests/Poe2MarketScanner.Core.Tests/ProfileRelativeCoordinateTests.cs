using System.IO;
using Poe2MarketScanner.Core.Configuration;
using Xunit;

namespace Poe2MarketScanner.Core.Tests;

public sealed class ProfileRelativeCoordinateTests
{
    [Fact]
    public void Save_ShouldPersistScreenRelativeCoordinates()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            var service = new JsonProfileStorageService(() => new ProfileScreenMetrics(0, 0, 2000, 1000));
            var profile = AppProfileFactory.CreateDefault();
            profile.Regions["goldCost"].X = 1000;
            profile.Regions["goldCost"].Y = 250;
            profile.Regions["goldCost"].Width = 500;
            profile.Regions["goldCost"].Height = 200;
            profile.Anchors["searchTarget"].X = 1500;
            profile.Anchors["searchTarget"].Y = 750;

            service.Save(tempFile, profile);
            var json = File.ReadAllText(tempFile);

            Assert.Contains(@"""coordinateSpaceMode"": ""screenRelative""", json);
            Assert.Contains(@"""relativeX"": 0.5", json);
            Assert.Contains(@"""relativeY"": 0.25", json);
            Assert.Contains(@"""relativeWidth"": 0.25", json);
            Assert.Contains(@"""relativeHeight"": 0.2", json);
            Assert.Contains(@"""relativeX"": 0.75", json);
            Assert.Contains(@"""relativeY"": 0.75", json);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_ShouldConvertRelativeCoordinatesUsingCurrentVirtualScreen()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            File.WriteAllText(tempFile, @"
{
  ""profileName"": ""default"",
  ""priceMode"": ""sell"",
  ""coordinateSpaceMode"": ""screenRelative"",
  ""referenceScreen"": {
    ""left"": 0,
    ""top"": 0,
    ""width"": 1920,
    ""height"": 1080
  },
  ""regions"": {
    ""goldCost"": {
      ""name"": ""gold"",
      ""relativeX"": 0.5,
      ""relativeY"": 0.25,
      ""relativeWidth"": 0.25,
      ""relativeHeight"": 0.1,
      ""color"": ""#FFB347""
    }
  },
  ""anchors"": {
    ""leftCurrency"": {
      ""name"": ""left"",
      ""relativeX"": 0.25,
      ""relativeY"": 0.75
    }
  },
  ""ocr"": {
    ""engine"": ""paddleocr"",
    ""detectDigitsOnly"": true,
    ""scale"": 2.0,
    ""threshold"": 160,
    ""enableGrayscale"": true,
    ""enableBinarization"": true
  },
  ""queryList"": {
    ""sourceFile"": ""currencies.txt"",
    ""items"": []
  },
  ""automation"": {
    ""reserved"": true,
    ""commonDelayMs"": 500,
    ""clickDelayMs"": 120,
    ""inputDelayMs"": 80
  }
}");

            var service = new JsonProfileStorageService(() => new ProfileScreenMetrics(-1280, 0, 3840, 2160));
            var loaded = service.Load(tempFile);

            Assert.Equal(640, loaded.Regions["goldCost"].X);
            Assert.Equal(540, loaded.Regions["goldCost"].Y);
            Assert.Equal(960, loaded.Regions["goldCost"].Width);
            Assert.Equal(216, loaded.Regions["goldCost"].Height);
            Assert.Equal(-313, loaded.Anchors["leftCurrency"].X);
            Assert.Equal(1635, loaded.Anchors["leftCurrency"].Y);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
