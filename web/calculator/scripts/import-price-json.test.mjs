import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const html = fs.readFileSync(path.join(__dirname, "..", "index.html"), "utf8");

test("target page exposes price json import next to trade mode selector", () => {
  assert.match(html, /id="price-json-import"/);
  assert.match(html, /data-action="import-price-json"/);
  assert.match(html, /导入价格表/);
});

test("price json import maps scanner currencies to local aliases and current mode", () => {
  assert.match(html, /const SCANNER_CURRENCY_ALIASES =/);
  assert.match(html, /"混沌石": "C"/);
  assert.match(html, /"神聖石": "D"/);
  assert.match(html, /"神圣石": "D"/);
  assert.match(html, /"崇高石": "E"/);
  assert.match(html, /"D": "D"/);
  assert.match(html, /function ensureCurrencyItem\(name\)/);
  assert.match(html, /state\.selectedModeKey = buyCurrency\.id \+ "\|" \+ sellCurrency\.id/);
});

test("price json import creates base pair and fills target rows", () => {
  assert.match(html, /function importPriceJson\(payload\)/);
  assert.match(html, /const pairRate = parseColonRatioToNumber\(payload\["当前交易对比例"\]\)/);
  assert.match(html, /ensureCurrencyPair\(sellCurrency\.id, buyCurrency\.id, pairRate\)/);
  assert.match(html, /buyCurrency\.id\]: formatImportedBuyPriceValue\(entry\["买入比例"\]\)/);
  assert.match(html, /sellCurrency\.id\]: formatImportedSellRatioValue\(entry\["卖出比例"\]\)/);
  assert.match(html, /state\.activeTab = "targets"/);
});

test("price json import keeps buy ratio as currency per target item", () => {
  assert.match(html, /function formatImportedBuyPriceValue\(value\)/);
  assert.match(html, /return parts \? formatImportedNumber\(Number\(parts\.left\) \/ Number\(parts\.right\)\) : ""/);
  assert.doesNotMatch(html, /formatImportedNumber\(1 \/ parsed\)/);
});

test("price json import keeps sell ratio as target items per sell currency", () => {
  assert.match(html, /function formatImportedRatioText\(value\)/);
  assert.match(html, /function formatImportedSellRatioValue\(value\)/);
  assert.match(html, /return formatImportedRatioText\(value\)/);
  assert.match(html, /return parts \? parts\.left \+ "\/" \+ parts\.right : ""/);
});

test("target page keeps price json import available in empty states", () => {
  assert.match(
    html,
    /if \(!targetItems\.length\) \{[\s\S]*data-action="import-price-json"[\s\S]*当前还没有可用的标的物/
  );
  assert.match(
    html,
    /if \(!currencyItems\.length \|\| !modes\.length\) \{[\s\S]*data-action="import-price-json"[\s\S]*当前还没有可用套利模式/
  );
});
