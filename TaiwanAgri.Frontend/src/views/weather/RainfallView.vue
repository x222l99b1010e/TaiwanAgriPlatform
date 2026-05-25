<!-- src/views/weather/RainfallView.vue -->
<template>
  <div class="rainfall-view">
    <h1>雨量趨勢</h1>

    <section class="filter-section">
      <CitySelector v-model="selectedCity" />
      <DateRangePicker
        v-model:startDate="startDate"
        v-model:endDate="endDate"
      />
      <button class="btn-query" :disabled="isLoading" @click="handleQuery">
        {{ isLoading ? '查詢中...' : '查詢' }}
      </button>
      <p v-if="errorMsg" class="error-msg">{{ errorMsg }}</p>
    </section>

    <div v-if="hasQueried && !isLoading">
      <p v-if="records.length === 0" class="empty-hint">查無資料</p>

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
                <button class="btn-toggle-all" @click="toggleAllSeries">
                {{ allVisible ? '全不選' : '全選' }}
                </button>
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
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick, onUnmounted } from 'vue'
import {
  Chart,
  LineElement, PointElement, LineController,
  CategoryScale, LinearScale,
  Tooltip, Legend, Filler,
} from 'chart.js'
import { weatherApi, type RainfallResponseDto } from '@/api/weather'
import CitySelector from '@/components/CitySelector.vue'
import DateRangePicker from '@/components/DateRangePicker.vue'

Chart.register(LineElement, PointElement, LineController, CategoryScale, LinearScale, Tooltip, Legend, Filler)

// ── 色盤（最多 10 條測站線）──────────────────────────
const PALETTE = [
  { main: '#7DD8CF', fade: 'rgba(125,216,207,0.10)' },
  { main: '#FFA05A', fade: 'rgba(255,160,90,0.10)'  },
  { main: '#64AADC', fade: 'rgba(100,170,220,0.10)' },
  { main: '#C896DC', fade: 'rgba(200,150,220,0.10)' },
  { main: '#F0C850', fade: 'rgba(240,200,80,0.10)'  },
  { main: '#6EBE8C', fade: 'rgba(110,190,140,0.10)' },
  { main: '#E87878', fade: 'rgba(232,120,120,0.10)' },
  { main: '#78C8E0', fade: 'rgba(120,200,224,0.10)' },
  { main: '#A8D87A', fade: 'rgba(168,216,122,0.10)' },
  { main: '#F2CF6A', fade: 'rgba(242,207,106,0.10)' },
]
const getColor = (i: number) => PALETTE[i % PALETTE.length]!

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
  if (!records.value.length) return { labels: [] as string[], datasets: [] as any[] }

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
    const color = getColor(i)
    return {
      label: station,
      data: labels.map(t => timeMap[t] ?? null),
      borderColor: color.main,
      backgroundColor: color.fade,
      borderWidth: 2,
      pointRadius: labels.length <= 60 ? 3.5 : 0,
      pointHoverRadius: 7,
      pointBackgroundColor: color.main,
      pointBorderColor: 'rgba(255,255,255,0.5)',
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
            color: 'rgba(170,185,205,0.55)',
            font: { size: 11 },
            callback(val, index) {
              return (this as any).getLabelForValue(index) ?? String(val)
            },
          },
          grid:   { color: 'rgba(255,255,255,0.05)' },
          border: { color: 'rgba(255,255,255,0.08)' },
        },
        y: {
          ticks: {
            color: 'rgba(170,185,205,0.55)',
            font: { size: 11 },
            callback: (val) => `${val} mm`,
          },
          grid:   { color: 'rgba(255,255,255,0.05)' },
          border: { color: 'rgba(255,255,255,0.08)' },
        },
      },
      plugins: {
        tooltip: {
          backgroundColor: 'rgba(18,28,20,0.92)',
          titleColor:      'rgba(200,215,200,0.9)',
          bodyColor:       'rgba(170,190,175,0.8)',
          borderColor:     'rgba(255,255,255,0.10)',
          borderWidth: 1,
          padding: 12,
          callbacks: {
            label: (ctx) =>
              ctx.parsed.y !== null ? ` ${ctx.dataset.label}：${ctx.parsed.y} mm` : '',
          },
        },
        legend: {
          position: 'top',
          labels: {
            color: 'rgba(190,205,195,0.75)',
            font: { size: 12 },
            usePointStyle: true,
            pointStyleWidth: 10,
          },
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
.rainfall-view {
  padding: 36px 56px;
  min-width: 960px;
  box-sizing: border-box;
}

h1 {
  font-size: 22px;
  font-weight: 700;
  color: rgba(200, 220, 200, 0.9);
  margin-bottom: 24px;
}

.filter-section {
  display: flex;
  align-items: flex-end;
  gap: 16px;
  flex-wrap: wrap;
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid rgba(255, 255, 255, 0.10);
  border-radius: 14px;
  padding: 24px;
  margin-bottom: 28px;
}

.btn-query {
  padding: 9px 26px;
  border-radius: 999px;
  border: none;
  background: #2e7d32;
  color: #ffffff;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.18s;
}
.btn-query:hover:not(:disabled) { background: #388e3c; }
.btn-query:disabled { background: rgba(80,120,80,0.4); cursor: not-allowed; }

.error-msg  { font-size: 13px; color: rgba(240,100,100,0.85); margin: 0; }
.empty-hint { font-size: 14px; color: rgba(170,185,205,0.5); text-align: center; padding: 40px 0; }

/* ── 摘要 ── */
.summary-bar {
  display: flex;
  gap: 14px;
  margin-bottom: 20px;
}

.stat-card {
  background: rgba(255,255,255,0.05);
  border: 1px solid rgba(255,255,255,0.10);
  border-radius: 12px;
  padding: 16px 22px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 130px;
}

.stat-label {
  font-size: 11px;
  color: rgba(170,185,205,0.5);
  letter-spacing: 0.05em;
  text-transform: uppercase;
}

.stat-value {
  font-size: 22px;
  font-weight: 700;
  color: rgba(125,216,160,0.9);
}

/* ── 圖表卡片 ── */
.chart-card {
  background: rgba(255,255,255,0.04);
  border: 1px solid rgba(255,255,255,0.09);
  border-radius: 16px;
  padding: 24px 28px 32px;
  margin-bottom: 24px;
}

.chart-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
}

.chart-title {
  font-size: 13px;
  font-weight: 600;
  color: rgba(170,185,205,0.6);
  letter-spacing: 0.04em;
}

/* 指標切換 Tab */
.metric-tabs {
  display: flex;
  gap: 4px;
  background: rgba(255,255,255,0.04);
  border: 1px solid rgba(255,255,255,0.09);
  border-radius: 8px;
  padding: 3px;
}

.metric-tab {
  padding: 5px 14px;
  border-radius: 6px;
  border: none;
  background: transparent;
  color: rgba(170,185,205,0.55);
  font-size: 13px;
  cursor: pointer;
  transition: background 0.15s, color 0.15s;
}

.metric-tab:hover { color: rgba(200,215,220,0.85); }

.metric-tab.active {
  background: rgba(125,216,160,0.15);
  color: rgba(125,216,160,0.9);
  font-weight: 600;
}

.canvas-wrap {
  position: relative;
  height: 420px;
  width: 100%;
}

/* ── 表格 ── */
.table-wrap {
  overflow-x: auto;
  border: 1px solid rgba(255,255,255,0.09);
  border-radius: 12px;
  margin-bottom: 8px;
}

.data-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13.5px;
}

.data-table thead tr {
  background: rgba(255,255,255,0.05);
}

.data-table th {
  padding: 12px 18px;
  text-align: left;
  font-size: 11.5px;
  font-weight: 600;
  color: rgba(170,185,205,0.55);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  border-bottom: 1px solid rgba(255,255,255,0.08);
}

.data-table th.num,
.data-table td.num { text-align: right; }

.data-table tbody tr {
  border-bottom: 1px solid rgba(255,255,255,0.05);
  transition: background 0.15s;
}
.data-table tbody tr:last-child { border-bottom: none; }
.data-table tbody tr:hover { background: rgba(255,255,255,0.04); }

.data-table tbody tr.heavy { background: rgba(255,140,60,0.06); }
.data-table tbody tr.heavy:hover { background: rgba(255,140,60,0.10); }

.data-table td {
  padding: 11px 18px;
  color: rgba(210,225,215,0.80);
}

.station-cell { font-weight: 600; color: rgba(125,210,155,0.88); }
.time-cell    { color: rgba(170,185,205,0.55); font-variant-numeric: tabular-nums; }

.rain-24        { font-weight: 600; }
.level-moderate { color: rgba(255,160,80,0.85); }
.level-heavy    { color: rgba(255,100,60,0.90); }

.hint {
  font-size: 12px;
  color: rgba(170,185,205,0.35);
  margin-top: 12px;
}

.toolbar-right {
  display: flex;
  align-items: center;
  gap: 10px;
}

.btn-toggle-all {
  padding: 5px 14px;
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.15);
  background: rgba(255, 255, 255, 0.05);
  color: rgba(170, 185, 205, 0.7);
  font-size: 13px;
  cursor: pointer;
  transition: background 0.15s, color 0.15s;
}

.btn-toggle-all:hover {
  background: rgba(255, 255, 255, 0.10);
  color: rgba(210, 225, 230, 0.9);
}
</style>