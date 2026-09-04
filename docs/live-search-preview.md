# 系统浏览器实时搜索试用版

日期：2026-09-05。开发分支：`codex/live-search-system-browser`。

## 实现范围

- 新增独立 `PoeTradeAssistant.LiveSearch` C# 模块和 WPF 实时搜索页。
- 使用 Microsoft.Playwright 1.60.0 的 Windows 驱动，优先启动本机 Google Chrome，未安装时回退 Microsoft Edge。
- 不下载或携带 Playwright Chromium；两个系统浏览器都不存在时显示明确错误，扫描器和计算器仍可使用。
- 游戏与区服组成四个独立环境，各自保存链接和专属登录 profile。
- 链接库增删改、启停、V1导入导出；最多200条，同时启用10条。
- 支持登录验证、启动/全部重连、暂停采集、关闭会话、命中详情、提示音和手动前往。
- 初始快照只展示，后续命中提醒；本次会话按监控ID和结果ID去重。
- 浏览器会话打开时禁止切换游戏或开始扫价；扫价时不能打开浏览器或前往。

系统浏览器使用本应用自己的用户数据目录，不读取用户日常使用的 Default profile：

```text
%LOCALAPPDATA%/PoeTradeAssistant/live-search/<game>/<region>/browser-profile-chrome
%LOCALAPPDATA%/PoeTradeAssistant/live-search/<game>/<region>/browser-profile-msedge
```

首次使用仍需在应用打开的浏览器窗口登录。切换 Chrome/Edge 时登录态不混用；导出文件不包含登录信息。

## 使用

1. 在侧栏选择游戏，进入“实时搜索”，选择国际服或国服。
2. 添加当前环境的完整官方搜索URL，包含赛季和搜索ID。
3. 点击“打开登录”，在 Chrome 或 Edge 中登录并处理可能出现的站点验证。
4. 返回应用点击“验证登录”，通过后点击“启动 / 全部重连”。
5. 选中命中可查看详情或手动前往。修改链接后需要重新启动监控。
6. “暂停采集”只停止读结果；“关闭会话”关闭本应用创建的浏览器。

默认按 Chrome → Edge 选择。调试时可指定优先顺序；指定项不存在时仍会回退：

```powershell
$env:POE_TRADE_BROWSER_CHANNEL = 'msedge' # 或 chrome
```

## 构建与验证

```powershell
dotnet test .\PoeTradeAssistant.sln -c Release
.\scripts\publish-win-x64.ps1 -SkipLaunch
dotnet run --project .\tools\LiveSearch.Smoke -c Release
```

发布只包含 win-x64 Playwright 驱动，不执行浏览器下载。分发必须复制整个目录，包括隐藏的 `.playwright` 目录。

浏览器冒烟测试使用检测到的系统浏览器。交易网页请求由本地模拟页面响应，不接触真实账号；覆盖中英文卡片、详情、禁用按钮、模拟登录、初始/更新命中、去重、单次点击、暂停和退出。

## 尚未实施及验证边界

- 自动前往策略、翻译、自定义声音、完整旧state迁移、单项重连和命中已读UI尚未迁移。
- 当前错误提示为会话状态文本，尚未提供每条监控的独立状态列。
- 页面选择器是首版适配，真实四区服登录、实时连接及游戏传送仍需实际验证。
- 未做真实10页长时间资源占用、跨机器发布或站点DOM变化兼容验证。
- 不向套利计算器写入上架价格。

扫描和计算器仍使用原生WPF与OCR链路；实时搜索会启动本应用拥有的系统浏览器子进程。
