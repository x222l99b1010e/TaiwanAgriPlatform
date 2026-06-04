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
          <span class="count-badge" :class="{ full: store.selectedCropCodes.length >= 5 }">
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
.market-filter { display: flex; flex-direction: column; gap: 20px; }

/* Tab */
.tab-group { display: flex; gap: 6px; }
.tab-btn {
  padding: 7px 18px; border-radius: 8px;
  border: 1px solid var(--border);
  background: var(--surface);
  color: var(--text-secondary);
  font-size: 13.5px; cursor: pointer;
  transition: all 0.18s;
}
.tab-btn:hover:not(:disabled) {
  border-color: var(--green); color: var(--green); background: #f0f7f0;
}
.tab-btn.active {
  background: #e8f5e9; border-color: var(--green);
  color: var(--green); font-weight: 600;
}
.tab-btn:disabled { opacity: 0.4; cursor: not-allowed; }

/* 欄位 */
.field-group { display: flex; flex-direction: column; gap: 8px; }
/* field-label 深一點 */
.field-label {
  font-size: 12px; color: var(--text-secondary);  /* 從 text-muted 改 text-secondary */
  letter-spacing: 0.06em; text-transform: uppercase;
  display: flex; align-items: center; gap: 8px;
  font-weight: 700;  /* 加粗 */
}

/* 市場下拉 */
.select-wrap { display: flex; align-items: center; gap: 10px; }
.market-select {
  padding: 8px 12px; background: var(--surface);
  border: 1px solid var(--border); border-radius: 8px;
  color: var(--text-primary); font-size: 13.5px;
  min-width: 200px; cursor: pointer;
  transition: border-color 0.18s, box-shadow 0.18s;
}
.market-select:focus {
  outline: none; border-color: var(--green);
  box-shadow: 0 0 0 3px rgba(46,125,50,0.12);
}

/* badge 深色立體 */
.count-badge {
  font-size: 11px; padding: 2px 9px; border-radius: 999px;
  background: #00897b;
  color: white;
  font-weight: 600; letter-spacing: 0; text-transform: none;
  box-shadow: 0 1px 3px rgba(0,137,123,0.35);
}
.count-badge.full {
  background: #bf360c;
  color: white;
  box-shadow: 0 1px 3px rgba(191,54,12,0.35);
}

/* Crop */
.crop-header { display: flex; align-items: center; justify-content: space-between; }
.crop-list { display: flex; flex-wrap: wrap; gap: 7px; }

.crop-btn {
  display: inline-flex; align-items: center; gap: 4px;
  padding: 5px 12px; border-radius: 999px;
  border: 1px solid var(--border);
  background: var(--surface);
  color: var(--text-secondary);
  font-size: 13px; cursor: pointer; transition: all 0.16s;
  white-space: nowrap;
}
.crop-btn:hover:not(.disabled) {
  border-color: var(--green); color: var(--green); background: #f0f7f0;
}
.crop-btn.selected {
  background: #e8f5e9; border-color: var(--green); color: var(--green);
}
.crop-btn.disabled { opacity: 0.35; cursor: not-allowed; }
.check-dot { font-size: 11px; font-weight: 700; }

.loading-hint { font-size: 12px; color: var(--text-muted); }
.limit-hint { font-size: 11.5px; color: var(--orange); }
.error-msg  { font-size: 13px; color: var(--red); }

.crop-container {
  max-height: 200px; overflow-y: auto; overflow-x: hidden;
  border: 1px solid var(--border); border-radius: 10px;
  padding: 12px; background: var(--surface-2);
  scrollbar-width: thin; scrollbar-color: rgba(0,0,0,0.15) transparent;
}
.crop-container::-webkit-scrollbar { width: 5px; }
.crop-container::-webkit-scrollbar-track { background: transparent; }
.crop-container::-webkit-scrollbar-thumb { background: rgba(0,0,0,0.15); border-radius: 999px; }

.crop-search-wrap { display: flex; align-items: center; gap: 8px; margin-bottom: 4px; }
.crop-search {
  flex: 1; padding: 7px 12px;
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 8px; color: var(--text-primary); font-size: 13.5px;
  transition: border-color 0.18s, box-shadow 0.18s;
}
.crop-search::placeholder { color: var(--text-muted); }
.crop-search:focus { outline: none; border-color: var(--green); box-shadow: 0 0 0 3px rgba(46,125,50,0.12); }

.search-count { font-size: 12px; color: var(--teal); white-space: nowrap; }
.search-clear {
  padding: 4px 8px; background: transparent;
  border: 1px solid var(--border); border-radius: 6px;
  color: var(--text-muted); font-size: 12px; cursor: pointer;
  transition: all 0.15s;
}
.search-clear:hover { background: var(--surface-2); color: var(--text-primary); }
.no-result { font-size: 13px; color: var(--text-muted); padding: 8px 4px; margin: 0; }
</style>