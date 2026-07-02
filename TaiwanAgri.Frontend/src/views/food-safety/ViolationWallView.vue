<template>
  <div class="violation-wall-view">

    <!-- 頁首 -->
    <div class="page-header">
      <h2 class="section-title">農藥違規警示牆</h2>
      <p class="section-subtitle">
        {{ hasSearched ? `近 ${appliedDays} 天農產品農藥殘留抽檢違規紀錄` : '請設定查詢條件並按下查詢' }}
      </p>
    </div>

    <!-- 篩選列 -->
    <div class="filter-bar">
      <!-- 查詢區間 -->
      <div class="filter-group">
        <span class="filter-label">查詢區間</span>
        <div class="days-tabs">
          <button
            class="tab-btn"
            :class="{ active: daysMode === 'preset' && selectedDays === 90 }"
            @click="selectPresetDays(90)"
          >近 90 天</button>
          <button
            class="tab-btn"
            :class="{ active: daysMode === 'preset' && selectedDays === 365 }"
            @click="selectPresetDays(365)"
          >近 365 天</button>
          <div class="custom-days">
            <input
              v-model.number="customDaysInput"
              type="number"
              min="1"
              class="custom-days-input"
              placeholder="自訂天數"
              @focus="daysMode = 'custom'"
              @keyup.enter="triggerSearch"
            />
            <span class="custom-days-unit">天</span>
          </div>
        </div>
      </div>

      <!-- 抽檢結果 -->
      <div class="filter-group">
        <span class="filter-label">抽檢結果</span>
        <div class="result-tabs">
          <button
            v-for="opt in resultOptions"
            :key="opt.value ?? 'all'"
            class="tab-btn"
            :class="{ active: selectedResult === opt.value }"
            @click="changeResult(opt.value)"
          >{{ opt.label }}</button>
        </div>
      </div>

      <!-- 查詢按鈕 -->
      <button class="btn-search" @click="triggerSearch">
        <span class="mdi mdi-magnify" />
        查詢
      </button>
    </div>

    <!-- 尚未查詢：等待狀態 -->
    <div v-if="!hasSearched" class="state-box hint-box">
      <span class="mdi mdi-magnify state-icon" />
      <span class="state-text">請設定查詢條件，按下查詢按鈕開始查詢</span>
    </div>

    <!-- 載入中 -->
    <div v-else-if="store.isLoadingViolations" class="state-box">
      <div class="loading-spinner" />
      <span class="state-text">資料載入中...</span>
    </div>

    <!-- 錯誤 -->
    <div v-else-if="store.violationsError" class="state-box error-box">
      <span class="mdi mdi-alert-circle state-icon" />
      <span class="state-text">{{ store.violationsError }}</span>
      <button class="btn-retry" @click="triggerSearch">重試</button>
    </div>

    <!-- 無資料 -->
    <div v-else-if="store.violationsPage && store.violationsPage.items.length === 0" class="state-box">
      <span class="mdi mdi-shield-check-outline state-icon" />
      <span class="state-text">此區間查無違規紀錄</span>
    </div>

    <!-- 資料表格 -->
    <div v-else-if="store.violationsPage" class="table-section">
      <div class="table-wrapper">
        <table class="violation-table">
          <thead>
            <tr>
              <th>序</th>
              <th>編號</th>
              <th>抽檢日期</th>
              <th>產品名稱</th>
              <th>經營業者</th>
              <th>採樣地點</th>
              <th>抽檢結果</th>
              <th>備註</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(item, index) in store.violationsPage.items" :key="item.number">
              <td class="cell-index">{{ rowNumber(index) }}</td>
              <td class="cell-number">{{ item.number }}</td>
              <td class="cell-date">{{ item.samplingDate }}</td>
              <td class="cell-product">{{ item.productName }}</td>
              <td>{{ item.producerName }}</td>
              <td class="cell-location">
                <a
                  v-if="isUrl(item.samplingLocation)"
                  :href="item.samplingLocation"
                  target="_blank"
                  rel="noopener noreferrer"
                  class="location-link"
                >
                  <span class="mdi mdi-link-variant" /> 查看來源
                </a>
                <span v-else class="location-text" :title="item.samplingLocation">
                  {{ item.samplingLocation }}
                </span>
              </td>
              <td>
                <span class="result-badge" :class="resultClass(item.inspectResult)">
                  {{ item.inspectResult }}
                </span>
              </td>
              <td class="cell-note">{{ item.note }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- 分頁控制 -->
      <div class="pagination-bar">
        <span class="pagination-info">
          共 {{ store.violationsPage.totalCount }} 筆，
          第 {{ store.violationsPage.page }} / {{ store.violationsPage.totalPages }} 頁
        </span>
        <div class="pagination-controls">
          <div class="page-size-group">
            <span class="jump-label">每頁</span>
            <select
              class="page-size-select"
              :value="pageSize"
              @change="handlePageSizeChange"
            >
              <option v-for="n in pageSizeOptions" :key="n" :value="n">{{ n }} 筆</option>
            </select>
          </div>

          <button
            class="page-btn"
            :disabled="currentPage <= 1"
            @click="changePage(1)"
            title="第一頁"
          ><span class="mdi mdi-page-first" /></button>

          <button
            class="page-btn"
            :disabled="currentPage <= 1"
            @click="changePage(currentPage - 1)"
          ><span class="mdi mdi-chevron-left" /></button>

          <button
            v-for="p in visiblePages"
            :key="p"
            class="page-btn"
            :class="{ active: p === currentPage }"
            @click="changePage(p)"
          >{{ p }}</button>

          <button
            class="page-btn"
            :disabled="currentPage >= store.violationsPage.totalPages"
            @click="changePage(currentPage + 1)"
          ><span class="mdi mdi-chevron-right" /></button>

          <button
            class="page-btn"
            :disabled="currentPage >= store.violationsPage.totalPages"
            @click="changePage(store.violationsPage.totalPages)"
            title="最後一頁"
          ><span class="mdi mdi-page-last" /></button>

          <div class="jump-to-page">
            <span class="jump-label">跳至</span>
            <input
              v-model.number="jumpPageInput"
              type="number"
              min="1"
              :max="store.violationsPage.totalPages"
              class="jump-input"
              @keyup.enter="handleJumpPage"
            />
            <span class="jump-label">頁</span>
            <button class="jump-btn" @click="handleJumpPage">Go</button>
          </div>
        </div>
      </div>
    </div>

  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useFoodSafetyStore } from '@/stores/foodSafety'

const store = useFoodSafetyStore()

// ─── 篩選狀態 ───────────────────────────────────────────────
const daysMode = ref<'preset' | 'custom'>('preset')
const selectedDays = ref(90)
const customDaysInput = ref<number | null>(null)

const selectedResult = ref<string | undefined>(undefined)
const resultOptions: { label: string; value: string | undefined }[] = [
  { label: '全部', value: undefined },
  { label: '不合格', value: '不合格' },
  { label: '標示合格', value: '標示合格' },
  { label: '合格', value: '合格' },
]

const currentPage = ref(1)
const pageSizeOptions = [10, 20, 50, 100]
const jumpPageInput = ref<number | null>(null)

// 每頁筆數：從 localStorage 讀取上次選擇，沒有的話預設 20
const PAGE_SIZE_STORAGE_KEY = 'violationWall.pageSize'
const storedPageSize = Number(localStorage.getItem(PAGE_SIZE_STORAGE_KEY))
const pageSize = ref(pageSizeOptions.includes(storedPageSize) ? storedPageSize : 20)

// 是否已經執行過查詢
const hasSearched = ref(false)

// 查詢當下實際套用的天數（顯示在頁首說明用，跟輸入框當下的值分開）
const appliedDays = ref(90)

// ─── 邏輯 ───────────────────────────────────────────────────

/** 計算目前生效的天數：優先用自訂輸入框的值，沒填就用預設按鈕的值 */
function resolveDays(): number {
  if (daysMode.value === 'custom' && customDaysInput.value && customDaysInput.value > 0) {
    return customDaysInput.value
  }
  return selectedDays.value
}

function selectPresetDays(d: number) {
  daysMode.value = 'preset'
  selectedDays.value = d
  customDaysInput.value = null
  // 點預設按鈕視為一次明確的查詢動作
  triggerSearch()
}

function changeResult(v: string | undefined) {
  selectedResult.value = v
  // 已經查詢過才自動重新查詢；尚未查詢過，只是記住選擇，等使用者按查詢
  if (hasSearched.value) {
    currentPage.value = 1
    doFetch()
  }
}

/**
 * 每頁筆數變更處理。
 * 刻意不用 v-model + @change 混用（曾經發生 handler 讀到舊值的時序問題），
 * 改成從原生 change 事件直接取值、手動賦值，確保 pageSize 更新完成後才觸發查詢。
 */
function handlePageSizeChange(event: Event) {
  const newSize = Number((event.target as HTMLSelectElement).value)
  pageSize.value = newSize

  // 不管有沒有查詢過，都先存進 localStorage 記住這個選擇
  localStorage.setItem(PAGE_SIZE_STORAGE_KEY, String(newSize))

  // 但只有查詢過，才重新打 API（未查詢時不自動查詢）
  if (hasSearched.value) {
    currentPage.value = 1
    doFetch()
  }
}

function changePage(p: number) {
  if (!store.violationsPage) return
  if (p < 1 || p > store.violationsPage.totalPages) return
  currentPage.value = p
  doFetch()
}

function handleJumpPage() {
  if (!store.violationsPage) return
  if (!jumpPageInput.value) return
  const target = Math.min(Math.max(1, jumpPageInput.value), store.violationsPage.totalPages)
  changePage(target)
  jumpPageInput.value = null
}

/** 使用者主動觸發查詢（按查詢鈕、按 Enter、點預設天數按鈕） */
function triggerSearch() {
  appliedDays.value = resolveDays()
  currentPage.value = 1
  hasSearched.value = true
  doFetch()
}

function doFetch() {
  store.fetchViolations(appliedDays.value, selectedResult.value, currentPage.value, pageSize.value)
}

/**
 * 計算表格序號：不是「這一頁裡的第幾筆」，而是「在全部符合條件的資料中排第幾筆」。
 * 例如 pageSize=20，目前在第 2 頁，這一頁第 1 筆（index=0）就是全域第 21 筆。
 */
function rowNumber(index: number): number {
  return (currentPage.value - 1) * pageSize.value + index + 1
}

function resultClass(result: string) {
  if (result === '不合格') return 'fail'
  if (result === '標示合格') return 'warn'
  return 'pass'
}

/** 判斷欄位內容是否為網址（部分農業部資料會誤填成銷售頁連結） */
function isUrl(value: string): boolean {
  return /^https?:\/\//i.test(value)
}

/** 分頁按鈕：最多顯示 5 個頁碼，以目前頁為中心 */
const visiblePages = computed(() => {
  const total = store.violationsPage?.totalPages ?? 0
  const current = currentPage.value
  const range = 2
  const start = Math.max(1, current - range)
  const end = Math.min(total, current + range)
  const pages: number[] = []
  for (let i = start; i <= end; i++) pages.push(i)
  return pages
})

// ── 注意：這裡刻意不用 onMounted 自動查詢，符合「未查詢前顯示等待」的需求 ──
</script>

<style scoped>
.violation-wall-view {
  padding: 36px 56px;
  width: 100%;
  box-sizing: border-box;
}

/* ── 頁首 ── */
.page-header { margin-bottom: 20px; }

.section-title {
  font-size: 22px;
  font-weight: 700;
  color: var(--text-primary);
  margin-bottom: 6px;
}

.section-subtitle {
  font-size: 13px;
  color: var(--text-muted);
}

/* ── 篩選列 ── */
.filter-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 24px;
  margin-bottom: 24px;
  padding: 16px 20px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 12px;
}

.filter-group {
  display: flex;
  align-items: center;
  gap: 12px;
}

.filter-label {
  font-size: 12px;
  font-weight: 700;
  color: var(--text-muted);
  white-space: nowrap;
}

.days-tabs, .result-tabs {
  display: flex;
  align-items: center;
  gap: 6px;
}

.tab-btn {
  padding: 6px 16px;
  border-radius: 999px;
  border: 1px solid var(--border);
  background: transparent;
  color: var(--text-muted);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.15s;
  white-space: nowrap;
}

.tab-btn:hover { border-color: #43a047; color: #2e7d32; }

.tab-btn.active {
  background: #2e7d32;
  border-color: #2e7d32;
  color: white;
}

.custom-days {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 14px;
  border-radius: 999px;
  border: 1px solid var(--border);
  min-width: 130px;
}

.custom-days-input {
  width: 90px;
  border: none;
  outline: none;
  font-size: 13px;
  background: transparent;
  color: var(--text-primary);
}

.custom-days-unit { font-size: 12px; color: var(--text-muted); white-space: nowrap; }

.page-size-select {
  padding: 5px 10px;
  border-radius: 8px;
  border: 1px solid var(--border);
  font-size: 13px;
  color: var(--text-primary);
  background: var(--surface);
}

.btn-search {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 8px 22px;
  border-radius: 999px;
  border: 1px solid #1a5220;
  background: linear-gradient(180deg, #4caf50 0%, #2e7d32 40%, #1b5e20 100%);
  color: white;
  font-size: 13px;
  font-weight: 700;
  cursor: pointer;
  margin-left: auto;
}

.btn-search:hover { background: linear-gradient(180deg, #66bb6a 0%, #388e3c 40%, #2e7d32 100%); }

/* ── 狀態容器 ── */
.state-box {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
  padding: 56px 32px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 16px;
}

.state-icon { font-size: 36px; color: #aaa; }
.state-text { font-size: 15px; color: var(--text-muted); }

.hint-box .state-icon { color: #c8e6c9; }

.error-box {
  background: #fff5f5;
  border-color: #ffcdd2;
  color: #c62828;
}

.btn-retry {
  padding: 8px 24px;
  border-radius: 999px;
  border: 1.5px solid #c62828;
  background: transparent;
  color: #c62828;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
}
.btn-retry:hover { background: #fff5f5; }

.loading-spinner {
  width: 36px;
  height: 36px;
  border: 3px solid #c8e6c9;
  border-top-color: #2e7d32;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin { to { transform: rotate(360deg); } }

/* ── 表格區塊 ── */
.table-section {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

/* 限制在框內、可上下左右捲動的關鍵 CSS */
.table-wrapper {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 16px;
  box-shadow: 0 2px 8px rgba(46, 125, 50, 0.06);
  max-height: 560px;
  overflow: auto;
}

.violation-table {
  width: 100%;
  min-width: 900px;
  border-collapse: collapse;
  font-size: 13px;
}

.violation-table thead th {
  position: sticky;
  top: 0;
  background: #f1f8f1;
  text-align: left;
  padding: 12px 16px;
  font-weight: 700;
  color: #1b5e20;
  border-bottom: 1px solid var(--border);
  white-space: nowrap;
  z-index: 1;
}

.violation-table td {
  padding: 12px 16px;
  border-bottom: 1px solid var(--border);
  color: var(--text-primary);
  vertical-align: top;
}

.violation-table tbody tr:hover { background: #fafdf9; }
.violation-table tbody tr:last-child td { border-bottom: none; }

.cell-index {
  font-family: monospace;
  font-size: 12px;
  color: var(--text-muted);
  text-align: right;
  white-space: nowrap;
}

.cell-number { font-family: monospace; font-size: 12px; color: var(--text-muted); white-space: nowrap; }
.cell-date { white-space: nowrap; font-variant-numeric: tabular-nums; }
.cell-product { font-weight: 600; }

.cell-location {
  max-width: 380px;
}

.location-text {
  display: block;
  font-size: 12px;
  color: var(--text-primary);
  white-space: normal;
  word-break: break-word;
  max-width: 380px;
  line-height: 1.5;
}

.location-link {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  color: #1565c0;
  text-decoration: none;
  white-space: nowrap;
}

.location-link:hover {
  text-decoration: underline;
}

.cell-note { max-width: 220px; font-size: 12px; color: var(--text-muted); }

.result-badge {
  display: inline-block;
  padding: 3px 10px;
  border-radius: 999px;
  font-size: 12px;
  font-weight: 700;
  white-space: nowrap;
}

.result-badge.fail { background: #ffebee; color: #c62828; }
.result-badge.warn { background: #fff3e0; color: #e65100; }
.result-badge.pass { background: #e8f5e9; color: #2e7d32; }

/* ── 分頁列 ── */
.pagination-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 12px;
}

.pagination-info {
  font-size: 12px;
  color: var(--text-muted);
}

.pagination-controls {
  display: flex;
  align-items: center;
  gap: 4px;
}

.page-size-group {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-right: 8px;
  padding-right: 8px;
  border-right: 1px solid var(--border);
}

.jump-to-page {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-left: 8px;
  padding-left: 8px;
  border-left: 1px solid var(--border);
}

.jump-label {
  font-size: 12px;
  color: var(--text-muted);
  white-space: nowrap;
}

.jump-input {
  width: 50px;
  padding: 4px 6px;
  border-radius: 6px;
  border: 1px solid var(--border);
  font-size: 13px;
  text-align: center;
  outline: none;
}

.jump-input:focus { border-color: #43a047; }

.jump-btn {
  padding: 4px 10px;
  border-radius: 6px;
  border: 1px solid #2e7d32;
  background: #2e7d32;
  color: white;
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
}

.jump-btn:hover { background: #388e3c; }

.page-btn {
  min-width: 32px;
  height: 32px;
  padding: 0 8px;
  border-radius: 8px;
  border: 1px solid var(--border);
  background: var(--surface);
  color: var(--text-primary);
  font-size: 13px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}

.page-btn:hover:not(:disabled) { border-color: #43a047; color: #2e7d32; }
.page-btn:disabled { opacity: 0.4; cursor: not-allowed; }

.page-btn.active {
  background: #2e7d32;
  border-color: #2e7d32;
  color: white;
}
</style>