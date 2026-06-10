<template>
  <div class="watchlist-view">
    <h1>監看清單</h1>

    <!-- ── 新增區 ──────────────────────────────────── -->
    <section class="add-section">
      <h2 class="section-title">新增監看</h2>

      <div class="add-form">
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
            <!-- 已選作物顯示 -->
            <div v-if="selectedCrop" class="selected-crop-tag">
              {{ selectedCrop.cropName }}
              <button @click="clearCrop">✕</button>
            </div>
            <!-- 下拉選單 -->
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

        <button
          class="btn-add"
          :disabled="!selectedCrop || store.isSaving"
          @click="handleAdd"
        >
          {{ store.isSaving ? '新增中...' : '+ 新增' }}
        </button>
      </div>

      <p v-if="store.errorMessage" class="error-msg">{{ store.errorMessage }}</p>
    </section>

    <!-- ── 清單區 ──────────────────────────────────── -->
    <section class="list-section">
      <div class="list-header">
        <h2 class="section-title">我的監看清單</h2>
        <button
          v-if="selectedIds.length > 0"
          class="btn-delete"
          :disabled="store.isSaving"
          @click="handleRemove"
        >
          刪除已選 ({{ selectedIds.length }})
        </button>
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
            <span class="item-crop">{{ item.cropName }}</span>
            <span class="item-market">{{ item.marketName ?? '全台均價' }}</span>
          </div>
          <span class="item-code">{{ item.cropCode }}</span>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useWatchlistStore } from '@/stores/watchlist'
import { marketApi } from '@/api/market'
import type { CropResponseDto, MarketResponseDto } from '@/api/market'

const store = useWatchlistStore()

// ─── 作物 Autocomplete 狀態 ─────────────────────────────────────────────
const allCrops = ref<CropResponseDto[]>([])
const cropSearchText = ref('')
const showCropDropdown = ref(false)
const selectedCrop = ref<CropResponseDto | null>(null)

const filteredCrops = computed(() => {
  if (!cropSearchText.value.trim()) return []
  return allCrops.value
    .filter(c => c.cropName.includes(cropSearchText.value.trim()))
    .slice(0, 10)
})

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
  store.errorMessage = null  // 重新選作物時清除上一次的錯誤
}

// ─── 市場狀態 ────────────────────────────────────────────────────────────
const markets = ref<MarketResponseDto[]>([])
const selectedMarketCode = ref<string | null>(null)

// ─── 勾選狀態（純 UI，不進 Store）──────────────────────────────────────
const selectedIds = ref<number[]>([])

function toggleSelect(id: number) {
  const idx = selectedIds.value.indexOf(id)
  if (idx >= 0) selectedIds.value.splice(idx, 1)
  else selectedIds.value.push(id)
}

// ─── 新增 ────────────────────────────────────────────────────────────────
async function handleAdd() {
  if (!selectedCrop.value) return

  const marketName = markets.value.find(m => m.marketCode === selectedMarketCode.value)?.marketName ?? null

    await store.addItem({
    cropCode: selectedCrop.value.cropCode,
    cropName: selectedCrop.value.cropName,
    marketCode: selectedMarketCode.value,
    marketName: marketName,
    })

    // 只有成功（沒有 errorMessage）才重置表單
    if (!store.errorMessage) {
    selectedCrop.value = null
    selectedMarketCode.value = null
    }
}

// ─── 刪除 ────────────────────────────────────────────────────────────────
async function handleRemove() {
  await store.removeItems(selectedIds.value)
  selectedIds.value = []  // 刪除成功後清空勾選
}

// ─── 初始化 ──────────────────────────────────────────────────────────────
onMounted(async () => {
  // 載入所有作物（三類合併）
  const [veg, fruit, flower] = await Promise.all([
    marketApi.getCrops('Veg'),
    marketApi.getCrops('Fruit'),
    marketApi.getCrops('Flower'),
  ])
  allCrops.value = [...veg, ...fruit, ...flower]

  // 載入蔬菜市場清單（Watchlist 以蔬菜市場為主，後續可擴充）
  markets.value = await marketApi.getMarkets('Veg')

  // 載入使用者的監看清單
  await store.fetchItems()
})

function clearCrop() {
  selectedCrop.value = null
  cropSearchText.value = ''
  store.errorMessage = null // 重新選作物時清除上一次的錯誤
}
</script>

<style scoped>
.watchlist-view { max-width: 720px; margin: 0 auto; padding: 36px 24px; }

h1 { font-size: 22px; font-weight: 700; color: var(--text-primary); margin-bottom: 28px; }

.section-title {
  font-size: 14px; font-weight: 700;
  color: var(--text-secondary);
  letter-spacing: 0.06em; text-transform: uppercase;
  margin-bottom: 16px;
}

/* ── 新增區 ── */
.add-section {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 14px; padding: 24px; margin-bottom: 28px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
}

.add-form { display: flex; align-items: flex-end; gap: 16px; flex-wrap: wrap; }

.field-group { display: flex; flex-direction: column; gap: 6px; }

.field-label {
  font-size: 12px; color: var(--text-muted); font-weight: 600;
  letter-spacing: 0.05em; text-transform: uppercase;
}

.field-input {
  padding: 8px 14px; border: 1px solid var(--border); border-radius: 8px;
  background: var(--surface); color: var(--text-primary); font-size: 14px;
  min-width: 200px;
  transition: border-color 0.18s, box-shadow 0.18s;
}
.field-input:focus { outline: none; border-color: var(--green); box-shadow: 0 0 0 3px rgba(46,125,50,0.12); }

.field-select {
  padding: 8px 14px; border: 1px solid var(--border); border-radius: 8px;
  background: var(--surface); color: var(--text-primary); font-size: 14px;
  min-width: 200px; cursor: pointer;
  transition: border-color 0.18s, box-shadow 0.18s;
}
.field-select:focus { outline: none; border-color: var(--green); box-shadow: 0 0 0 3px rgba(46,125,50,0.12); }

/* Autocomplete */
.autocomplete-wrapper { position: relative; }

.selected-crop-tag {
  display: inline-flex; align-items: center; gap: 6px;
  margin-top: 6px; padding: 4px 12px; border-radius: 999px;
  background: #e8f5e9; color: var(--green);
  font-size: 13px; font-weight: 600;
}
.selected-crop-tag button {
  background: none; border: none; color: var(--green);
  cursor: pointer; font-size: 12px; padding: 0; opacity: 0.7;
}
.selected-crop-tag button:hover { opacity: 1; }

.autocomplete-dropdown {
  position: absolute; top: 100%; left: 0; right: 0;
  background: white; border: 1px solid var(--border);
  border-radius: 8px; box-shadow: 0 4px 12px rgba(0,0,0,0.10);
  z-index: 100; max-height: 240px; overflow-y: auto;
}

.autocomplete-item {
  display: flex; justify-content: space-between; align-items: center;
  padding: 10px 14px; cursor: pointer; font-size: 14px; color: var(--text-primary);
}
.autocomplete-item:hover { background: #f0f4f0; }
.crop-code { font-size: 12px; color: var(--text-muted); }

.btn-add {
  padding: 9px 24px; border-radius: 999px;
  border: 1px solid #1a5220;
  background: linear-gradient(180deg, #4caf50 0%, #2e7d32 40%, #1b5e20 100%);
  color: white; font-size: 14px; font-weight: 700; cursor: pointer;
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.35), 0 2px 6px rgba(0,0,0,0.20);
  transition: all 0.15s; align-self: flex-end;
}
.btn-add:hover:not(:disabled) { background: linear-gradient(180deg, #66bb6a 0%, #388e3c 40%, #2e7d32 100%); }
.btn-add:disabled { background: #c8d8c8; color: #999; border-color: #b0c8b0; box-shadow: none; cursor: not-allowed; }

.error-msg { font-size: 13px; color: var(--red); margin-top: 12px; }

/* ── 清單區 ── */
.list-section {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 14px; padding: 24px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
}

.list-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; }

.btn-delete {
  padding: 7px 18px; border-radius: 999px;
  border: 1px solid #6a1010;
  background: linear-gradient(180deg, #ff6f43 0%, #e64a19 40%, #bf360c 100%);
  color: white; font-size: 13px; font-weight: 700; cursor: pointer;
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.35), 0 2px 6px rgba(0,0,0,0.18);
  transition: all 0.15s;
}
.btn-delete:hover:not(:disabled) { background: linear-gradient(180deg, #ff8a65 0%, #f4511e 40%, #e64a19 100%); }
.btn-delete:disabled { background: #c8d8c8; color: #999; border-color: #b0c8b0; box-shadow: none; cursor: not-allowed; }

.hint { font-size: 14px; color: var(--text-muted); text-align: center; padding: 32px 0; }

.item-list { display: flex; flex-direction: column; gap: 8px; }

.item-card {
  display: flex; align-items: center; gap: 14px;
  padding: 14px 18px; border-radius: 10px;
  border: 1px solid var(--border); background: var(--surface);
  cursor: pointer; transition: all 0.15s;
  box-shadow: 0 1px 3px rgba(0,0,0,0.04);
}
.item-card:hover { border-color: var(--green); background: #f6fbf6; }
.item-card.selected { border-color: var(--green); background: #e8f5e9; }

.item-card input[type="checkbox"] { accent-color: var(--green); width: 16px; height: 16px; cursor: pointer; flex-shrink: 0; }

.item-info { flex: 1; display: flex; flex-direction: column; gap: 3px; }
.item-crop { font-size: 15px; font-weight: 700; color: var(--text-primary); }
.item-market { font-size: 12px; color: var(--text-muted); }

.item-code { font-size: 12px; color: var(--text-muted); font-variant-numeric: tabular-nums; }
</style>