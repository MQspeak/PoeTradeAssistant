namespace PoeTradeAssistant.LiveSearch;

public sealed record SystemBrowserRuntime(string Channel, string Label, string ExecutablePath);

public static class SystemBrowserLocator
{
    public static SystemBrowserRuntime Resolve(string? requestedChannel = null) =>
        Resolve(requestedChannel, File.Exists) ?? throw new InvalidOperationException(
            "未检测到可用的 Google Chrome 或 Microsoft Edge。请先安装其中一个浏览器，再打开实时搜索。");

    internal static SystemBrowserRuntime? Resolve(string? requestedChannel, Func<string, bool> exists)
    {
        var requested = (requestedChannel ?? Environment.GetEnvironmentVariable("POE_TRADE_BROWSER_CHANNEL") ?? "auto")
            .Trim().ToLowerInvariant();
        var order = requested switch
        {
            "chrome" => new[] { "chrome", "msedge" },
            "msedge" or "edge" => new[] { "msedge", "chrome" },
            _ => new[] { "chrome", "msedge" }
        };
        foreach (var channel in order)
        {
            var path = Candidates(channel).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x) && exists(x));
            if (path is not null)
                return new(channel, channel == "chrome" ? "Google Chrome" : "Microsoft Edge", path);
        }
        return null;
    }

    private static IEnumerable<string> Candidates(string channel)
    {
        var executable = channel == "chrome" ? "chrome.exe" : "msedge.exe";
        var vendorPath = channel == "chrome"
            ? Path.Combine("Google", "Chrome", "Application", executable)
            : Path.Combine("Microsoft", "Edge", "Application", executable);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), vendorPath);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), vendorPath);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), vendorPath);
        if (!OperatingSystem.IsWindows()) yield break;
        foreach (var hive in new[] { "HKEY_CURRENT_USER", "HKEY_LOCAL_MACHINE" })
        {
            var registered = Microsoft.Win32.Registry.GetValue(
                $@"{hive}\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{executable}", "", null) as string;
            if (!string.IsNullOrWhiteSpace(registered)) yield return registered.Trim('"');
        }
    }
}
