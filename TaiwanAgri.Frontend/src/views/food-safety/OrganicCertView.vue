<template>
  <div class="page organic-cert-view">
    <PageHeader
      title="有機驗證查詢"
      subtitle="有機農產品驗證證書的有效狀態、驗證機構與品項範圍"
    />

    <div class="cert-layout">
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

        <StateBlock v-if="store.isLoadingOrganicCert" state="loading" message="資料載入中..." />
        <StateBlock
          v-else-if="store.organicCertError"
          state="error"
          :message="store.organicCertError"
          retryable
          @retry="fetchImmediate"
        />
        <StateBlock
          v-else-if="store.organicCertPage && store.organicCertPage.items.length === 0"
          state="empty"
          icon="mdi-file-search-outline"
          message="查無符合條件的驗證紀錄"
          hint="可以把左側的關鍵字放寬或清空再查一次"
        />

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
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useFoodSafetyStore } from '@/stores/foodSafety'
import { usePagination } from '@/composables/usePagination'
import type { OrganicCertificationQueryParams, OrganicCertificationResult } from '@/api/foodSafety'
import PageHeader from '@/components/ui/PageHeader.vue'
import StateBlock from '@/components/ui/StateBlock.vue'

const store = useFoodSafetyStore()

// ─── 篩選狀態 ───────────────────────────────────────────────
const operatorName = ref('')
const verificationBodyName = ref('')
const productKeyword = ref('')

// 分頁控制邏輯共用（與 ViolationWallView 同一份 composable）；
// pageSize 一併記憶 localStorage，統一先前兩頁不一致的行為
const {
  pageSizeOptions,
  pageSize,
  currentPage,
  jumpPageInput,
  visiblePages,
  changePage,
  handleJumpPage,
  handlePageSizeChange,
} = usePagination({
  storageKey: 'organicCert.pageSize',
  totalPages: () => store.organicCertPage?.totalPages,
  onChange: fetchImmediate,
})

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
/* 頁首在最上方，左側篩選欄與右側結果區包在 .cert-layout 裡——
   兩欄排版原本掛在頁面根元素上，導致這頁沒有地方可以放頁首。 */
.cert-layout {
  display: flex;
  gap: var(--space-6);
}

/* ── 側邊篩選欄 ── */
.filter-sidebar {
  flex: 0 0 240px;
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  padding: var(--space-5);
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  align-self: flex-start;
}

.filter-title {
  font-size: var(--text-base);
  font-weight: var(--weight-bold);
  color: var(--text-primary);
  margin: 0;
}

.filter-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.filter-label {
  font-size: var(--text-xs);
  font-weight: var(--weight-bold);
  color: var(--text-muted);
}

.filter-input {
  padding: var(--space-2) var(--space-3);
  border-radius: var(--radius-md);
  border: 1px solid var(--border);
  font-size: var(--text-sm);
  background: var(--surface);
  color: var(--text-primary);
  outline: none;
}

.filter-input:focus { border-color: var(--green-500); }

/* .filter-hint {
  font-size: var(--text-2xs);
  color: var(--text-muted);
  line-height: var(--leading-normal);
  display: flex;
  gap: 6px;
  align-items: flex-start;
} */
.filter-hint {
  font-size: var(--text-xs);
  color: var(--warning-500);
  line-height: var(--leading-normal);
  display: flex;
  gap: 6px;
  align-items: flex-start;
  background: var(--warning-50);
  border: 1px solid var(--warning-100);
  border-radius: var(--radius-md);
  padding: 10px var(--space-3);
}

/* ── 右側結果區 ── */
.result-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}

.result-header {
  display: flex;
  justify-content: flex-end;
}

.result-count {
  font-size: var(--text-xs);
  color: var(--text-muted);
}

/* ── 狀態容器（沿用 ViolationWallView 樣式） ── */

/* ── 卡片列表 ── */
.cert-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: var(--space-4);
}

.cert-card {
  padding: var(--space-4);
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

/* 決策：品項可能為多證號合併時，僅用邊框變色提示，不額外加文字標籤 */
.cert-card.ambiguous {
  border: 2px solid var(--warning-500);
}

.cert-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.cert-sn {
  font-family: monospace;
  font-size: var(--text-xs);
  color: var(--text-muted);
}

.status-badge {
  padding: 2px 10px;
  border-radius: var(--radius-full);
  font-size: var(--text-2xs);
  font-weight: var(--weight-bold);
}

.status-badge.active { background: var(--green-100); color: var(--green-600); }
/* .status-badge.inactive { background: var(--neutral-100); color: var(--neutral-500); } */
.status-badge.inactive { background: var(--neutral-200); color: var(--neutral-700); }

.cert-operator {
  font-size: var(--text-base);
  font-weight: var(--weight-bold);
  color: var(--text-primary);
}

.cert-row {
  font-size: var(--text-xs);
  color: var(--text-primary);
  line-height: 1.5;
}

.cert-row-products {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
}

.cert-row-products .cert-label {
  min-width: auto;
}

.products-text-clamp {
  font-size: var(--text-xs);
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
  font-size: var(--text-xs);
  color: var(--text-primary);
  line-height: var(--leading-normal);
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.expand-toggle {
  align-self: flex-start;
  font-size: var(--text-xs);
  font-weight: var(--weight-bold);
  padding: 3px 10px;
  border: none;
  border-radius: var(--radius-full);
  background: var(--green-100);
  color: var(--green-600);
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 3px;
  transition: background 0.15s;
}

.expand-toggle:hover {
  background: var(--green-200);
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
  font-weight: var(--weight-bold);
}

/* ── 分頁列（跟 ViolationWallView 相同結構） ── */
.pagination-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: var(--space-3);
}

.pagination-info { font-size: var(--text-xs); color: var(--text-muted); }

.pagination-controls {
  display: flex;
  align-items: center;
  gap: var(--space-1);
}

.page-size-group {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-right: var(--space-2);
  padding-right: var(--space-2);
  border-right: 1px solid var(--border);
}

.page-size-select {
  padding: 5px 10px;
  border-radius: var(--radius-md);
  border: 1px solid var(--border);
  font-size: var(--text-sm);
  color: var(--text-primary);
  background: var(--surface);
}

.jump-to-page {
  display: flex;
  align-items: center;
  gap: var(--space-1);
  margin-left: var(--space-2);
  padding-left: var(--space-2);
  border-left: 1px solid var(--border);
}

.jump-label { font-size: var(--text-xs); color: var(--text-muted); white-space: nowrap; }

.jump-input {
  width: 50px;
  padding: var(--space-1) 6px;
  border-radius: 6px;
  border: 1px solid var(--border);
  font-size: var(--text-sm);
  text-align: center;
  outline: none;
}

.jump-btn {
  padding: var(--space-1) 10px;
  border-radius: 6px;
  border: 1px solid var(--green-600);
  background: var(--green-600);
  color: var(--neutral-0);
  font-size: var(--text-xs);
  font-weight: var(--weight-bold);
  cursor: pointer;
}
.jump-btn:hover { background: var(--green-500); }

.page-btn {
  min-width: 32px;
  height: 32px;
  padding: 0 var(--space-2);
  border-radius: var(--radius-md);
  border: 1px solid var(--border);
  background: var(--surface);
  color: var(--text-primary);
  font-size: var(--text-sm);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}

.page-btn:hover:not(:disabled) { border-color: var(--green-500); color: var(--green-600); }
.page-btn:disabled { opacity: 0.4; cursor: not-allowed; }

.page-btn.active {
  background: var(--green-600);
  border-color: var(--green-600);
  color: var(--neutral-0);
}
</style>