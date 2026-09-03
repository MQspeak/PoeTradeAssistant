# `poe-trade-scan/v2`

扫描结果以 `schemaVersion: "poe-trade-scan/v2"` 标识。货币使用规范代码（当前 `C`、`D`、`E`）；无法归一的名称保持原值，避免丢失新赛季币种。

每个比例均包含 `left`、`right` 和 `rightPerLeft`。例如 `675:1` 表示 `left=675`、`right=1`、`rightPerLeft=1/675`。消费者不得依据展示文本猜测方向。

V2 保留单条 `status`、`errorMessage` 和 `capturedAt`；这修复旧价格表仅输出成功字段、无法区分 OCR 失败和空价格的问题。旧中文字段 V1 文件仍由 `PriceScanDocumentAdapter.FromLegacyTable` 支持导入。
