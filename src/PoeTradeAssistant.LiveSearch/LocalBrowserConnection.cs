namespace PoeTradeAssistant.LiveSearch;

public static class LocalBrowserConnection
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(1) };

    public static string Endpoint
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("POE_TRADE_BROWSER_CDP_URL")?.Trim();
            return string.IsNullOrWhiteSpace(value) ? "http://127.0.0.1:9222" : value;
        }
    }

    public static async Task<SystemBrowserRuntime?> EnsureAvailableAsync(string dataDirectory, CancellationToken cancellationToken = default)
    {
        var endpoint = Endpoint;
        if (await IsAvailableAsync(endpoint, cancellationToken)) return null;

        // A custom endpoint is assumed to be managed by an advanced user or another machine.
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POE_TRADE_BROWSER_CDP_URL")))
            throw new InvalidOperationException($"无法连接自定义浏览器地址：{endpoint}");

        var runtime = SystemBrowserLocator.Resolve();
        var profile = Path.Combine(dataDirectory, "local-browser-profile-" + runtime.Channel);
        Directory.CreateDirectory(profile);
        var start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = runtime.ExecutablePath,
            UseShellExecute = false
        };
        start.ArgumentList.Add("--remote-debugging-port=9222");
        start.ArgumentList.Add("--user-data-dir=" + profile);
        start.ArgumentList.Add("--no-first-run");
        start.ArgumentList.Add("--no-default-browser-check");
        System.Diagnostics.Process.Start(start)?.Dispose();

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsAvailableAsync(endpoint, cancellationToken)) return runtime;
            await Task.Delay(250, cancellationToken);
        }
        throw new InvalidOperationException($"已启动 {runtime.Label}，但未能建立浏览器连接。请关闭该窗口后重试。");
    }

    internal static async Task<bool> IsAvailableAsync(string endpoint, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Http.GetAsync(endpoint.TrimEnd('/') + "/json/version", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
    }

    public static string ConnectionHelp(string endpoint) =>
        $"无法连接本地浏览器实例（{endpoint}）。请关闭刚启动的浏览器窗口后重试。";
}
