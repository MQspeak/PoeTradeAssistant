# 本地浏览器实例实时搜索试用版

日期：2026-09-05。开发分支：`codex/live-search-system-browser`。

## 浏览器连接方式

实时搜索通过 Microsoft.Playwright 1.60.0 的 Windows驱动连接本机 Chrome/Edge 已开放的 CDP 端口，默认地址是 `http://127.0.0.1:9222`。应用不会下载 Chromium，也不会再创建应用专属浏览器 Profile。

浏览器必须在启动时开放调试端口。普通方式启动后无法被应用事后接管；请先完全退出浏览器，再用以下方式启动需要连接的实例：

```powershell
& "$env:ProgramFiles\Google\Chrome\Application\chrome.exe" --remote-debugging-port=9222
# 或
& "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe" --remote-debugging-port=9222
```

如果浏览器版本或管理策略不允许在默认用户数据目录启用远程调试，需要使用策略允许的 Profile/启动配置。此时只有该调试实例内已有的登录状态能被复用。使用其他端口时设置：

```powershell
$env:POE_TRADE_BROWSER_CDP_URL = 'http://127.0.0.1:9333'
```

点击“关闭会话”或退出应用时，只会关闭本功能创建的登录页和监控页并断开 CDP 连接，不会退出浏览器，也不会关闭连接前已存在的标签页。

## 实现范围

- 游戏与区服组成四个独立环境，各自保存链接；登录状态由所连接的浏览器实例决定。
- 链接库增删改、启停、V1 导入导出；最多 200 条，同时启用 10 条。
- 支持登录验证、启动/全部重连、暂停采集、关闭连接、命中详情、提示音和手动前往。
- 初始快照只展示，后续命中提醒；本次会话按监控 ID 和结果 ID 去重。
- 浏览器连接打开时禁止切换游戏或开始扫价；扫价时不能连接浏览器或前往。

## 使用

1. 以 `--remote-debugging-port=9222` 启动 Chrome 或 Edge，并在该实例中完成网页登录。
2. 在侧栏选择游戏，进入“实时搜索”，选择国际服或国服。
3. 添加当前环境的完整官方搜索 URL，包含赛季和搜索 ID。
4. 点击“连接本地浏览器”，再点击“验证登录”。
5. 验证通过后点击“启动 / 全部重连”。选中命中可查看详情或手动前往。
6. “暂停采集”保留网页连接；“关闭会话”关闭本功能创建的页面并断开连接。

## 构建与验证

```powershell
dotnet test .\PoeTradeAssistant.sln -c Release
.\scripts\publish-win-x64.ps1 -SkipLaunch
dotnet run --project .\tools\LiveSearch.Smoke -c Release
```

发布只包含 win-x64 Playwright 驱动，不包含浏览器。分发必须复制整个目录，包括隐藏的 `.playwright` 目录。

浏览器冒烟测试仍使用本机系统浏览器和本地模拟页面，不接触真实账号；覆盖中英文卡片、详情、禁用按钮、模拟登录、初始/更新命中、去重、单次点击、暂停和退出。真实四区服登录、实时连接及游戏传送仍需实际验证。

自动前往策略、翻译、自定义声音、完整旧 state 迁移、单项重连、命中已读 UI 和每条监控独立状态尚未迁移。
