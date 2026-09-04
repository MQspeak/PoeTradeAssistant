using Microsoft.Win32;
using PoeTradeAssistant.LiveSearch;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Poe2MarketScanner.App.Views;

public partial class LiveSearchView : UserControl
{
    private readonly SearchStorage _storage = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PoeTradeAssistant", "live-search"));
    private readonly BrowserSession _session = new();
    private readonly ObservableCollection<SearchLink> _links = [];
    private readonly ObservableCollection<SearchHit> _hits = [];
    private readonly HashSet<string> _submitted = [];
    private TradeEnvironment _environment = new("poe2", "international");
    private bool _busy;
    private bool _closing;
    private bool _loaded;
    private bool _scanning;
    private Task _operation = Task.CompletedTask;
    private long _epoch;
    public bool CanSwitchGame => !_busy && !_session.IsOpen;
    public event Action? AvailabilityChanged;

    public LiveSearchView()
    {
        InitializeComponent();
        LinkList.ItemsSource = _links;
        HitList.ItemsSource = _hits;
        _session.Status += message =>
        {
            var epoch = _epoch;
            Dispatcher.BeginInvoke(new Action(() => { if (!_closing && epoch == _epoch) StatusText.Text = message; }));
        };
        _session.Hit += hit =>
        {
            var epoch = _epoch;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_closing || epoch != _epoch) return;
                _hits.Insert(0, hit);
                while (_hits.Count > 50) _hits.RemoveAt(_hits.Count - 1);
                if (!hit.Initial) System.Media.SystemSounds.Asterisk.Play();
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
        NameInput.Clear(); UrlInput.Clear(); DetailText.Clear();
        try
        {
            foreach (var link in _storage.Load(_environment).Links) _links.Add(link);
            StatusText.Text = "工作区已加载。登录后验证，再启动监控。";
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
        TravelButton.IsEnabled = !_busy && !_closing && !_scanning && _session.IsOpen;
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
        try { await action(); }
        catch (Exception error) { StatusText.Text = error.Message; }
        finally { _busy = false; RefreshEnabled(); }
    }
    private async void Login_Click(object sender, RoutedEventArgs e) => await Run(() => _session.OpenLoginAsync(_environment, _storage.DirectoryFor(_environment)));
    private async void Validate_Click(object sender, RoutedEventArgs e) => await Run(() => _session.ValidateLoginAsync(_environment));
    private async void Start_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        ++_epoch; _hits.Clear(); _submitted.Clear();
        await _session.StartAsync(_environment, _links.ToArray());
    });
    private async void Pause_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        await _session.PauseAsync(); ++_epoch;
        StatusText.Text = "采集已暂停，网页实时连接及手动前往仍保留。关闭会话可完全释放浏览器。";
    });
    private async void Close_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        await _session.CloseAsync(); ++_epoch;
        StatusText.Text = "会话已关闭，登录保存在该环境专属目录。";
    });
    public async Task ShutdownAsync()
    {
        _closing = true; ++_epoch; RefreshEnabled();
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
    private void HitSelected(object sender, SelectionChangedEventArgs e) => DetailText.Text = (HitList.SelectedItem as SearchHit)?.Detail ?? "";
    private async void Travel_Click(object sender, RoutedEventArgs e)
    {
        if (HitList.SelectedItem is not SearchHit hit) return;
        await Run(async () =>
        {
            var key = hit.MonitorId + ":" + hit.Id;
            if (_submitted.Contains(key)) { StatusText.Text = "这条结果已提交前往请求。"; return; }
            if (await _session.TravelAsync(hit))
            {
                _submitted.Add(key);
                StatusText.Text = "已提交一次前往请求，请查看游戏。";
            }
            else StatusText.Text = "当前网页已没有该结果或前往按钮不可用。";
        });
    }
    private void ClearHits_Click(object sender, RoutedEventArgs e) { _hits.Clear(); DetailText.Clear(); }
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
}
