import { useEffect, useMemo, useState } from "react";

type ArbitrageLeg = {
  pair: string;
  from: string;
  to: string;
  rate: string;
  feePercent: string;
  feeFlat: string;
};

type ComputedLeg = {
  pairLabel: string;
  from: string;
  to: string;
  inputAmount: number;
  rawOutput: number;
  percentFeeAmount: number;
  flatFeeAmount: number;
  outputAmount: number;
  effectiveRate: number;
};

type CalculationResult =
  | {
      ok: true;
      startCurrency: string;
      finalCurrency: string;
      startAmount: number;
      finalAmount: number;
      profitAmount: number;
      roiPercent: number;
      multiplier: number;
      breakEvenRate: number;
      rateEdgePercent: number;
      legs: ComputedLeg[];
    }
  | {
      ok: false;
      message: string;
    };

const STORAGE_KEY = "poe2-triangular-arbitrage-calculator:v1";

const exampleLegs: ArbitrageLeg[] = [
  {
    pair: "Divine -> Exalted",
    from: "Divine",
    to: "Exalted",
    rate: "12",
    feePercent: "0",
    feeFlat: "0"
  },
  {
    pair: "Exalted -> Chaos",
    from: "Exalted",
    to: "Chaos",
    rate: "14",
    feePercent: "0",
    feeFlat: "0"
  },
  {
    pair: "Chaos -> Divine",
    from: "Chaos",
    to: "Divine",
    rate: "0.0064",
    feePercent: "0",
    feeFlat: "0"
  }
];

const emptyLegs: ArbitrageLeg[] = [
  { pair: "", from: "", to: "", rate: "", feePercent: "0", feeFlat: "0" },
  { pair: "", from: "", to: "", rate: "", feePercent: "0", feeFlat: "0" },
  { pair: "", from: "", to: "", rate: "", feePercent: "0", feeFlat: "0" }
];

function parsePositiveNumber(value: string): number | null {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

function parseNonNegativeNumber(value: string): number | null {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
}

function formatNumber(value: number, digits = 4): string {
  if (!Number.isFinite(value)) {
    return "-";
  }

  return new Intl.NumberFormat("zh-CN", {
    minimumFractionDigits: 0,
    maximumFractionDigits: digits
  }).format(value);
}

function buildCalculation(startAmountText: string, legs: ArbitrageLeg[]): CalculationResult {
  const startAmount = parsePositiveNumber(startAmountText);
  if (!startAmount) {
    return { ok: false, message: "请输入有效的初始资金。" };
  }

  const normalized = legs.map((leg, index) => {
    const from = leg.from.trim();
    const to = leg.to.trim();
    const rate = parsePositiveNumber(leg.rate);
    const feePercent = parseNonNegativeNumber(leg.feePercent);
    const feeFlat = parseNonNegativeNumber(leg.feeFlat);

    if (!from || !to) {
      return { ok: false as const, message: `第 ${index + 1} 跳缺少币种信息。` };
    }

    if (from === to) {
      return { ok: false as const, message: `第 ${index + 1} 跳的输入币种和输出币种不能相同。` };
    }

    if (rate === null) {
      return { ok: false as const, message: `第 ${index + 1} 跳的汇率必须大于 0。` };
    }

    if (feePercent === null || feePercent >= 100) {
      return { ok: false as const, message: `第 ${index + 1} 跳的手续费比例必须在 0 到 100 之间。` };
    }

    if (feeFlat === null) {
      return { ok: false as const, message: `第 ${index + 1} 跳的固定手续费不能为负数。` };
    }

    return {
      ok: true as const,
      pair: leg.pair.trim() || `${from}/${to}`,
      from,
      to,
      rate,
      feePercent,
      feeFlat
    };
  });

  const invalid = normalized.find((item) => !item.ok);
  if (invalid && !invalid.ok) {
    return invalid;
  }

  const safeLegs = normalized.filter((item): item is Extract<(typeof normalized)[number], { ok: true }> => item.ok);

  for (let index = 0; index < safeLegs.length - 1; index += 1) {
    if (safeLegs[index].to !== safeLegs[index + 1].from) {
      return {
        ok: false,
        message: `第 ${index + 1} 跳的输出币种需要和第 ${index + 2} 跳的输入币种一致。`
      };
    }
  }

  if (safeLegs[0].from !== safeLegs[safeLegs.length - 1].to) {
    return { ok: false, message: "三条路径没有闭环，最后一跳需要换回初始币种。" };
  }

  const computed: ComputedLeg[] = [];
  let currentAmount = startAmount;

  for (const leg of safeLegs) {
    const rawOutput = currentAmount * leg.rate;
    const percentFeeAmount = rawOutput * (leg.feePercent / 100);
    const flatFeeAmount = leg.feeFlat;
    const outputAmount = rawOutput - percentFeeAmount - flatFeeAmount;

    if (outputAmount <= 0) {
      return { ok: false, message: `${leg.pair} 扣除手续费后结果不大于 0，请检查汇率或手续费设置。` };
    }

    computed.push({
      pairLabel: leg.pair,
      from: leg.from,
      to: leg.to,
      inputAmount: currentAmount,
      rawOutput,
      percentFeeAmount,
      flatFeeAmount,
      outputAmount,
      effectiveRate: outputAmount / currentAmount
    });

    currentAmount = outputAmount;
  }

  const leg3 = safeLegs[2];
  const amountBeforeLeg3 = computed[1].outputAmount;
  const breakEvenRate = (startAmount + leg3.feeFlat) / (amountBeforeLeg3 * (1 - leg3.feePercent / 100));
  const rateEdgePercent = ((leg3.rate - breakEvenRate) / breakEvenRate) * 100;
  const finalAmount = computed[2].outputAmount;
  const profitAmount = finalAmount - startAmount;
  const roiPercent = (profitAmount / startAmount) * 100;

  return {
    ok: true,
    startCurrency: safeLegs[0].from,
    finalCurrency: safeLegs[2].to,
    startAmount,
    finalAmount,
    profitAmount,
    roiPercent,
    multiplier: finalAmount / startAmount,
    breakEvenRate,
    rateEdgePercent,
    legs: computed
  };
}

export function App() {
  const [startAmount, setStartAmount] = useState("1");
  const [legs, setLegs] = useState<ArbitrageLeg[]>(exampleLegs);
  const result = useMemo(() => buildCalculation(startAmount, legs), [startAmount, legs]);

  useEffect(() => {
    const saved = window.localStorage.getItem(STORAGE_KEY);
    if (!saved) {
      return;
    }

    try {
      const parsed = JSON.parse(saved) as { startAmount?: string; legs?: ArbitrageLeg[] };
      if (parsed.startAmount) {
        setStartAmount(parsed.startAmount);
      }
      if (Array.isArray(parsed.legs) && parsed.legs.length === 3) {
        setLegs(parsed.legs);
      }
    } catch {
      window.localStorage.removeItem(STORAGE_KEY);
    }
  }, []);

  useEffect(() => {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify({ startAmount, legs }));
  }, [startAmount, legs]);

  const updateLeg = (index: number, field: keyof ArbitrageLeg, value: string) => {
    setLegs((current) => current.map((leg, currentIndex) => (currentIndex === index ? { ...leg, [field]: value } : leg)));
  };

  return (
    <div className="app-shell">
      <section className="hero-card">
        <div>
          <p className="eyebrow">POE2 Currency Tool</p>
          <h1>POE2 三角套利计算器</h1>
          <p className="hero-copy">输入三条兑换路径的方向、汇率和手续费，快速判断闭环套利是否有利润空间。</p>
        </div>
        <div className="hero-actions">
          <button className="ghost-button" type="button" onClick={() => { setStartAmount("10"); setLegs(exampleLegs); }}>
            加载示例
          </button>
          <button className="ghost-button" type="button" onClick={() => { setStartAmount("1"); setLegs(emptyLegs); }}>
            清空
          </button>
        </div>
      </section>

      <section className="summary-grid">
        <label className="metric-card input-card">
          <span>初始资金</span>
          <input value={startAmount} onChange={(event) => setStartAmount(event.target.value)} placeholder="例如 10" />
        </label>
        <article className={`metric-card ${result.ok && result.profitAmount > 0 ? "profit" : result.ok ? "loss" : ""}`}>
          <span>净利润</span>
          <strong>{result.ok ? `${result.profitAmount >= 0 ? "+" : ""}${formatNumber(result.profitAmount, 6)} ${result.startCurrency}` : "--"}</strong>
          <small>{result.ok ? `ROI ${result.roiPercent >= 0 ? "+" : ""}${formatNumber(result.roiPercent, 3)}%` : result.message}</small>
        </article>
        <article className="metric-card">
          <span>闭环倍率</span>
          <strong>{result.ok ? `${formatNumber(result.multiplier, 6)}x` : "--"}</strong>
          <small>{result.ok ? `${formatNumber(result.startAmount, 4)} ${result.startCurrency} → ${formatNumber(result.finalAmount, 4)} ${result.finalCurrency}` : "等待有效输入"}</small>
        </article>
        <article className="metric-card">
          <span>第三跳保本汇率</span>
          <strong>{result.ok ? formatNumber(result.breakEvenRate, 8) : "--"}</strong>
          <small>{result.ok ? `当前优势 ${result.rateEdgePercent >= 0 ? "+" : ""}${formatNumber(result.rateEdgePercent, 3)}%` : "用于判断利润安全边际"}</small>
        </article>
      </section>

      <section className="legs-grid">
        {legs.map((leg, index) => (
          <article key={`leg-${index}`} className="leg-card">
            <div className="leg-head">
              <span className="leg-index">第 {index + 1} 跳</span>
              <input
                value={leg.pair}
                onChange={(event) => updateLeg(index, "pair", event.target.value)}
                placeholder={`例如 ${index === 0 ? "Divine -> Exalted" : index === 1 ? "Exalted -> Chaos" : "Chaos -> Divine"}`}
              />
            </div>
            <div className="leg-form">
              <label>
                <span>输入币种</span>
                <input value={leg.from} onChange={(event) => updateLeg(index, "from", event.target.value)} placeholder="From" />
              </label>
              <label>
                <span>输出币种</span>
                <input value={leg.to} onChange={(event) => updateLeg(index, "to", event.target.value)} placeholder="To" />
              </label>
              <label>
                <span>汇率</span>
                <input value={leg.rate} onChange={(event) => updateLeg(index, "rate", event.target.value)} placeholder="1 个输入币可换多少输出币" />
              </label>
              <label>
                <span>手续费 %</span>
                <input value={leg.feePercent} onChange={(event) => updateLeg(index, "feePercent", event.target.value)} placeholder="0" />
              </label>
              <label>
                <span>固定手续费</span>
                <input value={leg.feeFlat} onChange={(event) => updateLeg(index, "feeFlat", event.target.value)} placeholder="0" />
              </label>
            </div>
            {result.ok ? (
              <div className="leg-result">
                <span>{formatNumber(result.legs[index].inputAmount, 4)} {result.legs[index].from} → {formatNumber(result.legs[index].outputAmount, 4)} {result.legs[index].to}</span>
                <span>有效倍率 {formatNumber(result.legs[index].effectiveRate, 6)}x</span>
                <span>手续费损耗 {formatNumber(result.legs[index].percentFeeAmount + result.legs[index].flatFeeAmount, 6)} {result.legs[index].to}</span>
              </div>
            ) : (
              <div className="leg-result muted">{result.message}</div>
            )}
          </article>
        ))}
      </section>

      <section className="table-card">
        <table>
          <thead>
            <tr>
              <th>路径</th>
              <th>投入</th>
              <th>原始产出</th>
              <th>手续费</th>
              <th>净产出</th>
              <th>有效倍率</th>
            </tr>
          </thead>
          <tbody>
            {result.ok ? (
              result.legs.map((leg) => (
                <tr key={leg.pairLabel}>
                  <td>{leg.pairLabel}</td>
                  <td>{formatNumber(leg.inputAmount, 4)} {leg.from}</td>
                  <td>{formatNumber(leg.rawOutput, 4)} {leg.to}</td>
                  <td>{formatNumber(leg.percentFeeAmount + leg.flatFeeAmount, 6)} {leg.to}</td>
                  <td>{formatNumber(leg.outputAmount, 4)} {leg.to}</td>
                  <td>{formatNumber(leg.effectiveRate, 6)}x</td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={6} className="empty-row">{result.message}</td>
              </tr>
            )}
          </tbody>
        </table>
      </section>
    </div>
  );
}
