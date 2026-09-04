<template>
  <div class="page profile-view">
    <PageHeader
      title="農場設定"
      title-en="FARM SETTINGS"
      subtitle="設定所在縣市與顯示名稱，並管理密碼"
    />

    <!-- 個人管理相關的其他頁面入口（不掛週次分支新增：我的協尋貼文）放在這裡，
         日後若有更多「我的 xxx」功能，這個區塊可以繼續往下加，不需要另外設計導覽結構 -->
    <RouterLink to="/profile/lost-pets" class="section-link">
      <span class="mdi mdi-dog-side" />
      <span>我的協尋貼文</span>
      <span class="mdi mdi-chevron-right" />
    </RouterLink>

    <div v-if="profileStore.isLoading" class="loading">載入中...</div>

    <div v-else class="profile-form">
      <!-- 農場縣市 -->
      <div class="form-group">
        <label>農場所在縣市</label>
        <select v-model="farmCity" class="form-control">
          <option :value="null">請選擇</option>
          <option v-for="city in cityOptions" :key="city" :value="city">
            {{ city }}
          </option>
        </select>
      </div>

      <!-- 農場類型 -->
      <div class="form-group">
        <label>農場類型</label>
        <select v-model="farmType" class="form-control">
          <option :value="null">請選擇</option>
          <option v-for="type in farmTypeOptions" :key="type" :value="type">
            {{ type }}
          </option>
        </select>
      </div>

      <!-- 主要作物（Autocomplete） -->
      <div class="form-group">
        <label>主要作物</label>

        <!-- 已選作物標籤 -->
        <div class="crop-tags" v-if="selectedCrops.length > 0">
          <div
            v-for="(crop, index) in selectedCrops"
            :key="crop.cropCode"
            class="crop-tag"
          >
            {{ crop.cropName }}
            <button :aria-label="`移除 ${crop.cropName}`" @click="removeCrop(index)">
              <span class="mdi mdi-close" />
            </button>
          </div>
        </div>

        <!-- 搜尋輸入框 + 下拉 -->
        <div class="autocomplete-wrapper">
          <input
            v-model="cropSearchText"
            @input="onCropInput"
            @blur="onBlur"
            placeholder="輸入作物名稱搜尋，例如：番茄"
            class="form-control crop-search-input"
          />
          <div class="autocomplete-dropdown" v-if="showDropdown">
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

      <!-- 訊息顯示 -->
      <div v-if="profileStore.errorMessage" class="error">
        {{ profileStore.errorMessage }}
      </div>
      <div v-if="profileStore.successMessage" class="success">
        {{ profileStore.successMessage }}
      </div>

      <!-- 儲存按鈕 -->
      <Btn class="save-btn" icon="mdi-content-save-outline" :loading="profileStore.isSaving" @click="handleSave">
        {{ profileStore.isSaving ? '儲存中...' : '儲存設定' }}
      </Btn>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useProfileStore } from '../stores/profile'
import type { CropItem } from '../api/profile'
import { getAllCrops } from '../api/cropApi'
import PageHeader from '@/components/ui/PageHeader.vue'
import Btn from '@/components/ui/Btn.vue'

const profileStore = useProfileStore()

// 表單本地狀態
const farmCity = ref<string | null>(null)
const farmType = ref<string | null>(null)
const selectedCrops = ref<CropItem[]>([])



// Autocomplete 狀態
const cropSearchText = ref('')
const showDropdown = ref(false)

// 縣市選項
const cityOptions = [
  '台北市', '新北市', '桃園市', '台中市', '台南市', '高雄市',
  '基隆市', '新竹市', '嘉義市', '新竹縣', '苗栗縣', '彰化縣',
  '南投縣', '雲林縣', '嘉義縣', '屏東縣', '宜蘭縣', '花蓮縣',
  '台東縣', '澎湖縣', '金門縣', '連江縣'
]

// 農場類型選項
const farmTypeOptions = ['蔬菜', '果樹', '花卉', '雜糧', '特用作物']

// ProfileView 自己存三份作物清單
const cropSearchPool = ref<CropItem[]>([])

// 依搜尋文字過濾，排除已選的
const filteredCrops = computed(() => {
  if (!cropSearchText.value.trim()) return []
  return cropSearchPool.value
    .filter(c =>
      c.cropName.includes(cropSearchText.value.trim()) &&
      !selectedCrops.value.some(s => s.cropCode === c.cropCode)
    )
    .slice(0, 10) // 最多顯示 10 筆，避免下拉太長
})

onMounted(async () => {
  // 直接呼叫 API，不透過 store
  cropSearchPool.value = await getAllCrops()

  // 載入農場設定
  await profileStore.fetchFarmProfile()
  if (profileStore.farmProfile) {
    farmCity.value = profileStore.farmProfile.farmCity
    farmType.value = profileStore.farmProfile.farmType
    selectedCrops.value = [...profileStore.farmProfile.crops]
  }
})

// 選取作物
function selectCrop(crop: { cropCode: string; cropName: string }) {
  selectedCrops.value.push({
    cropCode: crop.cropCode,
    cropName: crop.cropName
  })
  cropSearchText.value = ''
  showDropdown.value = false
}

// 移除作物
function removeCrop(index: number) {
  selectedCrops.value.splice(index, 1)
}

// 輸入時顯示下拉
function onCropInput() {
  showDropdown.value = filteredCrops.value.length > 0
}

// 點外部關閉下拉
function onBlur() {
  // 延遲關閉，讓 click 事件先觸發
  setTimeout(() => {
    showDropdown.value = false
  }, 150)
}

// 儲存
async function handleSave() {
  await profileStore.saveFarmProfile(
    farmCity.value,
    farmType.value,
    selectedCrops.value
  )
}
</script>

<style scoped>
/* 顏色全部改用 semantic 層；輸入框走 base.css 的 .form-control。 */

/* 單欄表單：頁面容器維持 .page 的統一寬度，內容自己限寬並靠左 */
.section-link,
.loading,
.profile-form { max-width: var(--container-sm); }

.loading {
  color: var(--color-text-dim);
  padding: var(--space-8);
  text-align: center;
}

/* 「我的協尋貼文」是這一頁唯一的頁面入口，要跟表單欄位拉開層級
   （原本的邊框＋一般字重太不顯眼）。
   ⚠ 但原本的做法是「2px 綠框＋綠底＋陰影＋放大字」四個手段一起上，
   一個連結比整頁的主要動作（儲存設定）還搶眼。改成一張正常的卡片＋
   動作色的圖示圓底＋右側箭頭：形狀本身就在說「這是可以點進去的一列」。 */
.section-link {
  display: flex;
  align-items: center;
  gap: var(--space-4);
  padding: var(--space-4) var(--space-5);
  margin-bottom: var(--space-6);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  color: var(--color-text);
  font-size: var(--text-base);
  font-weight: var(--weight-medium);
  text-decoration: none;
  transition:
    border-color var(--duration-fast) var(--ease-work),
    background var(--duration-fast) var(--ease-work);
}
.section-link:hover { border-color: var(--color-action); background: var(--color-action-soft); }
.section-link:focus-visible { outline: none; border-color: var(--color-action); box-shadow: var(--shadow-focus); }
.section-link .mdi-dog-side {
  display: inline-flex; align-items: center; justify-content: center;
  width: 36px; height: 36px; border-radius: var(--radius-full);
  background: var(--color-action-soft-2); color: var(--color-action);
  font-size: var(--text-lg); flex-shrink: 0;
}
.section-link .mdi-chevron-right {
  margin-left: auto; font-size: var(--text-lg); color: var(--color-text-dim);
  transition: transform var(--duration-fast) var(--ease-work);
}
.section-link:hover .mdi-chevron-right { transform: translateX(2px); }

.form-group {
  margin-bottom: var(--space-6);
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

label {
  font-weight: var(--weight-medium);
  color: var(--color-text);
  font-size: var(--text-sm);
}

/* ── 已選作物標籤 ── */
.crop-tags {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  margin-bottom: var(--space-2);
}

.crop-tag {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  min-height: var(--control-h-sm);
  padding: 0 var(--space-2) 0 var(--space-3);
  background: var(--color-action-soft-2);
  color: var(--color-action);
  border-radius: var(--radius-full);
  font-size: var(--text-sm);
  font-weight: var(--weight-medium);
}

.crop-tag button {
  display: inline-flex; align-items: center; justify-content: center;
  width: 20px; height: 20px;
  background: none;
  border: none;
  border-radius: var(--radius-full);
  color: var(--color-action);
  cursor: pointer;
  padding: 0;
  font-size: var(--text-base);
  line-height: 1;
  opacity: 0.7;
  transition: opacity var(--duration-fast) var(--ease-work), background var(--duration-fast) var(--ease-work);
}

.crop-tag button:hover { opacity: 1; background: var(--seed-200); }
.crop-tag button:focus-visible { outline: 2px solid var(--color-action); outline-offset: 1px; }

/* ── Autocomplete ── */
.autocomplete-wrapper { position: relative; }
.crop-search-input { width: 100%; box-sizing: border-box; }

/* 這一層是真的浮在頁面上方的浮動層，所以准用陰影（浮動層是陰影的唯一例外） */
.autocomplete-dropdown {
  position: absolute;
  top: calc(100% + var(--space-1));
  left: 0;
  right: 0;
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-float);
  z-index: var(--z-dropdown);
  max-height: 240px;
  overflow-y: auto;
  padding: var(--space-1);
}

.autocomplete-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-2) var(--space-3);
  border-radius: var(--radius-sm);
  cursor: pointer;
  font-size: var(--text-sm);
  color: var(--color-text);
}

.autocomplete-item:hover { background: var(--color-action-soft); }

.crop-code {
  font-family: var(--font-num);
  font-size: var(--text-xs);
  color: var(--color-text-dim);
}

/* 儲存按鈕改用共用的 Btn，這裡只留它在表單裡的位置 */
.save-btn { margin-top: var(--space-2); align-self: flex-start; }

/* 訊息：只有文字顏色不夠，兩則訊息長得幾乎一樣、只差色相；加上左邊界與底色才分得開 */
.error,
.success {
  padding: var(--space-3) var(--space-4);
  margin-bottom: var(--space-4);
  border-radius: 0 var(--radius-md) var(--radius-md) 0;
  font-size: var(--text-sm);
  line-height: var(--leading-normal);
}
.error {
  background: var(--danger-50);
  border-inline-start: 3px solid var(--danger-500);
  color: var(--danger-700);
}
.success {
  background: var(--color-action-soft);
  border-inline-start: 3px solid var(--color-action);
  color: var(--color-action);
}
</style>