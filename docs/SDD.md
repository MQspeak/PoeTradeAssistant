# PoeTradeAssistant 软件设计文档（SDD）

- 文档状态：As-built（依据当前源码）
- 版本：1.0
- 更新日期：2026-09-03
- 适用平台：Windows 10 19041 及以上

## 1. 目的与范围

PoeTradeAssistant 是一个面向 Path of Exile 1/2 市场交易场景的本地桌面工具，将“屏幕自动化扫价、OCR 解析、版本化价格数据、套利收益计算”整合在一个 WPF 应用中。

本文描述当前实际实现，而非未来规划。当前正式交付物是 `PoeTradeAssistant.exe`；`web/calculator` 是旧收益计算器的迁移参考，不进入解决方案、构建产物或运行链路。

### 1.1 系统目标

- 通过可配置屏幕锚点和区域自动查询一批目标物品。
- 识别金币消耗、买入比例、卖出比例及基础交易对比例。
- 同时输出兼容旧消费者的 V1 JSON 和方向明确的 V2 JSON。
- 将 V2 扫描结果导入原生计算器，计算 ROI、净利润和金币效率。
- 隔离 POE1 与 POE2 的配置和计算器工作区。

### 1.2 非目标

- 不直接调用游戏或交易站 API。
- 不提供云同步、账号系统或多人协作。
- 不保证在非 Windows 平台运行。
- 当前原生版本不包含旧 Web 工具中的符文套利、公式套利和完整三角套利页面。

## 2. 总体架构

系统采用分层的单进程桌面架构：

```text
WPF 界面 / MainWindow
        |
        v
MainViewModel -----------------> NativeCalculatorViewModel
        |                                  |
        v                                  v
扫描编排与 Windows 输入服务          V2 导入、持久化、收益计算
        |
        +--> 屏幕截图 --> PaddleOCR --> OcrTextParser
        |
        v
SellQueryBatchResult
        |
        v
ProjectOutputJsonWriter --> V1 JSON + PriceScanDocument V2
                                   |
                                   v
                        PriceScanDocumentAdapter
```

依赖方向保持为：`App -> ScannerIntegration -> Contracts`，同时 `App -> Core`、`ScannerIntegration -> Core`。`Core` 与 `Contracts` 不依赖 UI。

## 3. 代码组织

| 模块 | 路径 | 职责 |
|---|---|---|
| 桌面宿主 | `src/Poe2MarketScanner.App` | WPF UI、MVVM 状态、Windows 输入、截图、PaddleOCR、扫描编排、原生计算器 |
| 扫描核心 | `src/Poe2MarketScanner.Core` | 配置模型、配置归一化与存储、OCR 文本解析、自动化抽象和结果模型 |
| 数据合约 | `src/PoeTradeAssistant.Contracts` | `poe-trade-scan/v2` 及旧 V1 数据结构 |
| 集成适配 | `src/PoeTradeAssistant.ScannerIntegration` | 扫描批次/V1 表格到 V2 合约的转换、币种名称归一化 |
| 自动化测试 | `tests/*` | Core、App 和集成适配器的 xUnit 回归测试 |
| 旧版参考 | `web/calculator` | React/Vite 及历史单页计算器的迁移参考，不参与正式运行 |
| 发布脚本 | `scripts` | 全量测试/构建及 Windows 自包含打包 |

## 4. 关键组件设计

### 4.1 WPF 宿主与状态协调

`MainWindow` 负责界面事件、文件选择、叠加层显示以及服务生命周期；`MainViewModel` 是界面状态中心，负责：

- 加载、保存和重置当前游戏模式的扫描配置；
- 导入查询清单和外部配置；
- 在 POE1/POE2 间切换并保存离开前状态；
- 暴露锚点、截图区域、OCR 参数和交易模式；
- 管理独立的 `NativeCalculatorViewModel`；
- 承接 OCR 调试结果和预览解析。

视图模型实现 `INotifyPropertyChanged`；行级模型同样支持属性通知。当前未引入依赖注入容器，服务由应用代码显式构造。

### 4.2 配置与坐标模型

`AppProfile` 聚合以下配置：

- `Regions`：金币和比例等 OCR 截图区域；
- `Anchors`：搜索框、币种选择、数量输入、空白区等点击位置；
- `Ocr`：缩放、灰度、二值化、阈值和 Otsu 回退；
- `QueryList`：待扫描项目列表；
- `Automation`：通用、点击和输入延迟及金币识别开关；
- `SelectedTradeModeKey`、繁体中文开关和输出目录。

`JsonProfileStorageService` 负责 JSON 持久化，`AppProfileFactory` 提供默认配置，`AppProfileNormalizer` 对旧配置和缺失字段进行补全。相对坐标通过屏幕度量换算，以支持不同分辨率。POE1 和 POE2 使用各自目录，避免配置互相覆盖。

### 4.3 扫描自动化

`SellQueryAutomationRunner` 是批处理编排器。一次扫描的主流程为：

1. 校验查询列表并解析交易模式。
2. 读取当前基础交易对比例。
3. 固定买入侧币种，逐项搜索并采集金币成本与买入比例。
4. 固定卖出侧币种，逐项搜索并采集卖出比例。
5. 汇总批次结果并写入 JSON。

输入操作由 `IInputAutomationRunner` 抽象，生产实现 `WindowsInputAutomationRunner` 使用 Windows 输入能力执行点击、组合键、全选、退格和剪贴板粘贴。所有耗时操作接受 `CancellationToken`。

执行前由 `AutomationPreflightValidator` 检查必要锚点、区域和查询项。单个项目发生异常时记录为 `failed`，批次继续处理其他项目。

### 4.4 截图、OCR 与解析

`ScreenCaptureService` 截取配置区域。`SellQueryOcrReader` 与 `PaddleOcrDebugService` 使用 PaddleOCR，并按全局或区域级配置执行缩放、灰度和二值化预处理。

`OcrTextParser` 将识别文本解析为金币成本或比例。扫描器每次读取最多重试 8 次；成功状态为 `ok`，持续失败会保留最后一次原始/归一化结果，并附加 OCR 超时信息。OCR 调试模式会保存原图、处理图和识别摘要，便于校准区域与阈值。

### 4.5 数据合约与输出

`ProjectOutputJsonWriter` 每个批次写出两个文件：

- `<timestamp>.json`：旧中文字段 V1，维持兼容性；
- `<timestamp>.v2.json`：`poe-trade-scan/v2`，供新计算器导入。

V2 顶层包含 `schemaVersion`、扫描时间、交易模式、基础交易对和条目列表。每个比例包含：

- `raw`：原始展示文本；
- `left`、`right`：比例两侧数值；
- `rightPerLeft`：明确方向的单位换算值。

条目保留 `status`、`errorMessage` 和 `capturedAt`，消费者不得从展示字符串推断比例方向。`PriceScanDocumentAdapter` 负责扫描批次转换、旧 V1 导入及币种别名规范化；未知币种保持原值，避免新赛季数据丢失。

### 4.6 原生收益计算器

`NativeCalculatorViewModel` 管理三类实体：

- 物品：币种或标的物，以及每单位金币成本；
- 基础交易对：卖出币种、买入币种及 `1 卖出币种 = N 买入币种`；
- 标的：针对各币种保存价格，切换交易对时恢复对应值。

V2 导入仅接收正确的 schema 版本和有效基础比例；状态为 `ok` 或 `imported-v1` 的条目才进入计算器。导入时复用同名实体和已有交易对，随后保存工作区。

当前计算公式（以买入币种为计价单位）：

```text
卖出收入 = 标的卖出价 * 卖出币种兑买入币种汇率
净利润   = 卖出收入 - 标的买入价
ROI      = 净利润 / 标的买入价 * 100%
总金币成本 = 标的金币成本
           + 标的卖出价 * 卖出币种金币成本
           + 标的买入价 * 买入币种金币成本
```

当净利润大于零时，金币效率换算为“每赚 1 个卖出币种所消耗金币”。输入必须为正数；无效数据返回“待计算”及原因，不抛出 UI 级异常。

工作区 schema 当前为 V2；加载器兼容旧 V1，并在迁移后立即保存为新结构。

## 5. 运行时数据与文件布局

默认运行数据位于应用确定的配置根目录，并按 `poe1`、`poe2` 隔离。每个游戏模式保存扫描 profile 与 `calculator-workspace.json`。扫描输出目录可配置：绝对路径直接使用，相对路径相对于项目/应用工作根目录解析。

构建期和运行期生成内容不属于源码：`.vs`、`bin`、`obj`、`TestResults`、`artifacts`、前端 `node_modules/dist/*.tsbuildinfo`、扫描 `output`、用户 `profiles` 及 `ocr-debug` 均由 `.gitignore` 排除。

## 6. 关键用例与时序

### 6.1 批量扫价并导入

```text
用户 -> WPF：选择游戏/交易模式并开始扫描
WPF -> 预检器：验证锚点、区域、查询清单
WPF -> 扫描编排器：RunAsync(profile)
扫描编排器 -> Windows 输入：搜索和切换币种
扫描编排器 -> OCR：截图并读取价格
OCR -> 解析器：标准化金币及比例
扫描编排器 -> 输出器：写 V1 + V2 JSON
WPF -> 原生计算器：导入 PriceScanDocument
原生计算器 -> 工作区：保存实体、价格与选择状态
原生计算器 -> WPF：展示 ROI、净利润、金币效率
```

### 6.2 游戏模式切换

切换前保存当前 profile 和计算器工作区；切换后从目标模式目录加载两者，并刷新所有绑定。首次迁移时可将旧 POE2 工作区复制到新的隔离目录。

## 7. 异常处理与安全边界

- 自动化开始前校验配置完整性，避免在错误坐标上操作。
- 叠加层截图期间由 `OverlayCaptureGuard` 临时隐藏，完成后恢复，避免标注框污染 OCR 图像。
- 单项 OCR/输入异常写入条目状态，不丢弃整个批次已有结果。
- JSON 读取仅捕获文件和格式类异常，并向 UI 返回可诊断消息。
- 所有批量自动化响应取消令牌；界面应允许用户中止扫描。
- 工具会模拟键鼠并读取屏幕，运行前必须确保游戏窗口、分辨率和界面布局与当前 profile 一致。

## 8. 构建、测试与发布

开发验证：

```powershell
dotnet test .\PoeTradeAssistant.sln
dotnet build .\PoeTradeAssistant.sln -c Release
```

项目使用 `.NET 10 LTS` 与 WPF UI Fluent 控件库。`scripts/test-all.ps1` 顺序执行测试和 Release 构建。测试分为：

- Core：OCR 解析、默认 profile、存储、相对坐标、几何与结果写入契约；
- App：预检、输入计划、OCR 读取/预处理、扫描编排、输出、叠加层和主视图模型；
- Integration：比例方向、状态保留、币种别名及 V1 兼容。

发布由 `scripts/publish-win-x64.ps1` 完成：恢复依赖、生成基于 .NET 10 的自包含 Windows 应用，并压缩为 `artifacts/PoeTradeAssistant-win-x64.zip`。当前保持多文件发布，以便可靠装载 WPF、OCR 模型和本机运行库。

## 9. 质量属性与约束

| 属性 | 当前设计 |
|---|---|
| 可维护性 | Core/Contracts 与 WPF 分离；但 `MainViewModel`、`MainWindow` 和原生计算器类仍偏大 |
| 可测试性 | 输入、OCR、存储和输出均有接口或纯逻辑边界；WPF 交互主要靠单元/文本回归测试 |
| 兼容性 | V1/V2 扫描数据并存；计算器工作区支持 V1 到 V2 迁移 |
| 可恢复性 | 扫描结果和工作区落盘；单项失败不终止全批次 |
| 性能 | 串行扫描以稳定 UI/OCR 为优先；OCR 最多 8 次重试会增加失败路径延迟 |
| 可移植性 | 依赖 WPF、Windows 输入、截图和 win-x64 OCR 运行时，仅支持 Windows |

## 10. 已知技术债务与演进建议

1. 统一命名：解决方案仍保留 `Poe2MarketScanner.*` 历史项目名，与最终产品名不一致。
2. 拆分大型类：将扫描会话协调、profile 切换和计算器持久化/领域计算分别抽离。
3. 明确旧 Web 生命周期：迁移完缺失功能后归档或删除 `web/calculator`，避免双实现漂移。
4. 持续升级依赖：项目已迁移至 `.NET 10 LTS` 与 WPF UI；后续随安全补丁同步验证 PaddleOCR、OpenCV 与 Fluent 主题。
5. 强化端到端测试：增加真实 WPF 交互、截图样本 OCR 回归和发布包启动冒烟测试。
6. 输出一致性：长期应让 V2 成为唯一内部事实来源，V1 仅在明确兼容出口按需生成。
7. 原子写入：profile、扫描结果和计算器工作区可采用临时文件加替换，降低异常退出造成半写文件的风险。

## 11. 设计决策记录摘要

- 选择 WPF 原生宿主与 WPF UI Fluent 主题，消除 WebView2/浏览器运行时依赖并保留 Windows 自动化兼容性。
- 以 `rightPerLeft` 明确比例方向，避免中文展示文本带来的歧义。
- 同批输出 V1/V2，在演进数据契约的同时保持旧消费者可用。
- POE1/POE2 工作区物理隔离，切换时保存并加载各自状态。
- 发布为自包含多文件目录并打 ZIP，以可靠携带 OCR 模型和本机运行库。
