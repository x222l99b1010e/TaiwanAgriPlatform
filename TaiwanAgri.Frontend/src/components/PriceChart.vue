<!-- src/components/PriceChart.vue -->
<script setup lang="ts">
import Btn from '@/components/ui/Btn.vue'
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
  type ChartDataset,
} from 'chart.js'
import type { PriceResponseDto, DisasterResponseDto } from '@/api/market'
import {
  seriesColor, seriesFill, seriesAccent, pointBorderColor, exportBackground,
  annotationColor, withAlpha, lineChartOptions, crosshairPlugin,
} from '@/constants/chartTheme'
// ⚠ 這一頁刻意不用 seriesDash：虛線在這裡已經被「7 日移動平均」佔用了。
//   同一個視覺屬性一次只能承載一種意思，兩種疊上去讀者就分不出虛線代表什麼。

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
  if (!props.prices.length) return { labels: [] as string[], datasets: [] as ChartDataset<'line'>[] }

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
  const datasets: ChartDataset<'line'>[] = []
  let ci = 0
  for (const [, g] of Object.entries(groups)) {
    const si = ci++
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
      borderColor: seriesColor(si),
      backgroundColor: seriesFill(si),
      borderWidth: 2,
      pointRadius: showPoints ? 3.5 : 0,
      pointHoverRadius: 7,
      pointBackgroundColor: seriesColor(si),
      pointBorderColor: pointBorderColor(),
      pointBorderWidth: 1,
      tension: 0.35,
      fill: true,
      spanGaps: true,
    })

    // 7 日移動平均虛線
    datasets.push({
      label: `${g.name}（7日均）`,
      data: maValues,
      borderColor: seriesAccent(si),
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
        ctx.strokeStyle = withAlpha(annotationColor(), 0.5)
        ctx.lineWidth = 1.5
        ctx.setLineDash([5, 4])
        ctx.stroke()

        // 頂部小三角標記
        ctx.beginPath()
        ctx.moveTo(x - 5, chartArea.top)
        ctx.lineTo(x + 5, chartArea.top)
        ctx.lineTo(x, chartArea.top + 8)
        ctx.closePath()
        ctx.fillStyle = withAlpha(annotationColor(), 0.7)
        ctx.fill()

        // 災害名稱（旋轉文字）
        ctx.save()
        ctx.translate(x + 10, chartArea.top + 16)
        ctx.rotate(Math.PI / 2)
        ctx.fillStyle = withAlpha(annotationColor(), 0.65)
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
    // 作物價格同樣是小幅波動，y 軸貼資料範圍；災害標註是這一頁專屬的外掛
    options: lineChartOptions({ unit: '元', fitY: 2 }),
    plugins: [crosshairPlugin, disasterPlugin],
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
// PriceChart.vue 裡的 exportChartImage
function exportChartImage() {
  if (!canvasRef.value) return

  const canvas = canvasRef.value
  const exportCanvas = document.createElement('canvas')
  exportCanvas.width = canvas.width
  exportCanvas.height = canvas.height

  const ctx = exportCanvas.getContext('2d')!
  ctx.fillStyle = exportBackground()
  ctx.fillRect(0, 0, exportCanvas.width, exportCanvas.height)
  ctx.drawImage(canvas, 0, 0)

  const url = exportCanvas.toDataURL('image/png')
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
            class="badge crop-tag"
            :style="{ borderColor: seriesColor(i), color: seriesColor(i) }"
          >{{ name }}</span>
        </div>
      </div>
      <div class="sep" />
      <div class="stat legend-note">
        <span class="stat-label">虛線 = 7 日移動平均</span>
      </div>
      <!-- 圖表右上角 -->
      <div class="chart-header">
        <Btn variant="secondary" size="sm" icon="mdi-image-outline" @click="exportChartImage">匯出圖片</Btn>
      </div>
    </div>

    <!-- 圖表畫布 -->
    <div class="canvas-wrap">
      <canvas ref="canvasRef" />
    </div>
  </div>
</template>

<style scoped>
/* 這一張是 PricesView 已經包在 .card--lg 裡的內容，所以自己不再畫一層卡片外框——
   顏色改用 semantic 層，陰影拿掉（style tile §三：卡片只用 1px 邊框＋底色差）。 */
.chart-card {
  border-radius: var(--radius-xl);
  animation: fadeUp var(--duration-slow) var(--ease-work);
  width: 100%;
  box-sizing: border-box;
}

@keyframes fadeUp {
  from { opacity: 0; transform: translateY(var(--lift-work)); }
  to   { opacity: 1; transform: translateY(0); }
}

.summary-bar {
  display: flex;
  align-items: center;
  gap: var(--space-6);
  flex-wrap: wrap;
  margin-bottom: var(--space-8);
  padding-bottom: var(--space-5);
  border-bottom: var(--border-width) solid var(--color-border);
}

.stat { display: flex; flex-direction: column; gap: var(--space-1); }

/* 這一列是圖表上方的小字摘要，不是 base.css 那組大數字統計卡，所以刻意壓小一階 */
.stat-label {
  font-size: var(--text-2xs);
  color: var(--color-text-dim);
  letter-spacing: 0.05em;
  text-transform: uppercase;
}

.stat-value {
  font-family: var(--font-num);
  font-size: var(--text-base);
  color: var(--color-text);
  font-variant-numeric: tabular-nums;
  font-weight: var(--weight-medium);
}

.sep {
  width: 1px;
  height: 36px;
  background: var(--color-border);
  flex-shrink: 0;
}

.tag-row { display: flex; gap: var(--space-2); flex-wrap: wrap; }

/* 標籤外殼已收進 base.css 的 .badge，這裡只留語意色 */
.crop-tag {
  border: var(--border-width) solid;
  opacity: 0.85;
  transition: opacity var(--duration-base);
}
.crop-tag:hover { opacity: 1; }

.legend-note .stat-label { font-style: italic; font-size: var(--text-2xs); }

.canvas-wrap {
  position: relative;
  height: 500px;
  width: 100%;
}

.chart-header { margin-left: auto; }
</style>