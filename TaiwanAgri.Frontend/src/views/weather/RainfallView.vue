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
                <tr v-for="(r, i) in records" :key="i" :class="{ heavy: (r.hour24 ?? 0) >= 80 }">
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
    }
  })

  return { labels, datasets }
})

// ── Chart.js 建立 / 更新 ──────────────────────────────
function buildChart() {
  if (!canvasRef.value || !chartData.value.labels.length) return
  chartInstance?.destroy()

  chartInstance = new Chart(canvasRef.value, {
    type: 'line',
    data: chartData.value,
    // 雨量不開 fitY：0 mm 是有意義的基準（沒下雨），軸從 0 起跳才讀得出「這天幾乎沒雨」
    options: lineChartOptions({ unit: 'mm', maxTicksLimit: 10 }),
    plugins: [crosshairPlugin],
  })
}

const allVisible = ref(true)

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
  } catch {
    errorMsg.value = '查詢失敗，請稍後再試'
  } finally {
    isLoading.value = false
  }
}
</script>

<style scoped>
/* 顏色全部改用 semantic 層（style tile §九）；摘要列、分段控制器、卡片外殼
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

.table-wrap {
  overflow-x: auto;
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}
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