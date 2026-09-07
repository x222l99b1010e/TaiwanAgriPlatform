<template>
  <div class="market-filter">
    <div class="segmented">
      <button
        v-for="tab in marketTypeTabs"
        :key="tab.value"
        class="segmented__btn"
        :class="{ 'is-active': store.marketType === tab.value }"
        :disabled="store.isLoadingMarkets || store.isLoadingCrops"
        @click="handleTabChange(tab.value)"
      >{{ tab.label }}</button>
    </div>

    <p v-if="store.error" class="error-msg">{{ store.error }}</p>

    <div class="field-group">
      <label class="field-label">市場</label>
      <div class="select-wrap">
        <select v-model="store.selectedMarketCode" :disabled="store.isLoadingMarkets" class="form-control market-select">
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
        <input v-model="cropSearch" class="form-control crop-search" placeholder="輸入關鍵字篩選，例如：菊" maxlength="20" />
        <span class="search-count" v-if="cropSearch.trim()">找到 {{ filteredCrops.length }} 項</span>
        <button class="search-clear" v-if="cropSearch.trim()" aria-label="清除關鍵字" @click="cropSearch = ''">
          <span class="mdi mdi-close" />
        </button>
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
/* 顏色全部改用 semantic 層，欄位外殼改用 base.css 的
   .field-group／.field-label／.form-control，這裡只留這個元件真正不同的部分。 */
.market-filter { display: flex; flex-direction: column; gap: var(--space-5); }

/* 蔬菜／水果／花卉走 base.css 的 .segmented（分段控制器），這裡不再自己寫一份。

   這一顆標籤裡塞了計數徽章，所以要橫排；其餘外觀走 base.css 的 .field-label */
.field-label { display: flex; align-items: center; gap: var(--space-2); }

/* 市場下拉 */
.select-wrap { display: flex; align-items: center; gap: var(--space-3); }
.market-select { min-width: 200px; }

/* 標籤外殼已收進 base.css 的 .badge，這裡只留語意色。
   ⚠ 原本用青色 --teal-500，那是舊色盤的次要強調色，不在秋田的十色裡；
   「已選幾項」是中性計數不是狀態，所以用動作色的淺階，額滿才轉警示色。 */
.count-badge {
  background: var(--color-action-soft-2);
  color: var(--color-action);
  letter-spacing: 0;
  text-transform: none;
  font-variant-numeric: tabular-nums;
}
.count-badge.full {
  background: var(--warning-50);
  color: var(--warning-700);
}

/* Crop */
.crop-header { display: flex; align-items: center; justify-content: space-between; }
.crop-list { display: flex; flex-wrap: wrap; gap: var(--space-2); }

.crop-btn {
  display: inline-flex; align-items: center; gap: var(--space-1);
  min-height: var(--control-h-sm);
  padding: 0 var(--space-3); border-radius: var(--radius-full);
  border: var(--border-width) solid var(--color-border);
  background: var(--color-surface);
  color: var(--color-text-dim);
  font-family: inherit; font-size: var(--text-sm); cursor: pointer;
  white-space: nowrap;
  transition:
    background var(--duration-fast) var(--ease-work),
    border-color var(--duration-fast) var(--ease-work),
    color var(--duration-fast) var(--ease-work);
}
.crop-btn:hover:not(.disabled) {
  border-color: var(--color-action); color: var(--color-action); background: var(--color-action-soft);
}
.crop-btn.selected {
  background: var(--color-action-soft-2); border-color: var(--color-action); color: var(--color-action);
  font-weight: var(--weight-medium);
}
.crop-btn:focus-visible { outline: none; border-color: var(--color-action); box-shadow: var(--shadow-focus); }
.crop-btn.disabled { opacity: 0.35; cursor: not-allowed; }
.check-dot { font-size: var(--text-2xs); font-weight: var(--weight-bold); }

.loading-hint { font-size: var(--text-xs); color: var(--color-text-dim); }
.limit-hint { font-size: var(--text-2xs); color: var(--warning-700); }
.error-msg  { font-size: var(--text-sm); color: var(--danger-700); }

.crop-container {
  max-height: 200px; overflow-y: auto; overflow-x: hidden;
  border: var(--border-width) solid var(--color-border); border-radius: var(--radius-lg);
  padding: var(--space-3); background: var(--color-bg-sunken);
  scrollbar-width: thin; scrollbar-color: var(--color-border-strong) transparent;
}
.crop-container::-webkit-scrollbar { width: 5px; }
.crop-container::-webkit-scrollbar-track { background: transparent; }
.crop-container::-webkit-scrollbar-thumb { background: var(--color-border-strong); border-radius: var(--radius-full); }

.crop-search-wrap { display: flex; align-items: center; gap: var(--space-2); margin-bottom: var(--space-1); }
.crop-search { flex: 1; }

.search-count { font-size: var(--text-xs); color: var(--color-text-dim); white-space: nowrap; }
.search-clear {
  display: inline-flex; align-items: center; justify-content: center;
  width: var(--control-h-sm); height: var(--control-h-sm);
  background: transparent;
  border: var(--border-width) solid var(--color-border); border-radius: var(--radius-md);
  color: var(--color-text-dim); font-size: var(--text-base); cursor: pointer;
  transition:
    background var(--duration-fast) var(--ease-work),
    color var(--duration-fast) var(--ease-work);
}
.search-clear:hover { background: var(--color-bg-sunken); color: var(--color-text); }
.search-clear:focus-visible { outline: none; border-color: var(--color-action); box-shadow: var(--shadow-focus); }
.no-result { font-size: var(--text-sm); color: var(--color-text-dim); padding: var(--space-2) var(--space-1); margin: 0; }
</style>