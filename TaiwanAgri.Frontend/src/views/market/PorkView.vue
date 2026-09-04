<template>
  <div class="page pork-view">
    <QueryLayout
      title="毛豬行情查詢"
      title-en="HOG PRICES"
      subtitle="毛豬拍賣的成交均價、交易頭數與平均重量，查詢後可再依單一市場篩選"
    >
      <template #actions>
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
      </template>

      <template #filters>
        <DateRangePicker v-model:startDate="startDate" v-model:endDate="endDate" />

        <div class="field-group">
          <label class="field-label" for="pork-market">市場</label>
          <select
            id="pork-market"
            class="form-control market-select"
            v-model="selectedMarket"
            :disabled="!hasQueried"
          >
            <option value="">全部市場</option>
            <option v-for="name in availableMarkets" :key="name" :value="name">
              {{ name }}
            </option>
          </select>
        </div>

        <div class="field-group">
          <span class="field-label">副指標</span>
          <div class="segmented">
            <button
              v-for="m in metricOptions"
              :key="m.key"
              class="segmented__btn"
              :class="{ 'is-active': activeMetric === m.key }"
              @click="activeMetric = m.key"
            >{{ m.label }}</button>
          </div>
        </div>
      </template>

      <!-- 市場下拉要等查詢回來才有選項可選，這件事沒有說明就會被當成「壞掉了」。
           說明從 select 底下移到 hint 插槽：那裡是這一頁所有說明的固定位置，
           擠在欄位底下會把整排篩選列撐高、與旁邊的欄位對不齊。 -->
      <template #hint>
        <HintBox v-if="!hasQueried">
          請先按「查詢行情」載入資料，查詢完成後可從市場下拉選擇單一市場篩選
        </HintBox>
        <HintBox v-else-if="availableMarkets.length > 0" tone="success">
          已載入 {{ availableMarkets.length }} 個市場的資料，可從上方下拉選擇單一市場篩選
        </HintBox>
      </template>

      <template #results>
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
              <span class="stat-value">{{ maxPrice }}<span class="stat-unit">元</span></span>
            </div>
            <div class="stat-card">
              <span class="stat-label">最低均價</span>
              <span class="stat-value">{{ minPrice }}<span class="stat-unit">元</span></span>
            </div>
          </div>

          <!-- 圖表 -->
          <div class="chart-card card card--lg">
            <div class="chart-toolbar">
              <span class="section-title">
                {{ metricOptions.find(m => m.key === activeMetric)?.label }} 趨勢
              </span>
              <div class="toolbar-right">
                <Btn variant="secondary" size="sm" @click="toggleAllSeries">
                  {{ allVisible ? '全不選' : '全選' }}
                </Btn>
                <Btn variant="secondary" size="sm" icon="mdi-image-outline" @click="exportChartImage">
                  匯出圖片
                </Btn>
              </div>
            </div>
            <div class="canvas-wrap">
              <canvas ref="canvasRef" />
              <!-- 預設全部隱藏：一次查回十幾個縣市市場，全畫出來是一團線。
                   空白圖表補提示，讓使用者從圖例自己點要比較的市場 -->
              <div v-if="visibleCount === 0" class="chart-empty-hint">
                <span class="mdi mdi-gesture-tap chart-empty-hint__icon" />
                <p class="chart-empty-hint__main">點上方圖例選擇要顯示的市場</p>
                <span class="chart-empty-hint__sub">預設全部隱藏，避免多個市場的線疊在一起看不清</span>
              </div>
            </div>
          </div>
        </div>
      </template>
    </QueryLayout>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick, onUnmounted } from 'vue'
import {
  Chart,
  LineElement, PointElement, LineController,
  CategoryScale, LinearScale,
  Tooltip, Legend,
  type ChartDataset,
} from 'chart.js'
import DateRangePicker from '@/components/DateRangePicker.vue'
import { marketApi, type PorkResponseDto } from '@/api/market'
import QueryLayout from '@/components/layouts/QueryLayout.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'
import HintBox from '@/components/ui/HintBox.vue'
import {
  seriesColor, seriesDash, pointBorderColor, exportBackground,
  lineChartOptions, crosshairPlugin,
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
// 預設全部隱藏，起點是 false（按鈕顯示「全選」）
const allVisible   = ref(false)
const visibleCount = ref(0)
const visibleCountPlugin = {
  id: 'visibleCount',
  afterUpdate(chart: Chart) {
    visibleCount.value = chart.data.datasets.reduce(
      (n, _d, i) => n + (chart.isDatasetVisible(i) ? 1 : 0), 0,
    )
  },
}
let   chartInstance: Chart | null = null

function toggleAllSeries() {
  if (!chartInstance) return
  const meta = chartInstance.data.datasets.map((_, i) => chartInstance!.getDatasetMeta(i))
  const next = !allVisible.value
  meta.forEach(m => { m.hidden = !next })
  allVisible.value = next
  chartInstance.update()
}

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
    borderDash: seriesDash(i),   // 顏色以外的第二個線索，見 chartTheme.seriesDash
    backgroundColor: 'transparent',
    borderWidth: 2,
    pointRadius: labels.length <= 90 ? 3 : 0,
    pointHoverRadius: 7,
    pointBackgroundColor: seriesColor(i),
    pointBorderColor: pointBorderColor(),
    pointBorderWidth: 1,
    tension: 0.3,
    spanGaps: true,
    // 預設隱藏：讓使用者從圖例自己點要比較的市場
    hidden: true,
  }))

  return { labels, datasets }
})

// ── Chart.js 建立 / 更新 ──────────────────────────────────────────────────
function buildChart() {
  if (!canvasRef.value || !chartData.value.labels.length) return
  chartInstance?.destroy()
  // 新資料一律回到「全部隱藏」的起點，按鈕文字（全選）與圖表狀態才對得上
  allVisible.value = false

  const unit = metricOptions.find(m => m.key === activeMetric.value)?.unit ?? ''

  chartInstance = new Chart(canvasRef.value, {
    type: 'line',
    data: chartData.value,
    // 毛豬的三個指標（價格／頭數／重量）數字級距差很多，但每一個都是小幅波動，
    // 所以一律讓 y 軸貼著資料範圍，不要從 0 起跳
    options: lineChartOptions({ unit, fitY: 2 }),
    plugins: [crosshairPlugin, visibleCountPlugin],
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
/* 顏色全部改用 semantic 層；篩選欄位、摘要列、分段控制器與卡片
   外殼都已收進 base.css，這裡只留這一頁真正不同的部分。 */
.pork-view { min-width: 960px; }

.market-select { min-width: 180px; }

/* 單位跟著數字走，但不搶數字的份量：小一階、換成次要文字色 */
.stat-unit {
  margin-inline-start: var(--space-1);
  font-family: var(--font-body);
  font-size: var(--text-sm);
  font-weight: var(--weight-normal);
  color: var(--color-text-dim);
}

.chart-card { padding-bottom: var(--space-10); }
.chart-toolbar {
  display: flex; align-items: center; justify-content: space-between;
  gap: var(--space-4);
  margin-bottom: var(--space-6);
}
.toolbar-right { display: flex; align-items: center; gap: var(--space-3); }
.canvas-wrap { position: relative; height: 500px; width: 100%; }

/* 空狀態提示：預設全部隱藏時蓋在空白圖表上，不擋圖例互動 */
.chart-empty-hint {
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  text-align: center;
  pointer-events: none;
  color: var(--color-text-dim);
}
.chart-empty-hint__icon { font-size: var(--text-4xl); color: var(--color-border-strong); }
.chart-empty-hint__main { font-size: var(--text-base); font-weight: var(--weight-medium); color: var(--color-text); }
.chart-empty-hint__sub { font-size: var(--text-xs); }
</style>