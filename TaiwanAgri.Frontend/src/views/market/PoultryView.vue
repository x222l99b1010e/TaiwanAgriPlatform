<template>
  <div class="page poultry-view">
    <PageHeader
      title="家禽行情查詢"
      subtitle="雞、鴨、鵝與雞蛋的產地價與批發價指標，可多選指標比較同一區間走勢"
    />

    <!-- 篩選區 -->
    <FilterCard layout="stack">
      <div class="filter-row">
        <DateRangePicker v-model:startDate="startDate" v-model:endDate="endDate" />
      </div>

      <!-- 指標勾選區：依來源分組，每個指標旁的百分比是「查詢區間內正常報價天數佔比」 -->
      <div class="metric-groups" v-if="metricsList.length">
        <div class="metric-groups-toolbar">
          <span class="group-label">指標（{{ selectedMetrics.length }}／{{ metricsList.length }}）</span>
          <div class="metric-bulk-actions">
            <Btn variant="secondary" size="sm" icon="mdi-checkbox-multiple-marked-outline" @click="selectAllMetrics">
              全選
            </Btn>
            <Btn variant="secondary" size="sm" icon="mdi-close-circle-outline" @click="clearAllMetrics">
              清空
            </Btn>
          </div>
        </div>
        <p v-if="hasQueried && completenessByMetric.size > 0" class="badge-legend">
          <span class="mdi mdi-information-outline hint-icon" />
          指標名稱右側的百分比 ＝ 該指標在目前區間內「正常報價」天數佔比，數字低是該指標的常態
          （如雞蛋產地價本來就少報價），並非同步異常。
        </p>
        <div v-for="group in metricGroups" :key="group.name" class="metric-group">
          <span class="group-label">{{ group.name }}</span>
          <div class="metric-chips">
            <label
              v-for="m in group.items"
              :key="m.metricCode"
              class="metric-chip"
              :class="{ active: selectedMetrics.includes(m.metricCode) }"
            >
              <input type="checkbox" :value="m.metricCode" v-model="selectedMetrics" />
              {{ m.displayName }}
              <span
                v-if="hasQueried && completenessByMetric.has(m.metricCode)"
                class="completeness-badge"
                :class="completenessClass(completenessByMetric.get(m.metricCode)!.pct)"
              >
                {{ completenessByMetric.get(m.metricCode)!.pct }}%
              </span>
            </label>
          </div>
        </div>
      </div>
      <p v-else class="query-hint">指標清單載入中...</p>

      <div class="action-row">
        <Btn icon="mdi-magnify" :loading="isLoading" @click="handleQuery">
          {{ isLoading ? '查詢中...' : '查詢行情' }}
        </Btn>
        <Btn v-if="filteredData.length > 0" variant="secondary" icon="mdi-file-chart" @click="handleExportCsv">
          匯出 CSV
        </Btn>
      </div>

      <p v-if="hasQueried && selectedMetrics.length === 0" class="query-hint">
        <span class="mdi mdi-information-outline hint-icon" />
        請至少勾選一項指標才會顯示圖表
      </p>
    </FilterCard>

    <StateBlock v-if="!hasQueried" state="hint" message="請設定日期區間與指標後按下查詢行情" />
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
      hint="請調整日期區間，或至少勾選一項指標後重試"
    />

    <!-- 查詢後區塊 -->
    <div v-else>
      <!-- 摘要統計列 -->
      <div class="summary-bar">
        <div class="stat-card">
          <span class="stat-label">已選指標</span>
          <span class="stat-value">{{ selectedMetrics.length }}</span>
        </div>
        <div class="stat-card">
          <span class="stat-label">資料筆數</span>
          <span class="stat-value">{{ filteredData.length }}</span>
        </div>
        <div class="stat-card">
          <span class="stat-label">正常報價佔比</span>
          <span class="stat-value">{{ overallCompletenessPct }}%</span>
        </div>
        <div class="stat-card">
          <span class="stat-label">非常態資料點</span>
          <span class="stat-value">{{ abnormalPoints.length }}</span>
        </div>
      </div>

      <!-- 圖表 -->
      <div class="chart-card">
        <div class="chart-toolbar">
          <span class="chart-title">家禽行情趨勢</span>
          <Btn variant="secondary" size="sm" icon="mdi-image-outline" @click="exportChartImage">
            匯出圖片
          </Btn>
        </div>
        <div class="canvas-wrap">
          <canvas ref="canvasRef" />
        </div>
        <p class="chart-note">
          <span class="mdi mdi-information-outline hint-icon" />
          線段中斷代表當日休市／未報價／議價（無公定價格），並非資料同步異常。
          不同指標的計價單位可能不同，圖表僅供趨勢比較，非同單位換算。
        </p>
      </div>

      <!-- 非常態資料明細 -->
      <div class="abnormal-card" v-if="abnormalPoints.length > 0">
        <!-- 刻意不轉 Btn：這是整列可點的展開/收合標頭（width:100%、文字左對齊、
             chevron 隨狀態換向），跟 Btn 的定寬藥丸鈕在結構上不同類，跟分頁/tab
             一樣屬於「有自己選取態與排列邏輯」的排除範圍（見 Btn.vue 檔頭註解）。 -->
        <button class="btn-toggle-abnormal" @click="showAbnormalTable = !showAbnormalTable">
          <span class="mdi" :class="showAbnormalTable ? 'mdi-chevron-up' : 'mdi-chevron-down'" />
          {{ showAbnormalTable ? '收合' : '展開' }}非常態資料明細（{{ abnormalPoints.length }} 筆）
        </button>
        <div class="abnormal-table-wrap" v-if="showAbnormalTable">
          <table class="abnormal-table">
            <thead>
              <tr>
                <th>日期</th>
                <th>指標</th>
                <th>狀態</th>
                <th>原始文字</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(p, i) in abnormalPoints" :key="i">
                <td>{{ p.transDate }}</td>
                <td>{{ p.displayName }}</td>
                <td>
                  <span class="status-chip" :class="statusClass(p.priceStatus)">
                    {{ statusLabel(p.priceStatus) }}
                  </span>
                </td>
                <td>{{ p.rawValue ?? '—' }}</td>
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
  type ChartDataset, type Scale,
} from 'chart.js'
import DateRangePicker from '@/components/DateRangePicker.vue'
import { marketApi, type PoultryResponseDto, type PoultryMetricDto } from '@/api/market'
import PageHeader from '@/components/ui/PageHeader.vue'
import FilterCard from '@/components/ui/FilterCard.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'
import {
  seriesColor, pointBorderColor, exportBackground,
  axisTicks, axisGrid, axisBorder, tooltipStyle, legendLabels,
} from '@/constants/chartTheme'

Chart.register(LineElement, PointElement, LineController, CategoryScale, LinearScale, Tooltip, Legend)

// ── PriceStatus 中文對照（前端自訂的顯示文字，非後端的 MetricCode 對照表——
//    後端只提供 MetricCode → 中文名，PriceStatus 的七態文字純粹是這個畫面的呈現需求） ──
const STATUS_LABELS: Record<string, string> = {
  Normal: '正常',
  Empty: '空值',
  Closed: '休市',
  NotQuoted: '未報價',
  Negotiated: '議價',
  RangeQuote: '區間報價',
  Unrecognized: '無法辨識',
}
const statusLabel = (status: string) => STATUS_LABELS[status] ?? status
const statusClass = (status: string) => `status-${status.toLowerCase()}`

// ── 指標分組（依 MetricCode 前綴歸類，純前端呈現用，不是重造中文對照表） ──
function categoryOf(code: string): string {
  if (code.startsWith('BoiledChicken') || code.startsWith('Egg')) return '白肉雞／雞蛋'
  if (code.startsWith('RedFeather')) return '紅羽土雞'
  if (code.startsWith('BlackFeather')) return '黑羽土雞'
  if (code.startsWith('Goose') || code.startsWith('Duck')) return '肉鵝／番鴨／鴨蛋'
  return '其他'
}
const GROUP_ORDER = ['白肉雞／雞蛋', '紅羽土雞', '黑羽土雞', '肉鵝／番鴨／鴨蛋', '其他']

// ── 指標清單（開頁就載入，跟查詢時機脫鉤——使用者要先看得到才能勾選） ──
const metricsList = ref<PoultryMetricDto[]>([])
const metricGroups = computed(() => {
  const map = new Map<string, PoultryMetricDto[]>()
  for (const m of metricsList.value) {
    const cat = categoryOf(m.metricCode)
    if (!map.has(cat)) map.set(cat, [])
    map.get(cat)!.push(m)
  }
  return GROUP_ORDER
    .filter(name => map.has(name))
    .map(name => ({ name, items: map.get(name)! }))
})

// 預設只勾白肉雞／雞蛋常用線（4 條），其餘 13 條可自行加選——全開會太亂
const DEFAULT_METRICS = ['BoiledChicken_2_0KgUp', 'BoiledChicken_1_75To1_95Kg', 'Egg_Producer', 'Egg_Transport']
const selectedMetrics = ref<string[]>([...DEFAULT_METRICS])

// 全選／清空：直接整批取代 selectedMetrics 的內容，不用逐一 push/splice
// 知識點：Vue 的 ref 陣列只要整包指派新陣列，響應式系統就會偵測到變化並觸發重新渲染，
// 不需要像原生 DOM 操作那樣手動同步每個 checkbox 的勾選狀態——
// checkbox 的 v-model="selectedMetrics" 是雙向綁定，這裡改 selectedMetrics.value，
// 畫面上 17 個 checkbox 的勾選狀態會自動跟著更新
function selectAllMetrics() {
  selectedMetrics.value = metricsList.value.map(m => m.metricCode)
}
function clearAllMetrics() {
  selectedMetrics.value = []
}

// ── 狀態 ──────────────────────────────────────────────────────────────────
const today        = new Date().toISOString().split('T')[0]!
const oneYearAgo    = new Date(Date.now() - 365 * 24 * 60 * 60 * 1000).toISOString().split('T')[0]!
const startDate     = ref(oneYearAgo)
const endDate       = ref(today)
const rawData       = ref<PoultryResponseDto[]>([])   // 查詢區間內「全部指標」的資料，不先篩
const isLoading     = ref(false)
const hasQueried    = ref(false)
const errorMsg      = ref('')
const showAbnormalTable = ref(false)
const canvasRef     = ref<HTMLCanvasElement | null>(null)
let   chartInstance: Chart | null = null

onMounted(async () => {
  try {
    metricsList.value = await marketApi.getPoultryMetrics()
  } catch {
    errorMsg.value = '指標清單載入失敗，請重新整理頁面'
  }
})

// ── computed：依目前勾選的指標，從已撈回的全量資料中篩選 ──────────────────
// 知識點：查詢時不帶 metricCodes、一次撈區間內全部 17 個指標（比照 PorkView
// 撈全部市場、用下拉框在前端篩的做法）——資料量以年為單位不大，換來的是
// 勾/取消勾選指標不必重新打 API，且完整度徽章能對「還沒勾選」的指標也顯示
const filteredData = computed(() =>
  rawData.value.filter(d => selectedMetrics.value.includes(d.metricCode))
)

// ── computed：每個指標的「正常報價」完整度（永遠算全量 rawData，跟目前勾選無關） ──
const completenessByMetric = computed(() => {
  const map = new Map<string, { total: number; normal: number; pct: number }>()
  for (const d of rawData.value) {
    const entry = map.get(d.metricCode) ?? { total: 0, normal: 0, pct: 0 }
    entry.total += 1
    if (d.priceStatus === 'Normal') entry.normal += 1
    map.set(d.metricCode, entry)
  }
  for (const entry of map.values()) {
    entry.pct = entry.total > 0 ? Math.round((entry.normal / entry.total) * 100) : 0
  }
  return map
})
function completenessClass(pct: number) {
  if (pct >= 90) return 'high'
  if (pct >= 50) return 'mid'
  return 'low'
}

// ── computed：整體正常報價佔比（目前勾選範圍內） ───────────────────────────
const overallCompletenessPct = computed(() => {
  if (!filteredData.value.length) return 0
  const normal = filteredData.value.filter(d => d.priceStatus === 'Normal').length
  return Math.round((normal / filteredData.value.length) * 100)
})

// ── computed：非常態資料點明細（目前勾選範圍內，日期新到舊） ────────────────
const abnormalPoints = computed(() =>
  filteredData.value
    .filter(d => d.priceStatus !== 'Normal')
    .slice()
    .sort((a, b) => b.transDate.localeCompare(a.transDate))
)

// ── computed：Chart.js 所需格式 ───────────────────────────────────────────
// 跟 PorkView 同一套轉換邏輯，差別是這裡分組鍵是 metricCode 而非 marketName，
// 且刻意不設 spanGaps——非 Normal 的資料點 Price 是 null，讓線自然斷開，
// 誠實呈現「這天沒有正常報價」，不用線性補間去掩蓋缺口
const chartData = computed(() => {
  if (!filteredData.value.length) return { labels: [] as string[], datasets: [] as ChartDataset<'line'>[] }

  const labels = [...new Set(filteredData.value.map(d => d.transDate))].sort()

  const groups: Record<string, Record<string, number | null>> = {}
  for (const d of filteredData.value) {
    if (!groups[d.metricCode]) groups[d.metricCode] = {}
    groups[d.metricCode]![d.transDate] = d.price
  }

  // 依 metricsList 的原始順序排列 dataset，讓色彩不會因為勾選順序而跳動
  const orderedCodes = metricsList.value
    .map(m => m.metricCode)
    .filter(code => groups[code])

  const datasets = orderedCodes.map((code, i) => {
    const displayName = metricsList.value.find(m => m.metricCode === code)?.displayName ?? code
    const dateMap = groups[code]!
    return {
      label: displayName,
      data: labels.map(date => dateMap[date] ?? null),
      borderColor: seriesColor(i),
      backgroundColor: 'transparent',
      borderWidth: 2,
      pointRadius: labels.length <= 90 ? 3 : 0,
      pointHoverRadius: 7,
      pointBackgroundColor: seriesColor(i),
      pointBorderColor: pointBorderColor(),
      pointBorderWidth: 1,
      tension: 0.3,
      spanGaps: false,   // 刻意不補間，缺資料就斷線
    }
  })

  return { labels, datasets }
})

// ── Chart.js 建立 / 更新 ──────────────────────────────────────────────────
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
            callback: (val) => `${val} 元`,
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
              ctx.parsed.y !== null ? ` ${ctx.dataset.label}：${ctx.parsed.y} 元` : '',
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

watch(
  () => chartData.value,
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
  showAbnormalTable.value = false

  try {
    // 不帶 metricCodes：一次撈區間內全部指標，勾選純粹是前端篩選（見上方 computed 說明）
    rawData.value = await marketApi.getPoultry({
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
  const header = ['日期', '指標代碼', '指標名稱', '價格', '狀態', '原始文字']
  const rows = filteredData.value
    .slice()
    .sort((a, b) => a.transDate.localeCompare(b.transDate) || a.metricCode.localeCompare(b.metricCode))
    .map(d => [
      d.transDate,
      d.metricCode,
      d.displayName,
      d.price ?? '',
      statusLabel(d.priceStatus),
      d.rawValue ?? '',
    ])
  const csv = [header, ...rows].map(r => r.join(',')).join('\n')
  const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = 'poultry_market.csv'
  a.click()
  URL.revokeObjectURL(url)
}

// ── 匯出圖片 ──────────────────────────────────────────────────────────────
function exportChartImage() {
  if (!canvasRef.value) return

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
  a.download = 'poultry_chart.png'
  a.click()
}
</script>

<style scoped>
.poultry-view { min-width: 960px; }
/* 篩選區 */
.filter-row { display: flex; align-items: flex-end; gap: 20px; flex-wrap: wrap; }
.action-row { display: flex; align-items: center; gap: 10px; }

/* 指標勾選區 */
.metric-groups { display: flex; flex-direction: column; gap: 12px; }
.metric-groups-toolbar {
  display: flex; align-items: center; justify-content: space-between;
  padding-bottom: 10px; border-bottom: 1px solid var(--border);
}
.metric-bulk-actions { display: flex; gap: 10px; }

.metric-group { display: flex; align-items: flex-start; gap: 14px; flex-wrap: wrap; }
.group-label {
  font-size: 12px; color: var(--text-muted); font-weight: 600;
  letter-spacing: 0.05em; text-transform: uppercase;
  min-width: 108px; padding-top: 7px; flex-shrink: 0;
}
.metric-chips { display: flex; gap: 8px; flex-wrap: wrap; }
.metric-chip {
  display: inline-flex; align-items: center; gap: 6px;
  padding: 6px 12px; border-radius: 999px;
  background: var(--surface-2); border: 1px solid var(--border);
  color: var(--neutral-600); font-size: 13px; font-weight: 600;
  cursor: pointer; transition: all 0.15s; user-select: none;
}
.metric-chip input { accent-color: var(--green); cursor: pointer; }
.metric-chip:hover { border-color: var(--green-300); }
.metric-chip.active { background: var(--green-100); border-color: var(--green-300); color: var(--green); }

.completeness-badge {
  font-size: 11px; font-weight: 700; padding: 1px 6px; border-radius: 999px;
}
.completeness-badge.high { background: var(--green-100); color: var(--green-600); }
.completeness-badge.mid  { background: var(--warning-50); color: var(--warning-500); }
.completeness-badge.low  { background: var(--danger-50); color: var(--danger-700); }

/* 徽章說明：刻意放在勾選區旁邊、跟徽章同時出現，不是只寫在下方圖表卡片的 chart-note
   裡——使用者第一眼看到「82%」的地方就是這裡，說明要跟著出現在同一個視野內。
   套用跟 .query-hint 同一套「淺底框＋深色粗體字」規格，說明文字要看得清楚，
   不能用次要文字才用的低對比灰階色 */
.badge-legend {
  display: flex; align-items: flex-start; gap: 8px;
  padding: 10px 16px; border-radius: 8px;
  background: var(--info-50); border: 1px solid var(--info-100);
  color: var(--info-500); font-size: 13px; font-weight: 600; line-height: 1.6;
  margin: 0;
}
.badge-legend .hint-icon { font-size: 17px; margin-top: 1px; flex-shrink: 0; }

/* 按鈕（沿用 PorkView 同一組樣式） */
/* 摘要列 */
.summary-bar { display: flex; gap: 14px; margin-bottom: 24px; flex-wrap: wrap; }
.stat-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 12px; padding: 16px 24px;
  display: flex; flex-direction: column; gap: 6px;
  box-shadow: 0 1px 4px rgba(0,0,0,0.05);
}
.stat-label {
  font-size: 12px; color: var(--neutral-500);
  letter-spacing: 0.05em; text-transform: uppercase; font-weight: 600;
}
.stat-value { font-size: 26px; font-weight: 700; color: var(--green-800); }

/* 圖表卡片 */
.chart-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 16px; padding: 28px 32px 30px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
  margin-bottom: 24px;
}
.chart-toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 24px; }
.chart-title { font-size: 15px; font-weight: 700; color: var(--neutral-700); }
.canvas-wrap { position: relative; height: 460px; width: 100%; }
.chart-note {
  display: flex; align-items: flex-start; gap: 8px;
  margin-top: 18px; padding: 10px 16px; border-radius: 8px;
  background: var(--info-50); border: 1px solid var(--info-100);
  color: var(--info-500); font-size: 13px; font-weight: 600; line-height: 1.6;
}
.query-hint {
  display: flex; align-items: center; gap: 8px;
  padding: 10px 16px; border-radius: 8px;
  background: var(--info-50); border: 1px solid var(--info-100);
  color: var(--info-500); font-size: 13px; font-weight: 600; line-height: 1.5;
}
.hint-icon { font-size: 18px; flex-shrink: 0; }

/* 非常態資料明細 */
.abnormal-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 16px; padding: 20px 28px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
}
.btn-toggle-abnormal {
  display: flex; align-items: center; gap: 6px;
  background: none; border: none; cursor: pointer;
  font-size: 14px; font-weight: 700; color: var(--neutral-700);
  padding: 4px 0; width: 100%; text-align: left;
}
.abnormal-table-wrap { margin-top: 16px; max-height: 360px; overflow-y: auto; overflow-x: auto; }
.abnormal-table { width: 100%; border-collapse: collapse; font-size: 13px; }
.abnormal-table th {
  position: sticky; top: 0; background: var(--surface-2);
  text-align: left; padding: 8px 12px; font-weight: 700;
  color: var(--neutral-600); border-bottom: 1px solid var(--border);
}
.abnormal-table td {
  padding: 7px 12px; border-bottom: 1px solid var(--neutral-100);
  color: var(--neutral-700);
}
.abnormal-table tbody tr:hover { background: var(--green-50); }

.status-chip {
  font-size: 11.5px; font-weight: 700; padding: 2px 8px; border-radius: 999px;
  white-space: nowrap;
}
.status-empty        { background: var(--neutral-100); color: var(--neutral-600); }
.status-closed        { background: var(--neutral-100); color: var(--neutral-600); }
.status-notquoted     { background: var(--warning-50); color: var(--warning-500); }
.status-negotiated    { background: var(--info-50); color: var(--info-500); }
.status-rangequote    { background: var(--green-100); color: var(--green-600); }
.status-unrecognized  { background: var(--danger-50); color: var(--danger-700); }
</style>
