<!--
  src/views/pet/AnimalDetailView.vue
  職責：單一收容動物的詳情頁 /pet/shelter-map/animals/:animalId（不掛週次分支新增，owner 實機
  測試後指出「地圖→收容所→動物」這條下鑽路徑少了最後一層：收容所詳情頁列出全部動物，
  但清單裡每一隻都還沒有自己的可分享網址）。

  唯讀頁面，不像 LostPetPost 有編輯／刪除——這批資料來自農業部同步 Worker，不是使用者
  自建內容，前端本來就不該有寫入動作。
-->
<template>
  <div class="animal-detail-view">
    <RouterLink :to="backLink" class="back-link">
      <span class="mdi mdi-arrow-left" /> 回收容所詳情
    </RouterLink>

    <div v-if="store.isLoadingShelterAnimalDetail" class="state-box">
      <div class="loading-spinner" />
      <span class="state-text">資料載入中...</span>
    </div>

    <div v-else-if="store.shelterAnimalDetailError" class="state-box error-box">
      <span class="mdi mdi-alert-circle state-icon" />
      <span class="state-text">{{ store.shelterAnimalDetailError }}</span>
      <button class="btn-retry" @click="fetchDetail">重試</button>
    </div>

    <article v-else-if="animal" class="detail-card">
      <div class="detail-header">
        <span class="kind-badge">{{ animalKindLabel(animal.kind) }}</span>
        <span class="sex-badge">{{ animalSexLabel(animal.sex) }}</span>
      </div>

      <h2 class="detail-title">{{ animal.animalSubId }}</h2>
      <p class="detail-meta">
        <RouterLink :to="`/pet/shelter-map/${animal.shelterPkId}`" class="shelter-link">
          {{ animal.shelterName }}
        </RouterLink>
        （{{ animal.county }}）・{{ animal.shelterAddress }}
      </p>

      <div class="info-grid">
        <div class="info-item"><span class="info-label">體型</span><span class="info-value">{{ animal.bodyType || '—' }}</span></div>
        <div class="info-item"><span class="info-label">年齡</span><span class="info-value">{{ animal.age || '—' }}</span></div>
        <div class="info-item"><span class="info-label">結紮</span><span class="info-value">{{ sterilizationLabel(animal.sterilization) }}</span></div>
        <div class="info-item"><span class="info-label">疫苗</span><span class="info-value">{{ bacterinLabel(animal.bacterin) }}</span></div>
        <div class="info-item"><span class="info-label">品種</span><span class="info-value">{{ animal.variety || '—' }}</span></div>
        <div class="info-item"><span class="info-label">毛色</span><span class="info-value">{{ animal.colour || '—' }}</span></div>
        <div class="info-item"><span class="info-label">建檔日期</span><span class="info-value">{{ animal.createdTime }}</span></div>
        <div v-if="animal.openDate" class="info-item"><span class="info-label">開放認養日</span><span class="info-value">{{ animal.openDate }}</span></div>
      </div>

      <p v-if="animal.foundPlace" class="detail-line">
        <span class="mdi mdi-map-marker-outline" /> 拾獲地點：{{ animal.foundPlace }}
      </p>
      <p v-if="animal.remark" class="detail-remark">{{ animal.remark }}</p>

      <div class="detail-actions">
        <a
          v-if="isDisplayableAlbumLink(animal.albumFile)"
          :href="animal.albumFile" target="_blank" rel="noopener noreferrer" class="action-link"
        >
          <span class="mdi mdi-image-multiple-outline" /> 查看相簿／照片
        </a>
        <a
          v-if="animal.latitude != null && animal.longitude != null"
          :href="googleMapsLink(animal.latitude, animal.longitude)" target="_blank" rel="noopener noreferrer" class="action-link"
        >
          <span class="mdi mdi-map-marker" /> 在 Google 地圖開啟收容所位置
        </a>
      </div>
    </article>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, watch } from 'vue'
import { usePetStore } from '@/stores/pet'
import {
  animalKindLabel, animalSexLabel, isDisplayableAlbumLink, sterilizationLabel, bacterinLabel,
} from '@/utils/shelterAnimal'
import { googleMapsLink } from '@/utils/lostPetPost' // Google 地圖連結建構邏輯跟 LostPetPost 那邊完全一樣，共用同一個函式

// id 由 router 的 props 函式模式轉成 number（見 router/index.ts）
const props = defineProps<{ animalId: number }>()

const store = usePetStore()
const animal = computed(() => store.shelterAnimalDetail)

// 還沒查到資料時，回上一頁的連結先退回收容所地圖總覽（沒有 shelterPkId 可用）；
// 查到之後改連到牠所屬的那間收容所詳情頁，是更精準的「回上一層」
const backLink = computed(() => animal.value ? `/pet/shelter-map/${animal.value.shelterPkId}` : '/pet/shelter-map')

function fetchDetail() {
  store.fetchShelterAnimalById(props.animalId)
}

onMounted(fetchDetail)
watch(() => props.animalId, fetchDetail)
</script>

<style scoped>
.animal-detail-view { padding: 36px 56px; max-width: 760px; margin: 0 auto; box-sizing: border-box; }

.back-link {
  display: inline-flex; align-items: center; gap: 4px;
  margin-bottom: 20px; color: var(--text-secondary); font-size: 13.5px; font-weight: 600;
  text-decoration: none;
}
.back-link:hover { color: var(--green); }

/* ── 狀態容器（跟 LostPetDetailView／ShelterDetailView 同一套視覺語彙） ── */
.state-box {
  display: flex; flex-direction: column; align-items: center; gap: 12px;
  padding: 56px 32px; background: var(--surface); border: 1px solid var(--border); border-radius: 16px;
}
.state-icon { font-size: 36px; color: #aaa; }
.state-text { font-size: 15px; color: var(--text-muted); }
.error-box { background: #fff5f5; border-color: #ffcdd2; color: #c62828; }
.loading-spinner {
  width: 36px; height: 36px; border: 3px solid #c8e6c9; border-top-color: var(--green);
  border-radius: 50%; animation: spin 0.8s linear infinite;
}
@keyframes spin { to { transform: rotate(360deg); } }
.btn-retry {
  padding: 8px 24px; border-radius: 999px; border: 1.5px solid #c62828;
  background: transparent; color: #c62828; font-size: 13px; font-weight: 600; cursor: pointer;
}
.btn-retry:hover { background: #fff5f5; }

/* ── 內容卡片 ── */
.detail-card {
  display: flex; flex-direction: column; gap: 14px;
  background: var(--surface); border: 1px solid var(--border); border-radius: 14px;
  padding: 28px 32px; box-shadow: 0 1px 4px rgba(0,0,0,0.05);
}

.detail-header { display: flex; gap: 8px; }
.kind-badge, .sex-badge {
  display: inline-block; padding: 3px 12px; border-radius: 999px; font-size: 12px; font-weight: 700;
  background: #e8f5e9; color: var(--green);
}
.sex-badge { background: #e3f2fd; color: #1565c0; }

.detail-title { font-size: 24px; font-weight: 700; color: var(--text-primary); font-family: monospace; }
.detail-meta { font-size: 13.5px; color: var(--text-muted); }
.shelter-link { color: var(--green); font-weight: 600; text-decoration: none; }
.shelter-link:hover { text-decoration: underline; }

.info-grid {
  display: grid; grid-template-columns: repeat(auto-fill, minmax(140px, 1fr)); gap: 12px 20px;
  padding: 16px 0; border-top: 1px solid var(--border); border-bottom: 1px solid var(--border);
}
.info-item { display: flex; flex-direction: column; gap: 2px; }
.info-label { font-size: 11.5px; color: var(--text-muted); font-weight: 600; letter-spacing: 0.04em; }
.info-value { font-size: 14.5px; color: var(--text-primary); font-weight: 600; }

.detail-line { font-size: 14.5px; color: var(--text-primary); }
.detail-remark { font-size: 14px; color: var(--text-secondary); line-height: 1.65; white-space: pre-wrap; }

.detail-actions { display: flex; flex-wrap: wrap; gap: 16px; margin-top: 4px; padding-top: 12px; border-top: 1px solid var(--border); }
.action-link {
  display: inline-flex; align-items: center; gap: 5px;
  color: #1565c0; font-size: 13.5px; font-weight: 600; text-decoration: none;
}
.action-link:hover { text-decoration: underline; }
</style>
