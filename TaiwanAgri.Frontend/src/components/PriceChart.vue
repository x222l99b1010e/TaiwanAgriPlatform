<!-- src/components/PriceChart.vue -->
<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch, nextTick } from 'vue'
import {
  Chart,
  LineElement,
  PointElement,
  LineController,
  CategoryScale,
  LinearScale,
  Tooltip,
  Legend,
  Filler,
} from 'chart.js'
import type { PriceResponseDto, DisasterResponseDto } from '@/api/market'

// Chart.js 採用「按需註冊」設計，沒 register 就沒功能
Chart.register(LineElement, PointElement, LineController, CategoryScale, LinearScale, Tooltip, Legend, Filler)

// ── Props ─────────────────────────────────────────────
// Props 新增 disasters
const props = defineProps<{
  prices: PriceResponseDto[]
  disasters: DisasterResponseDto[]
}>()

// ── Chart 實例 ─────────────────────────────────────────
const canvasRef = ref<HTMLCanvasElement | null>(null)
let chartInstance: Chart | null = null

// ── 色盤（最多 5 條作物線）─────────────────────────────
// PALETTE 換成這個
const PALETTE = [
  { main: '#2e7d32', fade: 'rgba(46,125,50,0.08)',   ma: 'rgba(46,125,50,0.28)'   },
  { main: '#e65100', fade: 'rgba(230,81,0,0.08)',    ma: 'rgba(230,81,0,0.28)'    },
  { main: '#1565c0', fade: 'rgba(21,101,192,0.08)',  ma: 'rgba(21,101,192,0.28)'  },
  { main: '#6a1b9a', fade: 'rgba(106,27,154,0.08)',  ma: 'rgba(106,27,154,0.28)'  },
  { main: '#c77700', fade: 'rgba(199,119,0,0.08)',   ma: 'rgba(199,119,0,0.28)'   },
]
// 加在 PALETTE 定義下方
const getColor = (i: number) => PALETTE[i % PALETTE.length]!
// ── 7 日移動平均計算 ───────────────────────────────────
function calcMA(values: number[], window = 7): number[] {
  return values.map((_, i) => {
    const start = Math.max(0, i - window + 1)
    const slice = values.slice(start, i + 1)
    const avg = slice.reduce((s, v) => s + v, 0) / slice.length
    return Math.round(avg * 10) / 10
  })
}

// ── 核心 computed：平鋪 prices → Chart.js datasets ────
// 這是 PriceChart.vue 的主要職責，類比 Controller 的 View Model 整理
const chartData = computed(() => {
  if (!props.prices.length) return { labels: [] as string[], datasets: [] as any[] }

  // 1. 收集所有日期作為 X 軸 labels
  const labels = Array.from(new Set(props.prices.map(p => p.transDate))).sort()

  // 2. 按 cropCode 分組
  const groups: Record<string, { name: string; priceMap: Record<string, number> }> = {}
  for (const p of props.prices) {
    if (!groups[p.cropCode]) groups[p.cropCode] = { name: p.cropName, priceMap: {} }
    const entry = groups[p.cropCode]!
    entry.priceMap[p.transDate] = p.avgPrice
  }

  // 3. 每個 cropCode → 兩條線（主線 + MA 虛線）
  const datasets: any[] = []
  let ci = 0
  for (const [, g] of Object.entries(groups)) {
    const color = getColor(ci++)
    const values = labels.map(d => g.priceMap[d] ?? null)

    // MA 只對有值的資料點計算，再映射回完整日期序列
    const actualPairs = labels
      .map((d, i) => ({ d, v: values[i] }))
      .filter((p): p is { d: string; v: number } => p.v !== null)
    const maRaw = calcMA(actualPairs.map(p => p.v))
    const maMap: Record<string, number> = {}
    actualPairs.forEach((p, i) => { maMap[p.d] = maRaw[i]! })
    const maValues = labels.map(d => maMap[d] ?? null)

    const showPoints = labels.length <= 60  // 資料點太多就隱藏圓點，保持清爽

    // 主折線
    datasets.push({
      label: g.name,
      data: values,
      borderColor: color.main,
      backgroundColor: color.fade,
      borderWidth: 2,
      pointRadius: showPoints ? 3.5 : 0,
      pointHoverRadius: 7,
      pointBackgroundColor: color.main,
      pointBorderColor: 'rgba(0,0,0,0.15)',
      pointBorderWidth: 1,
      tension: 0.35,
      fill: true,
      spanGaps: true,
    })

    // 7 日移動平均虛線
    datasets.push({
      label: `${g.name}（7日均）`,
      data: maValues,
      borderColor: color.ma,
      backgroundColor: 'transparent',
      borderWidth: 1.5,
      borderDash: [6, 5],
      pointRadius: 0,
      pointHoverRadius: 0,
      tension: 0.35,
      fill: false,
      spanGaps: true,
    })
  }

  return { labels, datasets }
})

// ── 摘要資訊（卡片頂部統計列）────────────────────────
const summary = computed(() => {
  if (!props.prices.length) return null
  const dates = props.prices.map(p => p.transDate).sort()
  return {
    total: props.prices.length,
    dateFrom: dates[0],
    dateTo: dates[dates.length - 1],
    crops: [...new Set(props.prices.map(p => p.cropName))],
  }
})

// ── Chart.js 建立 / 更新 / 銷毀 ──────────────────────
function buildChart() {
  if (!canvasRef.value || !chartData.value.labels.length) return
  chartInstance?.destroy()

  // 把每個 disasterName 的最早日期取出，作為垂直線位置
  const eventMap = new Map<string, string>()
  for (const d of props.disasters) {
    const existing = eventMap.get(d.disasterName)
    if (!existing || d.alertDate < existing) {
      eventMap.set(d.disasterName, d.alertDate)
    }
  }
  const disasterLines = Array.from(eventMap.entries())
    .map(([name, date]) => ({ name, date }))

  // Chart.js inline plugin：在指定日期畫垂直虛線
  const disasterPlugin = {
    id: 'disasterLines',
    afterDraw(chart: Chart) {
      const { ctx, chartArea, scales, data } = chart
      const labels = data.labels as string[]

      disasterLines.forEach(({ name, date }) => {
        const idx = labels.indexOf(date)
        if (idx === -1) return   // 該日期不在 X 軸，跳過

        const x = scales['x']!.getPixelForValue(idx)

        // 垂直虛線
        ctx.save()
        ctx.beginPath()
        ctx.moveTo(x, chartArea.top)
        ctx.lineTo(x, chartArea.bottom)
        ctx.strokeStyle = 'rgba(255, 130, 80, 0.5)'
        ctx.lineWidth = 1.5
        ctx.setLineDash([5, 4])
        ctx.stroke()

        // 頂部小三角標記
        ctx.beginPath()
        ctx.moveTo(x - 5, chartArea.top)
        ctx.lineTo(x + 5, chartArea.top)
        ctx.lineTo(x, chartArea.top + 8)
        ctx.closePath()
        ctx.fillStyle = 'rgba(255, 130, 80, 0.7)'
        ctx.fill()

        // 災害名稱（旋轉文字）
        ctx.save()
        ctx.translate(x + 10, chartArea.top + 16)
        ctx.rotate(Math.PI / 2)
        ctx.fillStyle = 'rgba(255, 160, 110, 0.65)'
        ctx.font = '10px sans-serif'
        ctx.textAlign = 'left'
        ctx.fillText(name, 0, 0)
        ctx.restore()

        ctx.restore()
      })
    }
  }

  chartInstance = new Chart(canvasRef.value, {
    type: 'line',
    data: chartData.value,
    options: {
      responsive: true,
      maintainAspectRatio: false,
      interaction: {
        mode: 'index' as const,
        intersect: false,
      },
      scales: {
        x: {
          ticks: {
            maxTicksLimit: 12,
             color: 'rgba(26,40,32,0.75)',  // 從 0.45 → 0.75
            font: { size: 12 },            // 從 11 → 12
            callback(val: unknown, index: number) {
              return (this as any).getLabelForValue(index) ?? String(val)
            },
          },
          grid:   { color: 'rgba(0,0,0,0.05)' },
          border: { color: 'rgba(0,0,0,0.08)' },
        },
        y: {
          ticks: {
            color: 'rgba(26,40,32,0.75)',  // 從 0.45 → 0.75
            font: { size: 12 },            // 從 11 → 12
            callback: (val: unknown) => `${val} 元`,
          },
          grid:   { color: 'rgba(0,0,0,0.05)' },
          border: { color: 'rgba(0,0,0,0.08)' },
        },
      },
      plugins: {
        tooltip: {
          backgroundColor: 'rgba(255,255,255,0.96)',
          titleColor:      'rgba(26,40,32,0.90)',
          bodyColor:       'rgba(26,40,32,0.70)',
          borderColor:     'rgba(0,0,0,0.10)',
          borderWidth: 1,
          padding: 12,
          callbacks: {
            label: (ctx: any) =>
              ctx.parsed.y !== null ? ` ${ctx.dataset.label}：${ctx.parsed.y} 元` : '',
          },
        },
        legend: {
          position: 'top' as const,
          labels: {
            color: 'rgba(26,40,32,0.85)',  // 從 0.65 → 0.85
            font: { size: 13 },            // 從 12 → 13
            usePointStyle: true,
            pointStyleWidth: 10,
          },
        },
      },
    },
    plugins: [disasterPlugin],
  })
}

onMounted(() => nextTick(buildChart))
onUnmounted(() => chartInstance?.destroy())
watch(
  () => [props.prices, props.disasters],
  () => nextTick(buildChart),
  { deep: true }
)

// 暴露給父元件或直接放在元件內
function exportChartImage() {
  if (!canvasRef.value) return
  const url = canvasRef.value.toDataURL('image/png')
  const a = document.createElement('a')
  a.href = url
  a.download = 'chart.png'
  a.click()
}
</script>

<template>
  <div class="chart-card" v-if="prices.length > 0">

    <!-- 頂部摘要統計列 -->
    <div class="summary-bar" v-if="summary">
      <div class="stat">
        <span class="stat-label">資料筆數</span>
        <span class="stat-value">{{ summary.total.toLocaleString() }}</span>
      </div>
      <div class="sep" />
      <div class="stat">
        <span class="stat-label">日期範圍</span>
        <span class="stat-value">{{ summary.dateFrom }} ～ {{ summary.dateTo }}</span>
      </div>
      <div class="sep" />
      <div class="stat">
        <span class="stat-label">查詢作物</span>
        <div class="tag-row">
          <span
            v-for="(name, i) in summary.crops"
            :key="i"
            class="crop-tag"
            :style="{ borderColor: getColor(i).main, color: getColor(i).main }"
          >{{ name }}</span>
        </div>
      </div>
      <div class="sep" />
      <div class="stat legend-note">
        <span class="stat-label">虛線 = 7 日移動平均</span>
      </div>
      <!-- 圖表右上角 -->
      <div class="chart-header">
        <button class="btn-export" @click="exportChartImage">匯出圖片</button>
      </div>
    </div>

    <!-- 圖表畫布 -->
    <div class="canvas-wrap">
      <canvas ref="canvasRef" />
    </div>
  </div>
</template>

<style scoped>
.chart-card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 16px;
  padding: 28px 32px 36px;
  animation: fadeUp 0.45s cubic-bezier(0.22, 1, 0.36, 1);
  width: 100%;
  box-sizing: border-box;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
}

@keyframes fadeUp {
  from { opacity: 0; transform: translateY(14px); }
  to   { opacity: 1; transform: translateY(0); }
}

.summary-bar {
  display: flex;
  align-items: center;
  gap: 24px;
  flex-wrap: wrap;
  margin-bottom: 28px;
  padding-bottom: 20px;
  border-bottom: 1px solid var(--border);
}

.stat { display: flex; flex-direction: column; gap: 5px; }

.stat-label {
  font-size: 11px;
  color: rgba(26,40,32,0.55);   /* 從 text-muted(0.40) → 0.55 */
  letter-spacing: 0.05em;
  text-transform: uppercase;
}

.stat-value {
  font-size: 14px;              /* 從 13px → 14px */
  color: var(--text-primary);
  font-variant-numeric: tabular-nums;
  font-weight: 600;             /* 加粗 */
}

.sep {
  width: 1px;
  height: 36px;
  background: var(--border);
  flex-shrink: 0;
}

.tag-row { display: flex; gap: 6px; flex-wrap: wrap; }

.crop-tag {
  font-size: 11px;
  padding: 2px 9px;
  border: 1px solid;
  border-radius: 999px;
  opacity: 0.85;
  transition: opacity 0.2s;
}
.crop-tag:hover { opacity: 1; }

.legend-note .stat-label { font-style: italic; font-size: 11px; }

.canvas-wrap {
  position: relative;
  height: 500px;
  width: 100%;
}

.chart-header { margin-left: auto; }

.btn-export {
  padding: 9px 20px; border-radius: 999px;
  border: 1px solid #4a148c;
  background: linear-gradient(
    180deg,
    #ab47bc 0%,
    #7b1fa2 40%,
    #4a148c 100%
  );
  color: white;
  font-size: 13.5px; font-weight: 700; cursor: pointer;
  box-shadow:
    inset 0 1px 0 rgba(255,255,255,0.35),
    inset 0 -2px 4px rgba(0,0,0,0.25),
    0 2px 6px rgba(0,0,0,0.18);
  transition: all 0.15s;
}
.btn-export:hover {
  background: linear-gradient(
    180deg,
    #ba68c8 0%,
    #8e24aa 40%,
    #6a1b9a 100%
  );
  box-shadow:
    inset 0 1px 0 rgba(255,255,255,0.45),
    inset 0 -2px 4px rgba(0,0,0,0.20),
    0 3px 10px rgba(0,0,0,0.22);
}
.btn-export:active {
  background: linear-gradient(
    180deg,
    #4a148c 0%,
    #7b1fa2 60%,
    #8e24aa 100%
  );
  box-shadow:
    inset 0 2px 6px rgba(0,0,0,0.35),
    inset 0 -1px 0 rgba(255,255,255,0.15),
    0 1px 3px rgba(0,0,0,0.15);
}
</style>