<template>
  <div class="page profile-view">
    <PageHeader
      title="農場設定"
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
        <select v-model="farmCity">
          <option :value="null">請選擇</option>
          <option v-for="city in cityOptions" :key="city" :value="city">
            {{ city }}
          </option>
        </select>
      </div>

      <!-- 農場類型 -->
      <div class="form-group">
        <label>農場類型</label>
        <select v-model="farmType">
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
            <button @click="removeCrop(index)">✕</button>
          </div>
        </div>

        <!-- 搜尋輸入框 + 下拉 -->
        <div class="autocomplete-wrapper">
          <input
            v-model="cropSearchText"
            @input="onCropInput"
            @blur="onBlur"
            placeholder="輸入作物名稱搜尋，例如：番茄"
            class="crop-search-input"
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
      <button
        class="save-btn"
        @click="handleSave"
        :disabled="profileStore.isSaving"
      >
        {{ profileStore.isSaving ? '儲存中...' : '儲存設定' }}
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useProfileStore } from '../stores/profile'
import type { CropItem } from '../api/profile'
import { getAllCrops } from '../api/cropApi'
import PageHeader from '@/components/ui/PageHeader.vue'

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
/* 單欄表單：頁面容器維持 .page 的統一寬度，內容自己限寬並靠左 */
.section-link,
.loading,
.profile-form { max-width: var(--container-sm); }
.loading {
  color: var(--neutral-500);
  padding: 2rem;
  text-align: center;
}

/* 原本用邊框+一般字重，在部分螢幕/亮度設定下太不顯眼（owner 2026-08-09 實機反應）。
   改成綠色底色卡片＋粗體放大字＋圖示放進圓底色塊，跟頁面上其他純表單元素拉開視覺層級，
   一眼就能認出「這是一個可以點的功能入口」而不是說明文字 */
.section-link {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 1rem 1.25rem;
  margin-bottom: 1.5rem;
  border: 2px solid var(--green-600);
  border-radius: 12px;
  background: var(--green-100);
  color: var(--green-800);
  font-size: 1.05rem;
  font-weight: 700;
  text-decoration: none;
  box-shadow: 0 2px 6px rgba(46, 125, 50, 0.12);
  transition: background 0.15s, box-shadow 0.15s;
}
.section-link:hover { background: var(--green-200); box-shadow: 0 3px 10px rgba(46, 125, 50, 0.20); }
.section-link .mdi-dog-side {
  display: inline-flex; align-items: center; justify-content: center;
  width: 2rem; height: 2rem; border-radius: 50%;
  background: var(--green-600); color: var(--neutral-0); font-size: 1.1rem; flex-shrink: 0;
}
.section-link .mdi-chevron-right { margin-left: auto; font-size: 1.2rem; }

.form-group {
  margin-bottom: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

label {
  font-weight: 600;
  color: var(--neutral-900);
  font-size: 0.95rem;
}

select {
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--neutral-200);
  border-radius: 6px;
  background: var(--neutral-0);
  color: var(--neutral-900);
  font-size: 0.95rem;
}

/* 已選作物標籤 */
.crop-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
}

.crop-tag {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.25rem 0.75rem;
  background: var(--green-100);
  color: var(--green-600);
  border-radius: 20px;
  font-size: 0.875rem;
  font-weight: 500;
}

.crop-tag button {
  background: none;
  border: none;
  color: var(--green-600);
  cursor: pointer;
  padding: 0;
  font-size: 0.75rem;
  line-height: 1;
  opacity: 0.7;
}

.crop-tag button:hover {
  opacity: 1;
}

/* Autocomplete */
.autocomplete-wrapper {
  position: relative;
}

.crop-search-input {
  width: 100%;
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--neutral-200);
  border-radius: 6px;
  background: var(--neutral-0);
  color: var(--neutral-900);
  font-size: 0.95rem;
  box-sizing: border-box;
}

.autocomplete-dropdown {
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  background: var(--neutral-0);
  border: 1px solid var(--neutral-200);
  border-radius: 6px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  z-index: 100;
  max-height: 240px;
  overflow-y: auto;
}

.autocomplete-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.6rem 0.75rem;
  cursor: pointer;
  font-size: 0.9rem;
  color: var(--neutral-700);
}

.autocomplete-item:hover {
  background: var(--green-50);
}

.crop-code {
  font-size: 0.75rem;
  color: var(--neutral-500);
}

/* 儲存按鈕 */
.save-btn {
  margin-top: 0.5rem;
  padding: 9px 28px;
  border-radius: 999px;
  border: 1px solid var(--green-800);
  background: linear-gradient(180deg, var(--green-500) 0%, var(--green-600) 40%, var(--green-800) 100%);
  color: var(--neutral-0);
  font-size: 14px;
  font-weight: 700;
  cursor: pointer;
  box-shadow:
    inset 0 1px 0 rgba(255,255,255,0.35),
    inset 0 -2px 4px rgba(0,0,0,0.25),
    0 2px 6px rgba(0,0,0,0.20);
  transition: all 0.15s;
}

.save-btn:hover:not(:disabled) {
  background: linear-gradient(180deg, var(--green-400) 0%, var(--green-500) 40%, var(--green-600) 100%);
  box-shadow:
    inset 0 1px 0 rgba(255,255,255,0.45),
    inset 0 -2px 4px rgba(0,0,0,0.20),
    0 3px 10px rgba(0,0,0,0.22);
}

.save-btn:active:not(:disabled) {
  background: linear-gradient(180deg, var(--green-800) 0%, var(--green-600) 60%, var(--green-500) 100%);
  box-shadow:
    inset 0 2px 6px rgba(0,0,0,0.35),
    0 1px 3px rgba(0,0,0,0.15);
}

.save-btn:disabled {
  background: var(--neutral-300);
  color: var(--neutral-400);
  border-color: var(--neutral-400);
  box-shadow: none;
  cursor: not-allowed;
}

.error {
  color: var(--danger-500);
  font-size: 0.9rem;
  margin-bottom: 0.5rem;
}

.success {
  color: var(--green-500);
  font-size: 0.9rem;
  margin-bottom: 0.5rem;
}
</style>