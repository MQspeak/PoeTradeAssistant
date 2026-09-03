import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const html = fs.readFileSync(path.join(__dirname, "..", "index.html"), "utf8");

test("target arbitrage buy-currency gold cost uses buy-side amount", () => {
  assert.match(html, /const buyCurrencyGoldCost = buyPrice \* getItemGoldCost\(mode\.buyCurrencyId\);/);
  assert.doesNotMatch(html, /const buyCurrencyGoldCost = revenueInBuyCurrency \* getItemGoldCost\(mode\.buyCurrencyId\);/);
});
