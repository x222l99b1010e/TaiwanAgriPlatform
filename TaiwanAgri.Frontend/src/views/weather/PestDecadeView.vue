<!-- src/views/weather/PestAlertsView.vue -->
<template>
  <div class="pest-view">
    <h1>病蟲害警報</h1>

    <section class="filter-section">
      <!-- 害蟲選擇 -->
      <div class="field-group">
        <label class="field-label">選擇害蟲</label>
        <select
          v-model="selectedPest"
          class="pest-select"
          :disabled="isLoadingNames"
        >
          <option v-if="isLoadingNames" value="">載入中...</option>
          <option
            v-for="name in pestNames"
            :key="name"
            :value="name"
          >{{ name }}</option>
        </select>
      </div>

      <button class="btn-query" :disabled="isLoading || !selectedPest" @click="handleQuery">
        {{ isLoading ? '查詢中...' : '查詢' }}
      </button>
      <p v-if="errorMsg" class="error-msg">{{ errorMsg }}</p>
    </section>

    <div v-if="hasQueried && !isLoading">
      <p v-if="records.length === 0" class="empty-hint">查無資料</p>

      <div v-else>
        <!-- 摘要 -->
        <div class="summary-bar">
          <div class="stat-card">
            <span class="stat-label">害蟲名稱</span>
            <span class="stat-value pest-name">{{ selectedPest }}</span>
          </div>
          <div class="stat-card">
            <span class="stat-label">城市數</span>
            <span class="stat-value">{{ cityCount }}</span>
          </div>
          <div class="stat-card">
            <span class="stat-label">資料筆數</span>
            <span class="stat-value">{{ records.length }}</span>
          </div>
          <div class="stat-card">
            <span class="stat-label">最高密度</span>
            <span class="stat-value">{{ maxAverage }}</span>
          </div>
        </div>

        <!-- 折線圖 -->
        <div class="chart-card">
          <div class="chart-toolbar">
            <span class="chart-title">旬密度趨勢（按城市）</span>
            <div class="toolbar-right">
              <button class="btn-toggle-all" @click="toggleAllSeries">
                {{ allVisible ? '全不選' : '全選' }}
              </button>
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
                <th>城市</th>
                <th>鄉鎮</th>
                <th class="num">年</th>
                <th class="num">月</th>
                <th class="num">旬</th>
                <th class="num">平均密度</th>
                <th class="num">全島比例</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(r, i) in records" :key="i" :class="densityLevel(r.average)">
                <td class="city-cell">{{ r.city }}</td>
                <td class="town-cell">{{ r.town }}</td>
                <td class="num">{{ r.year }}</td>
                <td class="num">{{ r.month }}</td>
                <td class="num">{{ tenDaysLabel(r.tenDays) }}</td>
                <td class="num density-val" :class="densityLevel(r.average)">
                  {{ r.average ?? '—' }}
                </td>
                <td class="num">{{ r.proportionIsland != null ? (r.proportionIsland * 100).toFixed(1) + '%' : '—' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from 'vue'
import {
  Chart,
  LineElement, PointElement, LineController,
  CategoryScale, LinearScale,
  Tooltip, Legend,
} from 'chart.js'
import { weatherApi, type PestDecadeResponseDto } from '@/api/weather'

Chart.register(LineElement, PointElement, LineController, CategoryScale, LinearScale, Tooltip, Legend)

// ── 色盤 ─────────────────────────────────────────────
const PALETTE = [
  '#7DD8CF', '#FFA05A', '#64AADC', '#C896DC', '#F0C850',
  '#6EBE8C', '#E87878', '#78C8E0', '#A8D87A', '#F2CF6A',
]
const getColor = (i: number) => PALETTE[i % PALETTE.length]!

// ── 狀態 ─────────────────────────────────────────────
const pestNames      = ref<string[]>([])
const selectedPest   = ref('')
const records        = ref<PestDecadeResponseDto[]>([])
const isLoadingNames = ref(false)
const isLoading      = ref(false)
const hasQueried     = ref(false)
const errorMsg       = ref('')
const canvasRef      = ref<HTMLCanvasElement | null>(null)
const allVisible     = ref(true)
let   chartInstance: Chart | null = null

// ── 統計 ─────────────────────────────────────────────
const cityCount = computed(() =>
  new Set(records.value.map(r => r.city)).size
)
const maxAverage = computed(() => {
  const vals = records.value.map(r => r.average ?? 0)
  return vals.length ? Math.max(...vals) : 0
})

// ── 旬標籤 ───────────────────────────────────────────
function tenDaysLabel(n: number) {
  return n === 1 ? '上旬' : n === 2 ? '中旬' : '下旬'
}

// ── 密度等級樣式 ──────────────────────────────────────
function densityLevel(val: number | null) {
  if (val === null) return ''
  if (val >= 10) return 'level-high'
  if (val >= 3)  return 'level-mid'
  return ''
}

// ── 圖表資料整理 ──────────────────────────────────────
// X 軸：年-月-旬 組合，排序後去重
// 每條線：同一城市的 average 值
const chartData = computed(() => {
  if (!records.value.length) return { labels: [] as string[], datasets: [] as any[] }

  // 組合 X 軸標籤
  const labelSet = new Set(
    records.value.map(r => `${r.year}-${String(r.month).padStart(2,'0')}-${tenDaysLabel(r.tenDays)}`)
  )
  const labels = Array.from(labelSet).sort()

  // 按城市分組
  const groups: Record<string, Record<string, number>> = {}
  for (const r of records.value) {
    const key = `${r.year}-${String(r.month).padStart(2,'0')}-${tenDaysLabel(r.tenDays)}`
    if (!groups[r.city]) groups[r.city] = {}
    // 同城市同旬取最大（可能有多個鄉鎮）
    const existing = groups[r.city]![key] ?? 0
    groups[r.city]![key] = Math.max(existing, r.average ?? 0)
  }

  const datasets = Object.entries(groups).map(([city, timeMap], i) => ({
    label: city,
    data: labels.map(l => timeMap[l] ?? null),
    borderColor: getColor(i),
    backgroundColor: 'transparent',
    borderWidth: 2,
    pointRadius: 3.5,
    pointHoverRadius: 7,
    pointBackgroundColor: getColor(i),
    pointBorderColor: 'rgba(255,255,255,0.5)',
    pointBorderWidth: 1,
    tension: 0.3,
    spanGaps: true,
  }))

  return { labels, datasets }
})

// ── Chart.js ──────────────────────────────────────────
function buildChart() {
  if (!canvasRef.value || !chartData.value.labels.length) return
  chartInstance?.destroy()
  allVisible.value = true

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
            color: 'rgba(170,185,205,0.55)',
            font: { size: 11 },
            maxRotation: 45,
          },
          grid:   { color: 'rgba(255,255,255,0.05)' },
          border: { color: 'rgba(255,255,255,0.08)' },
        },
        y: {
          ticks: {
            color: 'rgba(170,185,205,0.55)',
            font: { size: 11 },
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
              ctx.parsed.y !== null ? ` ${ctx.dataset.label}：${ctx.parsed.y}` : '',
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

function toggleAllSeries() {
  if (!chartInstance) return
  const meta = chartInstance.data.datasets.map((_, i) =>
    chartInstance!.getDatasetMeta(i)
  )
  if (allVisible.value) {
    meta.forEach(m => { m.hidden = true })
    allVisible.value = false
  } else {
    meta.forEach(m => { m.hidden = false })
    allVisible.value = true
  }
  chartInstance.update()
}

onUnmounted(() => chartInstance?.destroy())

watch(
  () => records.value,
  () => nextTick(buildChart),
  { deep: true }
)

// ── 初始化：載入害蟲清單 ──────────────────────────────
onMounted(async () => {
  isLoadingNames.value = true
  try {
    pestNames.value = await weatherApi.getPestNames()
    if (pestNames.value.length) selectedPest.value = pestNames.value[0]!
  } catch {
    errorMsg.value = '載入害蟲清單失敗'
  } finally {
    isLoadingNames.value = false
  }
})

// ── 查詢 ──────────────────────────────────────────────
async function handleQuery() {
  if (!selectedPest.value) return
  isLoading.value = true
  hasQueried.value = true
  errorMsg.value = ''
  records.value = []
  try {
    records.value = await weatherApi.getPestDecade(selectedPest.value)
  } catch {
    errorMsg.value = '查詢失敗，請稍後再試'
  } finally {
    isLoading.value = false
  }
}
</script>

<style scoped>
.pest-view {
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

/* ── 篩選區 ── */
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

.field-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.field-label {
  font-size: 12px;
  color: rgba(170, 185, 205, 0.5);
  font-weight: 600;
  letter-spacing: 0.05em;
  text-transform: uppercase;
}

.pest-select {
  padding: 8px 14px;
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.05);
  color: rgba(215, 225, 240, 0.88);
  font-size: 14px;
  min-width: 200px;
  cursor: pointer;
  transition: border-color 0.18s;
}

.pest-select option {
  background: #1a2e1f;
  color: rgba(215, 225, 240, 0.88);
}

.pest-select:focus {
  outline: none;
  border-color: rgba(125, 216, 160, 0.45);
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
.btn-query:disabled { background: rgba(80, 120, 80, 0.4); cursor: not-allowed; }

.error-msg  { font-size: 13px; color: rgba(240, 100, 100, 0.85); margin: 0; }
.empty-hint { font-size: 14px; color: rgba(170, 185, 205, 0.5); text-align: center; padding: 40px 0; }

/* ── 摘要 ── */
.summary-bar {
  display: flex;
  gap: 14px;
  margin-bottom: 20px;
  flex-wrap: wrap;
}

.stat-card {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.10);
  border-radius: 12px;
  padding: 16px 22px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 130px;
}

.stat-label {
  font-size: 11px;
  color: rgba(170, 185, 205, 0.5);
  letter-spacing: 0.05em;
  text-transform: uppercase;
}

.stat-value {
  font-size: 22px;
  font-weight: 700;
  color: rgba(125, 216, 160, 0.9);
}

.stat-value.pest-name {
  font-size: 16px;
}

/* ── 圖表卡片 ── */
.chart-card {
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid rgba(255, 255, 255, 0.09);
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
  color: rgba(170, 185, 205, 0.6);
  letter-spacing: 0.04em;
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

.canvas-wrap {
  position: relative;
  height: 420px;
  width: 100%;
}

/* ── 表格 ── */
.table-wrap {
  overflow-x: auto;
  border: 1px solid rgba(255, 255, 255, 0.09);
  border-radius: 12px;
  margin-bottom: 8px;
}

.data-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13.5px;
}

.data-table thead tr {
  background: rgba(255, 255, 255, 0.05);
}

.data-table th {
  padding: 12px 18px;
  text-align: left;
  font-size: 11.5px;
  font-weight: 600;
  color: rgba(170, 185, 205, 0.55);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.data-table th.num,
.data-table td.num { text-align: right; }

.data-table tbody tr {
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  transition: background 0.15s;
}
.data-table tbody tr:last-child { border-bottom: none; }
.data-table tbody tr:hover { background: rgba(255, 255, 255, 0.04); }

.data-table td {
  padding: 11px 18px;
  color: rgba(210, 225, 215, 0.80);
}

.city-cell { font-weight: 600; color: rgba(125, 210, 155, 0.88); }
.town-cell { color: rgba(170, 185, 205, 0.55); }

/* 密度等級 */
.density-val { font-weight: 600; }
.level-mid  { color: rgba(255, 190, 80, 0.85); }
.level-high { color: rgba(255, 90, 60, 0.90); }
</style>