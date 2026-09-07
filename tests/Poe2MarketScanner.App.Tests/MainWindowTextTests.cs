using System;
using System.IO;
using System.Linq;
using Poe2MarketScanner.App.Services;
using PoeTradeAssistant.Contracts.MarketScan;
using Xunit;

namespace Poe2MarketScanner.App.Tests;

public sealed class MainWindowTextTests
{
    [Fact]
    public void MainWindow_ShouldContainReadableRecognizeGoldLabel()
    {
        var root = FindRepositoryRoot();
        var mainWindowXaml = File.ReadAllText(Path.Combine(root, "src", "Poe2MarketScanner.App", "MainWindow.xaml"));

        Assert.Contains(">是否识别金币</TextBlock>", mainWindowXaml);
        Assert.Contains("Header=\"标的套利\"", mainWindowXaml);
        Assert.DoesNotContain("WebView2", mainWindowXaml);
        Assert.Contains("CornerRadius=\"8,8,8,8\"", mainWindowXaml);
        Assert.Contains("<StackPanel IsItemsHost=\"True\" Orientation=\"Vertical\" />", mainWindowXaml);
    }

    [Fact]
    public void CalculatorComboBoxes_ShouldNotSynchronizeSharedCollectionSelections()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "src", "Poe2MarketScanner.App", "App.xaml"));

        Assert.Contains("<Setter Property=\"IsSynchronizedWithCurrentItem\" Value=\"False\" />", appXaml);
        Assert.Contains("<Setter Property=\"HorizontalAlignment\" Value=\"Stretch\" />", appXaml);
        Assert.Contains("Width=\"{Binding ActualWidth, ElementName=ToggleButton}\"", appXaml);
        Assert.Contains("<Setter Property=\"HorizontalContentAlignment\" Value=\"Stretch\" />", appXaml);
        Assert.Contains("HorizontalContentAlignment=\"Stretch\"", appXaml);
    }

    [Fact]
    public void NativeCalculator_ShouldImportVersion2ScanAndCalculateTarget()
    {
        var calculator = new NativeCalculatorViewModel(Path.Combine(Path.GetTempPath(), $"poe-calculator-{Guid.NewGuid():N}.json"));
        var message = calculator.ImportScanDocument(new PriceScanDocument
        {
            Pair = new TradePairSnapshot
            {
                BuyCurrency = "D",
                SellCurrency = "C",
                CurrentRatio = new NormalizedRatio { Left = 10m, Right = 1m, RightPerLeft = 0.1m }
            },
            Items = new[]
            {
                new PriceScanItem
                {
                    Name = "测试标的",
                    GoldCost = "20",
                    BuyRatio = new NormalizedRatio { Left = 2m, Right = 1m, RightPerLeft = 0.5m },
                    SellRatio = new NormalizedRatio { Left = 2m, Right = 1m, RightPerLeft = 0.5m },
                    Status = "ok"
                }
            }
        });

        Assert.Contains("已导入 1 条", message);
        Assert.Single(calculator.Pairs);
        Assert.Equal("C", calculator.SelectedPair!.BaseItem!.Name);
        Assert.Equal("D", calculator.SelectedPair.QuoteItem!.Name);
        Assert.Single(calculator.Targets);
        Assert.Equal("测试标的", calculator.Targets[0].TargetItem!.Name);
        Assert.Equal("2", calculator.Targets[0].BuyPriceText);
        Assert.Equal("2/1", calculator.Targets[0].SellPriceText);
        Assert.Contains("%", calculator.Targets[0].RoiText);
    }

    [Fact]
    public void NativeCalculator_ShouldKeepExistingPairRate_WhenScanSkippedRatioRecognition()
    {
        var calculator = new NativeCalculatorViewModel(Path.Combine(Path.GetTempPath(), $"poe-calculator-{Guid.NewGuid():N}.json"));
        calculator.ImportScanDocument(new PriceScanDocument
        {
            Pair = new TradePairSnapshot
            {
                BuyCurrency = "D",
                SellCurrency = "C",
                CurrentRatio = new NormalizedRatio { Left = 10m, Right = 1m, RightPerLeft = 0.1m }
            }
        });

        calculator.ImportScanDocument(new PriceScanDocument
        {
            Pair = new TradePairSnapshot { BuyCurrency = "D", SellCurrency = "C" }
        });

        Assert.Equal("0.1", calculator.SelectedPair!.RateText);
    }

    [Fact]
    public void NativeCalculator_ShouldUseItemSelectionsAndCachePricesPerCurrency()
    {
        var calculator = new NativeCalculatorViewModel(Path.Combine(Path.GetTempPath(), $"poe-calculator-{Guid.NewGuid():N}.json"));
        calculator.ImportScanDocument(new PriceScanDocument
        {
            Pair = new TradePairSnapshot
            {
                BuyCurrency = "D",
                SellCurrency = "C",
                CurrentRatio = new NormalizedRatio { Left = 10m, Right = 1m, RightPerLeft = 0.1m }
            },
            Items = new[]
            {
                new PriceScanItem
                {
                    Name = "目标 A",
                    BuyRatio = new NormalizedRatio { Left = 2m, Right = 1m, RightPerLeft = 0.5m },
                    SellRatio = new NormalizedRatio { Left = 2m, Right = 1m, RightPerLeft = 0.5m },
                    Status = "ok"
                }
            }
        });

        var firstPair = calculator.SelectedPair!;
        var firstTarget = calculator.Targets[0];
        calculator.AddCurrency();
        var eCurrency = calculator.Items.Last();
        eCurrency.Name = "E";
        calculator.AddCurrency();
        var fCurrency = calculator.Items.Last();
        fCurrency.Name = "F";
        calculator.AddPair();
        var secondPair = calculator.Pairs.Last();
        secondPair.BaseItem = eCurrency;
        secondPair.QuoteItem = fCurrency;
        secondPair.RateText = "0.5";

        calculator.SelectedPair = secondPair;
        firstTarget.BuyPriceText = "8";
        firstTarget.SellPriceText = "4/1";
        calculator.SelectedPair = firstPair;

        Assert.Equal("2", firstTarget.BuyPriceText);
        Assert.Equal("2/1", firstTarget.SellPriceText);
        Assert.Same(firstTarget.TargetItem, calculator.TargetItems.Cast<CalculatorItemRow>().Single());
        Assert.All(calculator.Pairs, pair =>
        {
            Assert.NotNull(pair.BaseItem);
            Assert.NotNull(pair.QuoteItem);
        });
    }

    private static string FindRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (Directory.Exists(Path.Combine(directory, "src", "Poe2MarketScanner.App")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Could not locate repository root for MainWindow text tests.");
    }
}
