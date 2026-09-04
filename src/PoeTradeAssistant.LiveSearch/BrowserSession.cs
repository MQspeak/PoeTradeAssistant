using Microsoft.Playwright;
using System.Reflection;
using System.Text.Json;

namespace PoeTradeAssistant.LiveSearch;

/// <summary>Owned Chromium session. Public lifecycle operations are serialized by the host.</summary>
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
    private IBrowserContext? _context;
    private IPage? _loginPage;
    private readonly Dictionary<string, IPage> _pages = [];
    private CancellationTokenSource? _captureCancellation;
    private Task _captureTask = Task.CompletedTask;
    private long _generation;
    private bool _validated;
    public bool IsOpen => _context is not null;
    public event Action<SearchHit>? Hit;
    public event Action<string>? Status;
    public static string CardsScript { get; } = ReadScript("cards.js");

    private static string ReadScript(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"PoeTradeAssistant.LiveSearch.Scripts.{name}")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static void ConfigureBundledBrowser()
    {
        // Release always uses its own browser; development may use an explicitly configured cache.
        var bundled = Path.Combine(AppContext.BaseDirectory, "browsers");
        if (Directory.Exists(bundled)) Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", bundled);
    }

    public async Task OpenLoginAsync(TradeEnvironment environment, string dataDirectory)
    {
        environment.Validate();
        if (_context is not null)
        {
            _loginPage = _context.Pages.FirstOrDefault(x => !x.IsClosed) ?? await _context.NewPageAsync();
            await _loginPage.BringToFrontAsync();
            return;
        }
        ConfigureBundledBrowser();
        _playwright = await Playwright.CreateAsync();
        try
        {
            Directory.CreateDirectory(dataDirectory);
            _context = await _playwright.Chromium.LaunchPersistentContextAsync(Path.Combine(dataDirectory, "browser-profile"), new()
            {
                Headless = _headless, Channel = "chromium", Timeout = 30000,
                ViewportSize = new() { Width = 1440, Height = 900 }
            });
            _validated = false;
            if (_configureContext is not null) await _configureContext(_context);
            _context.Close += (_, _) => { _validated = false; Status?.Invoke("浏览器已关闭，请先关闭会话，再重新登录。"); };
            _loginPage = _context.Pages.FirstOrDefault() ?? await _context.NewPageAsync();
            await _loginPage.GotoAsync(environment.HomeUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 45000 });
            Status?.Invoke("请在 Chromium 中完成登录，再点击“验证登录”。浏览器可手动最小化。");
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
                Status?.Invoke("登录验证通过。可以启动已启用的监控链接。");
                return;
            }
            await Task.Delay(500);
        }
        throw new InvalidOperationException("未确认有效的交易页登录态，请在浏览器登录后再次验证。");
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
                try
                {
                    var page = await _context!.NewPageAsync();
                    _pages[link.Id] = page;
                    Status?.Invoke($"正在连接：{link.Name}");
                    await page.GotoAsync(environment.ValidateUrl(link.Url), new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
                    await ActivateLiveAsync(page, token);
                    token.ThrowIfCancellationRequested();
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
            if (token.IsCancellationRequested || generation != _generation) return;
            var id = card.GetProperty("id").GetString()!;
            if (!seen.Add(link.Id + ":" + id)) continue;
            // Bound a session explicitly instead of silently evicting IDs and notifying twice.
            if (seen.Count > 100000) throw new InvalidOperationException("本会话命中数量已达上限，请重新启动监控。");
            Hit?.Invoke(new(id, link.Id, link.Name, card.GetProperty("title").GetString()!,
                card.GetProperty("price").GetString()!, card.GetProperty("detail").GetString()!,
                card.GetProperty("url").GetString()!, card.GetProperty("canTravel").GetBoolean(), initial, DateTimeOffset.Now));
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
            """, hit.Id);
    }

    public async Task PauseAsync()
    {
        ++_generation;
        _captureCancellation?.Cancel();
        await _captureTask;
        _captureCancellation?.Dispose();
        _captureCancellation = null;
    }

    public async Task CloseAsync()
    {
        ++_generation;
        _validated = false;
        _captureCancellation?.Cancel();
        var context = _context;
        // Closing pages interrupts pending navigation before waiting for the capture loop.
        if (context is not null)
            await context.CloseAsync();
        await PauseAsync();
        _pages.Clear();
        _loginPage = null;
        _context = null;
        _playwright?.Dispose();
        _playwright = null;
    }
    public async ValueTask DisposeAsync() => await CloseAsync();
}
