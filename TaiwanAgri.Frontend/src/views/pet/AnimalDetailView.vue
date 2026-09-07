<!--
  src/views/pet/AnimalDetailView.vue
  職責：單一收容動物的詳情頁 /pet/shelter-map/animals/:animalId（不掛週次分支新增。
  「地圖→收容所→動物」這條下鑽路徑少了最後一層：收容所詳情頁列出全部動物，
  但清單裡每一隻都還沒有自己的可分享網址）。

  唯讀頁面，不像 LostPetPost 有編輯／刪除——這批資料來自農業部同步 Worker，不是使用者
  自建內容，前端本來就不該有寫入動作。
-->
<template>
  <div class="page animal-detail-view">
    <!-- 返回列由 DetailLayout 提供，而且是在資料載入前就先畫出來：原本的寫法是
         標題與返回連結各自獨立，載入失敗時畫面上只剩一則錯誤，回去的路要靠瀏覽器上一頁。 -->
    <DetailLayout
      :title="animal?.animalSubId ?? '收容動物詳情'"
      :back-to="backLink"
      back-label="回收容所詳情"
    >
      <template v-if="animal" #subtitle>
        <RouterLink :to="`/pet/shelter-map/${animal.shelterPkId}`" class="shelter-link">
          {{ animal.shelterName }}
        </RouterLink>
        （{{ animal.county }}）・{{ animal.shelterAddress }}
      </template>

      <template v-if="animal" #summary>
        <div class="detail-header">
          <span class="badge kind-badge">{{ animalKindLabel(animal.kind) }}</span>
          <span class="badge sex-badge">{{ animalSexLabel(animal.sex) }}</span>
        </div>
      </template>

      <StateBlock v-if="store.isLoadingShelterAnimalDetail" state="loading" message="資料載入中..." />
      <StateBlock
        v-else-if="store.shelterAnimalDetailError"
        state="error"
        :message="store.shelterAnimalDetailError"
        retryable
        @retry="fetchDetail"
      />

      <template v-else-if="animal">
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

        <div v-if="animal.foundPlace || animal.remark" class="detail-text">
          <p v-if="animal.foundPlace" class="detail-line">
            <span class="mdi mdi-map-marker-outline" /> 拾獲地點：{{ animal.foundPlace }}
          </p>
          <p v-if="animal.remark" class="detail-remark">{{ animal.remark }}</p>
        </div>

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
      </template>
    </DetailLayout>
  </div>
</template>

<script setup lang="ts">
import DetailLayout from '@/components/layouts/DetailLayout.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
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
/* 返回列、標題、限寬都由 DetailLayout 負責，這裡只留這一頁的內容樣式；
   顏色全部改用 semantic 層。 */
.detail-header { display: flex; gap: var(--space-2); }
/* 標籤外殼已收進 base.css 的 .badge，這裡只留語意色 */
.kind-badge { background: var(--color-action-soft-2); color: var(--color-action); }
.sex-badge { background: var(--info-50); color: var(--info-500); }

.shelter-link { color: var(--color-action); font-weight: var(--weight-medium); text-decoration: none; }
.shelter-link:hover { text-decoration: underline; }

.info-grid {
  display: grid; grid-template-columns: repeat(auto-fill, minmax(140px, 1fr)); gap: var(--space-4) var(--space-5);
  padding: var(--space-5) var(--space-6);
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
}
.info-item { display: flex; flex-direction: column; gap: var(--space-1); }
.info-label { font-size: var(--text-2xs); color: var(--color-text-dim); font-weight: var(--weight-medium); letter-spacing: 0.04em; }
.info-value { font-size: var(--text-base); color: var(--color-text); font-weight: var(--weight-medium); }

.detail-text { display: flex; flex-direction: column; gap: var(--space-3); }
.detail-line { font-size: var(--text-base); color: var(--color-text); }
.detail-remark { font-size: var(--text-base); color: var(--color-text-dim); line-height: var(--leading-normal); white-space: pre-wrap; }

.detail-actions {
  display: flex; flex-wrap: wrap; gap: var(--space-4);
  padding-top: var(--space-4); border-top: var(--border-width) solid var(--color-border);
}
/* 外部連結用動作色，不用藍：藍在這一版沒有「可點」的語意 */
.action-link {
  display: inline-flex; align-items: center; gap: var(--space-1);
  color: var(--color-action); font-size: var(--text-sm); font-weight: var(--weight-medium); text-decoration: none;
}
.action-link:hover { text-decoration: underline; }
</style>
