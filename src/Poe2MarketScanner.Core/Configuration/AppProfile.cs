using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Poe2MarketScanner.Core.Configuration;

public sealed class AppProfile
{
    public string ProfileName { get; set; } = "default";
    public string PriceMode { get; set; } = "sell";
    public string AnchorCoordinateMode { get; set; } = string.Empty;
    public bool UseTraditionalChinese { get; set; }
    public string OutputDirectory { get; set; } = string.Empty;
    public string SelectedTradeModeKey { get; set; } = TradeModeCatalog.All[0].Key;
    public Dictionary<string, ScreenRegion> Regions { get; set; } = new();
    public Dictionary<string, AnchorPoint> Anchors { get; set; } = new();
    public OcrSettings Ocr { get; set; } = new();
    public QueryListSettings QueryList { get; set; } = new();
    public AutomationSettings Automation { get; set; } = new();
}

public abstract class ObservableModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed class ScreenRegion : ObservableModel
{
    private double _x;
    private double _y;
    private double _width;
    private double _height;

    public string Name { get; init; } = string.Empty;

    public double X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    public double Width
    {
        get => _width;
        set => SetProperty(ref _width, value);
    }

    public double Height
    {
        get => _height;
        set => SetProperty(ref _height, value);
    }

    public string Color { get; init; } = "#FFFFFF";
}

public sealed class AnchorPoint : ObservableModel
{
    private double _x;
    private double _y;

    public string Name { get; init; } = string.Empty;

    public double X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }
}

public sealed class OcrSettings : ObservableModel
{
    private string _engine = "paddleocr";
    private bool _detectDigitsOnly = true;
    private double _scale = 2.0;
    private int _threshold = 160;
    private bool _enableGrayscale = true;
    private bool _enableBinarization = true;
    private bool _enableOtsuFallback = true;

    public string Engine
    {
        get => _engine;
        set => SetProperty(ref _engine, value);
    }

    public bool DetectDigitsOnly
    {
        get => _detectDigitsOnly;
        set => SetProperty(ref _detectDigitsOnly, value);
    }

    public double Scale
    {
        get => _scale;
        set => SetProperty(ref _scale, value);
    }

    public int Threshold
    {
        get => _threshold;
        set => SetProperty(ref _threshold, value);
    }

    public bool EnableGrayscale
    {
        get => _enableGrayscale;
        set => SetProperty(ref _enableGrayscale, value);
    }

    public bool EnableBinarization
    {
        get => _enableBinarization;
        set => SetProperty(ref _enableBinarization, value);
    }

    public bool EnableOtsuFallback
    {
        get => _enableOtsuFallback;
        set => SetProperty(ref _enableOtsuFallback, value);
    }

    public Dictionary<string, OcrRegionSettings> RegionOverrides { get; set; } = new();
}

public sealed class OcrRegionSettings : ObservableModel
{
    private bool _enabled;
    private double _scale = 2.0;
    private int _threshold = 160;
    private bool _enableGrayscale = true;
    private bool _enableBinarization = true;
    private bool _enableOtsuFallback = true;

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public double Scale
    {
        get => _scale;
        set => SetProperty(ref _scale, value);
    }

    public int Threshold
    {
        get => _threshold;
        set => SetProperty(ref _threshold, value);
    }

    public bool EnableGrayscale
    {
        get => _enableGrayscale;
        set => SetProperty(ref _enableGrayscale, value);
    }

    public bool EnableBinarization
    {
        get => _enableBinarization;
        set => SetProperty(ref _enableBinarization, value);
    }

    public bool EnableOtsuFallback
    {
        get => _enableOtsuFallback;
        set => SetProperty(ref _enableOtsuFallback, value);
    }
}

public sealed class QueryListSettings
{
    public string SourceFile { get; set; } = "currencies.txt";
    public List<string> Items { get; set; } = new();
}

public sealed class AutomationSettings : ObservableModel
{
    private bool _reserved = true;
    private bool _recognizeGoldCost = true;
    private int _commonDelayMs = 500;
    private int _clickDelayMs = 120;
    private int _inputDelayMs = 80;

    public bool Reserved
    {
        get => _reserved;
        set => SetProperty(ref _reserved, value);
    }

    public bool RecognizeGoldCost
    {
        get => _recognizeGoldCost;
        set => SetProperty(ref _recognizeGoldCost, value);
    }

    public int CommonDelayMs
    {
        get => _commonDelayMs;
        set => SetProperty(ref _commonDelayMs, value);
    }

    public int ClickDelayMs
    {
        get => _clickDelayMs;
        set => SetProperty(ref _clickDelayMs, value);
    }

    public int InputDelayMs
    {
        get => _inputDelayMs;
        set => SetProperty(ref _inputDelayMs, value);
    }
}
