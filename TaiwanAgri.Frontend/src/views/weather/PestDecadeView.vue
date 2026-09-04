<!-- src/views/weather/PestDecadeView.vue -->
<template>
  <div class="page pest-view">
    <QueryLayout
      title="病蟲害旬報查詢"
      title-en="PEST DECADE REPORT"
      subtitle="依害蟲名稱查詢各縣市鄉鎮的旬別發生率統計"
    >
      <template #actions>
        <Btn
          icon="mdi-magnify"
          :loading="isLoading"
          :disabled="!selectedPest"
          @click="handleQuery"
        >{{ isLoading ? '查詢中...' : '查詢' }}</Btn>
      </template>

      <template #filters>
        <div class="field-group">
          <label class="field-label" for="pest-select">選擇害蟲</label>
          <select
            id="pest-select"
            v-model="selectedPest"
            class="form-control pest-select"
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
      </template>

      <template #results>
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
              <span class="stat-value stat-value--text">{{ selectedPest }}</span>
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
          <div class="chart-card card card--lg">
            <div class="chart-toolbar">
              <span class="section-title">旬密度趨勢（按城市）</span>
              <div class="toolbar-right">
                <Btn variant="secondary" size="sm" @click="toggleAllSeries">
                  {{ allVisible ? '全不選' : '全選' }}
                </Btn>
              </div>
            </div>
            <div class="canvas-wrap">
              <canvas ref="canvasRef" />
              <!-- 預設全部隱藏：空白圖表補提示，避免看起來像壞掉 -->
              <div v-if="visibleCount === 0" class="chart-empty-hint">
                <span class="mdi mdi-gesture-tap chart-empty-hint__icon" />
                <p class="chart-empty-hint__main">點上方圖例選擇要顯示的城市</p>
                <span class="chart-empty-hint__sub">預設全部隱藏，避免多條線疊在一起看不清</span>
              </div>
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
                  <th>旬</th>
                  <th class="num">平均密度</th>
                  <th class="num">全島比例</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(r, i) in pagedRecords" :key="i" :class="densityLevel(r.average)">
                  <td class="city-cell">{{ r.city }}</td>
                  <td class="town-cell">{{ r.town }}</td>
                  <td class="num">{{ r.year }}</td>
                  <td class="num">{{ r.month }}</td>
                  <td>{{ tenDaysLabel(r.tenDays) }}</td>
                  <td class="num density-val" :class="densityLevel(r.average)">
                    {{ r.average ?? '—' }}
                  </td>
                  <td class="num">{{ r.proportionIsland != null ? (r.proportionIsland * 100).toFixed(1) + '%' : '—' }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="legend-row">
            <span class="legend-item"><i class="legend-swatch is-mid" />密度 3–9</span>
            <span class="legend-item"><i class="legend-swatch is-high" />密度 ≥ 10</span>
          </div>

          <PagerBar
            v-if="totalPages > 1"
            class="decade-pager"
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
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from 'vue'
import {
  Chart,
  LineElement, PointElement, LineController,
  CategoryScale, LinearScale,
  Tooltip, Legend,
  type ChartDataset,
} from 'chart.js'
import { weatherApi, type PestDecadeResponseDto } from '@/api/weather'
import QueryLayout from '@/components/layouts/QueryLayout.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'
import PagerBar from '@/components/PagerBar.vue'
import { usePagination } from '@/composables/usePagination'
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
// 預設全部隱藏，起點是 false（按鈕顯示「全選」），由使用者自己點圖例選城市
const allVisible     = ref(false)
const visibleCount   = ref(0)
const visibleCountPlugin = {
  id: 'visibleCount',
  afterUpdate(chart: Chart) {
    visibleCount.value = chart.data.datasets.reduce(
      (n, _d, i) => n + (chart.isDatasetVisible(i) ? 1 : 0), 0,
    )
  },
}
let   chartInstance: Chart | null = null

// ── 統計 ─────────────────────────────────────────────
const cityCount = computed(() =>
  new Set(records.value.map(r => r.city)).size
)
const maxAverage = computed(() => {
  const vals = records.value.map(r => r.average ?? 0)
  return vals.length ? Math.max(...vals) : 0
})

// ── 前端分頁 ──────────────────────────────────────────
// 一種害蟲橫跨全台鄉鎮 × 多個旬別，列數常常上百，整頁列出來會很長。資料已全在 records
// 記憶體裡，換頁只重切片、不重打 API，跟雨量頁同一套做法（onChange 因此是空的）。
const {
  pageSize, currentPage, jumpPageInput, visiblePages, totalPages,
  changePage, handleJumpPage, setPageSize,
} = usePagination({
  storageKey: 'pestDecade.pageSize',
  pageSizeOptions: [50, 100, 200],
  defaultPageSize: 50,
  totalCount: () => records.value.length,
  onChange: () => {},
})
const pagedRecords = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  return records.value.slice(start, start + pageSize.value)
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
    // 預設隱藏：一種害蟲橫跨全台多個城市，全畫出來線條互相蓋住。
    // 讓使用者從圖例點開要比較的城市（owner 2026-09-04）
    hidden: true,
  }))

  return { labels, datasets }
})

// ── Chart.js ──────────────────────────────────────────
function buildChart() {
  if (!canvasRef.value || !chartData.value.labels.length) return
  chartInstance?.destroy()
  // 新資料一律回到「全部隱藏」的起點，按鈕文字（全選）與圖表狀態才對得上
  allVisible.value = false

  chartInstance = new Chart(canvasRef.value, {
    type: 'line',
    data: chartData.value,
    // ⚠ 這裡的提示框原本寫「mm」，是從雨量頁抄過來時漏改的——這條線畫的是平均密度，
    // 不是雨量。收進共用設定後單位由 spec 指定，抄一份就跟著抄一次的機會不再有。
    // 密度是「越少越好」的量，從 0 起跳才讀得出絕對高低，所以不開 fitY。
    options: lineChartOptions({ maxTicksLimit: 10 }),
    plugins: [crosshairPlugin, visibleCountPlugin],
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
    currentPage.value = 1   // 新查詢回到第一頁，否則會停在上次的頁碼看不到資料
  } catch {
    errorMsg.value = '查詢失敗，請稍後再試'
  } finally {
    isLoading.value = false
  }
}
</script>

<style scoped>
/* 顏色全部改用 semantic 層（style tile §九）；欄位、摘要列與卡片外殼已收進 base.css，
   這裡只留這一頁真正不同的部分。 */
.pest-view { min-width: 960px; }

.pest-select { min-width: 200px; }
.stat-card { min-width: 130px; }

.chart-card { padding-block: var(--space-6) var(--space-8); margin-bottom: var(--space-6); }
.chart-toolbar {
  display: flex; align-items: center; justify-content: space-between;
  gap: var(--space-4); margin-bottom: var(--space-5);
}
.toolbar-right { display: flex; align-items: center; gap: var(--space-3); }

.canvas-wrap { position: relative; height: 420px; width: 100%; }

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

/* 資料收在一個有高度上限的 data grid：內部自己捲、表頭吸頂，配合下方分頁，
   整頁不會被上百列撐得很長（owner 2026-09-03 要求，跟雨量頁一致）。 */
.table-wrap {
  max-height: min(58vh, 620px);
  overflow: auto;
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}
.decade-pager { margin-top: var(--space-4); }
/* 表格外殼已收進 base.css 的 .data-table，這裡只留這一頁真正不同的部分 */

.city-cell  { font-weight: var(--weight-bold); color: var(--color-text); }
.town-cell  { color: var(--color-text-dim); }
.density-val { font-weight: var(--weight-bold); }
.level-mid  { color: var(--warning-700); }
.level-high { color: var(--danger-500); }

/* 圖例用實際顏色的色塊示範，不用文字描述顏色 */
.legend-row { display: flex; flex-wrap: wrap; gap: var(--space-5); margin-top: var(--space-3); }
.legend-item {
  display: inline-flex; align-items: center; gap: var(--space-2);
  font-size: var(--text-xs); color: var(--color-text-dim);
}
.legend-swatch { width: 10px; height: 10px; border-radius: var(--radius-sm); flex-shrink: 0; }
.legend-swatch.is-mid  { background: var(--warning-700); }
.legend-swatch.is-high { background: var(--danger-500); }
</style>