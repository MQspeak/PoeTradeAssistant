using Microsoft.Playwright;
using System.Reflection;
using System.Text.Json;

namespace PoeTradeAssistant.LiveSearch;

/// <summary>CDP connection to a local browser instance. Public lifecycle operations are serialized by the host.</summary>
public sealed class BrowserSession : IAsyncDisposable
{
    private readonly bool _headless;
    private readonly Func<IBrowserContext, Task>? _configureContext;
    public BrowserSession() { }
    internal BrowserSession(bool headless, Func<IBrowserContext, Task> configureContext)
    {
        _headless = headless;
        _configureContext = configureContext;
    }
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _loginPage;
    private readonly Dictionary<string, IPage> _pages = [];
    private CancellationTokenSource? _captureCancellation;
    private Task _captureTask = Task.CompletedTask;
    private long _generation;
    private bool _validated;
    private SystemBrowserRuntime? _runtime;
    private bool _ownsContext;
    private bool _canHideWindow;
    private bool _closingSession;
    private readonly BrowserWindow _window = new();
    public event Action? Disconnected;
    private void OnDisconnected()
    {
        if (_closingSession || _context is null) return;
        _validated = false;
        ++_generation;
        _captureCancellation?.Cancel();
        _window.Show();
        Disconnected?.Invoke();
    }
    public async Task ShowBrowserAsync()
    {
        _window.Show();
        if (_loginPage is { IsClosed: false }) await _loginPage.BringToFrontAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }
    private readonly HashSet<IPage> _createdPages = [];
    public bool IsValidated => _validated;
    private readonly HashSet<string> _stopped = [];
    public bool IsMonitorActive(string id) => !_stopped.Contains(id) && !_captureTask.IsCompleted;
    public bool IsMonitoring => !_captureTask.IsCompleted && _pages.Keys.Any(id => !_stopped.Contains(id));
    public bool IsOpen => _context is not null;
    public string BrowserLabel => _runtime?.Label ?? "本地浏览器实例";
    public event Action<SearchHit>? Hit;
    public event Action<string>? Status;
    public static string CardsScript { get; } = ReadScript("cards.js");

    private static string ReadScript(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"PoeTradeAssistant.LiveSearch.Scripts.{name}")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public async Task OpenLoginAsync(TradeEnvironment environment, string dataDirectory)
    {
        environment.Validate();
        if (_context is not null)
        {
            if (_loginPage is null || _loginPage.IsClosed)
            {
                _loginPage = await _context.NewPageAsync();
                _createdPages.Add(_loginPage);
                await _loginPage.GotoAsync(environment.HomeUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 45000 });
            }
            await ShowBrowserAsync();
            return;
        }
        _playwright = await Playwright.CreateAsync();
        try
        {
            if (_configureContext is not null)
            {
                // Test harness owns this isolated context.
                _runtime = SystemBrowserLocator.Resolve();
                Directory.CreateDirectory(dataDirectory);
                _context = await _playwright.Chromium.LaunchPersistentContextAsync(
                    Path.Combine(dataDirectory, "browser-profile-" + _runtime.Channel), new()
                {
                    Headless = _headless, Channel = _runtime.Channel, Timeout = 30000,
                    ViewportSize = new() { Width = 1440, Height = 900 }
                });
                _ownsContext = true;
                await _configureContext(_context);
            }
            else
            {
                var endpoint = LocalBrowserConnection.Endpoint;
                var launchedRuntime = await LocalBrowserConnection.EnsureAvailableAsync(dataDirectory);
                _canHideWindow = launchedRuntime is not null;
                try
                {
                    _browser = await _playwright.Chromium.ConnectOverCDPAsync(endpoint, new() { Timeout = 10000 });
                }
                catch (PlaywrightException error)
                {
                    throw new InvalidOperationException(LocalBrowserConnection.ConnectionHelp(endpoint), error);
                }
                _context = _browser.Contexts.FirstOrDefault() ?? throw new InvalidOperationException(
                    "本地浏览器没有可连接的上下文，请确认调试实例已经打开一个窗口。");
                _runtime = launchedRuntime is null
                    ? new("cdp", "已运行的本地 Chrome/Edge", endpoint)
                    : launchedRuntime with { Label = launchedRuntime.Label + "（本机）" };
                _browser.Disconnected += (_, _) => OnDisconnected();
            }
            _validated = false;
            _context.Close += (_, _) => OnDisconnected();
            _loginPage = await _context.NewPageAsync();
            _createdPages.Add(_loginPage);
            await _loginPage.GotoAsync(environment.HomeUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 45000 });
            Status?.Invoke($"已连接 {_runtime.Label}。请确认页面登录状态，再点击“验证登录”。");
        }
        catch { await CloseAsync(); throw; }
    }

    public async Task ValidateLoginAsync(TradeEnvironment environment)
    {
        _validated = false;
        if (_loginPage is null || _loginPage.IsClosed) throw new InvalidOperationException("请先打开登录浏览器。");
        await _loginPage.GotoAsync(environment.HomeUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 45000 });
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            var state = await ReadPageStateAsync(_loginPage);
            if (state == "challenge") throw new InvalidOperationException("请在浏览器完成站点验证后重试。");
            var loggedIn = await _loginPage.Locator("a[href*='/logout'], a[href*='signout']").CountAsync() > 0;
            var cookies = await _context!.CookiesAsync(new[] { environment.HomeUrl });
            var sessionCookie = cookies.Any(c => c.Name == "POESESSID" && !string.IsNullOrEmpty(c.Value));
            if (state == "ready" && (loggedIn || sessionCookie))
            {
                _validated = true;
                var hidden = await HideBrowserAsync(_loginPage);
                Status?.Invoke(hidden ? "登录验证成功，浏览器已隐藏。" : "登录验证成功。当前浏览器窗口保留可见，可继续监控。");
                return;
            }
            await Task.Delay(500);
        }
        throw new InvalidOperationException("未确认有效的交易页登录态，请在浏览器登录后再次验证。");
    }

    private async Task<bool> HideBrowserAsync(IPage page)
    {
        if (!_canHideWindow) return false;
        var marker = "PoeTradeAssistant-" + Guid.NewGuid().ToString("N");
        string? title = null;
        try
        {
            title = await page.TitleAsync().WaitAsync(TimeSpan.FromSeconds(3));
            await page.EvaluateAsync("title => document.title = title", marker).WaitAsync(TimeSpan.FromSeconds(3));
            await page.BringToFrontAsync().WaitAsync(TimeSpan.FromSeconds(3));
            for (var i = 0; i < 10; i++)
            {
                if (_window.Hide(marker)) return true;
                await Task.Delay(100);
            }
        }
        catch (Exception error) when (error is PlaywrightException or TimeoutException) { }
        finally
        {
            if (title is not null && !page.IsClosed)
                try { await page.EvaluateAsync("title => document.title = title", title).WaitAsync(TimeSpan.FromSeconds(2)); }
                catch (Exception error) when (error is PlaywrightException or TimeoutException) { }
        }
        return false;
    }

    public async Task StartAsync(TradeEnvironment environment, IReadOnlyList<SearchLink> links)
    {
        SearchStorage.ValidateLinks(environment, links);
        if (!_validated || _context is null) throw new InvalidOperationException("请先完成登录验证。");
        var enabled = links.Where(x => x.Enabled).ToArray();
        if (enabled.Length == 0) throw new InvalidOperationException("请至少启用一个监控链接。");
        await PauseAsync();
        foreach (var page in _pages.Values) if (!page.IsClosed) await page.CloseAsync();
        _pages.Clear();
        _stopped.Clear();
        _captureCancellation = new();
        var generation = ++_generation;
        _captureTask = CaptureAsync(environment, enabled, generation, _captureCancellation.Token);
    }

    private async Task CaptureAsync(TradeEnvironment environment, SearchLink[] links, long generation, CancellationToken token)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ready = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (var link in links)
            {
                token.ThrowIfCancellationRequested();
                if (_stopped.Contains(link.Id)) continue;
                try
                {
                    var page = await _context!.NewPageAsync();
                    _createdPages.Add(page);
                    _pages[link.Id] = page;
                    if (token.IsCancellationRequested || _stopped.Contains(link.Id))
                    {
                        await page.CloseAsync();
                        token.ThrowIfCancellationRequested();
                        continue;
                    }
                    Status?.Invoke($"正在连接：{link.Name}");
                    await page.GotoAsync(environment.ValidateUrl(link.Url), new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
                    await ActivateLiveAsync(page, token);
                    token.ThrowIfCancellationRequested();
                    if (_stopped.Contains(link.Id)) continue;
                    var cards = await page.EvaluateAsync<JsonElement>(CardsScript);
                    EmitCards(link, cards, seen, true, generation, token);
                    ready.Add(link.Id);
                    Status?.Invoke($"正在监控：{link.Name}");
                }
                catch (PlaywrightException error) { Status?.Invoke($"{link.Name}：{error.Message}"); }
                catch (InvalidOperationException error) { Status?.Invoke($"{link.Name}：{error.Message}"); }
            }
            while (ready.Count > 0)
            {
                await Task.Delay(1000, token);
                foreach (var link in links.Where(x => ready.Contains(x.Id)))
                {
                    token.ThrowIfCancellationRequested();
                    if (_stopped.Contains(link.Id)) { ready.Remove(link.Id); continue; }
                    try
                    {
                        var page = _pages[link.Id];
                        if (await ReadPageStateAsync(page) != "ready") throw new InvalidOperationException("页面失效或要求登录/验证，请停止后重连。");
                        if (!await IsLiveAsync(page)) throw new InvalidOperationException("实时搜索已断开，请停止后重连。");
                        var cards = await page.EvaluateAsync<JsonElement>(CardsScript);
                        EmitCards(link, cards, seen, false, generation, token);
                    }
                    catch (PlaywrightException error) { ready.Remove(link.Id); Status?.Invoke($"{link.Name}：{error.Message}"); }
                    catch (InvalidOperationException error) { ready.Remove(link.Id); Status?.Invoke($"{link.Name}：{error.Message}"); }
                }
            }
            Status?.Invoke("没有可用的监控页面，请检查浏览器后重连。");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { Status?.Invoke($"监控停止：{error.Message}"); }
    }

    private void EmitCards(SearchLink link, JsonElement cards, HashSet<string> seen, bool initial, long generation, CancellationToken token)
    {
        foreach (var card in cards.EnumerateArray())
        {
            if (token.IsCancellationRequested || generation != _generation || _stopped.Contains(link.Id)) return;
            var id = card.GetProperty("id").GetString()!;
            if (!seen.Add(link.Id + ":" + id)) continue;
            // Bound a session explicitly instead of silently evicting IDs and notifying twice.
            if (seen.Count > 100000) throw new InvalidOperationException("本会话命中数量已达上限，请重新启动监控。");
            Hit?.Invoke(new(id, link.Id, link.Name, card.GetProperty("title").GetString()!,
                card.GetProperty("price").GetString()!, card.GetProperty("detail").GetString()!,
                card.GetProperty("url").GetString()!, card.GetProperty("canTravel").GetBoolean(), initial, DateTimeOffset.Now, card.GetProperty("titleColor").GetString()!));
        }
    }

    private static async Task ActivateLiveAsync(IPage page, CancellationToken token)
    {
        var deadline = DateTime.UtcNow.AddSeconds(25);
        var clicked = false;
        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();
            var state = await ReadPageStateAsync(page);
            if (state == "challenge") throw new InvalidOperationException("请在浏览器完成站点验证，然后重连。");
            if (state == "login") throw new InvalidOperationException("需要登录，请重新验证登录。");
            if (await IsLiveAsync(page)) return;
            if (!clicked)
                clicked = await page.EvaluateAsync<bool>("""
                    () => {
                        const button = [...document.querySelectorAll('button, a')].find(x =>
                            /^(activate live search|启动实时搜索|啟動即時搜尋|开启实时搜索|開啟即時搜尋)$/i.test(x.textContent.trim()));
                        if (!button || button.disabled) return false;
                        button.click(); return true;
                    }
                    """);
            await Task.Delay(500, token);
        }
        throw new InvalidOperationException("未确认实时搜索已启动，请在浏览器启用实时搜索后重连。");
    }

    private static Task<bool> IsLiveAsync(IPage page) => page.EvaluateAsync<bool>("""
        () => /deactivate live search|live search:\s*searching|停止实时搜索|停用即時搜尋|关闭实时搜索|關閉即時搜尋/i.test(document.body.innerText)
        """);

    private static Task<string> ReadPageStateAsync(IPage page) => page.EvaluateAsync<string>("""
        () => {
            const text = document.body?.innerText || '';
            if (/just a moment|verify you are human|checking your browser|安全验证|安全驗證/i.test(text)) return 'challenge';
            if (/\/login(?:[/?#]|$)/i.test(location.href) || /you must (?:be logged in|sign in)|请先登录|請先登入/i.test(text)) return 'login';
            return document.querySelector('#trade, .trade, .search-panel, .search-bar, .resultset, .search-results') ? 'ready' : 'loading';
        }
        """);

    public async Task<bool> TravelAsync(SearchHit hit)
    {
        if (!_pages.TryGetValue(hit.MonitorId, out var page) || page.IsClosed) return false;
        // Exactly one DOM click; true means submitted, not proof of in-game arrival.
        return await page.EvaluateAsync<bool>("""
            id => {
                const card = [...document.querySelectorAll('.resultset [data-id], .search-results [data-id]')].find(x => x.dataset.id === id);
                if (!card) return false;
                const button = [...card.querySelectorAll('button, a, [role="button"]')].find(x =>
                    /travel to hideout|前往藏身[处處]/i.test(x.textContent || '') || x.classList.contains('direct-btn'));
                if (!button || button.disabled || button.getAttribute('aria-disabled') === 'true') return false;
                button.click(); return true;
            }
            """, hit.Id).WaitAsync(TimeSpan.FromSeconds(5));
    }

    public async Task StopMonitorAsync(string id)
    {
        _stopped.Add(id);
        if (_pages.TryGetValue(id, out var page) && !page.IsClosed) await page.CloseAsync();
        Status?.Invoke("监控已停止。");
    }

    public async Task StopAllAsync()
    {
        ++_generation;
        _captureCancellation?.Cancel();
        foreach (var id in _pages.Keys.ToArray()) _stopped.Add(id);
        foreach (var page in _pages.Values.ToArray()) if (!page.IsClosed) await page.CloseAsync();
        await PauseAsync();
    }

    public async Task PauseAsync()
    {
        ++_generation;
        _captureCancellation?.Cancel();
        await _captureTask.WaitAsync(TimeSpan.FromSeconds(5));
        _captureCancellation?.Dispose();
        _captureCancellation = null;
    }

    public async Task CloseAsync()
    {
        if (_closingSession) return;
        _closingSession = true;
        ++_generation;
        _validated = false;
        _captureCancellation?.Cancel();
        _window.Show();
        try
        {
            var context = _context;
            var tasks = _ownsContext && context is not null
                ? new[] { context.CloseAsync() }
                : _createdPages.Where(p => !p.IsClosed).Select(p => p.CloseAsync()).ToArray();
            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));
            await _captureTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (Exception error) when (error is PlaywrightException or TimeoutException or OperationCanceledException) { }
        finally
        {
            _context = null;
            _pages.Clear();
            _createdPages.Clear();
            _loginPage = null;
            _browser = null;
            _runtime = null;
            _ownsContext = false;
            _canHideWindow = false;
            _playwright?.Dispose();
            _playwright = null;
            _closingSession = false;
        }
    }
    public async ValueTask DisposeAsync() => await CloseAsync();
}
