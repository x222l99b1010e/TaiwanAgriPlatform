<template>
  <div class="page watchlist-view">
    <PageHeader
      title="監看清單"
      title-en="WATCHLIST"
      subtitle="追蹤指定作物與市場的最新成交均價"
    />

    <!-- ── 新增區 ──────────────────────────────────── -->
    <section class="add-section">
      <h2 class="section-title">新增監看</h2>

      <div class="add-form">
        <!-- MarketType Tab -->
        <div class="field-group">
          <span class="field-label">作物類別</span>
          <div class="segmented">
            <button
              v-for="tab in marketTypeTabs"
              :key="tab.value"
              class="segmented__btn"
              :class="{ 'is-active': selectedMarketType === tab.value }"
              @click="handleTabChange(tab.value)"
            >{{ tab.label }}</button>
          </div>
        </div>

        <!-- 作物搜尋 Autocomplete -->
        <div class="field-group">
          <label class="field-label" for="watchlist-crop-search">作物</label>
          <CropAutocomplete
            class="field-input"
            input-id="watchlist-crop-search"
            :options="currentCrops"
            :disabled="optionsError"
            placeholder="輸入作物名稱，例如：番茄"
            @select="selectCrop"
          />
          <div v-if="selectedCrop" class="selected-crop-tag">
            {{ selectedCrop.cropName }}
            <button :aria-label="`取消選取 ${selectedCrop.cropName}`" @click="clearCrop">
              <span class="mdi mdi-close" />
            </button>
          </div>
        </div>

        <!-- 市場選擇 -->
        <div class="field-group">
          <label class="field-label" for="watchlist-market">市場（選填）</label>
          <select id="watchlist-market" v-model="selectedMarketCode" class="form-control field-select">
            <option :value="null">全台均價</option>
            <option
              v-for="m in markets"
              :key="m.marketCode"
              :value="m.marketCode"
            >{{ m.marketName }}</option>
          </select>
        </div>

        <Btn
          class="btn-add"
          icon="mdi-plus"
          :disabled="!selectedCrop || optionsError"
          :loading="store.isSaving"
          @click="handleAdd"
        >
          {{ store.isSaving ? '新增中...' : '新增' }}
        </Btn>
      </div>

      <!-- 作物或市場清單載不回來時，新增區不能用，但下面「我的監看清單」照樣顯示 -->
      <p v-if="optionsError" class="error-msg">
        作物或市場清單載入失敗，暫時不能新增。
        <button type="button" class="link-btn" @click="loadOptions(selectedMarketType)">重新載入</button>
      </p>
      <p v-if="store.errorMessage" class="error-msg">{{ store.errorMessage }}</p>
    </section>

    <!-- ── 清單區 ──────────────────────────────────── -->
    <section class="list-section">
      <div class="list-header">
        <h2 class="section-title">我的監看清單</h2>
        <Btn
          v-if="selectedIds.length > 0"
          variant="danger"
          size="sm"
          icon="mdi-trash-can-outline"
          :disabled="store.isSaving"
          @click="handleRemove"
        >刪除已選 ({{ selectedIds.length }}{{ selectedIds.length >= 50 ? '，已達上限' : '' }})</Btn>
      </div>

      <div v-if="store.isLoading" class="hint">載入中...</div>
      <StateBlock
        v-else-if="store.loadError"
        state="error"
        :message="store.loadError"
        retryable
        @retry="store.fetchItems"
      />
      <div v-else-if="store.items.length === 0" class="hint">尚無監看項目，請從上方新增</div>

      <div v-else class="item-list">
        <div
          v-for="item in store.items"
          :key="item.id"
          class="item-card"
          :class="{ selected: selectedIds.includes(item.id) }"
          @click="toggleSelect(item.id)"
        >
          <input
            type="checkbox"
            :checked="selectedIds.includes(item.id)"
            @click.stop
            @change="toggleSelect(item.id)"
          />
          <div class="item-info">
            <div class="item-top-row">
              <span class="item-crop">{{ item.cropName }}</span>
              <span class="market-type-badge">{{ marketTypeLabel(item.marketType) }}</span>
            </div>
            <span class="item-market">{{ item.marketName ?? '全台均價' }}</span>
          </div>
          <!-- 動態價格區 -->
          <div class="item-price">
            <!-- 單位寫「元」，跟其他頁的價格一致（原本前面是「$」） -->
            <span class="price-value" v-if="item.avgPrice !== null">
              {{ item.avgPrice?.toFixed(1) }}<span class="price-unit">元</span>
            </span>
            <span class="price-value no-data" v-else>--</span>
            <span class="price-date" v-if="item.transDate">{{ item.transDate }}</span>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import Btn from '@/components/ui/Btn.vue'
import { ref, computed, onMounted } from 'vue'
import { useWatchlistStore } from '@/stores/watchlist'
import { marketApi } from '@/api/market'
import type { CropResponseDto, MarketResponseDto } from '@/api/market'
import type { MarketType } from '@/api/market'
import PageHeader from '@/components/ui/PageHeader.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import CropAutocomplete from '@/components/CropAutocomplete.vue'

const store = useWatchlistStore()

// ─── MarketType Tab ──────────────────────────────────────────────────────────
const marketTypeTabs: { label: string; value: MarketType }[] = [
  { label: '蔬菜', value: 'Veg' },
  { label: '水果', value: 'Fruit' },
  { label: '花卉', value: 'Flower' },
]
const selectedMarketType = ref<MarketType>('Veg')

function marketTypeLabel(type: MarketType): string {
  return marketTypeTabs.find(t => t.value === type)?.label ?? type
}

// ─── 作物 Autocomplete 狀態 ─────────────────────────────────────────────────
const cropsByType = ref<Record<MarketType, CropResponseDto[]>>({
  Veg: [], Fruit: [], Flower: []
})
const selectedCrop = ref<CropResponseDto | null>(null)

// 目前類別的作物清單
const currentCrops = computed(() => cropsByType.value[selectedMarketType.value])

// 作物與市場清單（新增區的選項）載入失敗時，只停用新增區
const optionsError = ref(false)

async function loadOptions(type: MarketType) {
  optionsError.value = false
  try {
    // 作物清單載過就不重抓；市場清單跟著類別換
    if (cropsByType.value[type].length === 0) {
      cropsByType.value[type] = await marketApi.getCrops(type)
    }
    markets.value = await marketApi.getMarkets(type)
  } catch {
    optionsError.value = true
  }
}

async function handleTabChange(type: MarketType) {
  selectedMarketType.value = type
  selectedCrop.value = null
  selectedMarketCode.value = null
  await loadOptions(type)
}

function selectCrop(crop: CropResponseDto) {
  selectedCrop.value = crop
  store.errorMessage = null
}

function clearCrop() {
  selectedCrop.value = null
  store.errorMessage = null
}

// ─── 市場狀態 ────────────────────────────────────────────────────────────────
const markets = ref<MarketResponseDto[]>([])
const selectedMarketCode = ref<string | null>(null)

// ─── 勾選狀態 ────────────────────────────────────────────────────────────────
const selectedIds = ref<number[]>([])

function toggleSelect(id: number) {
  const idx = selectedIds.value.indexOf(id)
  if (idx >= 0) {
    selectedIds.value.splice(idx, 1)
  } else {
    if (selectedIds.value.length >= 50) return
    selectedIds.value.push(id)
  }
}

// ─── 新增 ────────────────────────────────────────────────────────────────────
async function handleAdd() {
  if (!selectedCrop.value) return

  const marketName = markets.value.find(m => m.marketCode === selectedMarketCode.value)?.marketName ?? null

  await store.addItem({
    cropCode: selectedCrop.value.cropCode,
    cropName: selectedCrop.value.cropName,
    marketCode: selectedMarketCode.value,
    marketName: marketName,
    marketType: selectedMarketType.value,
  })

  if (!store.errorMessage) {
    selectedCrop.value = null
    selectedMarketCode.value = null
  }
}

// ─── 刪除 ────────────────────────────────────────────────────────────────────
async function handleRemove() {
  await store.removeItems(selectedIds.value)
  selectedIds.value = []
}

// ─── 初始化 ──────────────────────────────────────────────────────────────────
onMounted(() => {
  // 清單與選項互不相依，同時發出、各自處理失敗。原本先等作物與市場清單，
  // 那兩支一失敗，清單就不載了，畫面顯示「尚無監看項目」——看起來像清單被清空
  void store.fetchItems()
  void loadOptions('Veg') // 預設蔬菜類別
})
</script>

<style scoped>
/* 顏色全部改用 semantic 層；欄位外殼與分段控制器走 base.css。 */

/* 單欄清單：頁面容器維持 .page 的統一寬度，內容自己限寬並靠左 */
.add-section,
.list-section { max-width: var(--container-sm); }

/* ── 新增區 ── */
.add-section {
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--space-6);
  margin-bottom: var(--space-8);
}

.add-form { display: flex; align-items: flex-end; gap: var(--space-4); flex-wrap: wrap; }

.field-input,
.field-select { min-width: 200px; }

/* ── 已選作物（自動完成本身的樣式在 CropAutocomplete 元件裡） ── */
.selected-crop-tag {
  display: inline-flex; align-items: center; gap: var(--space-2);
  margin-top: var(--space-2);
  min-height: var(--control-h-sm);
  padding: 0 var(--space-2) 0 var(--space-3);
  border-radius: var(--radius-full);
  background: var(--color-action-soft-2); color: var(--color-action);
  font-size: var(--text-sm); font-weight: var(--weight-medium);
}
.selected-crop-tag button {
  display: inline-flex; align-items: center; justify-content: center;
  width: 20px; height: 20px;
  background: none; border: none; border-radius: var(--radius-full);
  color: var(--color-action);
  cursor: pointer; font-size: var(--text-base); padding: 0; opacity: 0.7;
  transition: opacity var(--duration-fast) var(--ease-work), background var(--duration-fast) var(--ease-work);
}
.selected-crop-tag button:hover { opacity: 1; background: var(--seed-200); }
.selected-crop-tag button:focus-visible { outline: 2px solid var(--color-action); outline-offset: 1px; }


/* 新增按鈕改用共用的 Btn，這裡只留它在篩選列裡的位置 */
.btn-add { align-self: flex-end; }

.error-msg { font-size: var(--text-sm); color: var(--danger-700); margin-top: var(--space-3); }
.link-btn {
  background: none; border: none; padding: 0;
  color: var(--color-action); font: inherit; text-decoration: underline; cursor: pointer;
}
.link-btn:focus-visible { outline: 2px solid var(--color-action); outline-offset: 2px; }

/* ── 清單區 ── */
.list-section {
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--space-6);
}

.list-header { display: flex; align-items: center; justify-content: space-between; gap: var(--space-4); margin-bottom: var(--space-4); }
.hint { font-size: var(--text-base); color: var(--color-text-dim); text-align: center; padding: var(--space-8) 0; }

.item-list { display: flex; flex-direction: column; gap: var(--space-2); }

/* 這幾列在卡片裡面，所以用比卡片深一階的底色而不是同色＋邊框——
   卡片裡再畫一圈同色邊框會看不出是兩層 */
.item-card {
  display: flex; align-items: center; gap: var(--space-4);
  padding: var(--space-4) var(--space-5); border-radius: var(--radius-md);
  border: var(--border-width) solid transparent;
  background: var(--color-bg-sunken);
  cursor: pointer;
  transition:
    border-color var(--duration-fast) var(--ease-work),
    background var(--duration-fast) var(--ease-work);
}
.item-card:hover { border-color: var(--color-border-strong); }
.item-card.selected { border-color: var(--color-action); background: var(--color-action-soft); }

.item-card input[type="checkbox"] { accent-color: var(--color-action); width: 16px; height: 16px; cursor: pointer; flex-shrink: 0; }

.item-info { flex: 1; display: flex; flex-direction: column; gap: var(--space-1); min-width: 0; }
.item-top-row { display: flex; align-items: center; gap: var(--space-2); }
.item-crop { font-size: var(--text-base); font-weight: var(--weight-bold); color: var(--color-text); }

.market-type-badge {
  font-size: var(--text-2xs); padding: var(--space-1) var(--space-2); border-radius: var(--radius-full);
  background: var(--color-surface); color: var(--color-text-dim);
  border: var(--border-width) solid var(--color-border); font-weight: var(--weight-medium);
}

.item-market { font-size: var(--text-xs); color: var(--color-text-dim); }

/* 價格區。用 --color-brand（品牌綠本體）不是動作色：這是資料強調，不是可點的東西 */
.item-price {
  display: flex; flex-direction: column; align-items: flex-end; gap: var(--space-1);
  min-width: 80px;
}
.price-value {
  font-family: var(--font-num);
  font-size: var(--text-lg); font-weight: var(--weight-bold); color: var(--color-brand);
  font-variant-numeric: tabular-nums;
}
.price-value.no-data { color: var(--color-text-dim); font-weight: var(--weight-normal); }
.price-unit {
  margin-inline-start: var(--space-1);
  font-family: var(--font-body); font-size: var(--text-xs);
  font-weight: var(--weight-normal); color: var(--color-text-dim);
}
.price-date { font-family: var(--font-num); font-size: var(--text-2xs); color: var(--color-text-dim); }
</style>