using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using Microsoft.Win32;
using Poe2MarketScanner.App.Services;
using Poe2MarketScanner.Core.Configuration;
using PoeTradeAssistant.Contracts.MarketScan;

namespace Poe2MarketScanner.App;

public partial class MainWindow : FluentWindow, IOverlayCaptureHost
{
    private readonly MainViewModel _viewModel;
    private CancellationTokenSource? _automationCancellation;
    private OverlayWindow? _overlayWindow;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(new JsonProfileStorageService(GetCurrentScreenMetrics));
        _viewModel.Load();
        DataContext = _viewModel;
    }

    public bool IsOverlayVisible => _overlayWindow?.IsVisible == true;

    public bool IsOverlayEditing { get; private set; }

    private void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.LoadProfileFromFile(dialog.FileName);
        }
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.TrySave(out var error))
        {
            MessageBox.Show(this, error ?? "无法保存配置。", "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportQueryFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.ImportQueryFile(dialog.FileName);
        }
    }

    private void ResetProfile_Click(object sender, RoutedEventArgs e) => _viewModel.ResetToDefaultProfile();

    private void ToggleOverlayEdit_Click(object sender, RoutedEventArgs e)
    {
        ShowOverlay();
        SetOverlayEditing(true);
    }

    private void HideOverlay_Click(object sender, RoutedEventArgs e) => HideOverlay();

    private async void RunSellAutomation_Click(object sender, RoutedEventArgs e)
    {
        var startError = AutomationPreflightValidator.ValidateStartRequirements(_viewModel.Profile, _viewModel.QueryItems.Count)
            ?? AutomationPreflightValidator.Validate(_viewModel.Profile);
        if (startError is not null)
        {
            MessageBox.Show(this, startError, "无法开始", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _viewModel.TrySave(out _);
        _automationCancellation?.Dispose();
        _automationCancellation = new CancellationTokenSource();
        RunAutomationButton.IsEnabled = false;
        StopAutomationButton.IsEnabled = true;

        var captureGuard = new OverlayCaptureGuard(this);
        try
        {
            captureGuard.BeginCapture();
            using var ocrReader = new SellQueryOcrReader();
            var writer = new ProjectOutputJsonWriter(ProjectOutputJsonWriter.DiscoverProjectRoot(), _viewModel.OutputDirectory);
            var runner = new SellQueryAutomationRunner(new WindowsInputAutomationRunner(), ocrReader, writer);
            var result = await runner.RunAsync(_viewModel.Profile, _automationCancellation.Token);
            var importStatus = await ImportScanIntoCalculatorAsync(result.OutputPath);
            MessageBox.Show(this, $"查询完成，结果已写入：{result.OutputPath}\n{importStatus}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show(this, "查询已停止。", "已停止", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "查询失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            captureGuard.Restore();
            RunAutomationButton.IsEnabled = true;
            StopAutomationButton.IsEnabled = false;
            _automationCancellation?.Dispose();
            _automationCancellation = null;
        }
    }

    private void StopAutomation_Click(object sender, RoutedEventArgs e) => _automationCancellation?.Cancel();

    private void BrowseOutputDirectory_Click(object sender, RoutedEventArgs e)
    {
        var outputDirectory = string.IsNullOrWhiteSpace(_viewModel.OutputDirectory)
            ? Path.Combine(ProjectOutputJsonWriter.DiscoverProjectRoot(), "output")
            : _viewModel.OutputDirectory;
        Directory.CreateDirectory(outputDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", outputDirectory) { UseShellExecute = true });
    }

    private void RunScreenOcrDebug_Click(object sender, RoutedEventArgs e)
    {
        var captureGuard = new OverlayCaptureGuard(this);
        try
        {
            captureGuard.BeginCapture();
            using var service = new PaddleOcrDebugService();
            _viewModel.ApplyOcrDebugResult(service.Run(_viewModel.Profile));
            MessageBox.Show(this, _viewModel.OcrPreviewSummary, "OCR 调试", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "OCR 调试失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            captureGuard.Restore();
        }
    }

    private void OpenDebugDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_viewModel.LatestDebugDirectory) && Directory.Exists(_viewModel.LatestDebugDirectory))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", _viewModel.LatestDebugDirectory) { UseShellExecute = true });
        }
    }

    private void ParseOcrPreview_Click(object sender, RoutedEventArgs e) => _viewModel.ParseOcrPreview();

    private async Task<string> ImportScanIntoCalculatorAsync(string legacyOutputPath)
    {
        var version2OutputPath = Path.ChangeExtension(legacyOutputPath, null) + ".v2.json";
        if (!File.Exists(version2OutputPath))
        {
            return "V2 扫描结果未生成，未自动导入计算器。";
        }

        try
        {
            var document = JsonSerializer.Deserialize<PriceScanDocument>(await File.ReadAllTextAsync(version2OutputPath));
            if (document is null)
            {
                return "V2 扫描结果为空，未导入计算器。";
            }

            return _viewModel.Calculator.ImportScanDocument(document);
        }
        catch (Exception exception)
        {
            return $"V2 文件已生成，但导入原生计算器失败：{exception.Message}";
        }
    }

    private async void ImportCalculatorScan_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "V2 price scan (*.v2.json;*.json)|*.v2.json;*.json|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var document = JsonSerializer.Deserialize<PriceScanDocument>(await File.ReadAllTextAsync(dialog.FileName));
            if (document is null)
            {
                throw new InvalidOperationException("价格表为空或格式不正确。");
            }

            _viewModel.Calculator.ImportScanDocument(document);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "导入价格表失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveCalculatorWorkspace_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _viewModel.Calculator.Save();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "保存计算器工作区失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RecalculateCalculator_Click(object sender, RoutedEventArgs e) => _viewModel.Calculator.RefreshCalculations();
    private void AddCalculatorCurrency_Click(object sender, RoutedEventArgs e) => _viewModel.Calculator.AddCurrency();
    private void AddCalculatorTargetItem_Click(object sender, RoutedEventArgs e) => _viewModel.Calculator.AddTargetItem();
    private void AddCalculatorPair_Click(object sender, RoutedEventArgs e) => _viewModel.Calculator.AddPair();
    private void AddCalculatorTarget_Click(object sender, RoutedEventArgs e) => _viewModel.Calculator.AddTarget();
    private void RemoveCalculatorItem_Click(object sender, RoutedEventArgs e) => _viewModel.Calculator.RemoveItem((sender as FrameworkElement)?.DataContext as CalculatorItemRow);
    private void RemoveCalculatorPair_Click(object sender, RoutedEventArgs e) => _viewModel.Calculator.RemovePair((sender as FrameworkElement)?.DataContext as CalculatorPairRow);
    private void RemoveCalculatorTarget_Click(object sender, RoutedEventArgs e) => _viewModel.Calculator.RemoveTarget((sender as FrameworkElement)?.DataContext as CalculatorTargetRow);

    private void CalculatorGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(_viewModel.Calculator.RefreshCalculations), DispatcherPriority.Background);
    }

    public void ShowOverlay()
    {
        if (_overlayWindow is null)
        {
            _overlayWindow = new OverlayWindow(_viewModel);
            _overlayWindow.Closed += (_, _) => _overlayWindow = null;
        }

        _overlayWindow.Show();
        _overlayWindow.Activate();
    }

    public void HideOverlay() => _overlayWindow?.Hide();

    public void SetOverlayEditing(bool editing)
    {
        IsOverlayEditing = editing;
        _overlayWindow?.SetEditing(editing);
    }

    private static ProfileScreenMetrics GetCurrentScreenMetrics() => new(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
}
