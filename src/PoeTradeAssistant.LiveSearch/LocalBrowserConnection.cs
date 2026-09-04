namespace PoeTradeAssistant.LiveSearch;

public static class LocalBrowserConnection
{
    public static string Endpoint
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("POE_TRADE_BROWSER_CDP_URL")?.Trim();
            return string.IsNullOrWhiteSpace(value) ? "http://127.0.0.1:9222" : value;
        }
    }

    public static string ConnectionHelp(string endpoint) =>
        $"无法连接本地浏览器实例（{endpoint}）。请完全退出 Chrome/Edge，再用 --remote-debugging-port=9222 启动需要复用的浏览器实例；" +
        "若使用其他端口，请设置 POE_TRADE_BROWSER_CDP_URL。浏览器未开放调试端口时，应用无法接管已经运行的窗口。";
}
