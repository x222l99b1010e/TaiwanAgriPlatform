<template>
  <div class="page organic-cert-view">
    <QueryLayout
      title="有機驗證查詢"
      title-en="ORGANIC CERTS"
      subtitle="有機農產品驗證證書的有效狀態、驗證機構與品項範圍"
    >
      <!-- 這一頁沒有查詢按鈕：三個關鍵字都是打字即查（onFilterChange 有 debounce），
           所以動作插槽放的是「目前有幾筆」與清除條件，不是送出。 -->
      <template #actions>
        <span class="result-count" v-if="store.organicCertPage">
          共 {{ store.organicCertPage.totalCount }} 筆
        </span>
        <Btn
          v-if="hasAnyFilter"
          variant="secondary"
          icon="mdi-filter-remove-outline"
          @click="clearFilters"
        >清除條件</Btn>
      </template>

      <!-- 原本這三個欄位在左側 240px 的側邊欄裡，是全站唯一一頁把查詢條件放左邊的。
           查詢條件的位置每頁不同，使用者換一頁就要重新找一次——改成跟其餘查詢頁
           同一個位置（頂部、吸頂）。 -->
      <template #filters>
        <div class="field-group cert-field">
          <label class="field-label" for="cert-operator">業者名稱</label>
          <input
            id="cert-operator"
            v-model="operatorName"
            type="text"
            class="form-control"
            placeholder="輸入業者名稱關鍵字"
            @input="onFilterChange"
          />
        </div>

        <div class="field-group cert-field">
          <label class="field-label" for="cert-body">驗證機構</label>
          <input
            id="cert-body"
            v-model="verificationBodyName"
            type="text"
            class="form-control"
            placeholder="輸入驗證機構名稱關鍵字"
            @input="onFilterChange"
          />
        </div>

        <div class="field-group cert-field">
          <label class="field-label" for="cert-product">品項關鍵字</label>
          <input
            id="cert-product"
            v-model="productKeyword"
            type="text"
            class="form-control"
            placeholder="輸入作物或產品名稱"
            @input="onFilterChange"
          />
        </div>
      </template>

      <!-- 用 warning 語氣（柿橙色條＋淡暖底）讓這則判讀提醒更醒目——而且色條的橙剛好
           跟下方 .ambiguous 卡片的邊框同色，提示與它在講的那種卡片就對得起來。 -->
      <template #hint>
        <HintBox tone="warning" title="資料判讀提醒">
          邊框帶色的卡片，代表品項資料可能來自多證號合併，數字請自行核對。
        </HintBox>
      </template>

      <template #results>
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
          hint="可以把上方的關鍵字放寬或清空再查一次"
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
              <span class="badge status-badge" :class="statusClass(item.status)">
                {{ item.status }}
              </span>
            </div>

            <div class="cert-operator">{{ item.operatorName }}</div>

            <div class="cert-row">
              <span class="cert-label">驗證機構</span>{{ item.verificationBodyName }}
            </div>
            <div class="cert-row cert-row-products">
              <span class="cert-label products-label">
                品項範圍
                <!-- 橙色邊框只是視覺線索，一般人不會把「橙框」聯想成「待核對」；
                     這裡補一句明講的小旗標，把邊框的含意直接寫出來（owner 2026-09-04） -->
                <span v-if="item.hasAmbiguousProductMapping" class="ambiguous-flag">
                  <span class="mdi mdi-alert-outline" />多證號合併・請核對
                </span>
              </span>

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

        <!-- 分頁列改用共用的 PagerBar。原本的註解寫著「沿用 ViolationWallView 的模式」，
             但沿用的是「複製一份」不是「共用一份」——那條分頁列在兩個檔案裡各有一份。 -->
        <PagerBar
          v-if="store.organicCertPage"
          :current-page="currentPage"
          :total-pages="store.organicCertPage.totalPages"
          :total-count="store.organicCertPage.totalCount"
          :visible-pages="visiblePages"
          v-model:jump-page-input="jumpPageInput"
          :page-size="pageSize"
          :page-size-options="pageSizeOptions"
          @change="changePage"
          @jump="handleJumpPage"
          @update:page-size="setPageSize"
        />
      </template>
    </QueryLayout>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, onMounted } from 'vue'
import { useFoodSafetyStore } from '@/stores/foodSafety'
import { usePagination } from '@/composables/usePagination'
import type { OrganicCertificationQueryParams, OrganicCertificationResult } from '@/api/foodSafety'
import QueryLayout from '@/components/layouts/QueryLayout.vue'
import PagerBar from '@/components/PagerBar.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import HintBox from '@/components/ui/HintBox.vue'
import Btn from '@/components/ui/Btn.vue'

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
  setPageSize,
} = usePagination({
  storageKey: 'organicCert.pageSize',
  totalPages: () => store.organicCertPage?.totalPages,
  onChange: fetchImmediate,
})

/** 有沒有任何一個關鍵字有值——決定要不要出現「清除條件」 */
const hasAnyFilter = computed(
  () => !!(operatorName.value || verificationBodyName.value || productKeyword.value),
)

function clearFilters() {
  operatorName.value = ''
  verificationBodyName.value = ''
  productKeyword.value = ''
  onFilterChange()
}

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
/* 顏色全部改用 semantic 層（style tile §九）。
   側邊篩選欄整個拿掉、分頁列整條換成共用的 PagerBar，所以原本 300 行的樣式
   （.filter-sidebar／.pagination-*／.page-btn／.jump-*）在這裡全部消失，
   只剩下這一頁真正獨有的：卡片本身。 */

/* 三個關鍵字欄位在頂部橫排，各自可伸縮但不要窄到看不出 placeholder */
.cert-field { flex: 1 1 220px; min-width: 0; }

.result-count {
  font-family: var(--font-num);
  font-size: var(--text-xs);
  color: var(--color-text-dim);
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

/* ── 卡片列表 ── */
.cert-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: var(--space-4);
}

.cert-card {
  padding: var(--space-4);
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  transition: border-color var(--duration-fast) var(--ease-work);
}
.cert-card:hover { border-color: var(--color-border-strong); }

/* 品項可能為多證號合併時的提示。
   ⚠ 不能用 `border: 2px` 加粗——比其餘卡片多 1px 會讓整張卡片位移半像素、在網格裡
   跟鄰居對不齊（這是原本從 2px 改回 1px 的原因）。改用 inset box-shadow 畫一道 4px 的
   橙色左邊條：它不佔盒模型、不影響對齊，但比 1px 邊框顯眼得多（owner 2026-09-04
   回報 1px 橙框「細心才看得到」）。邊框本身也換成更實的柿橙填色。 */
.cert-card.ambiguous {
  border-color: var(--color-accent-2-fill);
  box-shadow: inset 4px 0 0 var(--color-accent-2-fill);
}

/* 品項範圍那一行的小旗標：把橙框的含意明說出來。用 warning 語氣的暖色，
   跟卡片左邊條同一個色系，兩個線索指向同一件事。 */
.products-label { display: inline-flex; align-items: center; gap: var(--space-2); flex-wrap: wrap; }
.ambiguous-flag {
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
  padding: 2px var(--space-2);
  border-radius: var(--radius-sm);
  background: var(--warning-50);
  color: var(--warning-700);
  font-size: var(--text-2xs);
  font-weight: var(--weight-bold);
  letter-spacing: 0;
  text-transform: none;
}
.ambiguous-flag .mdi { font-size: var(--text-sm); }

.cert-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--space-2);
}

.cert-sn {
  font-family: var(--font-num);
  font-size: var(--text-xs);
  color: var(--color-text-dim);
}

/* 標籤外殼已收進 base.css 的 .badge，這裡只留語意色 */
.status-badge.active { background: var(--color-action-soft-2); color: var(--color-action); }
.status-badge.inactive { background: var(--color-bg-sunken); color: var(--color-text-dim); }

.cert-operator {
  font-size: var(--text-base);
  font-weight: var(--weight-bold);
  color: var(--color-text);
}

.cert-row {
  font-size: var(--text-xs);
  color: var(--color-text);
  line-height: var(--leading-normal);
}

.cert-row-products {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
}

.cert-row-products .cert-label { min-width: auto; }

.products-text-clamp {
  font-size: var(--text-xs);
  color: var(--color-text);
  line-height: var(--leading-normal);
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.products-list {
  margin: 0;
  padding-left: var(--space-5);
  font-size: var(--text-xs);
  color: var(--color-text);
  line-height: var(--leading-normal);
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
}

/* 展開／收合是卡片內的次要動作，做成低調的文字鈕而不是實心色塊——
   一整頁十幾張卡片各有一顆綠色藥丸時，最先被看到的會是這些按鈕而不是資料 */
.expand-toggle {
  align-self: flex-start;
  font-family: inherit;
  font-size: var(--text-xs);
  font-weight: var(--weight-medium);
  padding: var(--space-1) 0;
  border: none;
  background: none;
  color: var(--color-action);
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
  transition: color var(--duration-fast) var(--ease-work);
}
.expand-toggle:hover { color: var(--color-action-hover); text-decoration: underline; }
.expand-toggle:focus-visible { outline: 2px solid var(--color-action); outline-offset: 2px; border-radius: var(--radius-sm); }
.expand-toggle .mdi { font-size: var(--text-base); }

.cert-label {
  display: inline-block;
  min-width: 60px;
  color: var(--color-text-dim);
  font-weight: var(--weight-medium);
}
</style>