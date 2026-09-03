# 公式套利分页 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为单文件 POE2 套利工具新增“公式套利”分页，支持创建、保存、编辑公式，并基于物品列表和基础交易对实时计算收益率、总成本、纯利润和每 D 金币效率。

**Architecture:** 继续沿用 `index.html` 中的单文件状态树、计算函数、事件委托和全量 `render()` 模式。新增 `formulaArbitrage` 状态分支、公式计算函数、分页渲染函数和事件处理，同时扩展静态结构测试以覆盖新分页、状态结构和关键计算口径。

**Tech Stack:** 原生 HTML/CSS/JavaScript 单文件应用、Node `node:test` 静态断言脚本、Vite 单文件构建。

---

### Task 1: 先补公式套利分页的失败测试

**Files:**
- Create: `K:\CodeX project\安洁计算器项目\poe2-triangular-arbitrage-calculator\scripts\formula-arbitrage-tab.test.mjs`
- Modify: `K:\CodeX project\安洁计算器项目\poe2-triangular-arbitrage-calculator\package.json`
- Test: `K:\CodeX project\安洁计算器项目\poe2-triangular-arbitrage-calculator\scripts\formula-arbitrage-tab.test.mjs`

- [ ] **Step 1: 写失败测试，断言新分页、状态和计算函数存在**

```js
import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const html = fs.readFileSync(path.join(__dirname, "..", "index.html"), "utf8");

test("includes formula arbitrage tab after runes tab", () => {
  assert.match(
    html,
    /data-tab="items">[\s\S]*?data-tab="pairs">[\s\S]*?data-tab="targets">[\s\S]*?data-tab="runes">[\s\S]*?data-tab="formulas">/s
  );
});

test("defines formula arbitrage state and calculation entry points", () => {
  assert.match(html, /formulaArbitrage:/);
  assert.match(html, /draft:/);
  assert.match(html, /formulas:/);
  assert.match(html, /function calculateFormulaArbitrage\(formula\)/);
  assert.match(html, /function renderFormulasPage\(\)/);
});

test("formula arbitrage uses first revenue currency as settlement currency", () => {
  assert.match(html, /const settlementCurrencyId = revenueItems\[0\]\.currencyId/);
  assert.match(html, /缺少 .* -> .* 的基础交易对，请先去基础交易对列表构建交易对。/);
  assert.match(html, /当前结算币种缺少与 D 的基础交易对，请先去基础交易对列表构建交易对。/);
});

test("formula page includes editor and saved formula list", () => {
  assert.match(html, /<h2>公式套利<\/h2>/);
  assert.match(html, /公式名称/);
  assert.match(html, /成本卡片/);
  assert.match(html, /收益卡片/);
  assert.match(html, /保存公式/);
  assert.match(html, /已保存公式列表/);
  assert.match(html, /编辑公式/);
});
```

- [ ] **Step 2: 运行测试并确认失败**

Run: `node scripts/formula-arbitrage-tab.test.mjs`

Expected: 失败，报出 `data-tab="formulas"`、`formulaArbitrage` 或 `calculateFormulaArbitrage` 等断言不存在。

- [ ] **Step 3: 给 package.json 增加可复用测试命令**

```json
{
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "preview": "vite preview",
    "test:formula": "node scripts/formula-arbitrage-tab.test.mjs"
  }
}
```

- [ ] **Step 4: 再次运行测试命令，确认仍是同一类失败**

Run: `npm run test:formula`

Expected: 失败，但命令可执行，说明测试入口配置完成。

- [ ] **Step 5: 记录阶段性变更**

```bash
git add package.json scripts/formula-arbitrage-tab.test.mjs
git commit -m "test: add formula arbitrage tab coverage"
```

### Task 2: 扩展状态结构与本地存储读写

**Files:**
- Modify: `K:\CodeX project\安洁计算器项目\poe2-triangular-arbitrage-calculator\index.html`
- Test: `K:\CodeX project\安洁计算器项目\poe2-triangular-arbitrage-calculator\scripts\formula-arbitrage-tab.test.mjs`

- [ ] **Step 1: 先写一条更具体的失败断言，要求默认草稿包含成本项和收益项**

```js
test("formula arbitrage default draft includes cost and revenue entries", () => {
  assert.match(html, /function createDefaultFormulaArbitrageState\(\)/);
  assert.match(html, /costItems:\s*\[\s*createEmptyFormulaEntry\(/);
  assert.match(html, /revenueItems:\s*\[\s*createEmptyFormulaEntry\(/);
});
```

- [ ] **Step 2: 运行测试并确认失败**

Run: `node scripts/formula-arbitrage-tab.test.mjs`

Expected: 失败，提示 `createDefaultFormulaArbitrageState` 或草稿结构不存在。

- [ ] **Step 3: 在 index.html 中新增默认状态和存储回填**

```js
function createEmptyFormulaEntry() {
  return {
    id: createId(),
    label: "",
    currencyId: "",
    unitPrice: "",
    goldCostPerUnit: "",
    quantity: "1"
  };
}

function createDefaultFormulaDraft() {
  return {
    editingFormulaId: "",
    name: "",
    costItems: [createEmptyFormulaEntry()],
    revenueItems: [createEmptyFormulaEntry()]
  };
}

function createDefaultFormulaArbitrageState() {
  return {
    draft: createDefaultFormulaDraft(),
    formulas: []
  };
}
```

- [ ] **Step 4: 扩展根状态、activeTab 允许值和 localStorage 反序列化**

```js
activeTab: ["items", "pairs", "targets", "runes", "formulas"].includes(saved.activeTab) ? saved.activeTab : "items",
formulaArbitrage: saved.formulaArbitrage && typeof saved.formulaArbitrage === "object"
  ? {
      draft: normalizeSavedFormulaDraft(saved.formulaArbitrage.draft),
      formulas: Array.isArray(saved.formulaArbitrage.formulas)
        ? saved.formulaArbitrage.formulas.map(normalizeSavedFormulaRecord)
        : []
    }
  : createDefaultFormulaArbitrageState()
```

- [ ] **Step 5: 运行测试，确认状态相关断言转绿**

Run: `node scripts/formula-arbitrage-tab.test.mjs`

Expected: 新增的状态结构断言通过，但分页和计算函数断言仍失败。

- [ ] **Step 6: 记录阶段性变更**

```bash
git add index.html scripts/formula-arbitrage-tab.test.mjs
git commit -m "feat: add formula arbitrage state scaffolding"
```

### Task 3: 用 TDD 实现公式计算函数

**Files:**
- Modify: `K:\CodeX project\安洁计算器项目\poe2-triangular-arbitrage-calculator\scripts\formula-arbitrage-tab.test.mjs`
- Modify: `K:\CodeX project\安洁计算器项目\poe2-triangular-arbitrage-calculator\index.html`
- Test: `K:\CodeX project\安洁计算器项目\poe2-triangular-arbitrage-calculator\scripts\formula-arbitrage-tab.test.mjs`

- [ ] **Step 1: 添加失败断言，锁定结算币种、收益率和金币效率公式**

```js
test("formula arbitrage calculates settlement, profit, roi and gold efficiency", () => {
  assert.match(html, /const settlementCurrencyId = revenueItems\[0\]\.currencyId/);
  assert.match(html, /const totalCostInSettlementCurrency = costBreakdown\.reduce/);
  assert.match(html, /const totalRevenueInSettlementCurrency = revenueBreakdown\.reduce/);
  assert.match(html, /const netProfitInSettlementCurrency = totalRevenueInSettlementCurrency - totalCostInSettlementCurrency/);
  assert.match(html, /const roiPercent = \(\(totalRevenueInSettlementCurrency \/ totalCostInSettlementCurrency\) \* 100\) - 100/);
  assert.match(html, /goldEfficiencyPerDivine = totalGoldCost \/ netProfitInD/);
});
```

- [ ] **Step 2: 运行测试并确认失败**

Run: `node scripts/formula-arbitrage-tab.test.mjs`

Expected: 失败，提示 `calculateFormulaArbitrage` 或相关公式不存在。

- [ ] **Step 3: 在 index.html 中写最小计算实现**

```js
function calculateFormulaArbitrage(formula) {
  const name = String(formula.name || "").trim();
  const costItems = Array.isArray(formula.costItems) ? formula.costItems : [];
  const revenueItems = Array.isArray(formula.revenueItems) ? formula.revenueItems : [];
  const settlementCurrencyId = revenueItems[0].currencyId;

  if (!name) {
    return { status: "incomplete", message: "请先填写公式名称。" };
  }

  if (!settlementCurrencyId) {
    return { status: "incomplete", message: "请先为第一条收益项选择币种，作为结算币种。" };
  }

  // 逐条校验、换算并累加
}
```

- [ ] **Step 4: 在同一个函数中补齐缺失交易对和 D 换算逻辑**

```js
if (converted.status === "missing-rate") {
  return {
    status: "missing-rate",
    message: `缺少 ${getItemName(entry.currencyId)} -> ${getItemName(settlementCurrencyId)} 的基础交易对，请先去基础交易对列表构建交易对。`
  };
}

const dCurrency = getItemsByCategory("currency").find((item) => item.name.trim().toUpperCase() === "D") || null;
if (!dCurrency) {
  goldEfficiencyMessage = "请先在物品列表中新增 D 币种，才能计算金币效率。";
}
```

- [ ] **Step 5: 运行测试，确认计算相关断言通过**

Run: `node scripts/formula-arbitrage-tab.test.mjs`

Expected: 与计算函数相关的断言通过，但页面结构断言仍失败。

- [ ] **Step 6: 记录阶段性变更**

```bash
git add index.html scripts/formula-arbitrage-tab.test.mjs
git commit -m "feat: add formula arbitrage calculations"
```

### Task 4: 实现公式分页渲染与交互

**Files:**
- Modify: `K:\CodeX project\安洁计算器项目\poe2-triangular-arbitrage-calculator\index.html`
- Test: `K:\CodeX project\安洁计算器项目\poe2-triangular-arbitrage-calculator\scripts\formula-arbitrage-tab.test.mjs`

- [ ] **Step 1: 增加失败断言，要求页面包含编辑器、列表和按钮文案**

```js
test("formula page renders editor cards and saved list actions", () => {
  assert.match(html, /<h2>公式套利<\/h2>/);
  assert.match(html, /成本卡片/);
  assert.match(html, /收益卡片/);
  assert.match(html, /新增成本项/);
  assert.match(html, /新增收益项/);
  assert.match(html, /保存公式/);
  assert.match(html, /已保存公式列表/);
  assert.match(html, /编辑公式/);
});
```

- [ ] **Step 2: 运行测试并确认失败**

Run: `node scripts/formula-arbitrage-tab.test.mjs`

Expected: 失败，提示 `renderFormulasPage` 或页面文案不存在。

- [ ] **Step 3: 在 index.html 中新增 renderFormulasPage 和辅助渲染函数**

```js
function renderFormulaEntryCards(entries, type, currencyItems) {
  return entries.map((entry, index) => `
    <article class="summary-card">
      <div class="label">${type === "cost" ? "成本项" : "收益项"} ${index + 1}</div>
      <input data-action="formula-entry-field" data-type="${type}" data-id="${entry.id}" data-field="label" value="${escapeHtml(entry.label)}" placeholder="名称" />
    </article>
  `).join("");
}

function renderFormulasPage() {
  const currencyItems = getItemsByCategory("currency");
  const draft = state.formulaArbitrage.draft;
  const result = calculateFormulaArbitrage(draft);
  return `...`;
}
```

- [ ] **Step 4: 把新分页挂到 tab、render 主流程和事件委托中**

```js
<button class="tab-button ${state.activeTab === "formulas" ? "active" : ""}" data-action="switch-tab" data-tab="formulas">公式套利</button>
...
const formulasPage = renderFormulasPage();
...
<div class="${state.activeTab === "formulas" ? "" : "hidden"}">${formulasPage}</div>
```

- [ ] **Step 5: 新增草稿增删改、保存、编辑事件处理**

```js
if (action === "add-formula-cost") {
  addFormulaEntry("cost");
  return;
}

if (action === "save-formula") {
  saveFormulaDraft();
  return;
}

if (action === "edit-formula") {
  loadFormulaDraft(target.dataset.id);
  return;
}
```

- [ ] **Step 6: 运行测试，确认新分页结构断言通过**

Run: `node scripts/formula-arbitrage-tab.test.mjs`

Expected: 公式分页结构测试全部通过。

- [ ] **Step 7: 记录阶段性变更**

```bash
git add index.html scripts/formula-arbitrage-tab.test.mjs
git commit -m "feat: add formula arbitrage page"
```

### Task 5: 做完整验证并构建产物

**Files:**
- Modify: `K:\CodeX project\安洁计算器项目\poe2-triangular-arbitrage-calculator\dist\index.html`
- Test: `K:\CodeX project\安洁计算器项目\poe2-triangular-arbitrage-calculator\scripts\formula-arbitrage-tab.test.mjs`

- [ ] **Step 1: 运行静态测试，确认全部通过**

Run: `node scripts/rune-arbitrage-tab.test.mjs`

Expected: PASS

Run: `node scripts/gold-efficiency-display.test.mjs`

Expected: PASS

Run: `node scripts/formula-arbitrage-tab.test.mjs`

Expected: PASS

- [ ] **Step 2: 运行完整构建**

Run: `npm run build`

Expected: exit 0，生成新的 `dist/index.html`

- [ ] **Step 3: 如构建输出覆盖 dist/index.html，确认打包产物包含新分页**

Run: `rg -n "公式套利|data-tab=\"formulas\"" dist/index.html`

Expected: 能检索到“公式套利”和新 tab 标记。

- [ ] **Step 4: 记录最终变更**

```bash
git add index.html dist/index.html package.json scripts/formula-arbitrage-tab.test.mjs
git commit -m "feat: add custom formula arbitrage tab"
```

