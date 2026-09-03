# Target Buy Currency Gold Cost Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Update the target arbitrage gold-cost formula so the buy-currency gold component is based on the buy-side cost amount instead of the converted sale revenue amount.

**Architecture:** Keep the change inside `calculateTargetArbitrage()` in the standalone `index.html` entrypoint. Add a regression test that asserts the buy-currency gold component uses `buyPrice`, then implement the smallest code change needed to satisfy that behavior.

**Tech Stack:** HTML, inline JavaScript, Node.js `node:test`

---

### Task 1: Lock the Target Arbitrage Gold Formula

**Files:**
- Create: `K:\CodeX project\安洁计算器项目\poe2-triangular-arbitrage-calculator\scripts\target-buy-currency-gold-cost.test.mjs`
- Modify: `K:\CodeX project\安洁计算器项目\poe2-triangular-arbitrage-calculator\index.html`
- Modify: `K:\CodeX project\安洁计算器项目\poe2-triangular-arbitrage-calculator\dist\index.html`

- [ ] **Step 1: Write the failing test**

```js
test("target arbitrage buy-currency gold cost uses buy-side amount", () => {
  assert.match(html, /const buyCurrencyGoldCost = buyPrice \* getItemGoldCost\(mode\.buyCurrencyId\);/);
  assert.doesNotMatch(html, /const buyCurrencyGoldCost = revenueInBuyCurrency \* getItemGoldCost\(mode\.buyCurrencyId\);/);
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `node scripts/target-buy-currency-gold-cost.test.mjs`
Expected: `FAIL` because `index.html` still uses `revenueInBuyCurrency`.

- [ ] **Step 3: Write minimal implementation**

```js
const buyCurrencyGoldCost = buyPrice * getItemGoldCost(mode.buyCurrencyId);
```

Apply the same one-line formula change in `dist/index.html` to keep the shipped standalone output aligned with the main file.

- [ ] **Step 4: Run test to verify it passes**

Run: `node scripts/target-buy-currency-gold-cost.test.mjs`
Expected: `PASS`

- [ ] **Step 5: Run adjacent regression test**

Run: `node scripts/gold-efficiency-display.test.mjs`
Expected: `PASS`
