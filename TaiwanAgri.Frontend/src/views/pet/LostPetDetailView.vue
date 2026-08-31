<!--
  src/views/pet/LostPetDetailView.vue
  職責：遺失啟事詳情頁 /pet/lost-pets/:id（不掛週次分支新增）。
  核心價值是「可分享的固定網址」——把後端 GET /{id} 與前端 getLostPetPostById 接起來，
  渲染邏輯（照片／狀態徽章／地圖連結／詐騙警語）直接複用 LostPetsView 已經做好、
  抽到共用元件與 utils 的那份，這頁只負責「單筆抓取＋版面排列」，不重新發明卡片渲染。
-->
<template>
  <div class="page lost-pet-detail-view">
    <RouterLink to="/pet/lost-pets" class="back-link">
      <span class="mdi mdi-arrow-left" /> 回協尋列表
    </RouterLink>

    <StateBlock v-if="store.isLoadingLostPetPostDetail" class="content-sm" state="loading" message="資料載入中..." />
    <StateBlock
      v-else-if="store.lostPetPostDetailError"
      class="content-sm"
      state="error"
      :message="store.lostPetPostDetailError"
      retryable
      @retry="fetchDetail"
    />

    <!--
      isOwner 時可以原地編輯：owner 2026-08-09 裁定改掉原本「詳情頁唯讀、導回列表頁編輯」的設計
      ——貼文一多，使用者很難在列表頁裡翻找到自己那一篇，詳情頁本來就是靠分享連結／個人管理頁
      直接進來的，原地編輯比多繞一趟列表頁合理。表單抽到共用元件 LostPetPostForm，
      跟列表頁、個人管理頁共用同一份邏輯，不是這裡另外刻一份。
    -->
    <LostPetPostForm v-else-if="isEditing && post" :post="post" @saved="handleEditSaved" @cancel="isEditing = false" />

    <article v-else-if="post" class="detail-card">
      <div class="detail-header">
        <span class="status-badge" :class="statusClass(post.status)">{{ statusLabel(post.status) }}</span>
        <a
          v-if="post.latitude != null && post.longitude != null"
          class="coord-badge"
          :href="googleMapsLink(post.latitude, post.longitude)"
          target="_blank"
          rel="noopener noreferrer"
          title="在 Google 地圖開啟走失／拾獲地點"
        >
          <span class="mdi mdi-map-marker" /> 查看地點
        </a>
      </div>

      <!-- crop=false：詳情頁的重點是看清楚長相與特徵，裁切在這裡不划算（列表卡片才需要裁切維持格線） -->
      <LostPetPostPhoto :photo-url="post.photoUrl" :title="post.title" :crop="false" />

      <h2 class="detail-title">{{ post.title }}</h2>
      <p class="detail-meta">
        {{ post.county || '未提供縣市' }}・張貼於 {{ formatDate(post.createdAt) }}
        <template v-if="post.updatedAt !== post.createdAt">（{{ formatDate(post.updatedAt) }} 更新）</template>
      </p>

      <!--
        跟列表頁同一句警語（不抽共用元件：只有這一句話，抽元件的間接成本比直接複製貼上還高）。
        分享連結進來的人很可能沒經過列表頁、沒看過那句提醒，詳情頁必須再講一次。
      -->
      <div class="safety-notice">
        <span class="mdi mdi-alert-outline notice-icon" />
        <span>
          本篇啟事與聯絡方式皆由張貼者自行填寫，平台無法查證內容真偽。
          近期詐騙猖獗，聯繫前請自行確認對方身分，切勿先行匯款、支付酬金或提供個人敏感資料。
        </span>
      </div>

      <!-- 詳情頁本來就是「要看完整內容」的地方，描述不做行數截斷，跟列表卡片的 3 行 clamp 不同 -->
      <p class="detail-description">{{ post.description }}</p>

      <div class="detail-contact">
        <span v-if="post.phone" class="contact-item"><span class="mdi mdi-phone" /> {{ post.phone }}</span>
        <span v-if="post.email" class="contact-item"><span class="mdi mdi-email-outline" /> {{ post.email }}</span>
        <span v-if="!post.phone && !post.email" class="contact-item contact-missing">聯絡方式未提供</span>
      </div>

      <div v-if="post.isOwner" class="owner-actions-block">
        <div class="owner-actions">
          <Btn variant="secondary" size="sm" icon="mdi-pencil-outline" @click="openEdit">編輯</Btn>
          <Btn
            variant="danger"
            size="sm"
            icon="mdi-trash-can-outline"
            :disabled="store.isSavingLostPetPost"
            @click="handleDelete"
          >刪除</Btn>
        </div>
        <p v-if="store.saveLostPetPostError" class="error-msg">{{ store.saveLostPetPostError }}</p>
      </div>
    </article>
  </div>
</template>

<script setup lang="ts">
import Btn from '@/components/ui/Btn.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { usePetStore } from '@/stores/pet'
import LostPetPostPhoto from '@/components/LostPetPostPhoto.vue'
import LostPetPostForm from '@/components/LostPetPostForm.vue'
import {
  lostPetPostStatusLabel, lostPetPostStatusClass, formatLostPetPostDate, googleMapsLink,
} from '@/utils/lostPetPost'

// id 由 router 的 props 函式模式轉成 number 再傳進來（見 router/index.ts），
// 元件不需要自己處理 route.params 是字串這件事
const props = defineProps<{ id: number }>()

const router = useRouter()
const store = usePetStore()
const post = computed(() => store.lostPetPostDetail)

const statusLabel = lostPetPostStatusLabel
const statusClass = lostPetPostStatusClass
const formatDate = formatLostPetPostDate

const isEditing = ref(false)

function openEdit() {
  store.saveLostPetPostError = null // 避免上一次刪除失敗的殘留錯誤訊息，跟著切進編輯表單一起出現
  isEditing.value = true
}

function fetchDetail() {
  store.fetchLostPetPostById(props.id)
}

onMounted(fetchDetail)
// 這個元件在「同一個詳情頁換另一個 id」時會被 Vue Router 重用、不會重新掛載
// （path 對應到同一個 component），onMounted 只在第一次進來時觸發，id 變動要靠這個 watch 補上
watch(() => props.id, () => {
  isEditing.value = false
  fetchDetail()
})

// PUT 端點只回傳 204 No Content，共用表單元件本身拿不到更新後的完整內容，
// 存檔成功後由這裡重新查一次單筆，畫面才會反映剛剛改動的結果
function handleEditSaved() {
  isEditing.value = false
  fetchDetail()
}

async function handleDelete() {
  if (!confirm('確定要刪除這篇協尋啟事嗎？此操作無法復原。')) return

  const success = await store.deleteLostPetPost(props.id)
  if (success) {
    // 這篇貼文已經不存在了，詳情頁沒有內容可以停留，導回列表頁
    router.push('/pet/lost-pets')
  }
}
</script>

<style scoped>

.back-link {
  display: inline-flex; align-items: center; gap: 4px;
  margin-bottom: 20px; color: var(--text-secondary); font-size: 13.5px; font-weight: 600;
  text-decoration: none;
}
.back-link:hover { color: var(--green); }

/* ── 內容卡片 ──
   詳情頁是單欄文字，內容自己限寬並靠左——頁面容器本身維持 .page 的統一寬度，
   所以返回連結與頁首的左邊界跟其他頁對齊，不會因為這頁比較窄就整片內縮。 */
.detail-card {
  max-width: var(--container-sm);
  display: flex; flex-direction: column; gap: 14px;
  background: var(--surface); border: 1px solid var(--border); border-radius: 14px;
  padding: 28px 32px; box-shadow: 0 1px 4px rgba(0,0,0,0.05);
}

.detail-header { display: flex; align-items: center; justify-content: space-between; }
.status-badge {
  display: inline-block; padding: 3px 12px; border-radius: 999px;
  font-size: 12px; font-weight: 700;
}
.status-badge.searching { background: #fff3e0; color: #e65100; }
.status-badge.found { background: #e8f5e9; color: var(--green); }
.status-badge.withdrawn { background: #f0f0f0; color: #757575; }

.coord-badge {
  display: inline-flex; align-items: center; gap: 3px;
  color: var(--green); font-size: 13.5px; font-weight: 600; text-decoration: none;
}
.coord-badge:hover { text-decoration: underline; }

.detail-title { font-size: 24px; font-weight: 700; color: var(--text-primary); }
.detail-meta { font-size: 13.5px; color: var(--text-muted); }

.safety-notice {
  display: flex; align-items: flex-start; gap: 8px;
  padding: 12px 16px;
  background: #fff5f5; border: 1px solid #ffcdd2; border-left: 4px solid var(--red);
  border-radius: 10px;
  color: var(--red); font-size: 14.5px; font-weight: 700; line-height: 1.6;
}
.notice-icon { font-size: 18px; flex-shrink: 0; line-height: 1.5; }

.detail-description {
  font-size: 15px; color: var(--text-primary); line-height: 1.75;
  white-space: pre-wrap; /* 保留張貼者輸入的換行，特徵條列才不會被擠成一整段 */
}

.detail-contact { display: flex; flex-wrap: wrap; gap: 14px; font-size: 15px; color: var(--text-primary); }
.contact-item { display: inline-flex; align-items: center; gap: 4px; }
.contact-missing { color: var(--text-muted); font-style: italic; }

.owner-actions-block { margin-top: 4px; padding-top: 12px; border-top: 1px solid var(--border); }
.owner-actions { display: flex; gap: 8px; }
.error-msg { margin-top: 8px; font-size: 13px; color: var(--red); font-weight: 600; }
</style>
