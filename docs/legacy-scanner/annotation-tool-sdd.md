# POE 市场扫描与标注工具 SDD

> 状态：按当前仓库实现更新  
> 更新日期：2026-07-24  
> 适用范围：`K:\CodeX project\POEScanner`

## 1. 文档目的

本文描述当前 `POEScanner` Windows 桌面应用的已实现架构、核心数据、运行流程、部署方式、质量保障和后续演进边界。本文以 `src/`、`tests/`、`scripts/` 下的实际内容为准。

项目当前不是单纯的“标注器”，而是将以下能力集成在一个 WPF 应用中：

- 屏幕区域与点击锚点标注；
- Profile 配置持久化与兼容迁移；
- PaddleOCR 截图识别与预处理调试；
- POE 交易查询自动化；
- 查询结果 JSON 输出；
- 本地 Windows x64 发布。

## 2. 系统范围

### 2.1 已实现能力

1. 导入、保存、重置 JSON Profile。
2. 编辑两个 OCR 区域：`goldCost`、`ratio`。
3. 编辑八个自动化锚点：
   - `leftCurrency`
   - `rightCurrency`
   - `allTab`
   - `search`
   - `searchTarget`
   - `leftInput`
   - `rightInput`
   - `idle`
4. 通过透明 Overlay 拖动区域、调整区域大小、拖动锚点。
5. 保存屏幕相对坐标，并兼容旧版绝对坐标及旧锚点左上角坐标。
6. 配置全局 OCR 参数以及按区域覆盖参数。
7. 捕获屏幕区域、执行灰度化、缩放、二值化和 Otsu 回退。
8. 解析金币数值与比例文本。
9. 导入查询通货列表、选择交易模式并切换简体/繁体中文名称。
10. 执行卖出查询自动化，支持取消、失败继续、OCR 重试和可选金币识别。
11. 将批次结果写入带时间戳的 JSON 文件。
12. 以 self-contained 方式发布 Windows x64 应用。

### 2.2 尚未实现

下列内容存在设计文档或演进设想，但当前源码尚未落地：

- 可视化自动化流程编辑器；
- `AppProfile.Flows` 与流程图持久化；
- 通用流程运行时、条件连线与文本输入节点；
- 将标注工具拆为独立 solution 或独立仓库；
- 稳定版本化的外部 Annotation Profile 合约；
- 安装包、自动更新、签名和 CI/CD 发布链路。

## 3. 技术栈与运行环境

| 项目 | 当前选择 |
|---|---|
| 桌面 UI | WPF |
| 目标框架 | App：`net6.0-windows`；Core：`net6.0` |
| 输入自动化 | Win32 `SetCursorPos`、`SendInput` |
| 屏幕图像处理 | OpenCvSharp 4.11 |
| OCR | Sdcb.PaddleOCR 3.3.1、Paddle Inference MKL |
| 序列化 | `System.Text.Json` |
| 测试 | xUnit |
| 发布目标 | Windows x64、self-contained、多文件 |

应用启用了 WPF 和 Windows Forms；后者用于 Windows 桌面相关能力，不代表存在独立 WinForms UI。

## 4. 仓库结构

```text
POEScanner/
  Poe2MarketScanner.sln
  src/
    Poe2MarketScanner.App/
      MainWindow.xaml(.cs)
      MainViewModel.cs
      OverlayWindow.xaml(.cs)
      Services/
    Poe2MarketScanner.Core/
      Automation/
      Configuration/
      Ocr/
  tests/
    Poe2MarketScanner.App.Tests/
    Poe2MarketScanner.Core.Tests/
  docs/
    annotation-tool-sdd.md
    superpowers/
      specs/2026-06-30-flow-editor-design.md
      plans/2026-06-30-flow-editor-first-slice.md
  scripts/
    package.ps1
  tools/
    LegacyLayoutRecovery/
  publish/
  quick-publish.bat
```

`publish/`、各项目的 `bin/` 与 `obj/` 是生成产物，不属于源代码设计边界。`tools/LegacyLayoutRecovery/` 是遗留布局恢复辅助工具，不参与主应用运行。

## 5. 当前架构

### 5.1 逻辑分层

```text
MainWindow / OverlayWindow
          |
     MainViewModel
          |
  App Services --------------------------+
  | ScreenCaptureService                 |
  | PaddleOcrDebugService                |
  | SellQueryOcrReader                   |
  | SellQueryAutomationRunner            |
  | WindowsInputAutomationRunner         |
  | ProjectOutputJsonWriter              |
          |                              |
          +---------- Core --------------+
                     | Configuration
                     | OCR parsing
                     | Automation contracts/results
```

当前只有两个生产项目：

- `Poe2MarketScanner.App`：WPF UI、应用编排、截图、OCR 引擎接入、Win32 输入和结果输出。
- `Poe2MarketScanner.Core`：Profile 模型、配置兼容、交易模式、OCR 文本解析和自动化结果合约。

这是一种轻量的两层结构。服务实现仍集中在 App 项目中，尚未拆出独立 Application/Infrastructure 层。

### 5.2 UI 与状态管理

`MainWindow` 负责文件对话框、按钮事件、自动化任务生命周期以及 Overlay 显示状态。`MainViewModel` 负责：

- 加载、保存、导入与重置 Profile；
- 暴露区域、锚点、OCR 区域覆盖和交易模式集合；
- 导入查询列表；
- 解析 OCR 预览；
- 接收 OCR 调试结果；
- 监听布局对象属性变化并通知 Overlay 刷新。

当前不是完整的命令式 MVVM：部分应用编排仍位于 `MainWindow.xaml.cs`。

### 5.3 Overlay 编辑

`OverlayWindow` 使用透明 WPF 窗口渲染区域和锚点。编辑模式支持：

- 拖动 OCR 区域；
- 调整 OCR 区域大小；
- 以锚点中心为语义拖动锚点；
- 在模型属性变化后更新视觉元素。

执行屏幕捕获时，`OverlayCaptureGuard` 会暂时隐藏 Overlay，并在捕获后恢复之前的显示与编辑状态，避免标注层污染 OCR 截图。

## 6. Profile 数据设计

### 6.1 运行时模型

`AppProfile` 当前包含：

| 字段 | 用途 |
|---|---|
| `ProfileName` | Profile 名称 |
| `PriceMode` | 价格模式，默认 `sell` |
| `AnchorCoordinateMode` | 锚点坐标语义版本 |
| `UseTraditionalChinese` | 自动化输入是否使用繁体中文交易名 |
| `OutputDirectory` | 结果输出目录 |
| `SelectedTradeModeKey` | 当前交易模式 |
| `Regions` | OCR 屏幕区域 |
| `Anchors` | 自动化点击/输入锚点 |
| `Ocr` | 全局及分区 OCR 设置 |
| `QueryList` | 查询列表来源与条目 |
| `Automation` | 自动化开关及延迟 |

Profile 目前同时承载标注数据与业务运行参数。这有利于单应用使用，但也是未来拆分标注工具时的主要耦合点。

### 6.2 坐标持久化

`JsonProfileStorageService` 保存 Profile 时：

- 将区域位置、区域尺寸和锚点位置转换为屏幕相对比例；
- 写入 `coordinateSpaceMode: "screenRelative"`；
- 保存 `referenceScreen`；
- 运行时再按当前屏幕的 Left、Top、Width、Height 还原坐标。

兼容规则：

1. 缺失 Profile 文件时创建默认配置。
2. 缺失字段、区域、锚点或 OCR 区域覆盖时由 `AppProfileNormalizer` 补齐。
3. 未声明 `screenRelative` 的旧文件按绝对坐标读取。
4. 旧版 `LegacyPanelTopLeftMode` 锚点会迁移为 `MarkerCenterMode`。
5. 非法交易模式键会回退到 `TradeModeCatalog` 的有效默认项。
6. JSON 语法错误不会静默吞掉，而是向调用层抛出反序列化异常。

### 6.3 OCR 配置

全局 OCR 设置包括：

- `Engine`
- `DetectDigitsOnly`
- `Scale`
- `Threshold`
- `EnableGrayscale`
- `EnableBinarization`
- `EnableOtsuFallback`

`RegionOverrides` 可按区域覆盖缩放、阈值和预处理开关。只有覆盖项的 `Enabled` 为 `true` 时才生效，否则使用全局设置。

### 6.4 自动化配置

`AutomationSettings` 当前包含：

- `Reserved`
- `RecognizeGoldCost`
- `CommonDelayMs`
- `ClickDelayMs`
- `InputDelayMs`

实际动作等待时间由公共延迟和对应动作延迟组合而成。`RecognizeGoldCost` 可关闭金币识别步骤。

## 7. OCR 子系统

### 7.1 处理流程

```text
隐藏 Overlay
  -> 截取区域
  -> 灰度化（可选）
  -> 最近邻缩放
  -> 固定阈值二值化（可选）
  -> 前景为空时 Otsu 回退（可选）
  -> PaddleOCR
  -> 文本规范化与业务解析
  -> 恢复 Overlay
```

`PaddleOcrDebugService` 对 `goldCost` 和 `ratio` 两个区域执行调试，并将原图、处理图和识别摘要写入：

```text
%LOCALAPPDATA%\Poe2MarketScanner\debug\<yyyyMMdd-HHmmss>\
```

### 7.2 文本解析

`OcrTextParser` 提供两类业务解析：

- 金币消耗：提取数字并形成规范化值；
- 比例：兼容常见分隔符并形成规范化比例。

识别层与解析层已部分解耦，但 `SellQueryOcrReader` 和区域键仍带有明确业务语义，尚不是通用 OCR 框架。

## 8. 卖出查询自动化

### 8.1 前置校验

执行前由 `AutomationPreflightValidator` 检查运行所需配置，避免缺失锚点、区域或关键参数时直接驱动鼠标键盘。对于旧版布局，还会识别是否存在已自定义的遗留锚点。

### 8.2 输入能力

`WindowsInputAutomationRunner` 提供：

- 普通点击；
- 带 `Ctrl`、`Shift`、`Alt` 组合键的点击；
- Ctrl+Click；
- Ctrl+A；
- Backspace；
- Unicode 文本输入；
- 可取消等待。

所有输入动作均通过 Windows `user32.dll` 完成，依赖前台窗口焦点与当前桌面坐标。

### 8.3 批次执行语义

`SellQueryAutomationRunner` 负责：

- 按选定交易模式解析买入、卖出通货；
- 在简体/繁体中文名称之间切换；
- 驱动锚点点击、搜索框清空和文本输入；
- 对单条查询执行 OCR；
- 等待 OCR 成功或超时；
- 可按配置跳过金币识别；
- 单条失败后记录失败并继续下一条；
- 响应取消令牌；
- 最终交由 `IQueryResultWriter` 写出批次结果。

自动化仍是固定的卖出查询流程，不是数据驱动的通用工作流。

## 9. 输出合约

`ProjectOutputJsonWriter` 默认输出到项目根目录下的 `output/`；如 Profile 指定 `OutputDirectory`，则使用绝对路径或相对项目根目录解析。

文件名格式：

```text
yyyyMMdd-HHmmss.json
```

当前 JSON 顶层结构：

```json
{
  "购买用通货": "示例",
  "出售目标通货": "示例",
  "当前交易对比例": "1:2",
  "条目": [
    {
      "名字": "示例条目",
      "金币消耗": "100",
      "买入比例": "1:2",
      "卖出比例": "2:1"
    }
  ]
}
```

这是面向当前业务的中文字段合约，尚未包含显式 schema version。若有外部消费者，应在破坏性修改前增加版本字段和契约测试。

## 10. 错误处理与安全约束

当前设计采用以下策略：

- 自动化任务支持显式停止和取消传播；
- 单条查询失败不会终止整个批次；
- OCR 会重试直到成功或超时；
- Overlay 捕获使用 guard 恢复 UI 状态；
- 保存前通过 Profile 标准化补齐缺失数据；
- 无效 JSON 由上层显示错误，不使用损坏数据继续执行；
- 输出目录在写入前自动创建。

已知约束：

- 输入自动化依赖 POE 窗口位置、前台焦点和 UI 布局稳定；
- 当前没有管理员权限检测、目标窗口身份校验或 DPI 感知策略说明；
- 多屏相对坐标基于提供的屏幕度量，实际切换拓扑仍需人工验证；
- PaddleOCR 初始化和本地模型会增加启动、内存和发布体积成本。

## 11. 测试策略与现有覆盖

### 11.1 Core 测试

当前覆盖：

- 默认 Profile、交易模式和八个锚点；
- Profile 保存/加载 round-trip；
- 屏幕相对坐标保存与恢复；
- 缺失字段补齐和旧坐标兼容；
- 无效 JSON 行为；
- 锚点中心视觉几何；
- OCR 金币和比例解析；
- 卖出结果模型默认合约。

### 11.2 App 测试

当前覆盖：

- `MainViewModel` 加载、保存、导入和状态更新；
- 自动化前置校验；
- Overlay 捕获隐藏与恢复；
- OCR 预处理和读取结果；
- 卖出自动化动作顺序、OCR 等待、失败继续、繁体名称和跳过金币识别；
- Windows 输入计划中的清空顺序；
- 项目结果 JSON 的路径和字段结构；
- 关键 UI 文本资源。

### 11.3 质量门槛

提交前至少应执行：

```powershell
dotnet test .\Poe2MarketScanner.sln
dotnet build .\Poe2MarketScanner.sln -c Release
```

涉及发布脚本时还应执行一次本地发布并确认主程序可启动。

## 12. 构建与发布

标准发布命令：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1
```

也可运行：

```text
quick-publish.bat
```

当前发布规则：

- 配置：`Release`
- Runtime：`win-x64`
- `self-contained: true`
- `PublishSingleFile: false`
- 不输出调试符号
- 输出目录：仓库根目录 `publish/`
- 主程序：`publish/Poe2MarketScanner.App.exe`

`package.ps1` 会先清空 `publish/` 中的旧内容，再执行 `dotnet publish`。发布物是包含依赖和本地 OCR 运行库的多文件目录，不是单个 exe，也不生成 zip。

## 13. 关键设计决策

### 13.1 保持两项目结构

当前规模下，App + Core 足以支持开发和测试。只有在新增多个前端、多个 OCR 后端或独立标注产品时，才有必要继续拆分 Application、Infrastructure 和 Contracts。

### 13.2 Profile 先兼容、后拆分

现有 Profile 已被 UI、OCR 和自动化共同消费。短期应保持向后兼容；独立标注工具的拆分应通过新增版本化导出合约完成，而不是直接删除业务字段。

### 13.3 固定流程与流程编辑器分离

当前固定卖出查询流程是已验证的生产路径。`docs/superpowers/specs/2026-06-30-flow-editor-design.md` 描述的是未来通用流程编辑器，不能视为现有功能。落地时应新增独立模型与运行器，并保留现有 runner 作为兼容路径，直到行为和测试达到等价。

## 14. 演进路线

### 阶段 1：稳定当前产品

- 补充 README、运行前提、模型文件和故障排查说明；
- 为 Profile 和输出 JSON 增加 schema version；
- 增加 Release 构建与发布冒烟测试；
- 明确 DPI、多屏和目标窗口前台状态要求；
- 清理生成目录与源码目录的边界。

### 阶段 2：抽取稳定合约

- 定义仅包含区域、锚点和 OCR 设置的 Annotation Profile；
- 提供从现有 `AppProfile` 导出 Annotation Profile 的适配器；
- 提供版本迁移和契约测试；
- 让扫描器通过文件合约消费标注结果，而不是依赖标注 UI。

### 阶段 3：可视化流程编辑器

- 按既有流程编辑器设计新增 Flow 模型；
- 实现流程校验、持久化、节点画布和条件连线；
- 实现通用 Flow runner；
- 将固定卖出流程逐步映射为流程定义；
- 用回归测试保证现有自动化语义不变。

### 阶段 4：独立标注产品

- 新建独立 solution 或仓库；
- 迁移 Overlay、Profile 编辑、截图和 OCR 调试能力；
- 移除查询列表、交易模式、批次 runner 和业务输出；
- 建立独立版本号、发布脚本和兼容导入器。

## 15. 风险与缓解

| 风险 | 影响 | 缓解 |
|---|---|---|
| Profile 同时承载标注和业务字段 | 拆分时容易破坏旧数据 | 先增加版本化导出/导入适配器 |
| Win32 自动化依赖焦点与坐标 | 错点、误输入 | 强化前置校验、窗口识别和中止机制 |
| OCR 受分辨率与主题影响 | 识别不稳定 | 保留分区参数、调试图和重试机制 |
| 屏幕拓扑变化 | 相对坐标仍可能偏移 | 增加 DPI/多屏测试和可视校准提示 |
| 固定 runner 直接替换为流程引擎 | 回归风险高 | 双轨运行并建立动作序列等价测试 |
| 输出 JSON 无版本字段 | 外部消费者易被破坏 | 在下一次合约变更前引入 schema version |
| 发布物体积大 | 分发成本高 | 保留多文件可靠发布，后续评估模型裁剪或安装包 |

## 16. 当前验收基线

当前版本满足以下条件时可视为可发布：

1. solution 的全部自动化测试通过；
2. Release 构建成功；
3. `scripts/package.ps1` 成功生成 `publish/Poe2MarketScanner.App.exe` 和所需依赖；
4. Profile 可保存并在不同屏幕度量下恢复布局；
5. 旧版锚点坐标可迁移；
6. Overlay 编辑和 OCR 捕获互不污染；
7. OCR 调试可输出原图、处理图和解析摘要；
8. 卖出查询可启动、停止，并在单条失败后继续；
9. 简体/繁体交易名称、金币识别开关和自定义输出目录生效；
10. 输出 JSON 满足现有契约测试。

## 17. 结论

当前项目已经形成一套可运行、可测试、可发布的 POE 市场扫描桌面工具。其核心优势是标注、OCR 和自动化链路已闭环；主要结构性问题是 Profile、UI 与卖出查询业务仍耦合在同一应用中。

近期优先级应是稳定配置与输出合约、完善发布质量门槛；流程编辑器和独立标注工具应作为后续演进项目推进，不应在文档中提前视为现有能力。
