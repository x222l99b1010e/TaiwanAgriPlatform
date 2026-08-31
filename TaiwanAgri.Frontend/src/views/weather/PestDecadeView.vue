<!-- src/views/weather/PestDecadeView.vue -->
<template>
  <div class="page pest-view">
    <PageHeader
      title="病蟲害旬報查詢"
      subtitle="依害蟲名稱查詢各縣市鄉鎮的旬別發生率統計"
    />

    <FilterCard>
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

      <Btn
        icon="mdi-magnify"
        :loading="isLoading"
        :disabled="!selectedPest"
        @click="handleQuery"
      >{{ isLoading ? '查詢中...' : '查詢' }}</Btn>
    </FilterCard>

    <StateBlock v-if="!hasQueried" state="hint" message="請選擇害蟲後按下查詢" />
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
      hint="這種害蟲沒有旬報統計紀錄，可換一種害蟲再查"
    />

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
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from 'vue'
import {
  Chart,
  LineElement, PointElement, LineController,
  CategoryScale, LinearScale,
  Tooltip, Legend,
  type ChartDataset, type Scale,
} from 'chart.js'
import { weatherApi, type PestDecadeResponseDto } from '@/api/weather'
import PageHeader from '@/components/ui/PageHeader.vue'
import FilterCard from '@/components/ui/FilterCard.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'

Chart.register(LineElement, PointElement, LineController, CategoryScale, LinearScale, Tooltip, Legend)

// ── 色盤 ─────────────────────────────────────────────
const PALETTE = [
  '#2e7d32', '#e65100', '#1565c0', '#6a1b9a', '#c77700',
  '#00695c', '#b71c1c', '#0277bd', '#558b2f', '#f57f17',
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
  if (!records.value.length) return { labels: [] as string[], datasets: [] as ChartDataset<'line'>[] }

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
    pointBorderColor: 'rgba(0,0,0,0.15)',
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
            maxTicksLimit: 10,
            color: 'rgba(26,40,32,0.70)',
            font: { size: 12 },
            callback(this: Scale, val, index) {
              return this.getLabelForValue(index) ?? String(val)
            },
          },
          grid:   { color: 'rgba(0,0,0,0.05)' },
          border: { color: 'rgba(0,0,0,0.12)' },
        },
        y: {
          ticks: {
            color: 'rgba(26,40,32,0.70)',
            font: { size: 12 },
          },
          grid:   { color: 'rgba(0,0,0,0.05)' },
          border: { color: 'rgba(0,0,0,0.12)' },
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
              ctx.parsed.y !== null ? ` ${ctx.dataset.label}：${ctx.parsed.y} mm` : '',
          },
        },
        legend: {
          labels: {
            color: 'rgba(26,40,32,0.85)',
            font: { size: 13 },
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
.pest-view { min-width: 960px; }
.field-group { display: flex; flex-direction: column; gap: 6px; }
.field-label { font-size: 12px; color: var(--text-muted); font-weight: 600; letter-spacing: 0.05em; text-transform: uppercase; }

.pest-select {
  padding: 8px 14px; border: 1px solid var(--border);
  border-radius: 8px; background: var(--surface);
  color: var(--text-primary); font-size: 14px; min-width: 200px; cursor: pointer;
  transition: border-color 0.18s, box-shadow 0.18s;
}
.pest-select:focus { outline: none; border-color: var(--green); box-shadow: 0 0 0 3px rgba(46,125,50,0.12); }

/* 查詢按鈕金屬反光 */
.summary-bar { display: flex; gap: 14px; margin-bottom: 20px; flex-wrap: wrap; }

.stat-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 12px; padding: 16px 22px;
  display: flex; flex-direction: column; gap: 6px; min-width: 130px;
  box-shadow: 0 1px 4px rgba(0,0,0,0.05);
}
/* 摘要卡片 */
.stat-label {
  font-size: 12px;
  color: rgba(26,40,32,0.60);
  letter-spacing: 0.05em;
  text-transform: uppercase;
  font-weight: 600;
}
.stat-value {
  font-size: 26px;
  font-weight: 700;
  color: #1a5c20;
}
.stat-value.pest-name { font-size: 18px; }

.chart-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 16px; padding: 24px 28px 32px; margin-bottom: 24px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
}
.chart-toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 20px; }
/* 圖表標題 */
.chart-title {
  font-size: 14px;
  font-weight: 700;
  color: rgba(26,40,32,0.75);
  letter-spacing: 0.04em;
}
.toolbar-right { display: flex; align-items: center; gap: 10px; }

/* 全不選按鈕 */
.btn-toggle-all {
  padding: 5px 14px; border-radius: 6px;
  border: 1px solid var(--border); background: var(--surface);
  color: rgba(26,40,32,0.65);
  font-size: 13px; font-weight: 600;
  cursor: pointer; transition: all 0.15s;
}
.btn-toggle-all:hover { background: var(--surface-2); color: var(--text-primary); }

.canvas-wrap { position: relative; height: 420px; width: 100%; }

.table-wrap {
  overflow-x: auto; border: 1px solid var(--border);
  border-radius: 12px; margin-bottom: 8px;
  box-shadow: 0 1px 4px rgba(0,0,0,0.04);
}
.data-table { width: 100%; border-collapse: collapse; font-size: 13.5px; }
.data-table thead tr { background: var(--surface-2); }
/* 表格標頭 */
.data-table th {
  padding: 12px 18px; text-align: left;
  font-size: 13px;
  font-weight: 700;
  color: rgba(26,40,32,0.70);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  border-bottom: 1px solid var(--border);
}
.data-table th.num, .data-table td.num { text-align: right; }
.data-table tbody tr { border-bottom: 1px solid var(--border); transition: background 0.15s; }
.data-table tbody tr:last-child { border-bottom: none; }
.data-table tbody tr:hover { background: var(--surface-2); }
/* 表格內文 */
.data-table td {
  padding: 11px 18px;
  color: rgba(26,40,32,0.85);
  font-size: 14px;
}

.city-cell  { font-weight: 700; color: #1a5c20; }
.town-cell  { color: rgba(26,40,32,0.60); }
.density-val { font-weight: 700; }
.level-mid  { color: #c77700; }
.level-high { color: var(--red); }
</style>