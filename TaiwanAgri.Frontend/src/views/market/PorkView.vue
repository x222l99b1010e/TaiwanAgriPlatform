<template>
  <div class="page pork-view">
    <PageHeader
      title="毛豬行情查詢"
      subtitle="毛豬拍賣的成交均價、交易頭數與平均重量，查詢後可再依單一市場篩選"
    />

    <FilterCard layout="stack">
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
        <Btn icon="mdi-magnify" :loading="isLoading" @click="handleQuery">
          {{ isLoading ? '查詢中...' : '查詢行情' }}
        </Btn>
        <Btn v-if="rawData.length > 0" variant="secondary" icon="mdi-file-chart" @click="handleExportCsv">
          匯出 CSV
        </Btn>
        <Btn
          v-if="rawData.length > 0 && selectedMarket"
          variant="secondary"
          icon="mdi-filter-remove-outline"
          @click="selectedMarket = ''"
        >顯示全部市場</Btn>
      </div>
    </FilterCard>

    <StateBlock v-if="!hasQueried" state="hint" message="請設定日期區間後按下查詢行情" />
    <StateBlock v-else-if="isLoading" state="loading" message="資料載入中..." />
    <StateBlock
      v-else-if="errorMsg"
      state="error"
      :message="errorMsg"
      retryable
      @retry="handleQuery"
    />
    <StateBlock
      v-else-if="chartData.datasets.length === 0"
      state="empty"
      message="查無資料"
      hint="請調整日期區間或市場篩選後重試"
    />

    <div v-else>
      <!-- 摘要統計列 -->
      <div class="summary-bar">
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
      <div class="chart-card">
        <div class="chart-toolbar">
          <span class="chart-title">
            {{ metricOptions.find(m => m.key === activeMetric)?.label }} 趨勢
          </span>
          <Btn variant="secondary" size="sm" icon="mdi-image-outline" @click="exportChartImage">
            匯出圖片
          </Btn>
        </div>
        <div class="canvas-wrap">
          <canvas ref="canvasRef" />
        </div>
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
  type ChartDataset, type Scale,
} from 'chart.js'
import DateRangePicker from '@/components/DateRangePicker.vue'
import { marketApi, type PorkResponseDto } from '@/api/market'
import PageHeader from '@/components/ui/PageHeader.vue'
import FilterCard from '@/components/ui/FilterCard.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'
import {
  seriesColor, pointBorderColor, exportBackground,
  axisTicks, axisGrid, axisBorder, tooltipStyle, legendLabels,
} from '@/constants/chartTheme'

Chart.register(LineElement, PointElement, LineController, CategoryScale, LinearScale, Tooltip, Legend)

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
  if (!filteredData.value.length) return { labels: [] as string[], datasets: [] as ChartDataset<'line'>[] }

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
    borderColor: seriesColor(i),
    backgroundColor: 'transparent',
    borderWidth: 2,
    pointRadius: labels.length <= 90 ? 3 : 0,
    pointHoverRadius: 7,
    pointBackgroundColor: seriesColor(i),
    pointBorderColor: pointBorderColor(),
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
            ...axisTicks(),
            callback(this: Scale, val, index) {
              return this.getLabelForValue(index) ?? String(val)
            },
          },
          grid:   axisGrid(),
          border: axisBorder(),
        },
        y: {
          ticks: {
            ...axisTicks(),
            callback: (val) => `${val} ${unit}`,
          },
          grid:   axisGrid(),
          border: axisBorder(),
        },
      },
      plugins: {
        tooltip: {
          ...tooltipStyle(),
          callbacks: {
            label: (ctx) =>
              ctx.parsed.y !== null ? ` ${ctx.dataset.label}：${ctx.parsed.y} ${unit}` : '',
          },
        },
        legend: {
          position: 'top',
          labels: legendLabels(),
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
  ctx.fillStyle = exportBackground()
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
.pork-view { min-width: 960px; }
/* 篩選區 */
.filter-row { display: flex; align-items: flex-end; gap: var(--space-5); flex-wrap: wrap; }
.action-row { display: flex; align-items: center; gap: var(--space-3); }

.field-group { display: flex; flex-direction: column; gap: var(--space-2); }
.field-label {
  font-size: var(--text-xs); color: var(--text-muted); font-weight: var(--weight-medium);
  letter-spacing: 0.05em; text-transform: uppercase;
}

.market-select {
  padding: var(--space-2) var(--space-4); border: 1px solid var(--border); border-radius: var(--radius-md);
  background: var(--surface); color: var(--text-primary); font-size: var(--text-base);
  min-width: 180px; cursor: pointer;
  transition: border-color var(--duration-fast), box-shadow var(--duration-fast);
}
.market-select:focus {
  outline: none; border-color: var(--green);
  box-shadow: var(--shadow-focus);
}

/* 指標切換 */
.metric-tabs {
  display: flex; gap: var(--space-1);
  background: var(--surface-2); border: 1px solid var(--border);
  border-radius: var(--radius-md); padding: var(--space-1); align-self: flex-end;
}
.metric-tab {
  padding: var(--space-2) var(--space-4); border-radius: var(--radius-md); border: none;
  background: transparent; color: var(--neutral-500);
  font-size: var(--text-sm); font-weight: var(--weight-medium); cursor: pointer; transition: all var(--duration-fast);
}
.metric-tab:hover { color: var(--text-primary); }
.metric-tab.active { background: var(--green-100); color: var(--green); font-weight: var(--weight-bold); }

/* 按鈕 */
/* 摘要列 */
.summary-bar {
  display: flex; gap: var(--space-4); margin-bottom: var(--space-6); flex-wrap: wrap;
}
.stat-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: var(--radius-lg); padding: var(--space-4) var(--space-6);
  display: flex; flex-direction: column; gap: var(--space-2);
  box-shadow: var(--shadow-sm);
}
.stat-label {
  font-size: var(--text-xs); color: var(--neutral-500);
  letter-spacing: 0.05em; text-transform: uppercase; font-weight: var(--weight-medium);
}
.stat-value { font-size: var(--text-2xl); font-weight: var(--weight-bold); color: var(--green-800); }

/* 圖表卡片 */
.chart-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: var(--radius-xl); padding: var(--space-8) var(--space-8) var(--space-10);
  box-shadow: var(--shadow-md);
}
.chart-toolbar {
  display: flex; align-items: center; justify-content: space-between;
  margin-bottom: var(--space-6);
}
.chart-title { font-size: var(--text-base); font-weight: var(--weight-bold); color: var(--neutral-700); }
.canvas-wrap { position: relative; height: 500px; width: 100%; }
.query-hint {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-3) var(--space-4);
  border-radius: var(--radius-md);
  background: var(--info-50);
  border: 1px solid var(--info-100);
  color: var(--info-500);
  font-size: var(--text-sm);
  font-weight: var(--weight-medium);
  line-height: var(--leading-normal);
}
.query-hint.success {
  background: var(--green-100);
  border-color: var(--green-200);
  color: var(--green);
}
.hint-icon {
  font-size: var(--text-lg);
  flex-shrink: 0;
}
</style>