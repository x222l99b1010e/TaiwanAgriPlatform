<template>
  <div class="organic-cert-view">
    <!-- 側邊篩選欄 -->
    <aside class="filter-sidebar">
      <h3 class="filter-title">篩選條件</h3>

      <div class="filter-field">
        <label class="filter-label">業者名稱</label>
        <input
          v-model="operatorName"
          type="text"
          class="filter-input"
          placeholder="輸入業者名稱關鍵字"
          @input="onFilterChange"
        />
      </div>

      <div class="filter-field">
        <label class="filter-label">驗證機構</label>
        <input
          v-model="verificationBodyName"
          type="text"
          class="filter-input"
          placeholder="輸入驗證機構名稱關鍵字"
          @input="onFilterChange"
        />
      </div>

      <div class="filter-field">
        <label class="filter-label">品項關鍵字</label>
        <input
          v-model="productKeyword"
          type="text"
          class="filter-input"
          placeholder="輸入作物或產品名稱"
          @input="onFilterChange"
        />
      </div>

      <p class="filter-hint">
        <span class="mdi mdi-information-outline" />
        邊框變色的卡片代表品項資料可能來自多證號合併，請自行核對。
      </p>
    </aside>

    <!-- 右側結果區 -->
    <div class="result-area">
      <div class="result-header">
        <span class="result-count" v-if="store.organicCertPage">
          共 {{ store.organicCertPage.totalCount }} 筆
        </span>
      </div>

      <!-- 載入中 -->
      <div v-if="store.isLoadingOrganicCert" class="state-box">
        <div class="loading-spinner" />
        <span class="state-text">資料載入中...</span>
      </div>

      <!-- 錯誤 -->
      <div v-else-if="store.organicCertError" class="state-box error-box">
        <span class="mdi mdi-alert-circle state-icon" />
        <span class="state-text">{{ store.organicCertError }}</span>
        <button class="btn-retry" @click="fetchImmediate">重試</button>
      </div>

      <!-- 無資料 -->
      <div
        v-else-if="store.organicCertPage && store.organicCertPage.items.length === 0"
        class="state-box"
      >
        <span class="mdi mdi-file-search-outline state-icon" />
        <span class="state-text">查無符合條件的驗證紀錄</span>
      </div>

      <!-- 卡片列表 -->
      <div v-else-if="store.organicCertPage" class="cert-grid">
        <div
          v-for="item in store.organicCertPage.items"
          :key="item.id"
          class="cert-card"
          :class="{ ambiguous: item.hasAmbiguousProductMapping }"
          :title="item.hasAmbiguousProductMapping
            ? '此筆資料的品項可能為多證號合併，請自行核對'
            : undefined"
        >
          <div class="cert-card-header">
            <span class="cert-sn">{{ item.certOrganicSn }}</span>
            <span class="status-badge" :class="statusClass(item.status)">
              {{ item.status }}
            </span>
          </div>

          <div class="cert-operator">{{ item.operatorName }}</div>

          <div class="cert-row">
            <span class="cert-label">驗證機構</span>{{ item.verificationBodyName }}
          </div>
          <div class="cert-row cert-row-products">
            <span class="cert-label">品項範圍</span>

            <div v-if="!expandedIds.has(item.id)" class="products-text-clamp">
                {{ productText(item) }}
            </div>

            <ul v-else class="products-list">
                <li v-for="(product, idx) in splitProductItems(productText(item))" :key="idx">
                {{ product }}
                </li>
            </ul>

            <button type="button" class="expand-toggle" @click="toggleExpand(item.id)">
                {{ expandedIds.has(item.id) ? '收合' : '展開' }}
                <span class="mdi" :class="expandedIds.has(item.id) ? 'mdi-chevron-up' : 'mdi-chevron-down'" />
            </button>
          </div>
          <div class="cert-row" v-if="item.effectiveDate">
            <span class="cert-label">效期</span>{{ item.effectiveDate }}
          </div>
          <div class="cert-row">
            <span class="cert-label">地址</span>{{ item.address }}
          </div>
        </div>
      </div>

      <!-- 分頁列（跳頁 + 每頁筆數，沿用 ViolationWallView 的模式） -->
      <div v-if="store.organicCertPage" class="pagination-bar">
        <span class="pagination-info">
          第 {{ store.organicCertPage.page }} / {{ store.organicCertPage.totalPages }} 頁
        </span>
        <div class="pagination-controls">
          <div class="page-size-group">
            <span class="jump-label">每頁</span>
            <select class="page-size-select" :value="pageSize" @change="handlePageSizeChange">
              <option v-for="n in pageSizeOptions" :key="n" :value="n">{{ n }} 筆</option>
            </select>
          </div>

          <button class="page-btn" :disabled="currentPage <= 1" @click="changePage(1)" title="第一頁">
            <span class="mdi mdi-page-first" />
          </button>
          <button class="page-btn" :disabled="currentPage <= 1" @click="changePage(currentPage - 1)">
            <span class="mdi mdi-chevron-left" />
          </button>
          <button
            v-for="p in visiblePages"
            :key="p"
            class="page-btn"
            :class="{ active: p === currentPage }"
            @click="changePage(p)"
          >{{ p }}</button>
          <button
            class="page-btn"
            :disabled="currentPage >= store.organicCertPage.totalPages"
            @click="changePage(currentPage + 1)"
          >
            <span class="mdi mdi-chevron-right" />
          </button>
          <button
            class="page-btn"
            :disabled="currentPage >= store.organicCertPage.totalPages"
            @click="changePage(store.organicCertPage.totalPages)"
            title="最後一頁"
          >
            <span class="mdi mdi-page-last" />
          </button>

          <div class="jump-to-page">
            <span class="jump-label">跳至</span>
            <input
              v-model.number="jumpPageInput"
              type="number"
              min="1"
              :max="store.organicCertPage.totalPages"
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
import { ref, computed, onMounted } from 'vue'
import { useFoodSafetyStore } from '@/stores/foodSafety'
import type { OrganicCertificationQueryParams, OrganicCertificationResult } from '@/api/foodSafety'

const store = useFoodSafetyStore()

// ─── 篩選狀態 ───────────────────────────────────────────────
const operatorName = ref('')
const verificationBodyName = ref('')
const productKeyword = ref('')

const currentPage = ref(1)
const pageSizeOptions = [10, 20, 50, 100]
const pageSize = ref(20)
const jumpPageInput = ref<number | null>(null)

// 記錄哪些卡片目前是展開狀態（存 item.id）
const expandedIds = ref<Set<number>>(new Set())

function toggleExpand(id: number) {
  const next = new Set(expandedIds.value)
  if (next.has(id)) {
    next.delete(id)
  } else {
    next.add(id)
  }
  expandedIds.value = next
}

/** 統一取得品項文字：優先用 productScope，沒有才 fallback 到 products */
function productText(item: OrganicCertificationResult): string {
  return item.productScope || item.products
}

/** 依頓號拆解成陣列；filter(Boolean) 過濾掉空字串，避免字串尾端多一個頓號時產生空白項目 */
function splitProductItems(text: string): string[] {
  return text.split('、').map(s => s.trim()).filter(Boolean)
}

function buildParams(): OrganicCertificationQueryParams {
  return {
    operatorName: operatorName.value || undefined,
    verificationBodyName: verificationBodyName.value || undefined,
    productKeyword: productKeyword.value || undefined,
    page: currentPage.value,
    pageSize: pageSize.value,
  }
}

/** 立即查詢：分頁、每頁筆數變更時使用，這類操作使用者期待馬上有反應，不該被 debounce 拖慢 */
function fetchImmediate() {
  store.fetchOrganicCertifications(buildParams())
}

/** 篩選文字變更時使用：先把頁碼重置回第一頁，再以 debounce 方式查詢 */
function onFilterChange() {
  currentPage.value = 1
  store.fetchOrganicCertificationsDebounced(buildParams())
}

function changePage(p: number) {
  const total = store.organicCertPage?.totalPages ?? 1
  if (p < 1 || p > total) return
  currentPage.value = p
  fetchImmediate()
}

function handlePageSizeChange(event: Event) {
  const newSize = Number((event.target as HTMLSelectElement).value)
  pageSize.value = newSize
  currentPage.value = 1
  fetchImmediate()
}

function handleJumpPage() {
  if (!store.organicCertPage || !jumpPageInput.value) return
  const target = Math.min(Math.max(1, jumpPageInput.value), store.organicCertPage.totalPages)
  changePage(target)
  jumpPageInput.value = null
}

/** 分頁按鈕：最多顯示 5 個頁碼，以目前頁為中心（沿用 ViolationWallView 的邏輯） */
const visiblePages = computed(() => {
  const total = store.organicCertPage?.totalPages ?? 0
  const current = currentPage.value
  const range = 2
  const start = Math.max(1, current - range)
  const end = Math.min(total, current + range)
  const pages: number[] = []
  for (let i = start; i <= end; i++) pages.push(i)
  return pages
})

function statusClass(status: string) {
  if (status === '終止' || status === '結束') return 'inactive'
  return 'active'
}

// 進頁面就自動查詢一次（顯示預設分頁列表），跟 ViolationWallView 刻意不自動查詢的理由不同：
// 有機驗證查詢即使不帶任何條件，回來的成本也只是「第一頁 + 總筆數」，沒有天數可調大的風險
onMounted(() => {
  fetchImmediate()
})
</script>

<style scoped>
.organic-cert-view {
  display: flex;
  gap: 24px;
  padding: 36px 56px;
  width: 100%;
  box-sizing: border-box;
}

/* ── 側邊篩選欄 ── */
.filter-sidebar {
  flex: 0 0 240px;
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 20px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 12px;
  align-self: flex-start;
}

.filter-title {
  font-size: 15px;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0;
}

.filter-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.filter-label {
  font-size: 12px;
  font-weight: 700;
  color: var(--text-muted);
}

.filter-input {
  padding: 8px 12px;
  border-radius: 8px;
  border: 1px solid var(--border);
  font-size: 13px;
  background: var(--surface);
  color: var(--text-primary);
  outline: none;
}

.filter-input:focus { border-color: #43a047; }

/* .filter-hint {
  font-size: 11px;
  color: var(--text-muted);
  line-height: 1.6;
  display: flex;
  gap: 6px;
  align-items: flex-start;
} */
.filter-hint {
  font-size: 12px;
  color: #e65100;
  line-height: 1.6;
  display: flex;
  gap: 6px;
  align-items: flex-start;
  background: #fff3e0;
  border: 1px solid #ffcc80;
  border-radius: 8px;
  padding: 10px 12px;
}

/* ── 右側結果區 ── */
.result-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 16px;
  min-width: 0;
}

.result-header {
  display: flex;
  justify-content: flex-end;
}

.result-count {
  font-size: 12px;
  color: var(--text-muted);
}

/* ── 狀態容器（沿用 ViolationWallView 樣式） ── */
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

/* ── 卡片列表 ── */
.cert-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 16px;
}

.cert-card {
  padding: 16px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

/* 決策：品項可能為多證號合併時，僅用邊框變色提示，不額外加文字標籤 */
.cert-card.ambiguous {
  border: 2px solid #e65100;
}

.cert-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.cert-sn {
  font-family: monospace;
  font-size: 12px;
  color: var(--text-muted);
}

.status-badge {
  padding: 2px 10px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 700;
}

.status-badge.active { background: #e8f5e9; color: #2e7d32; }
/* .status-badge.inactive { background: #f5f5f5; color: #757575; } */
.status-badge.inactive { background: #e0e0e0; color: #424242; }

.cert-operator {
  font-size: 15px;
  font-weight: 700;
  color: var(--text-primary);
}

.cert-row {
  font-size: 12px;
  color: var(--text-primary);
  line-height: 1.5;
}

.cert-row-products {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.cert-row-products .cert-label {
  min-width: auto;
}

.products-text-clamp {
  font-size: 12px;
  color: var(--text-primary);
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.products-list {
  margin: 0;
  padding-left: 18px;
  font-size: 12px;
  color: var(--text-primary);
  line-height: 1.6;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.expand-toggle {
  align-self: flex-start;
  font-size: 12px;
  font-weight: 700;
  padding: 3px 10px;
  border: none;
  border-radius: 999px;
  background: #e8f5e9;
  color: #2e7d32;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 3px;
  transition: background 0.15s;
}

.expand-toggle:hover {
  background: #c8e6c9;
  text-decoration: none;
}

.expand-toggle .mdi {
  font-size: 14px;
  transition: transform 0.15s;
}

.cert-label {
  display: inline-block;
  min-width: 60px;
  color: var(--text-muted);
  font-weight: 700;
}

/* ── 分頁列（跟 ViolationWallView 相同結構） ── */
.pagination-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 12px;
}

.pagination-info { font-size: 12px; color: var(--text-muted); }

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

.page-size-select {
  padding: 5px 10px;
  border-radius: 8px;
  border: 1px solid var(--border);
  font-size: 13px;
  color: var(--text-primary);
  background: var(--surface);
}

.jump-to-page {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-left: 8px;
  padding-left: 8px;
  border-left: 1px solid var(--border);
}

.jump-label { font-size: 12px; color: var(--text-muted); white-space: nowrap; }

.jump-input {
  width: 50px;
  padding: 4px 6px;
  border-radius: 6px;
  border: 1px solid var(--border);
  font-size: 13px;
  text-align: center;
  outline: none;
}

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