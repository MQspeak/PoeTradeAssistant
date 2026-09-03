# POE2 套利工具架构说明

## 1. 架构结论

当前项目不是单一架构，而是两套前端实现并存：

1. `根目录单文件架构`
   - 入口：`index.html`
   - 启动方式：`启动套利工具.cmd`
   - 测试覆盖：`scripts/*.test.mjs`
   - 当前能力最完整
2. `React/Vite 架构`
   - 入口：`src/main.tsx`
   - 构建方式：`npm run build`
   - 当前只覆盖三角套利计算器

因此，当前最重要的架构问题不是“如何继续加功能”，而是“先明确哪套架构是主线”。

## 2. 当前目录角色

### 2.1 单文件实现

- `index.html`
  - 包含样式、状态模型、业务计算、渲染、事件绑定
- `启动套利工具.cmd`
  - 直接打开根目录 `index.html`
- `scripts/*.test.mjs`
  - 通过读取根目录 `index.html` 做静态断言测试

### 2.2 React 实现

- `src/App.tsx`
  - 三角套利页面、输入状态、核心计算
- `src/main.tsx`
  - React 挂载入口
- `src/styles.css`
  - React 版本样式
- `vite.config.ts`
  - 使用 `vite-plugin-singlefile` 输出单文件产物
- `dist/index.html`
  - React 构建产物

## 3. 单文件实现架构

### 3.1 结构特征

单文件版本采用“页面模板 + 全局状态 + 全量重渲染”的架构：

- 所有状态存于全局 `state`
- 所有页面通过 `render*Page()` 生成 HTML 字符串
- 所有用户操作通过事件委托监听 `data-action`
- 每次状态更新后执行 `saveState()` 和 `render()`

### 3.2 状态层

根状态包含：

- `activeTab`
- `selectedModeKey`
- `items`
- `targets`
- `pairs`
- `runeArbitrage`
- `formulaArbitrage`

状态初始化链路：

`getDefaultState()` -> `loadState()` -> 全局 `state`

持久化方式：

- 介质：`localStorage`
- 键：`poe2-arbitrage-tool-standalone-v3`

### 3.3 计算层

核心计算函数包括：

- `getConversionRate()`
- `convertPriceToCurrency()`
- `calculateRuneArbitrage()`
- `calculateFormulaArbitrage()`

公式套利规则：

- 以第一条收益项币种为结算币种
- 成本与收益统一折算后求和
- 利润为 `总收益 - 总成本`
- 收益率为 `((总收益 / 总成本) * 100) - 100`
- 金币效率依赖 D 币种及 `结算币种 -> D` 汇率

### 3.4 视图层

分页渲染函数包括：

- `renderItemsPage()`
- `renderPairsPage()`
- `renderTargetsPage()`
- `renderRunesPage()`
- `renderFormulasPage()`

总入口：

- `render()`

其职责：

- 渲染 Hero
- 渲染分页按钮
- 根据 `activeTab` 切换可见页面
- 尝试恢复输入焦点

### 3.5 交互层

事件处理采用原生 DOM 事件委托：

- `click`
- `input`
- `change`
- `compositionstart`
- `compositionend`
- `blur`

优点：

- 无框架依赖
- 直接本地运行简单

缺点：

- 业务逻辑、UI、事件高度耦合
- `index.html` 已明显膨胀
- 测试只能做静态字符串断言

## 4. React 实现架构

React 版本采用更标准的组件化结构，但功能范围有限。

### 4.1 结构

- `App` 组件统一管理页面
- 使用 `useState` 保存 `startAmount` 与 `legs`
- 使用 `useMemo` 计算 `buildCalculation()` 结果
- 使用 `useEffect` 做本地存储读写

### 4.2 业务规则

三角套利计算流程：

1. 校验初始资金
2. 校验三跳币种、汇率、手续费
3. 校验路径衔接与闭环
4. 逐跳计算净产出
5. 汇总利润、ROI、保本汇率

### 4.3 优势与不足

优势：

- 结构清晰
- 更适合继续模块化演进
- 构建链成熟

不足：

- 功能与单文件主工具不一致
- 没有复用单文件版的完整业务域
- 当前没有自动化测试覆盖 React 页面行为

## 5. 测试架构

当前测试全部是“静态文件字符串断言”：

- 读取根目录 `index.html`
- 断言某些函数名、文案、结构是否存在

这意味着：

- 能验证单文件实现是否包含特定能力
- 不能验证实际浏览器交互正确性
- 完全不能验证 React 构建产物是否符合产品预期

## 6. 发布架构现状

当前存在三条不一致的交付链：

1. 用户运行链：`启动套利工具.cmd` -> 根目录 `index.html`
2. 测试验证链：`scripts/*.test.mjs` -> 根目录 `index.html`
3. 构建发布链：`npm run build` -> `dist/index.html`（来自 React）

这是当前项目最大的架构断裂点。

## 7. 建议目标架构

建议二选一，不要继续并行：

### 方案 A：单文件版继续作为主实现

- 保留 `index.html` 为唯一入口
- 将内联脚本逐步拆到独立 JS 模块
- 保持最终仍可打包为单文件
- 测试继续围绕真实入口增强

适用条件：

- 优先保证离线打开即可运行
- 接受短期内继续使用原生 DOM

### 方案 B：React/Vite 作为主实现

- 以 `src` 为唯一源码事实来源
- 将单文件版所有业务能力迁移到 React
- 测试迁移到真正的组件/页面级测试
- 构建产物和用户入口统一到 `dist/index.html`

适用条件：

- 准备继续长期演进功能
- 愿意承担一次迁移成本

## 8. 当前建议

从仓库现状看，建议优先采用 `方案 B`：

- React 结构更适合继续维护
- `vite-plugin-singlefile` 已满足“最终单文件分发”的需要
- 只要把单文件版业务迁回 React，就能同时保留本地运行体验和更好的可维护性

但在真正迁移前，必须先把“唯一入口、唯一测试对象、唯一发布链”定下来。
