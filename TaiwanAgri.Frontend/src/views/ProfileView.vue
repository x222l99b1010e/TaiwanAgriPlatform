<template>
  <div class="page profile-view">
    <PageHeader
      title="農場設定"
      title-en="FARM SETTINGS"
      subtitle="設定農場所在縣市、類型與主要作物"
    />

    <!-- 個人管理相關的其他頁面入口放在這裡，
         日後若有更多「我的 xxx」功能，這個區塊可以繼續往下加，不需要另外設計導覽結構 -->
    <RouterLink to="/profile/lost-pets" class="section-link">
      <span class="mdi mdi-dog-side" />
      <span>我的協尋貼文</span>
      <span class="mdi mdi-chevron-right" />
    </RouterLink>

    <RouterLink to="/profile/notification-rules" class="section-link">
      <span class="mdi mdi-bell-cog-outline" />
      <span>通知規則</span>
      <span class="mdi mdi-chevron-right" />
    </RouterLink>

    <!-- 載入失敗時不顯示表單：後端是整份取代，空白的表單一按儲存就會把既有設定蓋掉 -->
    <StateBlock
      v-if="profileStore.loadError"
      class="load-error"
      state="error"
      :message="profileStore.loadError"
      hint="為了不把你既有的設定蓋掉，載入成功之前不能儲存"
      retryable
      @retry="loadProfile"
    />
    <div v-else-if="!profileLoaded" class="loading">載入中...</div>

    <div v-else class="profile-form">
      <!-- 農場縣市 -->
      <div class="form-group">
        <label for="profile-farm-city">農場所在縣市</label>
        <select id="profile-farm-city" v-model="farmCity" class="form-control">
          <option :value="null">請選擇</option>
          <option v-for="city in cityOptions" :key="city" :value="city">
            {{ city }}
          </option>
        </select>
      </div>

      <!-- 農場類型 -->
      <div class="form-group">
        <label for="profile-farm-type">農場類型</label>
        <select id="profile-farm-type" v-model="farmType" class="form-control">
          <option :value="null">請選擇</option>
          <option v-for="type in farmTypeOptions" :key="type" :value="type">
            {{ type }}
          </option>
        </select>
      </div>

      <!-- 主要作物（Autocomplete） -->
      <div class="form-group">
        <label for="profile-crop-search">主要作物（最多 {{ MAX_FARM_CROPS }} 項）</label>

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

        <!-- 作物清單（三支行情 API）載不回來只影響「新增作物」，已選的照樣顯示、照樣能存 -->
        <p v-if="cropPoolError" class="crop-note crop-note--error">
          作物清單載入失敗，暫時不能新增作物（已選的不受影響）。
          <button type="button" class="link-btn" @click="loadCropPool">重新載入</button>
        </p>
        <p v-else-if="selectedCrops.length >= MAX_FARM_CROPS" class="crop-note">
          已選滿 {{ MAX_FARM_CROPS }} 項，要換別的作物請先移除一項
        </p>
        <CropAutocomplete
          v-else
          input-id="profile-crop-search"
          :options="cropSearchPool"
          :exclude-codes="selectedCrops.map(c => c.cropCode)"
          placeholder="輸入作物名稱搜尋，例如：番茄"
          @select="selectCrop"
        />
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
import { ref, onMounted } from 'vue'
import { useProfileStore } from '../stores/profile'
import type { CropItem } from '../api/profile'
import { getAllCrops } from '../api/cropApi'
import PageHeader from '@/components/ui/PageHeader.vue'
import Btn from '@/components/ui/Btn.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import CropAutocomplete from '@/components/CropAutocomplete.vue'
import { TAIWAN_CITIES } from '@/constants/taiwanCities'

const profileStore = useProfileStore()

/** 主要作物的上限，跟後端的請求驗證（UpsertFarmProfileRequestDto）同一個數字 */
const MAX_FARM_CROPS = 5

// 表單本地狀態
const farmCity = ref<string | null>(null)
const farmType = ref<string | null>(null)
const selectedCrops = ref<CropItem[]>([])

// 縣市選項：全站共用的那一份（用「臺」）
const cityOptions = TAIWAN_CITIES

/**
 * 舊版的選項用「台」，之前存下來的設定是「台北市」這種寫法，對不到現在的選項，
 * 下拉會變回「請選擇」、一存就把縣市清掉。讀進來時先換成「臺」
 */
function normalizeCity(city: string | null): string | null {
  return city ? city.replace(/^台/, '臺') : city
}

// 農場類型選項
const farmTypeOptions = ['蔬菜', '果樹', '花卉', '雜糧', '特用作物']

// ProfileView 自己存三份作物清單
const cropSearchPool = ref<CropItem[]>([])

// ── 載入：既有設定與作物清單分開處理失敗 ─────────────────────────────
// 原本先等作物清單、再載設定，任何一支失敗（被限流、冷啟動逾時）後面就不跑了，
// 畫面是空白表單，一按儲存就把既有設定蓋成空白（2026-11 實測過）
const profileLoaded = ref(false)
const cropPoolError = ref(false)

async function loadProfile() {
  profileLoaded.value = false
  if (!(await profileStore.fetchFarmProfile())) return
  const profile = profileStore.farmProfile
  farmCity.value = normalizeCity(profile?.farmCity ?? null)
  farmType.value = profile?.farmType ?? null
  selectedCrops.value = profile ? [...profile.crops] : []
  profileLoaded.value = true
}

async function loadCropPool() {
  cropPoolError.value = false
  try {
    cropSearchPool.value = await getAllCrops()
  } catch {
    cropPoolError.value = true
  }
}

onMounted(() => {
  // 兩個互不相依，同時發出
  void loadProfile()
  void loadCropPool()
})

// 選取作物（已選的不會出現在建議裡，所以不用再檢查重複）
function selectCrop(crop: CropItem) {
  if (selectedCrops.value.length >= MAX_FARM_CROPS) return
  selectedCrops.value.push({ cropCode: crop.cropCode, cropName: crop.cropName })
}

// 移除作物
function removeCrop(index: number) {
  selectedCrops.value.splice(index, 1)
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
.load-error,
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
/* 選第一個圖示而不是指名某個 mdi class：指名的話每加一列入口就要多寫一條
   一模一樣的規則，而漏寫的那一列不會報錯，只是圖示沒有圓底、跟旁邊長得不一樣 */
.section-link > .mdi:first-child {
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

/* 作物清單載不回來、已選滿上限時，取代搜尋框的那一行說明 */
.crop-note {
  margin: 0;
  font-size: var(--text-sm);
  color: var(--color-text-dim);
}
.crop-note--error { color: var(--danger-700); }
.link-btn {
  background: none; border: none; padding: 0;
  color: var(--color-action); font: inherit; text-decoration: underline; cursor: pointer;
}
.link-btn:focus-visible { outline: 2px solid var(--color-action); outline-offset: 2px; }

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