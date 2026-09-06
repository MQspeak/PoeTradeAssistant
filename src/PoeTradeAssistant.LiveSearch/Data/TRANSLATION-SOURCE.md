# POE2 English to Simplified Chinese dictionary

Source: https://github.com/soifow/poe-ninja-translator
Data: https://raw.githubusercontent.com/soifow/poe-ninja-translator/main/src/data/dict.json
Retrieved: 2026-09-05
Upstream metadata: {"version": "20260629", "count": 13070, "updatedAt": "2026-06-29T03:01:09.592Z"}
Source SHA256: 0bf5a40f36c421090d98af3b49507c958003555b64f76f51c73b366248ac39a0

Only nonempty cn mappings are retained, using upstream category priority.
Upstream README states: 本项目仅供学习交流。翻译数据版权归 poe2db.tw 所有。
This local integration does not grant additional redistribution rights.
The C# matching implementation is implemented locally; no extension code is bundled.

# POE1 English to Simplified Chinese dictionary

Source: https://github.com/DonkiChen/Awakened-PoE-Trade-Simplified-Chinese
Data: `renderer/public/data/en/{items,stats}.ndjson` paired with `renderer/public/data/zh_CN/{items,stats}.ndjson`
Retrieved: 2026-09-06
Source commit: 997d1014a194b093b5eb727d8c3b5bde8a5fb659
Generated dictionary: 17,173 mappings, 1,478,286 bytes
Generated SHA256: 215cf7604ae7dbaf759e8233aaa5b744b6a8e7d17bdd19e6c80eb3cdceec8b55

Item names are paired through `refName`. Stat templates are paired through their shared English `ref` and matcher position. Duplicate English keys retain the first Simplified Chinese mapping so output is deterministic. The upstream repository is distributed under the MIT license; its copyright and permission notice apply to the derived dictionary.
