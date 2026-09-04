# 实时搜索监控合并预研

日期：2026-09-04。状态：设计建议，尚未实施合并。

## 1. 结论

可行，适合作为与扫价器、套利计算器并列的第三个功能模块。推荐沿用当前 WPF 宿主，以 Microsoft.Playwright 实现浏览器监控适配层，迁移原工具的业务模型和 DOM 提取脚本。界面仍是原生桌面控件；登录与实时搜索依赖外部浏览器。

这属于功能迁移，不能通过把 Electron 目录加入解决方案直接完成。主要工作在 WPF 页面、异步会话生命周期、游戏/区服隔离、自动前往与扫描互斥，以及既有监控实现的可靠性修正。

若只要求统一启动入口，启动原 Electron 程序成本最低，但仍是两个应用，不能满足统一工作区、结果通知和退出管理。若要求尽快复用 TypeScript 后端，可以用 WPF + Node 子进程过渡，代价是双运行时和 IPC 维护。

## 2. 阅读范围与验证证据

目标项目：`K:/CodeX project/PoeTradeAssistant`，HEAD `b0c17bc`，预研开始时工作树干净。

来源项目：`K:/CodeX project/poe2-live-search-manager`，HEAD `fe6f1f5`。来源有大量未提交修改和未跟踪源码，本报告以当前磁盘工作树为准，不能用该 HEAD 单独复现全部结论。实施前应记录完整迁移基线，避免漏掉未跟踪的 Electron、区服和测试文件。

来源未找到独立命名的 SDD 文件，设计依据是 `docs/architecture.md`、`docs/requirements.md`、`docs/product.md`，以及 POE1 扩展设计和实际源码。目标读取了 `docs/SDD.md`、宿主、扫描输入、工作区和数据合约代码。

实际执行：

- 来源 `npm.cmd run build`：通过，包含 Vite 构建及后端 TypeScript 编译。
- 来源 `node --test tests/*.test.mjs`：18/18 通过。包括区服迁移、声音路径、自动前往间隔、匹配规则，以及使用本地模拟 DOM 的 Chromium 提取测试。
- 未操作真实市集账号，未验证四区服登录、真实实时连接、前往藏身处或长期运行性能。
- 本次没有修改应用源码，也未重复运行目标 .NET 测试；不是已完成的合并原型或发布验证。

文档与代码有差异：架构/需求仍主要描述 `international/china` 两区服；POE1 扩展设计描述三种环境；实际 `shared/types.ts`、`regions.ts` 和存储代码已包含 POE1/POE2 × 国际服/国服四种环境。配置存在不等于真实站点验证通过。

## 3. 可迁移的功能边界

| 能力 | 来源实现 | 合并处理 |
|---|---|---|
| 区服及登录 | `regions.ts`、`authService.ts` | 保留四环境模型，重做异步登录/验证/清理服务 |
| 监控列表 | `backend.ts`、`jsonStorage.ts` | 迁移 CRUD、启用状态、来源链接关联及 10 项上限 |
| 链接库 | 同上 | 迁移搜索、导入导出、按名称+URL去重、200 项上限 |
| 实时观察 | `monitorManager.ts`、`monitorWorker.ts` | 迁移共享上下文、一项一页、DOM 观察和手动重连 |
| 命中列表 | `types.ts`、worker、renderer | 迁移详情、提醒、已读、50 项内存结果上限 |
| 自动前往 | `renderer/App.tsx`、`shared/autoTravel.ts` | 将单次/持续模式和 0–99 秒间隔迁至独立服务 |
| 翻译 | `translationService.ts` | 后续迁移百度翻译、缓存及国际服开关 |
| 声音 | renderer、`hitSound.ts` | 原生播放器重实现文件选择和播放 |
| 桌面外壳 | Electron main/preload、React | 用 WPF 页面和服务调用替代 |
| 联系卡及旧 Web 服务 | contact 模块、`webServer.ts` | 非实时搜索核心，不纳入首期 |

有利条件：`DesktopBackend` 虽然位于 `electron` 目录，但其 import 没有 Electron API，组合的主要是普通 Node/Playwright 服务。其接口可以作为迁移清单或 Node 过渡方案的边界。

限制：自动前往、成功后标记已读和提示音不是全部由后端负责，仅迁移 Backend 会丢功能。`priceThreshold` 虽保留在模型中，却只抽取价格字符串里的数字，没有币种换算，不能作为统一估值条件直接使用。

## 4. 技术路线比较

| 路线 | 复用方式 | 优点 | 代价与判断 |
|---|---|---|---|
| WPF 启动 Electron | 原应用完整保留 | 最快形成入口 | 双窗口、双数据目录；只适合临时入口 |
| WPF + Node 后台进程 | 复用 Backend、Manager、Worker | 后端迁移少，适合快速验证 | 自带 Node、协议版本、超时、退出清理和双语言维护 |
| WPF + C# + Playwright | 移植业务服务，提取 DOM JS 资源 | 与当前工程、测试、发布方式一致 | 初期迁移工作较多；推荐正式路线 |
| WPF + WebView2 嵌入 React | 复用部分视图 | 短期节省部分界面工作 | IPC仍需改，网页监控仍需浏览器能力，并重新引入目标已移除的 WebView2 依赖 |

Playwright 官方提供 .NET 库，支持 `chrome`、`msedge` 通道，可使用系统已安装浏览器。因此 WPF/C# 路线有现成技术基础，并不要求嵌入 WebView2。参考：[浏览器支持](https://playwright.dev/dotnet/docs/browsers)、[.NET 库与发布说明](https://playwright.dev/dotnet/docs/library)。

需要区分“不需要用户安装开发用 Node”和“运行时完全没有 Node/浏览器进程”：Playwright .NET 仍携带驱动资源并启动浏览器相关进程。当前自包含发布脚本需验证这些资源被完整保留，不能宣称继续保持完全不依赖浏览器。浏览器缺失时应提供明确诊断；若要捆绑 Chromium，需另定安装、更新和包体方案。

建议监控按需初始化。只使用扫价器/计算器时，不启动浏览器；监控依赖缺失或启动失败应局限在监控模块。

## 5. 推荐模块与接入点

建议新增：

```text
PoeTradeAssistant.Contracts/LiveSearch
    环境、监控配置、链接、命中、运行状态和事件
PoeTradeAssistant.LiveSearch.Core
    链接库规则、状态机、去重、自动前往策略、服务接口
PoeTradeAssistant.LiveSearch.Infrastructure
    Playwright、登录、DOM脚本、存储、翻译适配
Poe2MarketScanner.App/Views + ViewModels
    LiveSearchView / LiveSearchViewModel
```

依赖为 App → LiveSearch.Core/Infrastructure → Contracts；LiveSearch.Core 不依赖 WPF 或 Playwright。DOM 提取脚本提为独立 JS 资源，去掉 TypeScript 类型标注，并让页面检测和运行时 worker 共用同一实现。C# 使用页面脚本执行/回调接收结果，避免重写所有 DOM 规则。

具体接入点：

- `MainWindow.xaml` 的 `PrimaryNavigation` 添加“实时搜索”入口，包含监控、命中、链接库和设置；新增独立视图，不继续扩大主窗口文件。
- `MainViewModel.SelectedGameMode` 目前是同步保存/加载 setter。监控切换需要 await 关闭浏览器和清理回调，因此应由应用协调器执行异步游戏切换，再更新属性。
- `MainWindow.xaml.cs/SaveAndCloseAsync` 在现有取消扫描、等待导入、保存工作区流程中加入监控停止、登录窗口关闭及服务释放。关闭需有超时和错误反馈，且只能清理由本应用创建的浏览器资源。
- 后台事件通过 WPF Dispatcher 更新可观察集合；业务策略放在服务层，不能依赖页面是否可见或控件是否已卸载。
- 使用明确的 `PauseCapture` 与 `Shutdown` 两种操作。暂停采集保留结果页交互；彻底关闭释放浏览器，已有结果应显示交互已失效。

## 6. 游戏、区服及旧数据迁移

目标目前只按 `poe1/poe2` 隔离；来源按四环境隔离。建议统一为游戏维度加监控模块的区服维度：顶部游戏选择继续生效，监控页面选择国际服/国服。

建议新数据放在目标现有 LocalAppData 根目录下：

```text
PoeTradeAssistant/profiles/poe1/...       现有扫描与计算器
PoeTradeAssistant/profiles/poe2/...
PoeTradeAssistant/live-search/poe1/international/...
PoeTradeAssistant/live-search/poe1/china/...
PoeTradeAssistant/live-search/poe2/international/...
PoeTradeAssistant/live-search/poe2/china/...
```

首期只允许当前环境运行监控。切换游戏或区服：撤销自动前往 → 停止旧环境 → 清理旧回调 → 保存 → 加载新环境；使用会话代次标识丢弃晚到结果。后台同时监控多环境会扩大并发、通知归属和自动前往问题，不作为首期目标。

迁移分开处理：链接库可兼容来源导出格式；完整 `state.json` 可导入监控配置、链接关联和选项，同时执行旧 `international/china` 到 POE2 环境的映射。新工作区增加 schema 版本、备份和单次迁移标记。原数据不覆盖；音频路径需验证目标文件存在。登录态不默认复制整个系统浏览器 profile，首期采用新环境重新登录；会话兼容迁移另行验证。

目标计算器当前也没有区服/赛季隔离。独立接入实时搜索无需立即改造计算器；但未来若要传递报价，必须增加游戏、区服、赛季和来源校验，否则不能自动判断报价属于哪个计算工作区。

## 7. 合并前应处理的具体问题

以下来自静态代码审阅，除上述18项测试外，未做真实站点复现。

| 问题 | 代码证据 | 对合并的要求 |
|---|---|---|
| 自动前往干扰屏幕扫描 | 目标扫描依赖当前游戏画面；来源支持自动点击前往 | 扫描中允许被动监听，阻止前往；登录/打开浏览器等可能抢焦点的操作也通过协调器处理。不要在扫描后自动执行积压的过期前往请求 |
| “停止”并非停止网页实时连接 | `MonitorManager.stopAll` 调用 `pauseAll`，worker只断开注入的观察器 | UI说明为暂停采集；游戏切换和退出必须调用彻底关闭 |
| 首屏结果可触发自动前往 | worker把 initial 仅编码到 summary；renderer对每个新结果事件执行策略 | 合约显式携带 Initial/Update；默认初始快照只展示，不触发自动动作 |
| 前往动作可能重复且成功含义过强 | worker连续 `dispatchEvent(click)` 和 `click()`，随后直接返回true | 只触发一次，区分已发起、页面确认、失败；没有游戏证据时不宣称已抵达 |
| 持续前往失败后可能快速重试 | renderer finally触发下一轮，失败不标读，间隔允许0 | 按结果锁定、失败退避、最大尝试次数；停止/切换必须取消策略 |
| 重连首次启动丢失环境参数 | manager无context时调用 `startAll([monitor])`，未传会话/region | 所有启动路径统一传递环境及已验证会话；修正重连后的活动状态 |
| 登录资源释放不完整 | backend.shutdown仅关闭manager；selectRegion替换AuthService前未关闭旧登录窗口 | 统一释放登录窗口与监控上下文，并处理正在进行的登录请求 |
| 注销后仍可能保留profile会话 | clearSession删除storage-state文件但不清除persistent profile内的cookie | 明确“断开”与“清除登录态”语义；彻底注销应清除模块专用profile会话 |
| 去重和结果定位不统一 | worker按listing id去重，backend多个读写操作只按resultId；详情/前往又用monitorId | 使用环境+monitorId+listingId定位命中；另定义跨监控通知去重规则 |
| 数据可能覆盖/重置 | JsonStorage对各操作独立读改写；readState任何异常均写默认值 | 单写入队列或锁+原子替换；读失败保留原文件并报错，不能静默清空。AtomicFile只能解决单文件替换，不能解决并发丢更新 |
| 可用状态判断不够严格 | canStartMonitoring允许验证状态unknown；manager在worker报error后仍可标active | 区分有会话文件、已验证、监控启动、真正可用；启动前验证或受控复验 |
| 检测脚本与生产脚本可能漂移 | 模拟DOM测试调用inspector，worker内另有一套提取规则 | 共用提取脚本，并补worker生命周期/动作测试 |

此外，保存/导入 URL 时应校验 HTTPS、官方主机以及当前游戏对应路径；目前存储主要执行 trim，不能只靠前端输入控件保证环境正确。自动浏览器窗口默认可先创建再隐藏，应验证不会打断扫描输入。上述都是实际集成边界，不能仅通过编译判断解决。

## 8. 与套利计算器的数据关系

来源 `SearchResultItem` 是某条卖家上架记录，价格以 `priceText` 表示。目标 `PriceScanDocument` 是交易对批次，包含明确比例方向、四价、金币成本和识别状态。两者并非同一数据结构或市场含义。

首期命中只在实时搜索模块展示，不写入 `poe-trade-scan/v2`，也不覆盖计算器价格。

后续若需要“将命中作为参考报价”，单独增加适配能力：解析币种/金额/数量，保留游戏/区服/赛季/时间/来源，区分装备与可堆叠通货，确定报价映射到哪一个计算字段；缺失的卖价、金币成本、汇率不得自动推算或填零。四价策略仍由计算器现有规则处理。

目标 SDD 也应更新范围：增加官方网页 DOM 监控、外部浏览器及本地会话的说明。仍可保持“不直接调用交易站 API”的边界；网页自身的网络请求不等于应用自行对接交易API。

## 9. 分阶段实施与验收

| 阶段 | 交付 | 验收门槛 |
|---|---|---|
| A：最小验证 | C#启动系统浏览器、登录、一个链接、DOM回调、关闭 | 模拟页面稳定提取；真实账号验证至少一个目标环境；不残留本应用浏览器进程 |
| B：原生MVP | WPF监控页、链接CRUD/导入导出、启动/暂停/重连、命中/声音/详情/手动前往 | 四环境配置隔离；去重正确；切换/退出/登录失败可恢复；手动前往与扫描互斥 |
| C：策略与兼容 | 单次/持续自动前往、失败退避、旧数据迁移、翻译/自定义声音 | Initial不触发、不会重复点击、暂停/切换后无晚到动作、迁移失败保留原文件 |
| D：发布验证 | Playwright驱动完整打包、浏览器检测、真实环境检查 | 干净Windows机器启动；缺浏览器不影响扫描器；10监控资源占用实测；原扫描/计算器回归通过 |

不建议先全面搬代码再验证浏览器链路。阶段A先消除最大不确定性；真实站点登录、DOM变化和四区服行为必须逐项记录支持矩阵，未验证项明确标注。

工作量粗估，按一名熟悉当前项目的开发者、已有可验证账号、需求固定计算：最小验证1–2人日；C#服务和WPF MVP累计约6–10人日；包含自动策略、迁移、翻译与发布回归的完整范围累计约12–20人日。此为设计估算，不是承诺；不含等待账号、站点挑战处理或未来DOM改版。

建议先做阶段A，再完成独立模块MVP，最后迁移自动前往和翻译。当前证据支持“技术上可以合并、架构上适合独立扩展”，尚不足以宣称真实市集链路已经验证或可以零改动复用。
