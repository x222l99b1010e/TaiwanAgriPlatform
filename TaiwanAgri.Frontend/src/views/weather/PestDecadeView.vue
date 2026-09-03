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
            <Btn variant="secondary" size="sm" @click="toggleAllSeries">
              {{ allVisible ? '全不選' : '全選' }}
            </Btn>
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
  type ChartDataset,
} from 'chart.js'
import { weatherApi, type PestDecadeResponseDto } from '@/api/weather'
import PageHeader from '@/components/ui/PageHeader.vue'
import FilterCard from '@/components/ui/FilterCard.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'
import {
  seriesColor, seriesDash, pointBorderColor, lineChartOptions, crosshairPlugin,
} from '@/constants/chartTheme'

Chart.register(LineElement, PointElement, LineController, CategoryScale, LinearScale, Tooltip, Legend)

// ── 色盤 ─────────────────────────────────────────────


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
    borderColor: seriesColor(i),
    borderDash: seriesDash(i),   // 顏色以外的第二個線索，見 chartTheme.seriesDash
    backgroundColor: 'transparent',
    borderWidth: 2,
    pointRadius: 3.5,
    pointHoverRadius: 7,
    pointBackgroundColor: seriesColor(i),
    pointBorderColor: pointBorderColor(),
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
    // ⚠ 這裡的提示框原本寫「mm」，是從雨量頁抄過來時漏改的——這條線畫的是平均密度，
    // 不是雨量。收進共用設定後單位由 spec 指定，抄一份就跟著抄一次的機會不再有。
    // 密度是「越少越好」的量，從 0 起跳才讀得出絕對高低，所以不開 fitY。
    options: lineChartOptions({ maxTicksLimit: 10 }),
    plugins: [crosshairPlugin],
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
.field-group { display: flex; flex-direction: column; gap: var(--space-2); }
.field-label { font-size: var(--text-xs); color: var(--neutral-400); font-weight: var(--weight-medium); letter-spacing: 0.05em; text-transform: uppercase; }

.pest-select {
  padding: var(--space-2) var(--space-4); border: 1px solid var(--neutral-200);
  border-radius: var(--radius-md); background: var(--neutral-0);
  color: var(--neutral-900); font-size: var(--text-base); min-width: 200px; cursor: pointer;
  transition: border-color var(--duration-fast), box-shadow var(--duration-fast);
}
.pest-select:focus { outline: none; border-color: var(--green-600); box-shadow: var(--shadow-focus); }

/* 查詢按鈕金屬反光 */
.summary-bar { display: flex; gap: var(--space-4); margin-bottom: var(--space-5); flex-wrap: wrap; }

.stat-card {
  background: var(--neutral-0); border: 1px solid var(--neutral-200);
  border-radius: var(--radius-lg); padding: var(--space-4) var(--space-6);
  display: flex; flex-direction: column; gap: var(--space-2); min-width: 130px;
  box-shadow: var(--shadow-sm);
}
/* 摘要卡片 */
.stat-label {
  font-size: var(--text-xs);
  color: var(--neutral-500);
  letter-spacing: 0.05em;
  text-transform: uppercase;
  font-weight: var(--weight-medium);
}
.stat-value {
  font-size: var(--text-2xl);
  font-weight: var(--weight-bold);
  color: var(--green-800);
}
.stat-value.pest-name { font-size: var(--text-lg); }

.chart-card {
  background: var(--neutral-0); border: 1px solid var(--neutral-200);
  border-radius: var(--radius-xl); padding: var(--space-6) var(--space-8) var(--space-8); margin-bottom: var(--space-6);
  box-shadow: var(--shadow-md);
}
.chart-toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: var(--space-5); }
/* 圖表標題 */
.chart-title {
  font-size: var(--text-base);
  font-weight: var(--weight-bold);
  color: var(--neutral-600);
  letter-spacing: 0.04em;
}
.toolbar-right { display: flex; align-items: center; gap: var(--space-3); }

.canvas-wrap { position: relative; height: 420px; width: 100%; }

.table-wrap {
  overflow-x: auto; border: 1px solid var(--neutral-200);
  border-radius: var(--radius-lg); margin-bottom: var(--space-2);
  box-shadow: var(--shadow-sm);
}
/* 表格外殼已收進 base.css 的 .data-table，這裡只留這一頁真正不同的部分 */

.city-cell  { font-weight: var(--weight-bold); color: var(--green-800); }
.town-cell  { color: var(--neutral-500); }
.density-val { font-weight: var(--weight-bold); }
.level-mid  { color: var(--warning-500); }
.level-high { color: var(--danger-500); }
</style>