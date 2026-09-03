import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const html = fs.readFileSync(path.join(__dirname, "..", "index.html"), "utf8");

test("formula arbitrage tab supports triple p reveal and triple o hide", () => {
  assert.match(html, /showFormulaTab:\s*false/);
  assert.match(html, /state\.showFormulaTab \? `[\s\S]*data-tab="formulas"/);
  assert.match(html, /let formulaRevealKeyBuffer = ""/);
  assert.match(html, /formulaRevealKeyBuffer = \(formulaRevealKeyBuffer \+ key\)\.slice\(-3\)/);
  assert.match(html, /if \(formulaRevealKeyBuffer === "ppp"\)/);
  assert.match(html, /state\.showFormulaTab = true/);
  assert.match(html, /if \(formulaRevealKeyBuffer === "ooo"\)/);
  assert.match(html, /state\.showFormulaTab = false/);
  assert.match(html, /if \(state\.activeTab === "formulas"\) \{\s*state\.activeTab = "items";/);
});

test("defines formula arbitrage state and calculation entry points", () => {
  assert.match(html, /formulaArbitrage:/);
  assert.match(html, /draft:/);
  assert.match(html, /formulas:/);
  assert.match(html, /function calculateFormulaArbitrage\(formula\)/);
  assert.match(html, /function renderFormulasPage\(\)/);
});

test("formula arbitrage default draft includes cost and revenue entries", () => {
  assert.match(html, /function createDefaultFormulaArbitrageState\(\)/);
  assert.match(html, /costItems:\s*\[\s*createEmptyFormulaEntry\(/);
  assert.match(html, /revenueItems:\s*\[\s*createEmptyFormulaEntry\(/);
});

test("formula arbitrage uses first revenue currency as settlement currency", () => {
  assert.match(html, /const settlementCurrencyId = revenueItems\[0\]\.currencyId/);
  assert.match(html, /缺少 .* -> .* 的基础交易对，请先去基础交易对列表构建交易对。/);
  assert.match(html, /当前结算币种缺少与 D 的基础交易对，请先去基础交易对列表构建交易对。/);
});

test("formula arbitrage calculates settlement, profit, roi and gold efficiency", () => {
  assert.match(html, /const settlementCurrencyId = revenueItems\[0\]\.currencyId/);
  assert.match(html, /const totalCostInSettlementCurrency = costBreakdown\.reduce/);
  assert.match(html, /const totalRevenueInSettlementCurrency = revenueBreakdown\.reduce/);
  assert.match(html, /const netProfitInSettlementCurrency = totalRevenueInSettlementCurrency - totalCostInSettlementCurrency/);
  assert.match(html, /const roiPercent = \(\(totalRevenueInSettlementCurrency \/ totalCostInSettlementCurrency\) \* 100\) - 100/);
  assert.match(html, /goldEfficiencyPerDivine = totalGoldCost \/ netProfitInD/);
});

test("formula arbitrage includes currency acquisition gold cost for every entry", () => {
  assert.match(html, /const currencyGoldCostPerUnit = getItemGoldCost\(entry\.currencyId\)/);
  assert.match(html, /const totalGoldCost = \(goldCostPerUnit \+ \(currencyGoldCostPerUnit \* unitPrice\)\) \* quantity/);
});

test("formula page includes editor and saved formula list", () => {
  assert.match(html, /<h2>公式套利<\/h2>/);
  assert.match(html, /公式名称/);
  assert.match(html, /材料/);
  assert.match(html, /产物/);
  assert.match(html, /保存公式/);
  assert.match(html, /已保存公式列表/);
  assert.match(html, /编辑公式/);
});

test("formula page renders editor cards and saved list actions", () => {
  assert.match(html, /<h2>公式套利<\/h2>/);
  assert.match(html, /输入公式名称/);
  assert.match(html, /纯利润/);
  assert.match(html, /收益率/);
  assert.match(html, /金币效率/);
  assert.match(html, /新增材料/);
  assert.match(html, /新增产物/);
  assert.match(html, /保存公式/);
  assert.match(html, /已保存公式列表/);
  assert.match(html, /编辑公式/);
});

test("revenue cards use automatic currency gold cost instead of manual input", () => {
  assert.match(html, /type === "cost" \? "购买价" : "售价"/);
  assert.match(html, /type === "cost" \? "新增材料" : "新增产物"/);
  assert.match(html, /type === "cost"[\s\S]*?goldCostPerUnit[\s\S]*: ""/);
  assert.match(html, /该币种出售金币消耗自动读取物品列表/);
});

test("formula cards use hover delete action and inline meta rows", () => {
  assert.match(html, /\.formula-item-card:hover \.formula-card-delete/);
  assert.match(html, /formula-card-delete/);
  assert.match(html, /class="formula-inline-row"/);
  assert.match(html, /data-field="goldCostPerUnit"/);
  assert.match(html, /data-field="quantity"/);
});

test("formula page wraps topbar materials and products inside one outer panel", () => {
  assert.match(html, /<section class="panel formula-workspace">[\s\S]*class="formula-topbar[\s\S]*\$\{renderFormulaEntryCards\(draft\.costItems, "cost", currencyItems\)\}[\s\S]*\$\{renderFormulaEntryCards\(draft\.revenueItems, "revenue", currencyItems\)\}/);
});
