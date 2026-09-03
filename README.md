# PoeTradeAssistant

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

## Windows 打包

双击项目根目录的 `打包发布.bat`，或在 PowerShell 中执行：

```powershell
.\scripts\publish-win-x64.ps1
```

脚本会生成自包含的 `artifacts\publish\win-x64\PoeTradeAssistant.exe` 及其运行依赖，并输出可分发的 `artifacts\PoeTradeAssistant-win-x64.zip`。请先完整解压 ZIP，再从解压后的文件夹启动 `PoeTradeAssistant.exe`；不要只单独复制 EXE。该方式携带 WPF UI、OCR 模型与本机运行库，不需要网页资源或 Microsoft Edge WebView2 Runtime。

旧的 `web/calculator` 目录只作为功能迁移参考，不会被编译、打包或在运行时加载；符文套利与公式套利将在后续原生页面中按相同的数据模型迁移。
