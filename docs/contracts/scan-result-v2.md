# `poe-trade-scan/v2`

扫描结果以 `schemaVersion: "poe-trade-scan/v2"` 标识。货币使用规范代码（当前 `C`、`D`、`E`）；无法归一的名称保持原值，避免丢失新赛季币种。

每个比例均包含 `left`、`right` 和 `rightPerLeft`。例如 `675:1` 表示 `left=675`、`right=1`、`rightPerLeft=1/675`。消费者不得依据展示文本猜测方向。

V2 保留单条 `status`、`errorMessage` 和 `capturedAt`；这修复旧价格表仅输出成功字段、无法区分 OCR 失败和空价格的问题。旧中文字段 V1 文件仍由 `PriceScanDocumentAdapter.FromLegacyTable` 支持导入。

## 标的套利四价字段

每个 `items[]` 可额外写入四个可空十进制字段：`highestBuyPrice`（最高买入价）、`lowestBuyPrice`（最低买入价）、`highestSellPrice`（最高卖出价）、`lowestSellPrice`（最低卖出价）。买入价使用 `pair.buyCurrency`，卖出价使用 `pair.sellCurrency`，必须为正数。按游戏位置规则，从当前比例 `M:N` 直接取分量，不做除法、倒数或数值排序：购买通货在右侧取 N 为最高买入价，在左侧取 M 为最低买入价；售出通货在右侧取 N 为最高卖出价，在左侧取 M 为最低卖出价。识别失败或没有报价时写 `null`，不要写 0。

计算器四种模式分别选择高买/低卖、高买/高卖、低买/高卖、低买/低卖。收益率为 `(卖出单价 × 当前交易对汇率 − 买入单价) / 买入单价 × 100%`。缺少当前模式所需报价时显示待计算。

扫描先完成所有标的的买入扫描，再完成所有标的的卖出扫描。每个阶段先读取右侧通货方向，再 Ctrl+点击左侧通货对调，读取左侧通货方向，最后对调恢复。金币记录复用独立于四价读取。

新扫描包含 `priceObservations`，以四个价格字段名为键，分别保存 `raw`、`normalized`、`status`、`errorMessage`。`pending` 表示未执行，`ok` 表示成功，其他状态表示失败。单个价格失败不丢弃同一条目的其他成功价格。计算器导入新扫描时不以旧比例回填失败的价格，缺失值清空并按当前模式判断能否计算。

兼容旧数据：没有 `priceObservations` 的旧文件可继续使用原 `buyRatio` 和 `sellRatio` 作为默认高买低卖的报价；不会用旧报价推测低买或高卖价格。新扫描保留这两个兼容字段，但四种模式均应使用四价字段。当前扫描仅输出 .v2.json 文件，包含四价及逐字段识别状态，不再生成旧中文字段 JSON。
