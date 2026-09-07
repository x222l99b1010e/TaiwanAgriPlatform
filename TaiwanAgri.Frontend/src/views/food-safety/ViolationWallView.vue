<template>
  <div class="page violation-wall-view">

    <QueryLayout
      title="農藥違規警示牆"
      title-en="VIOLATION WALL"
      :subtitle="hasSearched
        ? `近 ${appliedDays} 天農產品農藥殘留抽檢違規紀錄`
        : '農產品農藥殘留抽檢的違規紀錄，可依查詢區間與抽檢結果篩選'"
    >
      <template #actions>
        <Btn icon="mdi-magnify" @click="triggerSearch">查詢</Btn>
      </template>

      <template #filters>
        <!-- 查詢區間 -->
        <div class="field-group">
          <span class="field-label">查詢區間</span>
          <div class="days-row">
            <div class="segmented">
              <button
                class="segmented__btn"
                :class="{ 'is-active': daysMode === 'preset' && selectedDays === 90 }"
                @click="selectPresetDays(90)"
              >近 90 天</button>
              <button
                class="segmented__btn"
                :class="{ 'is-active': daysMode === 'preset' && selectedDays === 365 }"
                @click="selectPresetDays(365)"
              >近 365 天</button>
            </div>
            <div class="custom-days" :class="{ 'is-active': daysMode === 'custom' }">
              <input
                v-model.number="customDaysInput"
                type="number"
                min="1"
                class="custom-days-input"
                placeholder="自訂"
                aria-label="自訂查詢天數"
                @focus="daysMode = 'custom'"
                @keyup.enter="triggerSearch"
              />
              <span class="custom-days-unit">天</span>
            </div>
          </div>
        </div>

        <!-- 抽檢結果 -->
        <div class="field-group">
          <span class="field-label">抽檢結果</span>
          <div class="segmented">
            <button
              v-for="opt in resultOptions"
              :key="opt.value ?? 'all'"
              class="segmented__btn"
              :class="{ 'is-active': selectedResult === opt.value }"
              @click="changeResult(opt.value)"
            >{{ opt.label }}</button>
          </div>
        </div>
      </template>

      <template #results>
        <StateBlock
          v-if="!hasSearched"
          state="hint"
          message="請設定查詢條件，按下查詢按鈕開始查詢"
        />
        <StateBlock v-else-if="store.isLoadingViolations" state="loading" message="資料載入中..." />
        <StateBlock
          v-else-if="store.violationsError"
          state="error"
          :message="store.violationsError"
          retryable
          @retry="triggerSearch"
        />
        <StateBlock
          v-else-if="store.violationsPage && store.violationsPage.items.length === 0"
          state="empty"
          icon="mdi-shield-check-outline"
          message="此區間查無違規紀錄"
          hint="沒有紀錄是好消息；要看更長的期間可以把查詢區間拉大"
        />

        <!-- 資料表格 -->
        <div v-else-if="store.violationsPage" class="table-section">
          <div class="table-wrapper">
            <table class="data-table violation-table">
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
                    <span class="badge result-badge" :class="resultClass(item.inspectResult)">
                      {{ item.inspectResult }}
                    </span>
                  </td>
                  <td class="cell-note">{{ item.note }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- 分頁控制改用共用的 PagerBar。原本這裡自己重寫了一整條分頁列（每頁筆數、
               五顆方向鈕、頁碼、跳頁），而 PagerBar 的檔頭註解甚至寫著它的邏輯就是從
               這一頁抽出去的——共用元件存在、邏輯也共用了，只有 template 沒回頭替換。
               PagerBar 原本缺「每頁筆數」那一格，這次補成選填的 prop。 -->
          <PagerBar
            :current-page="currentPage"
            :total-pages="store.violationsPage.totalPages"
            :total-count="store.violationsPage.totalCount"
            :visible-pages="visiblePages"
            v-model:jump-page-input="jumpPageInput"
            :page-size="pageSize"
            :page-size-options="pageSizeOptions"
            @change="changePage"
            @jump="handleJumpPage"
            @update:page-size="handlePageSize"
          />
        </div>
      </template>
    </QueryLayout>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useFoodSafetyStore } from '@/stores/foodSafety'
import { usePagination } from '@/composables/usePagination'
import QueryLayout from '@/components/layouts/QueryLayout.vue'
import PagerBar from '@/components/PagerBar.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'

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

// 是否已經執行過查詢
const hasSearched = ref(false)

// 分頁控制邏輯共用（與 OrganicCertView 同一份 composable）
const {
  pageSizeOptions,
  pageSize,
  currentPage,
  jumpPageInput,
  visiblePages,
  changePage,
  handleJumpPage,
  setPageSize,
  rowNumber,
} = usePagination({
  storageKey: 'violationWall.pageSize',
  totalPages: () => store.violationsPage?.totalPages,
  onChange: doFetch,
})

// 尚未查詢過時只記住每頁筆數選擇、不打 API（未查詢前顯示等待畫面）
function handlePageSize(size: number) {
  setPageSize(size, hasSearched.value)
}

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

function resultClass(result: string) {
  if (result === '不合格') return 'fail'
  if (result === '標示合格') return 'warn'
  return 'pass'
}

/** 判斷欄位內容是否為網址（部分農業部資料會誤填成銷售頁連結） */
function isUrl(value: string): boolean {
  return /^https?:\/\//i.test(value)
}

// ── 注意：這裡刻意不用 onMounted 自動查詢，符合「未查詢前顯示等待」的需求 ──
</script>

<style scoped>
/* 顏色全部改用 semantic 層；「近 90 天／近 365 天」與抽檢結果
   四選一都改用 base.css 的 .segmented，欄位標籤走 .field-group／.field-label。
   分頁列整條換成共用的 PagerBar，那一整段樣式（.pagination-*／.page-btn／
   .jump-*／.page-size-*）在這裡全部刪掉。 */

/* ── 篩選列 ── */
.days-row { display: flex; align-items: center; gap: var(--space-2); }

/* 自訂天數：外觀對齊 .segmented（同高、同圓角、同邊框），但它是輸入框不是選項，
   所以不共用那組 class——選中的樣子由 is-active 表示，跟左邊的分段控制器互斥。 */
.custom-days {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  height: var(--control-h);
  padding: 0 var(--space-3);
  border-radius: var(--radius-md);
  border: var(--border-width) solid var(--color-border);
  background: var(--color-surface);
  transition: border-color var(--duration-fast) var(--ease-work);
}
.custom-days:focus-within { border-color: var(--color-action); box-shadow: var(--shadow-focus); }
.custom-days.is-active { border-color: var(--color-action); }

.custom-days-input {
  width: 72px;
  border: none;
  outline: none;
  font-family: var(--font-num);
  font-size: var(--text-sm);
  background: transparent;
  color: var(--color-text);
}
.custom-days-input::placeholder { color: var(--color-text-dim); font-family: var(--font-body); }

.custom-days-unit { font-size: var(--text-xs); color: var(--color-text-dim); white-space: nowrap; }

/* ── 表格區塊 ── */
.table-section {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

/* 限制在框內、可上下左右捲動的關鍵 CSS */
.table-wrapper {
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  max-height: 560px;
  overflow: auto;
}

/* 表格外殼已收進 base.css 的 .data-table，這裡只留這一頁真正不同的部分 */
.violation-table { min-width: 900px; }

.cell-index {
  font-family: var(--font-num);
  font-size: var(--text-xs);
  color: var(--color-text-dim);
  text-align: right;
  white-space: nowrap;
}

.cell-number { font-family: var(--font-num); font-size: var(--text-xs); color: var(--color-text-dim); white-space: nowrap; }
.cell-date { white-space: nowrap; font-family: var(--font-num); font-variant-numeric: tabular-nums; }
.cell-product { font-weight: var(--weight-medium); }

.cell-location {
  max-width: 380px;
}

.location-text {
  display: block;
  font-size: var(--text-xs);
  color: var(--color-text);
  white-space: normal;
  word-break: break-word;
  max-width: 380px;
  line-height: var(--leading-normal);
}

.location-link {
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
  font-size: var(--text-xs);
  color: var(--info-500);
  text-decoration: none;
  white-space: nowrap;
}

.location-link:hover {
  text-decoration: underline;
}

.cell-note { max-width: 220px; font-size: var(--text-xs); color: var(--color-text-dim); }

/* 標籤外殼已收進 base.css 的 .badge，這裡只留語意色 */
.result-badge.fail { background: var(--danger-50); color: var(--danger-500); }
.result-badge.warn { background: var(--warning-50); color: var(--warning-700); }
.result-badge.pass { background: var(--color-action-soft-2); color: var(--color-action); }

</style>