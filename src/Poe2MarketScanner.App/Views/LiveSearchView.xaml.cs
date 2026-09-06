using Microsoft.Win32;
using PoeTradeAssistant.LiveSearch;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Poe2MarketScanner.App.Views;

public partial class LiveSearchView : UserControl
{
    private readonly SearchStorage _storage = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PoeTradeAssistant", "live-search"));
    private readonly BrowserSession _session = new();
    private readonly ObservableCollection<SearchLink> _links = [];
    private readonly ObservableCollection<MonitorItem> _monitors = [];
    private readonly ObservableCollection<SearchHitItem> _hits = [];
    private readonly HashSet<string> _submitted = [];
    private readonly Queue<SearchHit> _autoTravelQueue = [];
    private TradeEnvironment _environment = new("poe2", "international");
    private bool _busy;
    private bool _closing;
    private bool _loaded;
    private bool _scanning;
    private bool _monitoring;
    private bool _autoTravelEnabled;
    private bool _autoTravelContinuous;
    private bool _autoTravelProcessing;
    private DateTimeOffset? _lastAutoTravelAt;
    private CancellationTokenSource _autoTravelCancellation = new();
    private Task _operation = Task.CompletedTask;
    private long _epoch;
    public bool CanSwitchGame => !_busy && !_session.IsOpen;
    public event Action? AvailabilityChanged;

    public LiveSearchView()
    {
        InitializeComponent();
        LinkList.ItemsSource = _links;
        MonitorList.ItemsSource = _monitors;
        HitList.ItemsSource = _hits;
        _session.Disconnected += () => Dispatcher.BeginInvoke(new Action(async () =>
        {
            ++_epoch;
            ResetAutoTravelQueue();
            AutoTravelToggle.IsChecked = false;
            _monitoring = false;
            await _session.CloseAsync();
            StatusText.Text = "浏览器连接已断开，监控已停止。可以重新连接或使用其他功能。";
            RefreshEnabled();
        }));
        _session.Status += message =>
        {
            var epoch = _epoch;
            Dispatcher.BeginInvoke(new Action(() => { if (!_closing && epoch == _epoch) { StatusText.Text = message; _monitoring = _session.IsMonitoring; RefreshEnabled(); } }));
        };
        _session.Hit += hit =>
        {
            var epoch = _epoch;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_closing || epoch != _epoch) return;
                _hits.Insert(0, new(hit, _environment.Game));
                while (_hits.Count > 50) _hits.RemoveAt(_hits.Count - 1);
                if (!hit.Initial) System.Media.SystemSounds.Asterisk.Play();
                QueueAutoTravel(hit, epoch);
            }));
        };
        _loaded = true;
        LoadWorkspace();
    }

    public void SetGame(string game)
    {
        if (game == _environment.Game) return;
        if (!CanSwitchGame) throw new InvalidOperationException("请先关闭实时搜索会话再切换游戏。");
        _environment = new(game, _environment.Region);
        ++_epoch;
        LoadWorkspace();
    }
    public void SetScanning(bool scanning) { _scanning = scanning; RefreshEnabled(); }
    private void LoadWorkspace()
    {
        EnvironmentText.Text = $"{_environment.Game.ToUpperInvariant()} · {(_environment.Region == "china" ? "国服" : "国际服")} · 独立登录与链接库";
        _links.Clear(); _hits.Clear(); _submitted.Clear();
        _monitors.Clear();
        ResetAutoTravelQueue();
        NameInput.Clear(); UrlInput.Clear();
        try
        {
            foreach (var link in _storage.Load(_environment).Links) _links.Add(link);
            RefreshMonitors();
            StatusText.Text = "工作区已加载。点击“一键启动 / 连接”，登录并验证后即可启动监控。";
            Editor.IsEnabled = LinkActions.IsEnabled = true;
        }
        catch (Exception error)
        {
            StatusText.Text = "工作区读取失败，原文件已保留：" + error.Message;
            Editor.IsEnabled = LinkActions.IsEnabled = false;
        }
        RefreshEnabled();
    }
    private void RefreshEnabled()
    {
        Actions.IsEnabled = !_busy && !_closing && !_scanning;
        RegionPicker.IsEnabled = CanSwitchGame && !_scanning;
        AutoTravelToggle.IsEnabled = !_busy && !_closing && !_scanning;
        ContinuousTravelToggle.IsEnabled = AutoTravelToggle.IsEnabled && !_monitoring;
        TravelIntervalInput.IsEnabled = AutoTravelToggle.IsEnabled && !_monitoring;
        ValidateButton.Content = _session.IsValidated ? "断开" : "验证登录";
        ValidateButton.Background = _session.IsValidated ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(143, 46, 56)) : (System.Windows.Media.Brush)FindResource("AppSurfaceRaisedBrush");
        StartButton.Content = _monitoring ? "全部停止" : "启动全部";
        StartButton.IsEnabled = _session.IsValidated;
        RefreshMonitors();
        AvailabilityChanged?.Invoke();
    }
    private void RegionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        _environment = new(_environment.Game, RegionPicker.SelectedIndex == 1 ? "china" : "international");
        ++_epoch;
        LoadWorkspace();
    }
    private Task Run(Func<Task> action)
    {
        if (_busy || _closing || _scanning) return Task.CompletedTask;
        _busy = true; RefreshEnabled();
        return _operation = ExecuteAsync(action);
    }
    private async Task ExecuteAsync(Func<Task> action)
    {
        try { await action().WaitAsync(TimeSpan.FromSeconds(60)); }
        catch (TimeoutException)
        {
            ++_epoch; ResetAutoTravelQueue(); _monitoring = false;
            await _session.CloseAsync();
            StatusText.Text = "浏览器操作超时，会话已释放，请重新连接。";
        }
        catch (Exception error) { StatusText.Text = error.Message; }
        finally { _busy = false; RefreshEnabled(); }
    }
    private async void ShowBrowser_Click(object sender, RoutedEventArgs e) => await Run(() => _session.ShowBrowserAsync());
    private async void Login_Click(object sender, RoutedEventArgs e) => await Run(() => _session.OpenLoginAsync(_environment, _storage.DirectoryFor(_environment)));
    private async void Validate_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (_session.IsValidated)
        {
            ++_epoch; ResetAutoTravelQueue(); _monitoring = false;
            await _session.CloseAsync();
            StatusText.Text = "已断开浏览器连接。";
        }
        else
        {
            await _session.ValidateLoginAsync(_environment);
            StatusText.Text = "登录验证成功，可以启动监控。";
        }
    });
    private async void Start_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (_monitoring)
        {
            ++_epoch; ResetAutoTravelQueue(); _monitoring = false;
            await _session.StopAllAsync(); StatusText.Text = "全部监控已停止，链接仍保留。"; return;
        }
        ++_epoch; _hits.Clear(); _submitted.Clear(); ResetAutoTravelQueue();
        await _session.StartAsync(_environment, _links.ToArray());
        _monitoring = true;
    });
    private async void Pause_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        await _session.PauseAsync(); ++_epoch; _monitoring = false; ResetAutoTravelQueue();
        StatusText.Text = "采集已暂停，网页实时连接及手动前往仍保留。关闭会话会关闭本功能页面并断开连接。";
    });
    private async void Close_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        await _session.CloseAsync(); ++_epoch; _monitoring = false; ResetAutoTravelQueue();
        StatusText.Text = "连接已断开，本功能创建的交易页已关闭；本地浏览器仍保持运行。";
    });
    public async Task ShutdownAsync()
    {
        _closing = true; ++_epoch; RefreshEnabled();
        ResetAutoTravelQueue();
        await _operation;
        await _session.CloseAsync();
    }
    public void ResumeAfterFailedShutdown() { _closing = false; RefreshEnabled(); }
    private void Mutate(Func<List<SearchLink>, List<SearchLink>> update)
    {
        if (_busy || _closing || !Editor.IsEnabled) return;
        try
        {
            var next = update(_links.ToList());
            _storage.Save(_environment, next);
            _links.Clear(); foreach (var link in next) _links.Add(link);
            RefreshMonitors();
            StatusText.Text = "链接库已保存。运行中的监控需点击“启动 / 全部重连”以应用修改。";
        }
        catch (Exception error) { StatusText.Text = error.Message; }
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var selected = LinkList.SelectedItem as SearchLink;
        Mutate(links =>
        {
            var link = new SearchLink(selected?.Id ?? Guid.NewGuid().ToString("N"), NameInput.Text.Trim(), _environment.ValidateUrl(UrlInput.Text), selected?.Enabled ?? links.Count(x => x.Enabled) < 10);
            links.RemoveAll(x => x.Id == link.Id); links.Add(link); return links;
        });
    }
    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (LinkList.SelectedItem is SearchLink selected)
            Mutate(links => links.Select(x => x.Id == selected.Id ? x with { Enabled = !x.Enabled } : x).ToList());
    }
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (LinkList.SelectedItem is SearchLink selected) Mutate(links => links.Where(x => x.Id != selected.Id).ToList());
    }
    private void New_Click(object sender, RoutedEventArgs e) { LinkList.SelectedItem = null; NameInput.Clear(); UrlInput.Clear(); }
    private void LinkSelected(object sender, SelectionChangedEventArgs e)
    {
        if (LinkList.SelectedItem is SearchLink link) { NameInput.Text = link.Name; UrlInput.Text = link.Url; }
    }


    private void EditMonitor_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not MonitorItem monitor) return;
        var link = monitor.Link;
        LinkList.SelectedItem = link;
        NameInput.Text = link.Name; UrlInput.Text = link.Url;
        WorkspaceNavigation.SelectedIndex = 1;
    }

    private async void StopMonitor_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not MonitorItem item) return;
        await Run(async () =>
        {
            await _session.StopMonitorAsync(item.Id);
            _monitoring = _session.IsMonitoring;
            StatusText.Text = $"已停止 {item.Name}，链接仍保留在监控列表。";
        });
    }

    private void ToggleLinkMonitor_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is SearchLink link)
            Mutate(links => links.Select(x => x.Id == link.Id ? x with { Enabled = !x.Enabled } : x).ToList());
    }

    private void EditLink_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not SearchLink link) return;
        LinkList.SelectedItem = link;
        NameInput.Text = link.Name; UrlInput.Text = link.Url;
    }

    private void DeleteLink_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is SearchLink link)
            Mutate(links => links.Where(x => x.Id != link.Id).ToList());
    }

    private void RefreshMonitors()
    {
        _monitors.Clear();
        foreach (var link in _links.Where(x => x.Enabled)) _monitors.Add(new(link, _session.IsMonitorActive(link.Id)));
    }

    private void AutoTravelChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        _autoTravelEnabled = AutoTravelToggle.IsChecked == true;
        _autoTravelContinuous = _autoTravelEnabled && ContinuousTravelToggle.IsChecked == true;
        ContinuousTravelToggle.Visibility = _autoTravelEnabled ? Visibility.Visible : Visibility.Collapsed;
        TravelIntervalPanel.Visibility = _autoTravelContinuous ? Visibility.Visible : Visibility.Collapsed;
        if (!_autoTravelEnabled)
        {
            ContinuousTravelToggle.IsChecked = false;
            ResetAutoTravelQueue();
        }
        StatusText.Text = _autoTravelEnabled
            ? (_autoTravelContinuous ? "持续自动前往已开启，将按最小间隔处理新命中。" : "单次自动前往已开启，首个新命中处理后将暂停全部监控。")
            : "自动前往已关闭。";
    }

    private void TravelIntervalLostFocus(object sender, RoutedEventArgs e) =>
        TravelIntervalInput.Text = AutoTravelIntervalSeconds().ToString();

    private int AutoTravelIntervalSeconds() =>
        Math.Clamp(int.TryParse(TravelIntervalInput.Text, out var seconds) ? seconds : 0, 0, 99);

    private void QueueAutoTravel(SearchHit hit, long epoch)
    {
        if (hit.Initial || !hit.CanTravel || !_autoTravelEnabled || !_monitoring || epoch != _epoch) return;
        _autoTravelQueue.Enqueue(hit);
        if (!_autoTravelProcessing) _ = ProcessAutoTravelQueueAsync(epoch, _autoTravelCancellation.Token);
    }

    private async Task ProcessAutoTravelQueueAsync(long epoch, CancellationToken token)
    {
        _autoTravelProcessing = true;
        RefreshEnabled();
        try
        {
            while (_autoTravelQueue.Count > 0 && _autoTravelEnabled && _monitoring && epoch == _epoch)
            {
                var hit = _autoTravelQueue.Dequeue();
                if (_autoTravelContinuous && _lastAutoTravelAt is { } last)
                {
                    var wait = TimeSpan.FromSeconds(AutoTravelIntervalSeconds()) - (DateTimeOffset.Now - last);
                    if (wait > TimeSpan.Zero) await Task.Delay(wait, token);
                }
                token.ThrowIfCancellationRequested();
                var key = hit.MonitorId + ":" + hit.Id;
                var ok = _session.IsMonitorActive(hit.MonitorId) && !_submitted.Contains(key) && await _session.TravelAsync(hit);
                if (ok)
                {
                    _submitted.Add(key);
                    MarkRead(hit);
                    _lastAutoTravelAt = DateTimeOffset.Now;
                }
                if (!_autoTravelContinuous)
                {
                    await _session.PauseAsync();
                    _monitoring = false;
                    _autoTravelQueue.Clear();
                    StatusText.Text = ok
                        ? $"已自动前往：{hit.Title}；全部监控已暂停。"
                        : $"检测到 {hit.Title}，但前往按钮不可用；全部监控已暂停。";
                    _autoTravelEnabled = false;
                    AutoTravelToggle.IsChecked = false;
                    break;
                }
                StatusText.Text = ok ? $"已自动前往：{hit.Title}" : $"自动前往失败或按钮已失效：{hit.Title}";
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { StatusText.Text = "自动前往失败：" + error.Message; }
        finally { _autoTravelProcessing = false; RefreshEnabled(); }
    }

    private void ResetAutoTravelQueue()
    {
        _autoTravelCancellation.Cancel();
        _autoTravelCancellation.Dispose();
        _autoTravelCancellation = new();
        _autoTravelQueue.Clear();
        _lastAutoTravelAt = null;
    }
    private async void Travel_Click(object sender, RoutedEventArgs e)
    {
        if (HitList.SelectedItem is not SearchHitItem item) return;
        await TravelHitAsync(item);
    }

    private async void TravelHit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is SearchHitItem item)
            await TravelHitAsync(item);
    }

    private async Task TravelHitAsync(SearchHitItem item)
    {
        var hit = item.Hit;
        await Run(async () =>
        {
            var key = hit.MonitorId + ":" + hit.Id;
            if (_submitted.Contains(key)) { StatusText.Text = "这条结果已提交前往请求。"; return; }
            if (await _session.TravelAsync(hit))
            {
                _submitted.Add(key);
                item.Read = true;
                StatusText.Text = "已提交一次前往请求，请查看游戏。";
            }
            else StatusText.Text = "当前网页已没有该结果或前往按钮不可用。";
        });
    }

    private void MarkRead_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is SearchHitItem item) item.Read = true;
    }

    private void MarkRead(SearchHit hit)
    {
        var item = _hits.FirstOrDefault(x => x.Hit.Id == hit.Id && x.Hit.MonitorId == hit.MonitorId);
        if (item is not null) item.Read = true;
    }
    private void ClearHits_Click(object sender, RoutedEventArgs e) { _hits.Clear(); }
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "JSON|*.json", FileName = $"{_environment.Key}-links.json" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(new LinkExport(1, _environment.Key,
                _links.Select(x => new SharedLink(x.Name, x.Url)).ToList()), SearchStorage.JsonOptions));
            StatusText.Text = "链接库已导出，不包含登录信息。";
        }
        catch (Exception error) { StatusText.Text = error.Message; }
    }
    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "JSON|*.json" };
        if (dialog.ShowDialog() != true) return;
        Mutate(links =>
        {
            if (new FileInfo(dialog.FileName).Length > 2 * 1024 * 1024) throw new InvalidDataException("链接库文件超过2MB。");
            var payload = JsonSerializer.Deserialize<LinkExport>(File.ReadAllText(dialog.FileName), SearchStorage.JsonOptions);
            if (payload is null || payload.Version != 1 || payload.Region != _environment.Key || payload.Links is null)
                throw new InvalidDataException("文件版本或游戏区服不匹配。");
            foreach (var item in payload.Links)
            {
                var url = _environment.ValidateUrl(item.LiveSearchUrl);
                if (!links.Any(x => x.Name.Equals(item.Name.Trim(), StringComparison.OrdinalIgnoreCase) && x.Url == url))
                    links.Add(new(Guid.NewGuid().ToString("N"), item.Name.Trim(), url, false));
            }
            return links;
        });
    }

    private sealed record MonitorItem(SearchLink Link, bool Active)
    {
        public string Id => Link.Id;
        public string Name => Link.Name;
        public string Url => Link.Url;
        public string RuntimeStatus => Active ? "运行中" : "已停止";
    }

    private sealed class SearchHitItem(SearchHit hit, string game) : INotifyPropertyChanged
    {
        private bool _read;
        public SearchHit Hit { get; } = hit;
        public string DisplayTitle { get; } = ChineseTranslator.Instance.Translate(hit.Title, game);
        public string DisplayPrice { get; } = ChineseTranslator.Instance.Translate(hit.Price, game);
        public string DisplayDetail { get; } = ChineseTranslator.Instance.Translate(hit.Detail, game) + "\n\n—— 英文原文 ——\n" + hit.Detail;
        public bool Read
        {
            get => _read;
            set
            {
                if (_read == value) return;
                _read = value;
                PropertyChanged?.Invoke(this, new(nameof(Read)));
                PropertyChanged?.Invoke(this, new(nameof(ReadVisibility)));
            }
        }
        public Visibility ReadVisibility => Read ? Visibility.Visible : Visibility.Collapsed;
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
