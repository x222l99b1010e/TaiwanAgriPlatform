<!-- src/views/weather/RainfallView.vue -->
<template>
  <div class="page rainfall-view">
    <PageHeader
      title="雨量趨勢"
      subtitle="指定縣市與區間內，各測站的 24 小時累積雨量走勢"
    />

    <FilterCard>
      <CitySelector v-model="selectedCity" />
      <DateRangePicker
        v-model:startDate="startDate"
        v-model:endDate="endDate"
      />
      <Btn icon="mdi-magnify" :loading="isLoading" @click="handleQuery">
        {{ isLoading ? '查詢中...' : '查詢' }}
      </Btn>
    </FilterCard>

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
          <span class="stat-value">{{ maxHour24 }} mm</span>
        </div>
      </div>

      <!-- 折線圖 -->
      <div class="chart-card">
          <div class="chart-toolbar">
          <span class="chart-title">24h 累積雨量趨勢</span>
          <div class="toolbar-right">
              <Btn variant="secondary" size="sm" @click="toggleAllSeries">
              {{ allVisible ? '全不選' : '全選' }}
              </Btn>
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
              <td class="time-cell">{{ r.observedAt.replace('T', ' ').slice(0, 16) }}</td>
              <td class="num">{{ r.hour3 ?? '—' }}</td>
              <td class="num">{{ r.hour6 ?? '—' }}</td>
              <td class="num">{{ r.hour12 ?? '—' }}</td>
              <td class="num rain-24" :class="rainLevel(r.hour24)">{{ r.hour24 ?? '—' }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <p class="hint">※ 24h 雨量 ≥ 80mm 標記為大雨（橘色）</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick, onUnmounted } from 'vue'
import {
  Chart,
  LineElement, PointElement, LineController,
  CategoryScale, LinearScale,
  Tooltip, Legend, Filler,
  type ChartDataset, type Scale,
} from 'chart.js'
import { weatherApi, type RainfallResponseDto } from '@/api/weather'
import CitySelector from '@/components/CitySelector.vue'
import DateRangePicker from '@/components/DateRangePicker.vue'
import PageHeader from '@/components/ui/PageHeader.vue'
import FilterCard from '@/components/ui/FilterCard.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'
import {
  seriesColor, seriesFill, pointBorderColor,
  axisTicks, axisGrid, axisBorder, tooltipStyle, legendLabels,
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
    options: {
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: 'index', intersect: false },
      scales: {
        x: {
          ticks: {
            maxTicksLimit: 10,
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
            callback: (val) => `${val} mm`,
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
              ctx.parsed.y !== null ? ` ${ctx.dataset.label}：${ctx.parsed.y} mm` : '',
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
.rainfall-view { min-width: 960px; }
.summary-bar { display: flex; gap: var(--space-4); margin-bottom: var(--space-5); }

.stat-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: var(--radius-lg); padding: var(--space-4) var(--space-6);
  display: flex; flex-direction: column; gap: var(--space-2); min-width: 130px;
  box-shadow: var(--shadow-sm);
}
/* 摘要卡片 */
.stat-label {
  font-size: var(--text-xs);
  color: var(--neutral-500);   /* 從 text-muted → 深一點 */
  letter-spacing: 0.05em;
  text-transform: uppercase;
  font-weight: var(--weight-medium);
}
.stat-value {
  font-size: var(--text-2xl);              /* 從 22px → 26px */
  font-weight: var(--weight-bold);
  color: var(--green-800);               /* 深綠，不透明 */
}

.chart-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: var(--radius-xl); padding: var(--space-6) var(--space-8) var(--space-8); margin-bottom: var(--space-6);
  box-shadow: var(--shadow-md);
}
.chart-toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: var(--space-5); }
/* 圖表標題 */
.chart-title {
  font-size: var(--text-base);              /* 從 13px → 14px */
  font-weight: var(--weight-bold);
  color: var(--neutral-600);   /* 從 text-muted → 深很多 */
  letter-spacing: 0.04em;
}

.toolbar-right { display: flex; align-items: center; gap: var(--space-3); }

.metric-tabs {
  display: flex; gap: var(--space-1);
  background: var(--surface-2); border: 1px solid var(--border);
  border-radius: var(--radius-md); padding: var(--space-1);
}
.metric-tab {
  padding: var(--space-1) var(--space-4); border-radius: var(--radius-md); border: none;
  background: transparent;
  color: var(--neutral-500);   /* 從 text-muted → 深一點 */
  font-size: var(--text-sm); font-weight: var(--weight-medium);
  cursor: pointer; transition: all var(--duration-fast);
}
.metric-tab:hover { color: var(--text-primary); }
.metric-tab.active { background: var(--green-100); color: var(--green); font-weight: var(--weight-bold); }

.canvas-wrap { position: relative; height: 420px; width: 100%; }

.table-wrap {
  overflow-x: auto; border: 1px solid var(--border);
  border-radius: var(--radius-lg); margin-bottom: var(--space-2);
  box-shadow: var(--shadow-sm);
}
/* 表格外殼已收進 base.css 的 .data-table，這裡只留這一頁真正不同的部分 */
/* 大雨的列整列上色，比在某一格裡標記更容易掃到 */
.data-table tbody tr.heavy { background: var(--warning-50); }
.data-table tbody tr.heavy:hover { background: var(--warning-100); }

.station-cell { font-weight: var(--weight-bold); color: var(--green-800); }  /* 深綠不透明 */
.time-cell    { color: var(--neutral-500); font-variant-numeric: tabular-nums; }
.rain-24        { font-weight: var(--weight-medium); }
.level-moderate { color: var(--warning-500); }
.level-heavy    { color: var(--red); }

.hint { font-size: var(--text-sm); color: var(--neutral-500); margin-top: var(--space-3); }
</style>