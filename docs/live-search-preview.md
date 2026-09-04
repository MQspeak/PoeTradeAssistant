# 完整 Chromium 实时搜索试用版

日期：2026-09-05。开发分支：`codex/live-search-chromium`。

## 本次验证结果

- Release构建成功；原生界面导航和深色列表经截图检查。
- 解决方案79项测试通过，其中12项为本次新增的环境URL、存储隔离/损坏保护、游戏切换保护测试。
- Chromium 148.0.7778.96（revision 1223）本地浏览器测试通过：中英文卡片、增量结果、详情、禁用按钮、模拟登录、监控、初始快照/更新、去重、单次前往点击、暂停和完整退出。
- 最终试用目录未压缩约1,220.1 MiB，浏览器415.8 MiB，Windows驱动102.3 MiB。只发布win-x64，未携带其他平台Node驱动和独立headless shell。
- Playwright版本与来源项目实际安装的1.60.0对齐。真实市集验证仍待完成。

## 已实现范围

- 新增独立 `PoeTradeAssistant.LiveSearch` C# 模块和 WPF 实时搜索页。
- 锁定 Microsoft.Playwright 1.60.0；发布时安装其匹配的完整 Chromium，使用 `Channel=chromium`，不依赖系统 Chrome/Edge。
- 顶部游戏选择与监控页面区服选择组成四环境，各自保存链接和专属浏览器 profile。
- 链接库增删改、启用/停用、原工具 V1 链接库导入导出；200条容量，最多10条启用监控。导入项默认停用，名称及URL去重；首版不迁移备注和旧账号profile。
- 可见的 Chromium 登录窗口、显式登录验证、启动/全部重连、暂停采集、关闭会话。
- 每秒读取官方网页结果DOM，区分初始快照与后续命中；本次会话按监控ID及结果ID去重。首期使用轮询，尚未迁移 MutationObserver。
- 最近50条内存结果、详情文本、后续命中提示音、手动前往。一次操作只触发一次点击，显示“已提交请求”，不宣称游戏已抵达。
- 暂停保留结果页，关闭会话释放浏览器；退出应用等待服务关闭。登录profile留在本地，以便下次登录。
- 浏览器会话打开或正在操作时，禁止切换游戏/区服和开始扫价；扫价中不能打开浏览器或前往。首期采取保守互斥。

## 使用

1. 在侧栏选择游戏，进入“实时搜索”，选择国际服或国服。
2. 输入名称和完整官方搜索URL，添加链接。URL必须属于当前游戏和区服，包含赛季及搜索ID。
3. 点击“打开登录”，在 Chromium 内通过官方网站正常登录；如网站要求安全验证，在该窗口中处理。
4. 返回应用点击“验证登录”，通过后点击“启动 / 全部重连”。浏览器可以手动最小化。
5. 查看命中，选中后查看详情或手动前往。修改链接后需重新启动监控。
6. “暂停采集”仅停止本工具读结果；“关闭会话”才关闭浏览器，并解除扫价/切换游戏的限制。

登录文件只保存在 `%LOCALAPPDATA%/PoeTradeAssistant/live-search/<game>/<region>/browser-profile`，不会写入导出的链接库。链接工作区使用版本化JSON及临时文件替换，读取失败保留原文件。

## 构建与发布

```powershell
dotnet test .\PoeTradeAssistant.sln -c Release
.\scripts\publish-win-x64.ps1 -SkipLaunch
```

需要项目指定的 .NET 10 SDK。发布需要联网下载匹配的 Chromium。脚本检查驱动和完整浏览器，失败不报告发布成功。浏览器安装在发布目录 `browsers`，驱动位于 `.playwright`；分发时必须复制整个目录，包含隐藏目录。

独立试用输出目录使用 `artifacts/live-search-chromium/win-x64`，以保留原 `artifacts/publish/win-x64` 版本。可用以下命令生成：

```powershell
dotnet publish .\src\Poe2MarketScanner.App\Poe2MarketScanner.App.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\live-search-chromium\win-x64
Start-Process .\artifacts\live-search-chromium\win-x64\PoeTradeAssistant.exe -ArgumentList '--install-chromium' -WindowStyle Hidden -Wait
```

单独安装模式不会打开主界面，写入 `chromium-install.log`。开发构建可安装浏览器到Playwright缓存；已存在发布目录 `browsers` 时优先使用随包浏览器。

浏览器功能测试（全部请求由本地模拟页面响应，不接触真实账号）：

```powershell
dotnet run --project .\tools\LiveSearch.Smoke -c Release -- .\artifacts\live-search-chromium\win-x64\browsers
```

## 尚未实施及验证边界

- 自动前往策略、翻译、自定义声音、完整旧state迁移、单项重连、命中已读UI尚未迁移。
- 当前错误提示为会话状态文本，尚未提供每条监控的独立状态列。
- 页面选择器是可测试的首版适配，真实四区服登录、实时连接及游戏传送仍需实际验证；未验证区服不应标为正式支持。
- 未做真实10页长时间资源占用、跨机器安装包或站点DOM变化兼容验证。
- 不向套利计算器写入上架价格。

本文件描述试用实现；主SDD中原先“完全不依赖浏览器”的描述对启用本分支实时搜索后的应用不再成立。扫描和计算器本身仍使用原生WPF与OCR链路。

