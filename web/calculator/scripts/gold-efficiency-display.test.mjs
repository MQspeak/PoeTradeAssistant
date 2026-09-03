import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const html = fs.readFileSync(path.join(__dirname, "..", "index.html"), "utf8");

test("gold efficiency hint only references the destination currency", () => {
  assert.match(html, /formatGoldCompact\(result\.goldPerProfitSellCurrency\).*selectedMode\.sellCurrencyName/s);
  assert.doesNotMatch(html, /formatGoldCompact\(result\.goldPerProfitBuyCurrency\).*selectedMode\.buyCurrencyName/s);
});

test("non-profitable gold efficiency copy only mentions the destination currency", () => {
  assert.doesNotMatch(html, /\$\{selectedMode\.buyCurrencyName\} \/ \$\{selectedMode\.sellCurrencyName\}/);
});
