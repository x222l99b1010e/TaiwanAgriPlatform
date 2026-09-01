<template>
  <div class="page watchlist-view">
    <PageHeader
      title="監看清單"
      subtitle="追蹤指定作物與市場的最新成交價，價格更新時可在通知中看到"
    />

    <!-- ── 新增區 ──────────────────────────────────── -->
    <section class="add-section">
      <h2 class="section-title">新增監看</h2>

      <div class="add-form">
        <!-- MarketType Tab -->
        <div class="field-group">
          <label class="field-label">作物類別</label>
          <div class="tab-group">
            <button
              v-for="tab in marketTypeTabs"
              :key="tab.value"
              class="tab-btn"
              :class="{ active: selectedMarketType === tab.value }"
              @click="handleTabChange(tab.value)"
            >{{ tab.label }}</button>
          </div>
        </div>

        <!-- 作物搜尋 Autocomplete -->
        <div class="field-group">
          <label class="field-label">作物</label>
          <div class="autocomplete-wrapper">
            <input
              v-model="cropSearchText"
              @input="onCropInput"
              @blur="onBlur"
              class="field-input"
              placeholder="輸入作物名稱，例如：番茄"
            />
            <div v-if="selectedCrop" class="selected-crop-tag">
              {{ selectedCrop.cropName }}
              <button @click="clearCrop">✕</button>
            </div>
            <div class="autocomplete-dropdown" v-if="showCropDropdown">
              <div
                v-for="crop in filteredCrops"
                :key="crop.cropCode"
                class="autocomplete-item"
                @mousedown="selectCrop(crop)"
              >
                {{ crop.cropName }}
                <span class="crop-code">{{ crop.cropCode }}</span>
              </div>
            </div>
          </div>
        </div>

        <!-- 市場選擇 -->
        <div class="field-group">
          <label class="field-label">市場（選填）</label>
          <select v-model="selectedMarketCode" class="field-select">
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
          :disabled="!selectedCrop"
          :loading="store.isSaving"
          @click="handleAdd"
        >
          {{ store.isSaving ? '新增中...' : '新增' }}
        </Btn>
      </div>

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
            <span class="price-value" v-if="item.avgPrice !== null">
              ${{ item.avgPrice?.toFixed(1) }}
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
import type { MarketType } from '@/api/watchlist'
import PageHeader from '@/components/ui/PageHeader.vue'

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
const cropSearchText = ref('')
const showCropDropdown = ref(false)
const selectedCrop = ref<CropResponseDto | null>(null)

// 目前類別的作物清單
const currentCrops = computed(() => cropsByType.value[selectedMarketType.value])

const filteredCrops = computed(() => {
  if (!cropSearchText.value.trim()) return []
  return currentCrops.value
    .filter(c => c.cropName.includes(cropSearchText.value.trim()))
    .slice(0, 10)
})

async function handleTabChange(type: MarketType) {
  selectedMarketType.value = type
  selectedCrop.value = null
  cropSearchText.value = ''
  // 若該類別尚未載入才去 fetch
  if (cropsByType.value[type].length === 0) {
    cropsByType.value[type] = await marketApi.getCrops(type)
  }
  // 市場清單也跟著換
  markets.value = await marketApi.getMarkets(type)
  selectedMarketCode.value = null
}

function onCropInput() {
  showCropDropdown.value = filteredCrops.value.length > 0
}

function onBlur() {
  setTimeout(() => { showCropDropdown.value = false }, 150)
}

function selectCrop(crop: CropResponseDto) {
  selectedCrop.value = crop
  cropSearchText.value = ''
  showCropDropdown.value = false
  store.errorMessage = null
}

function clearCrop() {
  selectedCrop.value = null
  cropSearchText.value = ''
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
onMounted(async () => {
  // 預設載入蔬菜類別
  cropsByType.value['Veg'] = await marketApi.getCrops('Veg')
  markets.value = await marketApi.getMarkets('Veg')
  await store.fetchItems()
})
</script>

<style scoped>
/* 單欄清單：頁面容器維持 .page 的統一寬度，內容自己限寬並靠左 */
.add-section,
.list-section { max-width: var(--container-sm); }
.section-title {
  font-size: var(--text-base); font-weight: var(--weight-bold);
  color: var(--neutral-500);
  letter-spacing: 0.06em; text-transform: uppercase;
  margin-bottom: var(--space-4);
}

/* ── 新增區 ── */
.add-section {
  background: var(--neutral-0); border: 1px solid var(--neutral-200);
  border-radius: var(--radius-lg); padding: var(--space-6); margin-bottom: var(--space-8);
  box-shadow: var(--shadow-md);
}

.add-form { display: flex; align-items: flex-end; gap: var(--space-4); flex-wrap: wrap; }

.field-group { display: flex; flex-direction: column; gap: var(--space-2); }

.field-label {
  font-size: var(--text-xs); color: var(--neutral-400); font-weight: var(--weight-medium);
  letter-spacing: 0.05em; text-transform: uppercase;
}

/* Tab */
.tab-group { display: flex; gap: var(--space-2); }
.tab-btn {
  padding: var(--space-2) var(--space-4); border-radius: var(--radius-md);
  border: 1px solid var(--neutral-200);
  background: var(--neutral-0); color: var(--neutral-500);
  font-size: var(--text-sm); cursor: pointer; transition: all var(--duration-fast);
}
.tab-btn:hover { border-color: var(--green-600); color: var(--green-600); background: var(--green-50); }
.tab-btn.active { background: var(--green-100); border-color: var(--green-600); color: var(--green-600); font-weight: var(--weight-medium); }

.field-input {
  padding: var(--space-2) var(--space-4); border: 1px solid var(--neutral-200); border-radius: var(--radius-md);
  background: var(--neutral-0); color: var(--neutral-900); font-size: var(--text-base);
  min-width: 200px;
  transition: border-color var(--duration-fast), box-shadow var(--duration-fast);
}
.field-input:focus { outline: none; border-color: var(--green-600); box-shadow: var(--shadow-focus); }

.field-select {
  padding: var(--space-2) var(--space-4); border: 1px solid var(--neutral-200); border-radius: var(--radius-md);
  background: var(--neutral-0); color: var(--neutral-900); font-size: var(--text-base);
  min-width: 200px; cursor: pointer;
  transition: border-color var(--duration-fast), box-shadow var(--duration-fast);
}
.field-select:focus { outline: none; border-color: var(--green-600); box-shadow: var(--shadow-focus); }

/* Autocomplete */
.autocomplete-wrapper { position: relative; }

.selected-crop-tag {
  display: inline-flex; align-items: center; gap: var(--space-2);
  margin-top: var(--space-2); padding: var(--space-1) var(--space-3); border-radius: var(--radius-full);
  background: var(--green-100); color: var(--green-600);
  font-size: var(--text-sm); font-weight: var(--weight-medium);
}
.selected-crop-tag button {
  background: none; border: none; color: var(--green-600);
  cursor: pointer; font-size: var(--text-xs); padding: 0; opacity: 0.7;
}
.selected-crop-tag button:hover { opacity: 1; }

.autocomplete-dropdown {
  position: absolute; top: 100%; left: 0; right: 0;
  background: var(--neutral-0); border: 1px solid var(--neutral-200);
  border-radius: var(--radius-md); box-shadow: var(--shadow-md);
  z-index: var(--z-dropdown); max-height: 240px; overflow-y: auto;
}

.autocomplete-item {
  display: flex; justify-content: space-between; align-items: center;
  padding: var(--space-3) var(--space-4); cursor: pointer; font-size: var(--text-base); color: var(--neutral-900);
}
.autocomplete-item:hover { background: var(--green-50); }
.crop-code { font-size: var(--text-xs); color: var(--neutral-400); }

/* 新增按鈕改用共用的 Btn，這裡只留它在篩選列裡的位置 */
.btn-add { align-self: flex-end; }

.error-msg { font-size: var(--text-sm); color: var(--danger-500); margin-top: var(--space-3); }

/* ── 清單區 ── */
.list-section {
  background: var(--neutral-0); border: 1px solid var(--neutral-200);
  border-radius: var(--radius-lg); padding: var(--space-6);
  box-shadow: var(--shadow-md);
}

.list-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: var(--space-4); }
.hint { font-size: var(--text-base); color: var(--neutral-400); text-align: center; padding: var(--space-8) 0; }

.item-list { display: flex; flex-direction: column; gap: var(--space-2); }

.item-card {
  display: flex; align-items: center; gap: var(--space-4);
  padding: var(--space-4) var(--space-5); border-radius: var(--radius-lg);
  border: 1px solid var(--neutral-200); background: var(--neutral-0);
  cursor: pointer; transition: all var(--duration-fast);
  box-shadow: var(--shadow-sm);
}
.item-card:hover { border-color: var(--green-600); background: var(--green-50); }
.item-card.selected { border-color: var(--green-600); background: var(--green-100); }

.item-card input[type="checkbox"] { accent-color: var(--green-600); width: 16px; height: 16px; cursor: pointer; flex-shrink: 0; }

.item-info { flex: 1; display: flex; flex-direction: column; gap: var(--space-1); }

.item-top-row { display: flex; align-items: center; gap: var(--space-2); }

.item-crop { font-size: var(--text-base); font-weight: var(--weight-bold); color: var(--neutral-900); }

.market-type-badge {
  font-size: var(--text-2xs); padding: var(--space-1) var(--space-2); border-radius: var(--radius-full);
  background: var(--green-100); color: var(--green-600);
  border: 1px solid var(--green-200); font-weight: var(--weight-medium);
}

.item-market { font-size: var(--text-xs); color: var(--neutral-400); }

/* 價格區 */
.item-price {
  display: flex; flex-direction: column; align-items: flex-end; gap: var(--space-1);
  min-width: 80px;
}
.price-value {
  font-size: var(--text-base); font-weight: var(--weight-bold); color: var(--green-600);
  font-variant-numeric: tabular-nums;
}
.price-value.no-data { color: var(--neutral-400); font-weight: var(--weight-normal); }
.price-date { font-size: var(--text-2xs); color: var(--neutral-400); }
</style>