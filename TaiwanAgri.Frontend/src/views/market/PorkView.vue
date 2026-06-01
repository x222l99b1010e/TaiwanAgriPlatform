<template>
  <div class="pork-view">
    <h1>毛豬行情查詢</h1>

    <!-- 篩選區 -->
    <section class="filter-section">
      <div class="filter-row">
        <DateRangePicker v-model:startDate="startDate" v-model:endDate="endDate" />

    <div class="field-group">
    <label class="field-label">市場</label>
    <select class="market-select" v-model="selectedMarket" :disabled="!hasQueried">
        <option value="">全部市場</option>
        <option v-for="name in availableMarkets" :key="name" :value="name">
        {{ name }}
        </option>
    </select>
    <!-- 提示放在 select 正下方 -->
    <div class="query-hint" v-if="!hasQueried">
        <span class="mdi mdi-information-outline hint-icon" />
        請先按「查詢行情」載入資料，查詢完成後可從市場下拉選擇單一市場篩選
    </div>
    <div class="query-hint success" v-else-if="availableMarkets.length > 0">
        <span class="mdi mdi-check-circle-outline hint-icon" />
        已載入 {{ availableMarkets.length }} 個市場的資料，可從上方下拉選擇單一市場篩選
    </div>
    </div>

        <div class="metric-tabs">
          <button
            v-for="m in metricOptions"
            :key="m.key"
            class="metric-tab"
            :class="{ active: activeMetric === m.key }"
            @click="activeMetric = m.key"
          >{{ m.label }}</button>
        </div>
      </div>

      <div class="action-row">
        <button class="btn-query" :disabled="isLoading" @click="handleQuery">
          {{ isLoading ? '查詢中...' : '查詢行情' }}
        </button>
        <button v-if="rawData.length > 0" class="btn-export" @click="handleExportCsv">
          匯出 CSV
        </button>
        <button v-if="rawData.length > 0 && selectedMarket" class="btn-reset" @click="selectedMarket = ''">
          顯示全部市場
        </button>
      </div>

      <p v-if="errorMsg" class="error-msg">{{ errorMsg }}</p>
    </section>

    <!-- 查詢後區塊 -->
    <div v-if="hasQueried">
      <!-- 摘要統計列 -->
      <div class="summary-bar" v-if="chartData.datasets.length > 0">
        <div class="stat-card">
          <span class="stat-label">市場數</span>
          <span class="stat-value">{{ availableMarkets.length }}</span>
        </div>
        <div class="stat-card">
          <span class="stat-label">資料筆數</span>
          <span class="stat-value">{{ filteredData.length }}</span>
        </div>
        <div class="stat-card">
          <span class="stat-label">最高均價</span>
          <span class="stat-value">{{ maxPrice }} 元</span>
        </div>
        <div class="stat-card">
          <span class="stat-label">最低均價</span>
          <span class="stat-value">{{ minPrice }} 元</span>
        </div>
      </div>

      <!-- 圖表 -->
      <div class="chart-card" v-if="chartData.datasets.length > 0">
        <div class="chart-toolbar">
          <span class="chart-title">
            {{ metricOptions.find(m => m.key === activeMetric)?.label }} 趨勢
          </span>
          <button class="btn-export-img" @click="exportChartImage">匯出圖片</button>
        </div>
        <div class="canvas-wrap">
          <canvas ref="canvasRef" />
        </div>
      </div>

      <div class="empty-hint" v-else-if="!isLoading">
        查無資料，請調整篩選條件後重試
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick, onUnmounted } from 'vue'
import {
  Chart,
  LineElement, PointElement, LineController,
  CategoryScale, LinearScale,
  Tooltip, Legend,
} from 'chart.js'
import DateRangePicker from '@/components/DateRangePicker.vue'
import { marketApi, type PorkResponseDto } from '@/api/market'

Chart.register(LineElement, PointElement, LineController, CategoryScale, LinearScale, Tooltip, Legend)

// ── 色盤 ─────────────────────────────────────────────────────────────────
const PALETTE = [
  '#2e7d32', '#e65100', '#1565c0', '#6a1b9a', '#c77700',
  '#00695c', '#b71c1c', '#0277bd', '#558b2f', '#f57f17',
]
const getColor = (i: number) => PALETTE[i % PALETTE.length]!

// ── 副指標選項 ────────────────────────────────────────────────────────────
type MetricKey = 'excludeFreezerAvgPrice' | 'excludeFreezerAvgWeight' | 'excludeFreezerCount'
const metricOptions: { key: MetricKey; label: string; unit: string }[] = [
  { key: 'excludeFreezerAvgPrice',  label: '不含冷凍廠均價',  unit: '元/公斤' },
  { key: 'excludeFreezerAvgWeight', label: '不含冷凍廠平均體重', unit: '公斤' },
  { key: 'excludeFreezerCount',     label: '不含冷凍廠成交頭數', unit: '頭' },
]
const activeMetric = ref<MetricKey>('excludeFreezerAvgPrice')

// ── 狀態 ──────────────────────────────────────────────────────────────────
const today        = new Date().toISOString().split('T')[0]!
const oneYearAgo   = new Date(Date.now() - 365 * 24 * 60 * 60 * 1000).toISOString().split('T')[0]!
const startDate    = ref(oneYearAgo)
const endDate      = ref(today)
const selectedMarket = ref('')          // '' = 全部市場
const rawData      = ref<PorkResponseDto[]>([])
const isLoading    = ref(false)
const hasQueried   = ref(false)
const errorMsg     = ref('')
const canvasRef    = ref<HTMLCanvasElement | null>(null)
let   chartInstance: Chart | null = null

// ── computed：從原始資料萃取市場清單 ─────────────────────────────────────
// 知識點：market 清單不是從 API 獨立撈的
// 而是從回傳資料裡用 computed 動態產生，原始資料更新時自動重算
const availableMarkets = computed(() => {
  const names = rawData.value.map(d => d.marketName)
  return [...new Set(names)].sort()
})

// ── computed：根據選擇的市場過濾資料 ─────────────────────────────────────
const filteredData = computed(() => {
  if (!selectedMarket.value) return rawData.value
  return rawData.value.filter(d => d.marketName === selectedMarket.value)
})

// ── computed：統計數字 ────────────────────────────────────────────────────
const maxPrice = computed(() => {
  const prices = filteredData.value.map(d => d.excludeFreezerAvgPrice)
  return prices.length ? Math.max(...prices) : 0
})
const minPrice = computed(() => {
  const prices = filteredData.value.map(d => d.excludeFreezerAvgPrice)
  return prices.length ? Math.min(...prices) : 0
})

// ── computed：Chart.js 所需格式 ───────────────────────────────────────────
// 知識點：把 PorkResponseDto[] 轉成 Chart.js 的 datasets 格式
// 步驟：
//   1. 收集所有不重複日期（升冪）→ X 軸 labels
//   2. 按 marketName 分組，每個市場建立 { 日期 → 數值 } 的 map
//   3. 每個市場對應一條線（一個 dataset），data[] 按 labels 日期順序對齊
const chartData = computed(() => {
  if (!filteredData.value.length) return { labels: [] as string[], datasets: [] as any[] }

  const metric = activeMetric.value

  // 步驟 1：收集日期，升冪排列（圖表左到右是時間軸）
  const labels = [...new Set(filteredData.value.map(d => d.transDate))].sort()

  // 步驟 2：按市場分組，建立 { 日期 → 數值 } 的 map
  const groups: Record<string, Record<string, number>> = {}
  for (const d of filteredData.value) {
    if (!groups[d.marketName]) groups[d.marketName] = {}
    groups[d.marketName]![d.transDate] = d[metric]
  }

  // 步驟 3：每個市場 → 一個 dataset
  const datasets = Object.entries(groups).map(([marketName, dateMap], i) => ({
    label: marketName,
    data: labels.map(date => dateMap[date] ?? null),  // 該日期沒資料 → null（Chart.js 會跳過）
    borderColor: getColor(i),
    backgroundColor: 'transparent',
    borderWidth: 2,
    pointRadius: labels.length <= 90 ? 3 : 0,
    pointHoverRadius: 7,
    pointBackgroundColor: getColor(i),
    pointBorderColor: 'rgba(0,0,0,0.15)',
    pointBorderWidth: 1,
    tension: 0.3,
    spanGaps: true,
  }))

  return { labels, datasets }
})

// ── Chart.js 建立 / 更新 ──────────────────────────────────────────────────
function buildChart() {
  if (!canvasRef.value || !chartData.value.labels.length) return
  chartInstance?.destroy()

  const unit = metricOptions.find(m => m.key === activeMetric.value)?.unit ?? ''

  chartInstance = new Chart(canvasRef.value, {
    type: 'line',
    data: chartData.value,
    options: {
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: 'index', intersect: false },
      scales: {
        x: {
          ticks: {
            maxTicksLimit: 12,
            color: 'rgba(26,40,32,0.75)',
            font: { size: 12 },
            callback(val, index) {
              return (this as any).getLabelForValue(index) ?? String(val)
            },
          },
          grid:   { color: 'rgba(0,0,0,0.05)' },
          border: { color: 'rgba(0,0,0,0.08)' },
        },
        y: {
          ticks: {
            color: 'rgba(26,40,32,0.75)',
            font: { size: 12 },
            callback: (val) => `${val} ${unit}`,
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
            label: (ctx) =>
              ctx.parsed.y !== null ? ` ${ctx.dataset.label}：${ctx.parsed.y} ${unit}` : '',
          },
        },
        legend: {
          position: 'top',
          labels: {
            color: 'rgba(26,40,32,0.85)',
            font: { size: 12 },
            usePointStyle: true,
            pointStyleWidth: 10,
          },
        },
      },
    },
  })
}

// chartData 或指標切換時重繪
watch(
  () => [chartData.value, activeMetric.value],
  () => nextTick(buildChart),
  { deep: true }
)

onUnmounted(() => chartInstance?.destroy())

// ── 查詢 ──────────────────────────────────────────────────────────────────
async function handleQuery() {
  isLoading.value = true
  hasQueried.value = true
  errorMsg.value = ''
  rawData.value = []
  selectedMarket.value = ''   // 重置市場選擇

  try {
    rawData.value = await marketApi.getPork({
      startDate: startDate.value,
      endDate: endDate.value,
    })
  } catch {
    errorMsg.value = '查詢失敗，請稍後再試'
  } finally {
    isLoading.value = false
  }
}

// ── 匯出 CSV ──────────────────────────────────────────────────────────────
function handleExportCsv() {
  if (!filteredData.value.length) return
  const header = ['日期', '市場', '不含冷凍廠均價', '不含冷凍廠平均體重', '不含冷凍廠成交頭數']
  const rows = filteredData.value.map(d => [
    d.transDate,
    d.marketName,
    d.excludeFreezerAvgPrice,
    d.excludeFreezerAvgWeight,
    d.excludeFreezerCount,
  ])
  const csv = [header, ...rows].map(r => r.join(',')).join('\n')
  const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = 'pork_market.csv'
  a.click()
  URL.revokeObjectURL(url)
}

// ── 匯出圖片 ──────────────────────────────────────────────────────────────
function exportChartImage() {
  if (!canvasRef.value) return

  // 建立一個同尺寸的暫存 canvas，先填白底再疊上圖表
  const canvas = canvasRef.value
  const exportCanvas = document.createElement('canvas')
  exportCanvas.width = canvas.width
  exportCanvas.height = canvas.height

  const ctx = exportCanvas.getContext('2d')!
  ctx.fillStyle = '#ffffff'
  ctx.fillRect(0, 0, exportCanvas.width, exportCanvas.height)
  ctx.drawImage(canvas, 0, 0)

  const url = exportCanvas.toDataURL('image/png')
  const a = document.createElement('a')
  a.href = url
  a.download = 'pork_chart.png'
  a.click()
}
</script>

<style scoped>
.pork-view { width: 100%; min-width: 960px; padding: 36px 56px; box-sizing: border-box; }

h1 { font-size: 22px; font-weight: 700; color: var(--text-primary); margin-bottom: 24px; }

/* 篩選區 */
.filter-section {
  display: flex; flex-direction: column; gap: 16px;
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 14px; padding: 28px; margin-bottom: 28px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
}
.filter-row { display: flex; align-items: flex-end; gap: 20px; flex-wrap: wrap; }
.action-row { display: flex; align-items: center; gap: 10px; }

.field-group { display: flex; flex-direction: column; gap: 6px; }
.field-label {
  font-size: 12px; color: var(--text-muted); font-weight: 600;
  letter-spacing: 0.05em; text-transform: uppercase;
}

.market-select {
  padding: 8px 14px; border: 1px solid var(--border); border-radius: 8px;
  background: var(--surface); color: var(--text-primary); font-size: 14px;
  min-width: 180px; cursor: pointer;
  transition: border-color 0.18s, box-shadow 0.18s;
}
.market-select:focus {
  outline: none; border-color: var(--green);
  box-shadow: 0 0 0 3px rgba(46,125,50,0.12);
}

/* 指標切換 */
.metric-tabs {
  display: flex; gap: 4px;
  background: var(--surface-2); border: 1px solid var(--border);
  border-radius: 8px; padding: 3px; align-self: flex-end;
}
.metric-tab {
  padding: 6px 14px; border-radius: 6px; border: none;
  background: transparent; color: rgba(26,40,32,0.60);
  font-size: 13px; font-weight: 600; cursor: pointer; transition: all 0.15s;
}
.metric-tab:hover { color: var(--text-primary); }
.metric-tab.active { background: #e8f5e9; color: var(--green); font-weight: 700; }

/* 按鈕 */
.btn-query {
  padding: 9px 26px; border-radius: 999px;
  border: 1px solid #1a5220;
  background: linear-gradient(180deg, #4caf50 0%, #2e7d32 40%, #1b5e20 100%);
  color: white; font-size: 14px; font-weight: 700; cursor: pointer;
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.35), inset 0 -2px 4px rgba(0,0,0,0.25), 0 2px 6px rgba(0,0,0,0.20);
  transition: all 0.15s;
}
.btn-query:hover:not(:disabled) {
  background: linear-gradient(180deg, #66bb6a 0%, #388e3c 40%, #2e7d32 100%);
}
.btn-query:active:not(:disabled) {
  background: linear-gradient(180deg, #1b5e20 0%, #2e7d32 60%, #388e3c 100%);
  box-shadow: inset 0 2px 6px rgba(0,0,0,0.35), 0 1px 3px rgba(0,0,0,0.15);
}
.btn-query:disabled { background: #c8d8c8; color: #999; border-color: #b0c8b0; box-shadow: none; cursor: not-allowed; }

.btn-export {
  padding: 9px 20px; border-radius: 999px;
  border: 1px solid #005f6b;
  background: linear-gradient(180deg, #00bcd4 0%, #0097a7 40%, #006978 100%);
  color: white; font-size: 13.5px; font-weight: 700; cursor: pointer;
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.35), inset 0 -2px 4px rgba(0,0,0,0.25), 0 2px 6px rgba(0,0,0,0.18);
  transition: all 0.15s;
}
.btn-export:hover {
  background: linear-gradient(180deg, #26c6da 0%, #00acc1 40%, #0097a7 100%);
}
.btn-export:active {
  background: linear-gradient(180deg, #006978 0%, #0097a7 60%, #00acc1 100%);
  box-shadow: inset 0 2px 6px rgba(0,0,0,0.35), 0 1px 3px rgba(0,0,0,0.15);
}

.btn-reset {
  padding: 9px 18px; border-radius: 999px;
  border: 1px solid #9e9e9e;
  background: linear-gradient(180deg, #f5f5f5 0%, #e0e0e0 40%, #bdbdbd 100%);
  color: #1a2820; font-size: 13.5px; font-weight: 700; cursor: pointer;
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.80), inset 0 -2px 4px rgba(0,0,0,0.15), 0 2px 6px rgba(0,0,0,0.18);
  transition: all 0.15s;
}
.btn-reset:hover {
  background: linear-gradient(180deg, #ffffff 0%, #eeeeee 40%, #e0e0e0 100%);
}
.btn-reset:active {
  background: linear-gradient(180deg, #bdbdbd 0%, #e0e0e0 60%, #eeeeee 100%);
  box-shadow: inset 0 2px 6px rgba(0,0,0,0.20), 0 1px 3px rgba(0,0,0,0.12);
}

/* 摘要列 */
.summary-bar {
  display: flex; gap: 14px; margin-bottom: 24px; flex-wrap: wrap;
}
.stat-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 12px; padding: 16px 24px;
  display: flex; flex-direction: column; gap: 6px;
  box-shadow: 0 1px 4px rgba(0,0,0,0.05);
}
.stat-label {
  font-size: 12px; color: rgba(26,40,32,0.60);
  letter-spacing: 0.05em; text-transform: uppercase; font-weight: 600;
}
.stat-value { font-size: 26px; font-weight: 700; color: #1a5c20; }

/* 圖表卡片 */
.chart-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 16px; padding: 28px 32px 36px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
}
.chart-toolbar {
  display: flex; align-items: center; justify-content: space-between;
  margin-bottom: 24px;
}
.chart-title { font-size: 15px; font-weight: 700; color: rgba(26,40,32,0.80); }

.btn-export-img {
  padding: 9px 20px; border-radius: 999px;
  border: 1px solid #4a148c;
  background: linear-gradient(180deg, #ab47bc 0%, #7b1fa2 40%, #4a148c 100%);
  color: white; font-size: 13.5px; font-weight: 700; cursor: pointer;
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.35), inset 0 -2px 4px rgba(0,0,0,0.25), 0 2px 6px rgba(0,0,0,0.18);
  transition: all 0.15s;
}
.btn-export-img:hover {
  background: linear-gradient(180deg, #ba68c8 0%, #8e24aa 40%, #6a1b9a 100%);
}
.btn-export-img:active {
  background: linear-gradient(180deg, #4a148c 0%, #7b1fa2 60%, #8e24aa 100%);
  box-shadow: inset 0 2px 6px rgba(0,0,0,0.35), 0 1px 3px rgba(0,0,0,0.15);
}

.canvas-wrap { position: relative; height: 500px; width: 100%; }

.error-msg { font-size: 13px; color: var(--red); }
.empty-hint {
  font-size: 14px; color: var(--text-muted);
  text-align: center; padding: 60px 0;
}

.query-hint {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 16px;
  border-radius: 8px;
  background: #e3f2fd;
  border: 1px solid rgba(21,101,192,0.20);
  color: #1565c0;
  font-size: 13px;
  font-weight: 600;
  line-height: 1.5;
}
.query-hint.success {
  background: #e8f5e9;
  border-color: rgba(46,125,50,0.20);
  color: var(--green);
}
.hint-icon {
  font-size: 18px;
  flex-shrink: 0;
}
</style>