# PoeTradeAssistant

当前开发分支已接入“实时搜索 · 本地浏览器实例”，通过 CDP 连接用户启动的 Chrome/Edge 调试实例并复用其中的登录态。功能范围、运行步骤及验证边界见 [试用说明](docs/live-search-preview.md)。以下关于无浏览器依赖的描述仅适用于扫描器和原生计算器；监控模块仍会携带 Playwright 的 Windows 驱动。

统一 POE 交易辅助项目的实施根目录。扫描器、计算器、测试与合并代码均位于本目录；原始两个项目保留为未改动的历史副本。

当前已实现：

- `PoeTradeAssistant.Contracts`：`poe-trade-scan/v2` 的版本化 JSON 合约；
- `PoeTradeAssistant.ScannerIntegration`：将现有扫描批次结果及计算器旧 V1 价格表转换为 V2；
- `PoeTradeAssistant.ScannerIntegration.Tests`：比例方向、币种别名、状态保留与 V1 兼容回归测试。
- 原生 WPF 套利计算器：币种金币成本、基础交易对、标的 ROI / 净利润 / 金币效率、V2 价格表导入与本地工作区保存；扫描完成后会自动导入当前批次。

`PoeTradeAssistant.sln` 只引用本目录内的项目。扫描器源码位于 `src/Poe2MarketScanner.*`，计算器源码位于 `web/calculator`；现阶段保留原有项目名称，后续再统一重命名为 `PoeTradeAssistant.*`。

统一宿主基于 .NET 10 LTS、WPF 与 WPF UI Fluent 主题，要求 Windows 10 19041 或更高版本。套利计算器完全使用原生控件和 C# 计算，不依赖 WebView2、浏览器运行时或网页资源。

验证命令：

```powershell
dotnet test .\PoeTradeAssistant.sln
dotnet build .\PoeTradeAssistant.sln -c Release
```

## Windows 发布

打包需要 `global.json` 指定的 .NET SDK（当前为 10.0.400，允许同一主次版本中更高的功能版本），仅安装 .NET 6 或运行时无法编译。脚本会自动检查 `DOTNET_ROOT`、PATH、用户 SDK 目录以及本机临时工具目录中的兼容 SDK；自定义安装位置可通过 `DOTNET_ROOT` 指定。SDK 检查失败时不会清理已有发布文件。临时目录中的 SDK 若被清理，需要重新安装 SDK。

双击项目根目录的 `打包发布.bat`，或在 PowerShell 中执行：

```powershell
.\scripts\publish-win-x64.ps1
```

发布成功后，脚本会自动启动发布目录中的 `PoeTradeAssistant.exe`，并将该目录作为程序工作目录。若自动启动失败，会显示警告，已生成的发布文件仍可使用。再次发布前请先关闭该程序。

脚本只生成自包含的 `artifacts\publish\win-x64\PoeTradeAssistant.exe` 及其运行依赖，不再生成 ZIP 文件。如需分发，请复制整个发布目录，不要只单独复制 EXE。该方式携带 WPF UI、OCR 模型与本机运行库，不需要网页资源或 Microsoft Edge WebView2 Runtime。

旧的 `web/calculator` 目录只作为功能迁移参考，不会被编译、打包或在运行时加载；符文套利与公式套利将在后续原生页面中按相同的数据模型迁移。
