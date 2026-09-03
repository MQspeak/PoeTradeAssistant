# 神圣石繁体别名兼容 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让价格表导入将 `神聖石`、`神圣石` 和 `D` 统一识别为本地币种 `D`。

**Architecture:** 扩展现有 `SCANNER_CURRENCY_ALIASES` 常量，不新增解析分支。现有 `normalizeScannerCurrencyName`、币种创建和模式切换流程继续消费规范化后的 `D`。

**Tech Stack:** HTML、原生 JavaScript、Node.js `node:test`

---

### Task 1: 增加繁体神圣石导入别名

**Files:**
- Modify: `scripts/import-price-json.test.mjs`
- Modify: `index.html`

- [ ] **Step 1: 写入失败的回归测试**

在币种别名测试中加入：

```js
assert.match(html, /"神聖石": "D"/);
assert.match(html, /"神圣石": "D"/);
assert.match(html, /"D": "D"/);
```

- [ ] **Step 2: 运行测试并确认失败**

Run: `node scripts/import-price-json.test.mjs`

Expected: 别名测试因找不到 `"神聖石": "D"` 而失败。

- [ ] **Step 3: 写入最小实现**

在 `SCANNER_CURRENCY_ALIASES` 中保持三项映射：

```js
"神聖石": "D",
"神圣石": "D",
"D": "D",
```

- [ ] **Step 4: 运行导入测试并确认通过**

Run: `node scripts/import-price-json.test.mjs`

Expected: 全部导入测试通过。

- [ ] **Step 5: 运行回归测试与构建**

Run:

```powershell
node scripts/formula-arbitrage-tab.test.mjs
node scripts/rune-arbitrage-tab.test.mjs
node scripts/gold-efficiency-display.test.mjs
npm run build
```

Expected: 所有测试和构建均以退出码 `0` 完成。

- [ ] **Step 6: 提交变更**

该工作区没有 `.git`，因此不执行 Git 提交；保留工作区文件变更供用户直接使用。
