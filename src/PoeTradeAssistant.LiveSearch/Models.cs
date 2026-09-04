using System.Text.Json;

namespace PoeTradeAssistant.LiveSearch;

public sealed record TradeEnvironment(string Game, string Region)
{
    public string Key => $"{Game}-{Region}";
    public string Host => Region == "china" ? "poe.game.qq.com" : "www.pathofexile.com";
    public string SearchPath => Game == "poe1" ? "/trade/search/" : "/trade2/search/poe2/";
    public string HomeUrl => $"https://{Host}{SearchPath}" + (Region == "china" ? "" : "Standard");
    public void Validate()
    {
        if (Game is not ("poe1" or "poe2") || Region is not ("international" or "china"))
            throw new ArgumentException("不支持的游戏或区服。");
    }
    public string ValidateUrl(string text)
    {
        Validate();
        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Host != Host ||
            !uri.AbsolutePath.StartsWith(SearchPath, StringComparison.Ordinal) ||
            uri.AbsolutePath[SearchPath.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries).Length < 2)
            throw new ArgumentException("请输入当前游戏和区服的官方搜索链接（包含赛季及搜索ID）。");
        return uri.AbsoluteUri;
    }
}

public sealed record SearchLink(string Id, string Name, string Url, bool Enabled = true);
public sealed record SearchWorkspace(int Version, List<SearchLink> Links);
public sealed record LinkExport(int Version, string Region, List<SharedLink> Links);
public sealed record SharedLink(string Name, string LiveSearchUrl, string Notes = "");
public sealed record SearchHit(string Id, string MonitorId, string MonitorName, string Title,
    string Price, string Detail, string Url, bool CanTravel, bool Initial, DateTimeOffset SeenAt);

public sealed class SearchStorage(string root)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public string DirectoryFor(TradeEnvironment environment)
    {
        environment.Validate();
        return Path.Combine(root, environment.Game, environment.Region);
    }
    public SearchWorkspace Load(TradeEnvironment environment)
    {
        var path = Path.Combine(DirectoryFor(environment), "workspace.json");
        if (!File.Exists(path)) return new(1, []);
        var data = JsonSerializer.Deserialize<SearchWorkspace>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("实时搜索工作区为空。");
        if (data.Version != 1 || data.Links is null || data.Links.Count > 200)
            throw new InvalidDataException("实时搜索工作区版本或容量不正确，原文件已保留。");
        ValidateLinks(environment, data.Links);
        return data;
    }
    public void Save(TradeEnvironment environment, IEnumerable<SearchLink> links)
    {
        var snapshot = links.ToList();
        ValidateLinks(environment, snapshot);
        var path = Path.Combine(DirectoryFor(environment), "workspace.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(file, new SearchWorkspace(1, snapshot), JsonOptions);
                file.Flush(true);
            }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static void ValidateLinks(TradeEnvironment environment, IReadOnlyList<SearchLink> links)
    {
        if (links.Count > 200 || links.Count(x => x.Enabled) > 10)
            throw new ArgumentException("最多保存200个链接，同时启用10个监控。");
        if (links.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.Name)) ||
            links.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != links.Count)
            throw new InvalidDataException("链接名称或ID无效。");
        foreach (var link in links) environment.ValidateUrl(link.Url);
    }
}
