<template>
  <div class="market-filter">
    <div class="tab-group">
      <button
        v-for="tab in marketTypeTabs"
        :key="tab.value"
        class="tab-btn"
        :class="{ active: store.marketType === tab.value }"
        :disabled="store.isLoadingMarkets || store.isLoadingCrops"
        @click="handleTabChange(tab.value)"
      >{{ tab.label }}</button>
    </div>

    <p v-if="store.error" class="error-msg">{{ store.error }}</p>

    <div class="field-group">
      <label class="field-label">市場</label>
      <div class="select-wrap">
        <select v-model="store.selectedMarketCode" :disabled="store.isLoadingMarkets" class="market-select">
          <option :value="null">全台均價</option>
          <option v-for="market in store.markets" :key="market.marketCode" :value="market.marketCode">
            {{ market.marketName }}
          </option>
        </select>
        <span v-if="store.isLoadingMarkets" class="loading-hint">載入中...</span>
      </div>
    </div>

    <div class="field-group">
      <div class="crop-header">
        <label class="field-label">
          查詢作物
          <span class="badge count-badge" :class="{ full: store.selectedCropCodes.length >= 5 }">
            {{ store.selectedCropCodes.length }} / 5
          </span>
        </label>
        <span v-if="store.isLoadingCrops" class="loading-hint">載入中...</span>
      </div>

      <div class="crop-search-wrap" v-if="!store.isLoadingCrops">
        <input v-model="cropSearch" class="crop-search" placeholder="輸入關鍵字篩選，例如：菊" maxlength="20" />
        <span class="search-count" v-if="cropSearch.trim()">找到 {{ filteredCrops.length }} 項</span>
        <button class="search-clear" v-if="cropSearch.trim()" @click="cropSearch = ''">✕</button>
      </div>

      <div class="crop-container" v-if="!store.isLoadingCrops">
        <div class="crop-list">
          <button
            v-for="crop in filteredCrops"
            :key="crop.cropCode"
            class="crop-btn"
            :class="{
              selected: store.selectedCropCodes.includes(crop.cropCode),
              disabled: store.selectedCropCodes.length >= 5 && !store.selectedCropCodes.includes(crop.cropCode)
            }"
            :disabled="store.selectedCropCodes.length >= 5 && !store.selectedCropCodes.includes(crop.cropCode)"
            @click="toggleCrop(crop.cropCode)"
          >
            <span class="check-dot" v-if="store.selectedCropCodes.includes(crop.cropCode)">✓</span>
            {{ crop.cropName }}
          </button>
          <p class="no-result" v-if="filteredCrops.length === 0">找不到「{{ cropSearch }}」相關作物</p>
        </div>
      </div>

      <p v-if="store.selectedCropCodes.length >= 5" class="limit-hint">已達上限，請先取消再選其他作物</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref, computed } from 'vue'
import { useMarketStore } from '@/stores/market'
import type { MarketType } from '@/api/market'

const store = useMarketStore()
const marketTypeTabs: { label: string; value: MarketType }[] = [
  { label: '蔬菜', value: 'Veg' },
  { label: '水果', value: 'Fruit' },
  { label: '花卉', value: 'Flower' },
]

async function handleTabChange(type: MarketType) {
  if (type === store.marketType) return
  await store.setMarketType(type)
}

const cropSearch = ref('')
const filteredCrops = computed(() =>
  cropSearch.value.trim() === ''
    ? store.crops
    : store.crops.filter(c => c.cropName.includes(cropSearch.value.trim()))
)

function toggleCrop(cropCode: string) {
  const idx = store.selectedCropCodes.indexOf(cropCode)
  if (idx >= 0) store.selectedCropCodes.splice(idx, 1)
  else if (store.selectedCropCodes.length < 5) store.selectedCropCodes.push(cropCode)
}

onMounted(() => store.initialize())
</script>

<style scoped>
.market-filter { display: flex; flex-direction: column; gap: var(--space-5); }

/* Tab */
.tab-group { display: flex; gap: var(--space-2); }
.tab-btn {
  padding: var(--space-2) var(--space-5); border-radius: var(--radius-md);
  border: 1px solid var(--neutral-200);
  background: var(--neutral-0);
  color: var(--neutral-500);
  font-size: var(--text-sm); cursor: pointer;
  transition: all var(--duration-fast);
}
.tab-btn:hover:not(:disabled) {
  border-color: var(--green-600); color: var(--green-600); background: var(--green-50);
}
.tab-btn.active {
  background: var(--green-100); border-color: var(--green-600);
  color: var(--green-600); font-weight: var(--weight-medium);
}
.tab-btn:disabled { opacity: 0.4; cursor: not-allowed; }

/* 欄位 */
.field-group { display: flex; flex-direction: column; gap: var(--space-2); }
/* field-label 深一點 */
.field-label {
  font-size: var(--text-xs); color: var(--neutral-500);  /* 從 text-muted 改 text-secondary */
  letter-spacing: 0.06em; text-transform: uppercase;
  display: flex; align-items: center; gap: var(--space-2);
  font-weight: var(--weight-bold);  /* 加粗 */
}

/* 市場下拉 */
.select-wrap { display: flex; align-items: center; gap: var(--space-3); }
.market-select {
  padding: var(--space-2) var(--space-3); background: var(--neutral-0);
  border: 1px solid var(--neutral-200); border-radius: var(--radius-md);
  color: var(--neutral-900); font-size: var(--text-sm);
  min-width: 200px; cursor: pointer;
  transition: border-color var(--duration-fast), box-shadow var(--duration-fast);
}
.market-select:focus {
  outline: none; border-color: var(--green-600);
  box-shadow: var(--shadow-focus);
}

/* 標籤外殼已收進 base.css 的 .badge，這裡只留語意色 */
.count-badge {
  background: var(--teal-500);
  color: var(--neutral-0);
  letter-spacing: 0;
  text-transform: none;
  box-shadow: var(--shadow-sm);
}
.count-badge.full {
  background: var(--warning-700);
  color: var(--neutral-0);
  box-shadow: var(--shadow-sm);
}

/* Crop */
.crop-header { display: flex; align-items: center; justify-content: space-between; }
.crop-list { display: flex; flex-wrap: wrap; gap: var(--space-2); }

.crop-btn {
  display: inline-flex; align-items: center; gap: var(--space-1);
  padding: var(--space-1) var(--space-3); border-radius: var(--radius-full);
  border: 1px solid var(--neutral-200);
  background: var(--neutral-0);
  color: var(--neutral-500);
  font-size: var(--text-sm); cursor: pointer; transition: all var(--duration-fast);
  white-space: nowrap;
}
.crop-btn:hover:not(.disabled) {
  border-color: var(--green-600); color: var(--green-600); background: var(--green-50);
}
.crop-btn.selected {
  background: var(--green-100); border-color: var(--green-600); color: var(--green-600);
}
.crop-btn.disabled { opacity: 0.35; cursor: not-allowed; }
.check-dot { font-size: var(--text-2xs); font-weight: var(--weight-bold); }

.loading-hint { font-size: var(--text-xs); color: var(--neutral-400); }
.limit-hint { font-size: var(--text-2xs); color: var(--warning-700); }
.error-msg  { font-size: var(--text-sm); color: var(--danger-500); }

.crop-container {
  max-height: 200px; overflow-y: auto; overflow-x: hidden;
  border: 1px solid var(--neutral-200); border-radius: var(--radius-lg);
  padding: var(--space-3); background: var(--neutral-50);
  scrollbar-width: thin; scrollbar-color: var(--neutral-300) transparent;
}
.crop-container::-webkit-scrollbar { width: 5px; }
.crop-container::-webkit-scrollbar-track { background: transparent; }
.crop-container::-webkit-scrollbar-thumb { background: var(--neutral-300); border-radius: var(--radius-full); }

.crop-search-wrap { display: flex; align-items: center; gap: var(--space-2); margin-bottom: var(--space-1); }
.crop-search {
  flex: 1; padding: var(--space-2) var(--space-3);
  background: var(--neutral-0); border: 1px solid var(--neutral-200);
  border-radius: var(--radius-md); color: var(--neutral-900); font-size: var(--text-sm);
  transition: border-color var(--duration-fast), box-shadow var(--duration-fast);
}
.crop-search::placeholder { color: var(--neutral-400); }
.crop-search:focus { outline: none; border-color: var(--green-600); box-shadow: var(--shadow-focus); }

.search-count { font-size: var(--text-xs); color: var(--teal-500); white-space: nowrap; }
.search-clear {
  padding: var(--space-1) var(--space-2); background: transparent;
  border: 1px solid var(--neutral-200); border-radius: var(--radius-md);
  color: var(--neutral-400); font-size: var(--text-xs); cursor: pointer;
  transition: all var(--duration-fast);
}
.search-clear:hover { background: var(--neutral-50); color: var(--neutral-900); }
.no-result { font-size: var(--text-sm); color: var(--neutral-400); padding: var(--space-2) var(--space-1); margin: 0; }
</style>