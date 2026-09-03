import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const html = fs.readFileSync(path.join(__dirname, "..", "index.html"), "utf8");

test("includes rune arbitrage tab after the existing three tabs", () => {
  assert.match(
    html,
    /data-tab="items">[\s\S]*?data-tab="pairs">[\s\S]*?data-tab="targets">[\s\S]*?data-tab="runes">/s
  );
});

test("defines rune arbitrage page structure and state", () => {
  assert.match(html, /runeArbitrage:/);
  assert.match(html, /function renderRunesPage\(\)/);
  assert.match(html, /function calculateRuneArbitrage\(runeRow\)/);
});

test("rune arbitrage page uses updated formula and simplified table", () => {
  const runesSectionMatch = html.match(/function renderRunesPage\(\) \{[\s\S]*?function render\(\) \{/);
  assert.ok(runesSectionMatch);
  const runesSection = runesSectionMatch[0];

  assert.doesNotMatch(runesSection, /<th>提示<\/th>/);
  assert.match(runesSection, /<th>纯利润<\/th>/);
  assert.match(runesSection, /<th>总成本<\/th>/);
  assert.match(html, /ratioPercent:\s*\(\(\(4 \* perfectPrice\) \/ totalCostInPerfectCurrency\) \* 100\) - 100/);
  assert.match(html, /netProfitInPerfectCurrency\s*=\s*totalRevenueInPerfectCurrency - totalCostInPerfectCurrency/);
  assert.match(runesSection, /formatNumber\(result\.netProfitInPerfectCurrency, 4\)/);
});

test("rune arbitrage supports ratio input and gold efficiency", () => {
  assert.match(html, /parseTradeValue\(runeRow\.perfectPrice, \{ allowRatio: true \}\)/);
  assert.match(html, /convertTradeValueToCurrency\(component\.amount, component\.currencyId, runeRow\.perfectCurrencyId, \{/);
  assert.match(html, /goldEfficiencyPerDivine/);
  assert.match(html, /D.*基础交易对/);
  assert.match(html, /smallGoldCost/);
  assert.match(html, /getItemGoldCost\(runeRow\.perfectCurrencyId\)/);
  assert.match(html, /const componentCurrencyGoldCost = getItemGoldCost\(component\.currencyId\)/);
  assert.match(html, /totalGoldCost \+= \(goldCost \+ \(componentCurrencyGoldCost \* converted\.amount\)\) \* component\.multiplier/);
  assert.doesNotMatch(html, /perfectGoldCost:/);
  assert.match(html, /formatGoldCompact\(result\.goldEfficiencyPerDivine\)/);
  assert.match(html, /goldEfficiencyPerDivine\s*=\s*\(totalGoldCost \+ totalGoldRevenueCost\) \/ netProfitInD/);
});
