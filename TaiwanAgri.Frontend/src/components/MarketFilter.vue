<!-- src/components/MarketFilter.vue -->
<template>
  <div class="market-filter">

    <!-- ① marketType 切換 Tab（邏輯不變） -->
    <div class="tab-group">
      <button
        v-for="tab in marketTypeTabs"
        :key="tab.value"
        class="tab-btn"
        :class="{ active: store.marketType === tab.value }"
        :disabled="store.isLoadingMarkets || store.isLoadingCrops"
        @click="handleTabChange(tab.value)"
      >
        {{ tab.label }}
      </button>
    </div>

    <p v-if="store.error" class="error-msg">{{ store.error }}</p>

    <!-- ② 市場下拉（邏輯不變，樣式配合深色主題） -->
    <div class="field-group">
      <label class="field-label">市場</label>
      <div class="select-wrap">
        <select
          v-model="store.selectedMarketCode"
          :disabled="store.isLoadingMarkets"
          class="market-select"
        >
          <option :value="null">全台均價</option>
          <option
            v-for="market in store.markets"
            :key="market.marketCode"
            :value="market.marketCode"
          >
            {{ market.marketName }}
          </option>
        </select>
        <span v-if="store.isLoadingMarkets" class="loading-hint">載入中...</span>
      </div>
    </div>

    <!-- ③ 作物選擇 -->
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

      <!-- 搜尋框 -->
      <div class="crop-search-wrap" v-if="!store.isLoadingCrops">
        <input
          v-model="cropSearch"
          class="crop-search"
          placeholder="輸入關鍵字篩選，例如：菊"
          maxlength="20"
        />
        <span class="search-count" v-if="cropSearch.trim()">
          找到 {{ filteredCrops.length }} 項
        </span>
        <button
          class="search-clear"
          v-if="cropSearch.trim()"
          @click="cropSearch = ''"
        >✕</button>
      </div>

      <!-- 捲動框 -->
      <div class="chip-container" v-if="!store.isLoadingCrops">
        <div class="chip-list">
          <button
            v-for="crop in filteredCrops"
            :key="crop.cropCode"
            class="chip"
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

          <!-- 搜尋無結果 -->
          <p class="no-result" v-if="filteredCrops.length === 0">
            找不到「{{ cropSearch }}」相關作物
          </p>
        </div>
      </div>

      <p v-if="store.selectedCropCodes.length >= 5" class="limit-hint">
        已達上限，請先取消再選其他作物
      </p>
    </div>

  </div>
</template>

<script setup lang="ts">
import { onMounted, ref, computed } from 'vue'  // ref, computed 補上
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

// 加在 toggleCrop 上方
const cropSearch = ref('')

const filteredCrops = computed(() =>
  cropSearch.value.trim() === ''
    ? store.crops
    : store.crops.filter(c => c.cropName.includes(cropSearch.value.trim()))
)

// Chip 點擊：已選則取消，未選且未滿則加入
function toggleCrop(cropCode: string) {
  const idx = store.selectedCropCodes.indexOf(cropCode)
  if (idx >= 0) {
    store.selectedCropCodes.splice(idx, 1)   // 取消
  } else if (store.selectedCropCodes.length < 5) {
    store.selectedCropCodes.push(cropCode)   // 加入
  }
}

onMounted(() => {
  store.initialize()
})
</script>

<style scoped>
.market-filter {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

/* ── Tab ── */
.tab-group {
  display: flex;
  gap: 6px;
}

.tab-btn {
  padding: 7px 18px;
  border-radius: 8px;
  border: 1px solid rgba(255, 255, 255, 0.12);
  background: rgba(255, 255, 255, 0.04);
  color: rgba(200, 215, 230, 0.7);
  font-size: 13.5px;
  cursor: pointer;
  transition: all 0.18s ease;
}

.tab-btn:hover:not(:disabled) {
  background: rgba(125, 216, 207, 0.1);
  border-color: rgba(125, 216, 207, 0.3);
  color: rgba(200, 215, 230, 0.95);
}

.tab-btn.active {
  background: rgba(125, 216, 207, 0.15);
  border-color: rgba(125, 216, 207, 0.5);
  color: #7DD8CF;
  font-weight: 600;
}

.tab-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

/* ── 欄位通用 ── */
.field-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.field-label {
  font-size: 12px;
  color: rgba(170, 185, 205, 0.5);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  display: flex;
  align-items: center;
  gap: 8px;
}

/* ── 市場下拉 ── */
.select-wrap {
  display: flex;
  align-items: center;
  gap: 10px;
}

.market-select {
  padding: 8px 12px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: 8px;
  color: rgba(215, 225, 240, 0.88);
  font-size: 13.5px;
  min-width: 200px;
  cursor: pointer;
  transition: border-color 0.18s;
}

.market-select:focus {
  outline: none;
  border-color: rgba(125, 216, 207, 0.4);
}

.market-select option {
  background: #1a2035;
}

/* ── 作物 Chip ── */
.crop-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.count-badge {
  font-size: 11px;
  padding: 1px 7px;
  border-radius: 999px;
  background: rgba(125, 216, 207, 0.12);
  color: rgba(125, 216, 207, 0.7);
  font-weight: normal;
  letter-spacing: 0;
  text-transform: none;
  transition: all 0.2s;
}

.count-badge.full {
  background: rgba(242, 207, 106, 0.15);
  color: rgba(242, 207, 106, 0.8);
}

.chip-list {
  display: flex;
  flex-wrap: wrap;
  gap: 7px;
}

.chip {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 5px 12px;
  border-radius: 999px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  background: rgba(255, 255, 255, 0.04);
  color: rgba(190, 205, 220, 0.75);
  font-size: 13px;
  cursor: pointer;
  transition: all 0.16s ease;
  white-space: nowrap;
}

.chip:hover:not(.disabled) {
  background: rgba(125, 216, 207, 0.08);
  border-color: rgba(125, 216, 207, 0.25);
  color: rgba(210, 225, 235, 0.95);
}

.chip.selected {
  background: rgba(125, 216, 207, 0.14);
  border-color: rgba(125, 216, 207, 0.5);
  color: #7DD8CF;
}

.chip.disabled {
  opacity: 0.3;
  cursor: not-allowed;
}

.check-dot {
  font-size: 11px;
  font-weight: 700;
}

/* ── 提示文字 ── */
.loading-hint {
  font-size: 12px;
  color: rgba(170, 185, 205, 0.45);
}

.limit-hint {
  font-size: 11.5px;
  color: rgba(242, 207, 106, 0.65);
  margin: 0;
}

.error-msg {
  font-size: 13px;
  color: rgba(240, 100, 100, 0.8);
}

.chip-container {
  max-height: 200px;
  overflow-y: auto;
  overflow-x: hidden;    /* ← 加這行，chip 不往外撐 */
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 10px;
  padding: 12px;
  background: rgba(255, 255, 255, 0.02);
  /* 捲軸美化（Webkit） */
  scrollbar-width: thin;
  scrollbar-color: rgba(125, 216, 207, 0.25) transparent;
}

.chip-container::-webkit-scrollbar {
  width: 5px;
}

.chip-container::-webkit-scrollbar-track {
  background: transparent;
}

.chip-container::-webkit-scrollbar-thumb {
  background: rgba(125, 216, 207, 0.25);
  border-radius: 999px;
}

.chip-container::-webkit-scrollbar-thumb:hover {
  background: rgba(125, 216, 207, 0.45);
}

/* 搜尋框 */
.crop-search-wrap {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 4px;
}

.crop-search {
  flex: 1;
  padding: 7px 12px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: 8px;
  color: rgba(215, 225, 240, 0.88);
  font-size: 13.5px;
  transition: border-color 0.18s;
}

.crop-search::placeholder {
  color: rgba(170, 185, 205, 0.35);
}

.crop-search:focus {
  outline: none;
  border-color: rgba(125, 216, 207, 0.45);
}

.search-count {
  font-size: 12px;
  color: rgba(125, 216, 207, 0.6);
  white-space: nowrap;
}

.search-clear {
  padding: 4px 8px;
  background: transparent;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  color: rgba(170, 185, 205, 0.5);
  font-size: 12px;
  cursor: pointer;
  transition: all 0.15s;
}

.search-clear:hover {
  background: rgba(255, 255, 255, 0.06);
  color: rgba(200, 215, 230, 0.8);
}

.no-result {
  font-size: 13px;
  color: rgba(170, 185, 205, 0.4);
  padding: 8px 4px;
  margin: 0;
}
</style>