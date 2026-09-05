using Microsoft.Playwright;
using PoeTradeAssistant.LiveSearch;
using System.Text.Json;

var runtime = SystemBrowserLocator.Resolve();
using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new() { Channel = runtime.Channel, Headless = true });
var page = await browser.NewPageAsync();
await page.SetContentAsync("""
    <div class="search-results"><div class="row" data-id="item1">
      <div class="title">Vaal Regalia</div><div class="itemName">Doom Mantle</div>
      <div class="price">Asking Price 1 divine</div>
      <div class="explicitMod">+100 maximum life</div>
      <button class="direct-btn">Travel to Hideout</button>
    </div></div>
    """);
var cards = await page.EvaluateAsync<JsonElement>(BrowserSession.CardsScript);
if (cards.GetArrayLength() != 1 || cards[0].GetProperty("title").GetString() != "Doom Mantle" ||
    !cards[0].GetProperty("canTravel").GetBoolean() || !cards[0].GetProperty("detail").GetString()!.Contains("+100 maximum life"))
    throw new Exception("Baseline card extraction failed.");
await page.EvaluateAsync("() => document.querySelector('.search-results').insertAdjacentHTML('beforeend', '<div class=\"row\" data-id=\"item2\"><div class=\"itemName\">新物品</div><div class=\"price\">询价 2 divine</div><button disabled class=\"direct-btn\">前往藏身处</button></div>')");
cards = await page.EvaluateAsync<JsonElement>(BrowserSession.CardsScript);
if (cards.GetArrayLength() != 2 || cards[1].GetProperty("canTravel").GetBoolean()) throw new Exception("Update or disabled-action extraction failed.");
Console.WriteLine($"PASS: system {runtime.Label} {browser.Version}; English/Chinese cards, updates, details, disabled actions.");
await page.SetContentAsync("""
    <div class="resultset"><div data-id="verified-test" class="row">
    <div>Verified</div><div class="itemHeader rare"><div class="itemName">Verified</div><div class="itemName">Fate Rend</div></div>
    <div>Exact Price: 1×Divine Orb lovecoc#3942 listed just now</div>
    </div></div>
    """);
cards = await page.EvaluateAsync<JsonElement>(BrowserSession.CardsScript);
if (cards[0].GetProperty("title").GetString() != "Fate Rend" ||
    cards[0].GetProperty("price").GetString() != "1×Divine Orb" ||
    cards[0].GetProperty("titleColor").GetString() != "#FFFF77")
    throw new Exception("Verified, seller suffix or rarity regression.");
Console.WriteLine("PASS: Verified skipped; seller excluded; rare title color.");
await browser.CloseAsync();
if (browser.IsConnected) throw new Exception("Browser did not close.");
Console.WriteLine("PASS: browser shutdown.");

var root = Path.Combine(Path.GetTempPath(), "poe-live-smoke-" + Guid.NewGuid().ToString("N"));
IBrowserContext? ownedContext = null;
var contextClosed = false;
var initialHit = new TaskCompletionSource<SearchHit>(TaskCreationOptions.RunContinuationsAsynchronously);
var updateHit = new TaskCompletionSource<SearchHit>(TaskCreationOptions.RunContinuationsAsynchronously);
var hitCount = 0;
await using var session = new BrowserSession(true, async context =>
{
    ownedContext = context;
    context.Close += (_, _) => contextClosed = true;
    // All requests are fulfilled locally: this does not log into or contact the trade site.
    await context.RouteAsync("**/*", route => route.FulfillAsync(new()
    {
        ContentType = "text/html", Body = """
            <div id="trade"><a href="/logout">Log out</a><button>Deactivate Live Search</button>
            <div class="resultset"><div class="row" data-id="baseline">
              <div class="itemName">Baseline</div><div class="price">Asking Price 1 divine</div>
              <button class="direct-btn" onclick="window.clickCount=(window.clickCount||0)+1">Travel to Hideout</button>
            </div></div></div>
            """
    }));
});
session.Hit += hit =>
{
    Interlocked.Increment(ref hitCount);
    if (hit.Initial) initialHit.TrySetResult(hit); else updateHit.TrySetResult(hit);
};
try
{
    var environment = new TradeEnvironment("poe1", "international");
    await session.OpenLoginAsync(environment, root);
    await session.ValidateLoginAsync(environment);
    await session.StartAsync(environment, [new("monitor", "Fixture", environment.HomeUrl + "/fixture")]);
    var baseline = await initialHit.Task.WaitAsync(TimeSpan.FromSeconds(10));
    var monitorPage = ownedContext!.Pages.Single(x => x.Url.EndsWith("/fixture", StringComparison.Ordinal));
    if (!await session.TravelAsync(baseline) || await monitorPage.EvaluateAsync<int>("() => window.clickCount") != 1)
        throw new Exception("Manual travel must dispatch exactly one click.");
    await monitorPage.EvaluateAsync("() => document.querySelector('.resultset').insertAdjacentHTML('beforeend', '<div class=\"row\" data-id=\"updated\"><div class=\"itemName\">Updated</div></div>')");
    var updated = await updateHit.Task.WaitAsync(TimeSpan.FromSeconds(10));
    if (updated.Initial || updated.Id != "updated") throw new Exception("Initial/update classification failed.");
    await Task.Delay(1500);
    if (hitCount != 2) throw new Exception("Duplicate notifications.");
    await session.PauseAsync();
    await monitorPage.EvaluateAsync("() => document.querySelector('.resultset').insertAdjacentHTML('beforeend', '<div class=\"row\" data-id=\"paused\"><div class=\"itemName\">Paused</div></div>')");
    await Task.Delay(1500);
    if (hitCount != 2 || monitorPage.IsClosed) throw new Exception("Pause should stop capture but retain the page.");
    await session.StartAsync(environment, [new("monitor", "Fixture", environment.HomeUrl + "/fixture"), new("second", "Second", environment.HomeUrl + "/second")]);
    var deadline = DateTime.UtcNow.AddSeconds(10);
    while (ownedContext.Pages.Count(x => x.Url.EndsWith("/second")) == 0 && DateTime.UtcNow < deadline) await Task.Delay(100);
    await session.StopMonitorAsync("monitor");
    if (session.IsMonitorActive("monitor") || !session.IsMonitorActive("second")) throw new Exception("Individual stop affected another monitor.");
    await session.StopAllAsync();
    if (session.IsMonitoring) throw new Exception("Stop all left monitors active.");
    Console.WriteLine("PASS: independent monitor stop and stop all.");
    await session.CloseAsync();
    if (session.IsOpen || !contextClosed) throw new Exception("Owned persistent context did not close.");
    Console.WriteLine("PASS: routed fixture login, monitoring, initial/update, deduplication, single travel click, pause, persistent-context shutdown.");
}
finally
{
    await session.CloseAsync();
    if (Directory.Exists(root)) Directory.Delete(root, true);
}
