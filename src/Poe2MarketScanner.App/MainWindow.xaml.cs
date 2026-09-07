using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using Microsoft.Win32;
using Poe2MarketScanner.App.Services;
using Poe2MarketScanner.Core.Configuration;
using PoeTradeAssistant.Contracts.MarketScan;

namespace Poe2MarketScanner.App;

public partial class MainWindow : FluentWindow, IOverlayCaptureHost
{
    private const int StartScanHotKeyId = 0x5107;
    private const int StopScanHotKeyId = 0x5108;
    private const int WmHotKey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private const uint VirtualKeyF7 = 0x76;
    private const uint VirtualKeyF8 = 0x77;

    private readonly MainViewModel _viewModel;
    private CancellationTokenSource? _automationCancellation;
    private OverlayWindow? _overlayWindow;
    private bool _isClosing;
    private bool _closeApproved;
    private TaskCompletionSource? _automationCompletion;
    private TaskCompletionSource? _calculatorImportCompletion;
    private HwndSource? _windowSource;
    private bool _startHotKeyRegistered;
    private bool _stopHotKeyRegistered;

    public MainWindow()
    {
        InitializeComponent();
        foreach (var column in CalculatorTargetsGrid.Columns)
            column.CanUserSort = column == CalculatorRoiColumn || column == CalculatorGoldEfficiencyColumn;
        _viewModel = new MainViewModel(new JsonProfileStorageService(GetCurrentScreenMetrics));
        _viewModel.Load();
        DataContext = _viewModel;
        _viewModel.CanSwitchGame = () => !_isClosing && _automationCancellation is null && LiveSearch.CanSwitchGame;
        LiveSearch.SetGame(_viewModel.IsPoe1Mode ? "poe1" : "poe2");
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedGameMode))
                LiveSearch.SetGame(_viewModel.IsPoe1Mode ? "poe1" : "poe2");
        };
        LiveSearch.AvailabilityChanged += () => GameModeSelector.IsEnabled = LiveSearch.CanSwitchGame && _automationCancellation is null;
    }

    public bool IsOverlayVisible => _overlayWindow?.IsVisible == true;

    public bool IsOverlayEditing { get; private set; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(handle);
        _windowSource?.AddHook(HandleWindowMessage);
        _startHotKeyRegistered = RegisterHotKey(handle, StartScanHotKeyId, ModNoRepeat, VirtualKeyF7);
        _stopHotKeyRegistered = RegisterHotKey(handle, StopScanHotKeyId, ModNoRepeat, VirtualKeyF8);
    }

    private IntPtr HandleWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmHotKey)
        {
            return IntPtr.Zero;
        }

        switch (wParam.ToInt32())
        {
            case StartScanHotKeyId:
                RunSellAutomation_Click(this, new RoutedEventArgs());
                handled = true;
                break;
            case StopScanHotKeyId:
                _automationCancellation?.Cancel();
                handled = true;
                break;
        }

        return IntPtr.Zero;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || _closeApproved)
            return;

        e.Cancel = true;
        if (_isClosing)
            return;
        _isClosing = true;
        // Start after the current Closing event returns; never recursively close here.
        Dispatcher.BeginInvoke(new Action(async () => await SaveAndCloseAsync()));
    }

    private async Task SaveAndCloseAsync()
    {
        var overlayWasVisible = IsOverlayVisible;
        try
        {
            CommitPendingEdits(ApplicationContent);
            Keyboard.ClearFocus();
            ApplicationContent.IsEnabled = false;
            ShutdownOverlay.Visibility = Visibility.Visible;
            ShutdownOverlay.Focus();
            HideOverlay();
            ShutdownStatusText.Text = "正在停止扫描并等待当前操作完成…";
            var automationFinished = _automationCompletion?.Task ?? Task.CompletedTask;
            _automationCancellation?.Cancel();
            await automationFinished;
            await (_calculatorImportCompletion?.Task ?? Task.CompletedTask);
            ShutdownStatusText.Text = "正在关闭实时搜索浏览器…";
            await LiveSearch.ShutdownAsync();
            ShutdownStatusText.Text = "正在保存所有工作区的数据、配置和设置，完成后将自动退出。";
            await Dispatcher.Yield(DispatcherPriority.Background);
            await _viewModel.SaveAllWorkspacesAsync();
            _closeApproved = true;
            Close();
        }
        catch (Exception exception)
        {
            _isClosing = false;
            LiveSearch.ResumeAfterFailedShutdown();
            _closeApproved = false;
            ShutdownOverlay.Visibility = Visibility.Collapsed;
            ApplicationContent.IsEnabled = true;
            if (overlayWasVisible)
                ShowOverlay();
            MessageBox.Show(this, $"未能完整保存，应用尚未关闭。请处理后重新关闭以重试。\n\n{exception.Message}",
                "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void CommitPendingEdits(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            CommitPendingEdits(VisualTreeHelper.GetChild(parent, index));
        if (parent is TextBox textBox)
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if (parent is DataGrid grid &&
            (!grid.CommitEdit(DataGridEditingUnit.Cell, true) || !grid.CommitEdit(DataGridEditingUnit.Row, true)))
            throw new InvalidOperationException("表格中有无法保存的输入，请修正后重试。");
        if (Validation.GetHasError(parent))
            throw new InvalidOperationException("界面中有格式不正确的输入，请修正后重试。");
    }

    protected override void OnClosed(EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (_startHotKeyRegistered)
            UnregisterHotKey(handle, StartScanHotKeyId);
        if (_stopHotKeyRegistered)
            UnregisterHotKey(handle, StopScanHotKeyId);
        _windowSource?.RemoveHook(HandleWindowMessage);
        _overlayWindow?.Close();
        base.OnClosed(e);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);

    private void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.LoadProfileFromFile(dialog.FileName);
        }
    }

    private void CalculatorTargetsGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        if (e.Column != CalculatorRoiColumn && e.Column != CalculatorGoldEfficiencyColumn)
        {
            e.Handled = true;
            return;
        }

        CalculatorRoiColumn.Header = "ROI";
        CalculatorGoldEfficiencyColumn.Header = "金币转化率";
        var direction = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        e.Column.Header = e.Column == CalculatorRoiColumn
            ? $"ROI {(direction == ListSortDirection.Ascending ? "▲" : "▼")}"
            : $"金币转化率 {(direction == ListSortDirection.Ascending ? "▲" : "▼")}";
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

    private void RemoveQueryItem_Click(object sender, RoutedEventArgs e) =>
        _viewModel.RemoveQueryItem((sender as FrameworkElement)?.DataContext as QueryItemEntry);

    private void ToggleAllQueryItems_Click(object sender, RoutedEventArgs e) =>
        _viewModel.ToggleAllQueryItems();

    private void QueryItemRow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source is not null && !ReferenceEquals(source, sender))
        {
            if (source is Button or CheckBox)
            {
                return;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        if ((sender as FrameworkElement)?.DataContext is QueryItemEntry item)
        {
            item.IsEnabled = !item.IsEnabled;
            e.Handled = true;
        }
    }

    private void ResetProfile_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            this,
            $"确定要重置{_viewModel.ActiveGameDisplayName}的扫描配置吗？此操作会恢复默认值。",
            "重置当前工作区配置",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.ResetToDefaultProfile();
        }
    }

    private void ToggleOverlayEdit_Click(object sender, RoutedEventArgs e)
    {
        ShowOverlay();
        SetOverlayEditing(true);
    }

    private void HideOverlay_Click(object sender, RoutedEventArgs e) => HideOverlay();

    private async void RunSellAutomation_Click(object sender, RoutedEventArgs e)
    {
        if (_isClosing || _automationCancellation is not null)
            return;

        if (!LiveSearch.CanSwitchGame)
        {
            MessageBox.Show(this, "请先在实时搜索页关闭浏览器会话，再开始扫价。", "浏览器会话仍在使用中");
            return;
        }

        var startError = AutomationPreflightValidator.ValidateStartRequirements(_viewModel.Profile, _viewModel.EnabledQueryItemCount)
            ?? AutomationPreflightValidator.Validate(_viewModel.Profile);
        if (startError is not null)
        {
            MessageBox.Show(this, startError, "无法开始", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _viewModel.TrySave(out _);
        _automationCancellation?.Dispose();
        _automationCancellation = new CancellationTokenSource();
        _automationCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RunAutomationButton.IsEnabled = false;
        StopAutomationButton.IsEnabled = true;
        LiveSearch.SetScanning(true);
        GameModeSelector.IsEnabled = false;

        var captureGuard = new OverlayCaptureGuard(this);
        try
        {
            captureGuard.BeginCapture();
            using var ocrReader = new SellQueryOcrReader();
            var writer = new ProjectOutputJsonWriter(ProjectOutputJsonWriter.DiscoverProjectRoot(), _viewModel.OutputDirectory);
            var runner = new SellQueryAutomationRunner(new WindowsInputAutomationRunner(), ocrReader, writer);
            var goldCosts = new ScanGoldCostTable();
            foreach (var item in _viewModel.Calculator.Items)
                goldCosts.Record(item.Name, item.GoldCostText);
            var result = await runner.RunAsync(_viewModel.Profile, _automationCancellation.Token, goldCosts);
            if (_isClosing)
                return;
            var importStatus = await ImportScanIntoCalculatorAsync(result.OutputPath);
            if (_isClosing)
                return;
            MessageBox.Show(this, $"查询完成，结果已写入：{result.OutputPath}\n{importStatus}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            if (_isClosing)
                return;
            MessageBox.Show(this, "查询已停止。", "已停止", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            if (_isClosing)
                return;
            MessageBox.Show(this, exception.Message, "查询失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (!_isClosing)
                captureGuard.Restore();
            RunAutomationButton.IsEnabled = true;
            StopAutomationButton.IsEnabled = false;
            _automationCancellation?.Dispose();
            _automationCancellation = null;
            LiveSearch.SetScanning(false);
            GameModeSelector.IsEnabled = LiveSearch.CanSwitchGame;
            _automationCompletion?.TrySetResult();
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

    private async Task<string> ImportScanIntoCalculatorAsync(string version2OutputPath)
    {
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
        if (_isClosing || _calculatorImportCompletion?.Task.IsCompleted == false)
            return;
        var dialog = new OpenFileDialog { Filter = "V2 price scan (*.v2.json;*.json)|*.v2.json;*.json|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _calculatorImportCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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
            if (!_isClosing)
                MessageBox.Show(this, exception.Message, "导入价格表失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _calculatorImportCompletion.TrySetResult();
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
        if (_isClosing)
            return;

        if (_overlayWindow is null)
        {
            _overlayWindow = new OverlayWindow(_viewModel) { Owner = this };
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
