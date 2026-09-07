<!-- src/views/weather/RainfallView.vue -->
<template>
  <div class="page rainfall-view">
    <QueryLayout
      title="雨量趨勢"
      title-en="RAINFALL"
      subtitle="指定縣市與區間內，各測站的 24 小時累積雨量走勢"
    >
      <template #actions>
        <Btn icon="mdi-magnify" :loading="isLoading" @click="handleQuery">
          {{ isLoading ? '查詢中...' : '查詢' }}
        </Btn>
      </template>

      <template #filters>
        <CitySelector v-model="selectedCity" />
        <DateRangePicker
          v-model:startDate="startDate"
          v-model:endDate="endDate"
        />
      </template>

      <template #results>
        <StateBlock v-if="!hasQueried" state="hint" message="請選擇縣市與日期區間後按下查詢" />
        <StateBlock v-else-if="isLoading" state="loading" message="資料載入中..." />
        <StateBlock
          v-else-if="errorMsg"
          state="error"
          :message="errorMsg"
          retryable
          @retry="handleQuery"
        />
        <StateBlock
          v-else-if="records.length === 0"
          state="empty"
          message="查無資料"
          hint="這個縣市在所選區間內沒有雨量觀測紀錄，可以把區間拉長再試"
        />

        <div v-else>
          <!-- 摘要統計 -->
          <div class="summary-bar">
            <div class="stat-card">
              <span class="stat-label">測站數</span>
              <span class="stat-value">{{ stationCount }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-label">資料筆數</span>
              <span class="stat-value">{{ records.length }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-label">最高 24h 雨量</span>
              <span class="stat-value">{{ maxHour24 }}<span class="stat-unit">mm</span></span>
            </div>
          </div>

          <!-- 折線圖 -->
          <div class="chart-card card card--lg">
            <div class="chart-toolbar">
              <span class="section-title">
                {{ metricOptions.find(m => m.key === activeMetric)?.label }} 累積雨量趨勢
              </span>
              <div class="toolbar-right">
                <Btn variant="secondary" size="sm" @click="toggleAllSeries">
                  {{ allVisible ? '全不選' : '全選' }}
                </Btn>
                <div class="segmented segmented--sm">
                  <button
                    v-for="m in metricOptions"
                    :key="m.key"
                    class="segmented__btn"
                    :class="{ 'is-active': activeMetric === m.key }"
                    @click="activeMetric = m.key"
                  >{{ m.label }}</button>
                </div>
              </div>
            </div>
            <div class="canvas-wrap">
              <canvas ref="canvasRef" />
              <!-- 預設全部隱藏，畫面一開始是空的：補一個提示告訴使用者去點圖例，
                   否則空白圖表看起來像壞掉。有任一條線顯示後就消失。 -->
              <div v-if="visibleCount === 0" class="chart-empty-hint">
                <span class="mdi mdi-gesture-tap chart-empty-hint__icon" />
                <p class="chart-empty-hint__main">點上方圖例選擇要顯示的測站</p>
                <span class="chart-empty-hint__sub">預設全部隱藏，避免十幾條線疊在一起看不清</span>
              </div>
            </div>
          </div>

          <!-- 明細表格 -->
          <div class="table-wrap">
            <table class="data-table">
              <thead>
                <tr>
                  <th>測站</th>
                  <th>觀測時間</th>
                  <th class="num">3h (mm)</th>
                  <th class="num">6h (mm)</th>
                  <th class="num">12h (mm)</th>
                  <th class="num">24h (mm)</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(r, i) in pagedRecords" :key="i" :class="{ heavy: (r.hour24 ?? 0) >= 80 }">
                  <td class="station-cell">{{ r.stationName }}</td>
                  <td class="num time-cell">{{ r.observedAt.replace('T', ' ').slice(0, 16) }}</td>
                  <td class="num">{{ r.hour3 ?? '—' }}</td>
                  <td class="num">{{ r.hour6 ?? '—' }}</td>
                  <td class="num">{{ r.hour12 ?? '—' }}</td>
                  <td class="num rain-24" :class="rainLevel(r.hour24)">{{ r.hour24 ?? '—' }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- 圖例放在表格底下、用實際的顏色示範，不是用文字描述顏色：
               「橘色代表大雨」這句話本身讀不出橘色是哪一個橘 -->
          <div class="legend-row">
            <span class="legend-item"><i class="legend-swatch is-moderate" />30–79 mm 中雨</span>
            <span class="legend-item"><i class="legend-swatch is-heavy" />≥ 80 mm 大雨（整列標記）</span>
          </div>

          <PagerBar
            v-if="totalPages > 1"
            class="rainfall-pager"
            :current-page="currentPage"
            :total-pages="totalPages"
            :total-count="records.length"
            :visible-pages="visiblePages"
            :jump-page-input="jumpPageInput"
            :page-size="pageSize"
            :page-size-options="[50, 100, 200]"
            @change="changePage"
            @update:page-size="setPageSize"
            @update:jump-page-input="jumpPageInput = $event"
            @jump="handleJumpPage"
          />
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
  Tooltip, Legend, Filler,
  type ChartDataset,
} from 'chart.js'
import { weatherApi, type RainfallResponseDto } from '@/api/weather'
import CitySelector from '@/components/CitySelector.vue'
import DateRangePicker from '@/components/DateRangePicker.vue'
import QueryLayout from '@/components/layouts/QueryLayout.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'
import PagerBar from '@/components/PagerBar.vue'
import { usePagination } from '@/composables/usePagination'
import {
  seriesColor, seriesFill, seriesDash, pointBorderColor,
  lineChartOptions, crosshairPlugin,
} from '@/constants/chartTheme'

Chart.register(LineElement, PointElement, LineController, CategoryScale, LinearScale, Tooltip, Legend, Filler)


// ── 指標切換 ─────────────────────────────────────────
type MetricKey = 'hour3' | 'hour6' | 'hour12' | 'hour24'
const metricOptions: { key: MetricKey; label: string }[] = [
  { key: 'hour3',  label: '3h'  },
  { key: 'hour6',  label: '6h'  },
  { key: 'hour12', label: '12h' },
  { key: 'hour24', label: '24h' },
]
const activeMetric = ref<MetricKey>('hour24')

// ── 狀態 ─────────────────────────────────────────────
const selectedCity = ref('臺北市')
const startDate    = ref('')
const endDate      = ref('')
const records      = ref<RainfallResponseDto[]>([])
const isLoading    = ref(false)
const hasQueried   = ref(false)
const errorMsg     = ref('')
const canvasRef    = ref<HTMLCanvasElement | null>(null)
let   chartInstance: Chart | null = null

// ── 統計 ─────────────────────────────────────────────
const stationCount = computed(() =>
  new Set(records.value.map(r => r.stationName)).size
)
const maxHour24 = computed(() => {
  const vals = records.value.map(r => r.hour24 ?? 0)
  return vals.length ? Math.max(...vals) : 0
})

// ── 前端分頁 ──────────────────────────────────────────
// 單一縣市 × 一段區間 × 多測站，查回的列數常常很多，整頁列出來會非常長。資料已經全在
// records 記憶體裡，分頁只是把當前頁切出來顯示，換頁不必重打 API，所以 onChange 是空的。
const {
  pageSize, currentPage, jumpPageInput, visiblePages, totalPages,
  changePage, handleJumpPage, setPageSize,
} = usePagination({
  storageKey: 'rainfall.pageSize',
  pageSizeOptions: [50, 100, 200],
  defaultPageSize: 50,
  totalCount: () => records.value.length,
  onChange: () => {},
})
const pagedRecords = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  return records.value.slice(start, start + pageSize.value)
})

// ── 圖表資料整理 ──────────────────────────────────────
// X 軸：把 observedAt 截成 YYYY-MM-DD HH:mm，去重後排序
// 每條線：同一測站的雨量值按 X 軸順序對齊
const chartData = computed(() => {
  if (!records.value.length) return { labels: [] as string[], datasets: [] as ChartDataset<'line'>[] }

  const metric = activeMetric.value

  // 收集所有時間點
  const labels = Array.from(
    new Set(records.value.map(r => r.observedAt.replace('T', ' ').slice(0, 16)))
  ).sort()

  // 按測站分組
  const groups: Record<string, Record<string, number>> = {}
  for (const r of records.value) {
    const time = r.observedAt.replace('T', ' ').slice(0, 16)
    if (!groups[r.stationName]) groups[r.stationName] = {}
    groups[r.stationName]![time] = r[metric] ?? 0
  }

  const datasets = Object.entries(groups).map(([station, timeMap], i) => {
    return {
      label: station,
      data: labels.map(t => timeMap[t] ?? null),
      borderColor: seriesColor(i),
      borderDash: seriesDash(i),   // 顏色以外的第二個線索，見 chartTheme.seriesDash
      backgroundColor: seriesFill(i),
      borderWidth: 2,
      pointRadius: labels.length <= 60 ? 3.5 : 0,
      pointHoverRadius: 7,
      pointBackgroundColor: seriesColor(i),
      pointBorderColor: pointBorderColor(),
      pointBorderWidth: 1,
      tension: 0.35,
      fill: false,
      spanGaps: true,
      // 預設隱藏：一個縣市常常有十幾個測站，全畫出來是一團互相蓋住的線。
      // 讓使用者從圖例自己點開要比較的那幾站
      hidden: true,
    }
  })

  return { labels, datasets }
})

// ── Chart.js 建立 / 更新 ──────────────────────────────
function buildChart() {
  if (!canvasRef.value || !chartData.value.labels.length) return
  chartInstance?.destroy()
  // 新資料一律回到「全部隱藏」的起點，按鈕文字（全選）與圖表狀態才不會對不上
  allVisible.value = false

  chartInstance = new Chart(canvasRef.value, {
    type: 'line',
    data: chartData.value,
    // 雨量不開 fitY：0 mm 是有意義的基準（沒下雨），軸從 0 起跳才讀得出「這天幾乎沒雨」
    options: lineChartOptions({ unit: 'mm', maxTicksLimit: 10 }),
    // 第二個外掛回報「目前有幾條線顯示中」，給空狀態提示用（見 template 的 chart-empty-hint）
    plugins: [crosshairPlugin, visibleCountPlugin],
  })
}

// 預設全部隱藏，所以起點是 false（按鈕顯示「全選」）
const allVisible = ref(false)
const visibleCount = ref(0)
const visibleCountPlugin = {
  id: 'visibleCount',
  afterUpdate(chart: Chart) {
    visibleCount.value = chart.data.datasets.reduce(
      (n, _d, i) => n + (chart.isDatasetVisible(i) ? 1 : 0), 0,
    )
  },
}

function toggleAllSeries() {
  if (!chartInstance) return
  const meta = chartInstance.data.datasets.map((_, i) =>
    chartInstance!.getDatasetMeta(i)
  )
  if (allVisible.value) {
    // 全不選
    meta.forEach(m => { m.hidden = true })
    allVisible.value = false
  } else {
    // 全選
    meta.forEach(m => { m.hidden = false })
    allVisible.value = true
  }
  chartInstance.update()
}

onUnmounted(() => chartInstance?.destroy())

// 資料或指標切換時重繪
watch(
  () => [records.value, activeMetric.value],
  () => nextTick(buildChart),
  { deep: true }
)

// ── 查詢 ──────────────────────────────────────────────
function rainLevel(val: number | null) {
  if (val === null) return ''
  if (val >= 80) return 'level-heavy'
  if (val >= 30) return 'level-moderate'
  return ''
}

async function handleQuery() {
  if (!startDate.value || !endDate.value) {
    errorMsg.value = '請選擇開始與結束日期'
    return
  }
  isLoading.value = true
  hasQueried.value = true
  errorMsg.value = ''
  records.value = []
  try {
    records.value = await weatherApi.getRainfall(
      selectedCity.value,
      startDate.value,
      endDate.value
    )
    currentPage.value = 1   // 新查詢回到第一頁，否則會停在上次的頁碼看不到資料
  } catch {
    errorMsg.value = '查詢失敗，請稍後再試'
  } finally {
    isLoading.value = false
  }
}
</script>

<style scoped>
/* 顏色全部改用 semantic 層；摘要列、分段控制器、卡片外殼
   都已收進 base.css，這裡只留這一頁真正不同的部分。 */
.rainfall-view { min-width: 960px; }

.stat-card { min-width: 130px; }
.stat-unit {
  margin-inline-start: var(--space-1);
  font-family: var(--font-body); font-size: var(--text-sm);
  font-weight: var(--weight-normal); color: var(--color-text-dim);
}

.chart-card { padding-block: var(--space-6) var(--space-8); margin-bottom: var(--space-6); }
.chart-toolbar {
  display: flex; align-items: center; justify-content: space-between;
  gap: var(--space-4); flex-wrap: wrap;
  margin-bottom: var(--space-5);
}
.toolbar-right { display: flex; align-items: center; gap: var(--space-3); }

.canvas-wrap { position: relative; height: 420px; width: 100%; }

/* 空狀態提示：預設全部隱藏時蓋在空白圖表上。pointer-events:none 讓它不擋圖例互動 */
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

/* 資料收在一個有高度上限的 data grid 裡，內部自己捲、表頭吸頂（.data-table thead 已是
   sticky），配合下方分頁，整頁就不會被幾百列撐得很長。 */
.table-wrap {
  max-height: min(58vh, 620px);
  overflow: auto;
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}
.rainfall-pager { margin-top: var(--space-4); }
/* 表格外殼已收進 base.css 的 .data-table，這裡只留這一頁真正不同的部分 */
/* 大雨的列整列上色，比在某一格裡標記更容易掃到 */
.data-table tbody tr.heavy { background: var(--warning-50); }
.data-table tbody tr.heavy:hover { background: var(--warning-100); }

.station-cell { font-weight: var(--weight-bold); color: var(--color-text); }
.time-cell    { color: var(--color-text-dim); text-align: left; }
.rain-24        { font-weight: var(--weight-bold); }
.level-moderate { color: var(--warning-700); }
.level-heavy    { color: var(--danger-500); }

/* 圖例：用實際顏色的色塊示範，不用文字描述顏色 */
.legend-row {
  display: flex; flex-wrap: wrap; gap: var(--space-5);
  margin-top: var(--space-3);
}
.legend-item {
  display: inline-flex; align-items: center; gap: var(--space-2);
  font-size: var(--text-xs); color: var(--color-text-dim);
}
.legend-swatch {
  width: 10px; height: 10px; border-radius: var(--radius-sm); flex-shrink: 0;
}
.legend-swatch.is-moderate { background: var(--warning-700); }
.legend-swatch.is-heavy    { background: var(--danger-500); }
</style>