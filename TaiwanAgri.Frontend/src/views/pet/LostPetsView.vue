<template>
  <div class="page lost-pets-view">
    <QueryLayout
      title="遺失啟事協尋"
      title-en="LOST PETS"
      subtitle="使用者自行張貼的走失／拾獲協尋，登入後可管理自己的貼文"
    >
      <template #actions>
        <template v-if="auth.isLoggedIn">
          <!-- 不掛週次分支新增：貼文一多，這頁的公開清單很難翻到自己那幾篇，
               個人管理頁（/profile/lost-pets）用 OnlyMine 篩選只查自己的 -->
          <RouterLink to="/profile/lost-pets" class="my-posts-link">
            <span class="mdi mdi-account-box-outline" /> 我的協尋貼文
          </RouterLink>
          <Btn icon="mdi-plus" @click="openCreateForm">張貼協尋啟事</Btn>
        </template>
        <RouterLink v-else class="login-hint" :to="{ name: 'login', query: { redirect: '/pet/lost-pets' } }">
          登入後即可張貼協尋啟事
        </RouterLink>
      </template>

      <template #filters>
        <div class="field-group">
          <span class="field-label">狀態</span>
          <div class="segmented">
            <button
              v-for="opt in statusOptions"
              :key="opt.value"
              class="segmented__btn"
              :class="{ 'is-active': selectedStatus === opt.value }"
              @click="changeStatus(opt.value)"
            >{{ opt.label }}</button>
          </div>
        </div>

        <CitySelector v-model="selectedCounty" include-all />

        <div class="field-group">
          <label class="field-label" for="sort-by">排序</label>
          <div class="sort-control">
            <select id="sort-by" v-model="sortBy" class="form-control filter-select">
              <option v-for="opt in sortByOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
            </select>
            <button
              type="button" class="sort-dir-btn" :title="sortDescending ? '降冪，點擊切換升冪' : '升冪，點擊切換降冪'"
              @click="sortDescending = !sortDescending"
            >
              <span class="mdi" :class="sortDescending ? 'mdi-sort-descending' : 'mdi-sort-ascending'" />
            </button>
          </div>
        </div>
      </template>

      <!--
        本頁全部是使用者自建內容（跟收容動物地圖／合法業者那兩頁的政府開放資料性質完全不同），
        平台不驗證任何一筆的真實性。這個警語放頁面層級而不是每張卡片：同一句話印 12 次會被當成
        版面裝飾自動略過，放在入口處只出現一次反而讀得到。
      -->
      <template #hint>
        <div class="safety-notice">
          <span class="mdi mdi-alert-outline notice-icon" />
          <span>
            本頁啟事與聯絡方式皆由張貼者自行填寫，平台無法查證內容真偽。
            近期詐騙猖獗，聯繫前請自行確認對方身分，切勿先行匯款、支付酬金或提供個人敏感資料。
          </span>
        </div>
      </template>

      <template #results>
        <!-- 新增／編輯表單：抽成共用元件 LostPetPostForm，詳情頁與個人管理頁也用同一份
             （共用，不是三處各刻一份表單邏輯） -->
        <LostPetPostForm v-if="isFormOpen" :post="editingPost" @saved="handleFormSaved" @cancel="closeForm" />

        <!-- 清單 -->
        <StateBlock v-if="store.isLoadingLostPetPosts" state="loading" message="資料載入中..." />
        <StateBlock
          v-else-if="store.lostPetPostsError"
          state="error"
          :message="store.lostPetPostsError"
          retryable
          @retry="fetchList"
        />
        <StateBlock
          v-else-if="!store.lostPetPostsPage || store.lostPetPostsPage.items.length === 0"
          state="empty"
          icon="mdi-dog-side"
          message="目前沒有符合條件的協尋啟事"
          hint="可以換一個縣市或狀態再看看"
        />

        <div v-else class="post-grid">
          <article v-for="post in store.lostPetPostsPage.items" :key="post.id" class="post-card">
            <div class="post-card-header">
              <span class="badge status-badge" :class="statusClass(post.status)">{{ statusLabel(post.status) }}</span>
              <!--
                座標原本只是一個「有標記」的圖示，看的人無法知道究竟標在哪裡——這個欄位等於只寫不讀。
                本頁是分頁列表不是地圖（資料形狀決定，見既有設計前提），與其在每張卡片塞一個 Leaflet
                實例（12 個地圖實例的成本不合理），不如把座標交給專業地圖服務：協尋情境真正需要的是
                「怎麼過去」，外部地圖能直接導航，站內小圖做不到。
              -->
              <a
                v-if="post.latitude != null && post.longitude != null"
                class="coord-badge"
                :href="`https://www.google.com/maps?q=${post.latitude},${post.longitude}`"
                target="_blank"
                rel="noopener noreferrer"
                title="在 Google 地圖開啟走失／拾獲地點"
              >
                <span class="mdi mdi-map-marker" /> 查看地點
              </a>
            </div>

            <!-- 照片：抽成共用元件 LostPetPostPhoto，詳情頁也會用同一份（不掛週次分支） -->
            <LostPetPostPhoto :photo-url="post.photoUrl" :title="post.title" />

            <!-- 標題可點入詳情頁：可分享的固定網址是這個分支的核心價值，其餘欄位維持原地展開／連結行為 -->
            <RouterLink :to="`/pet/lost-pets/${post.id}`" class="post-title-link">
              <h4 class="post-title">{{ post.title }}</h4>
            </RouterLink>
            <p class="post-meta">{{ post.county || '未提供縣市' }}・{{ formatDate(post.createdAt) }}</p>
            <!--
              描述欄位允許 2000 字，但總覽固定截斷 3 行（約 70 字）——不處理的話填長描述等於白填，
              9 成以上的內容永遠不會被看到。就地展開比另開詳情頁便宜得多：完整內容本來就在
              手上這份 DTO 裡，不必新增路由、不必再打一次 API，而且預設仍是截斷狀態，
              整頁的預設長度完全不變，只有讀者主動想看時才變長。
            -->
            <p class="post-description" :class="{ expanded: expandedIds[post.id] }">{{ post.description }}</p>
            <button
              v-if="isDescriptionClampable(post.description)"
              type="button" class="btn-expand"
              @click="expandedIds[post.id] = !expandedIds[post.id]"
            >
              {{ expandedIds[post.id] ? '收合' : '展開全文' }}
              <span class="mdi" :class="expandedIds[post.id] ? 'mdi-chevron-up' : 'mdi-chevron-down'" />
            </button>

            <div class="post-contact">
              <span v-if="post.phone" class="contact-item"><span class="mdi mdi-phone" /> {{ post.phone }}</span>
              <span v-if="post.email" class="contact-item"><span class="mdi mdi-email-outline" /> {{ post.email }}</span>
              <span v-if="!post.phone && !post.email" class="contact-item contact-missing">聯絡方式未提供</span>
            </div>

            <div v-if="post.isOwner" class="post-actions">
              <Btn variant="secondary" size="sm" icon="mdi-pencil-outline" @click="openEditForm(post)">編輯</Btn>
              <Btn variant="danger" size="sm" icon="mdi-trash-can-outline" @click="handleDelete(post.id)">刪除</Btn>
            </div>
          </article>
        </div>

      </template>

      <!-- 分頁控制：沿用 usePagination 共用邏輯 + PagerBar 共用元件 -->
      <template #pager>
        <PagerBar
          v-if="store.lostPetPostsPage && store.lostPetPostsPage.totalPages > 1"
          :current-page="currentPage"
          :total-pages="store.lostPetPostsPage.totalPages"
          :total-count="store.lostPetPostsPage.totalCount"
          :visible-pages="visiblePages"
          :jump-page-input="jumpPageInput"
          @change="changePage"
          @update:jump-page-input="jumpPageInput = $event"
          @jump="handleJumpPage"
        />
      </template>
    </QueryLayout>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, watch } from 'vue'

import CitySelector from '@/components/CitySelector.vue'
import LostPetPostPhoto from '@/components/LostPetPostPhoto.vue'
import LostPetPostForm from '@/components/LostPetPostForm.vue'
import PagerBar from '@/components/PagerBar.vue'
import { usePetStore } from '@/stores/pet'
import { useAuthStore } from '@/stores/authStore'
import { usePagination } from '@/composables/usePagination'
import QueryLayout from '@/components/layouts/QueryLayout.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'
import {
  lostPetPostStatusOptions, lostPetPostStatusLabel, lostPetPostStatusClass, formatLostPetPostDate,
} from '@/utils/lostPetPost'
import type { LostPetPostResponseDto, LostPetPostStatusValue, LostPetPostSortByValue } from '@/api/pet'

const store = usePetStore()
const auth = useAuthStore()

// ─── 篩選狀態 ───────────────────────────────────────────────────────────

const selectedStatus = ref<LostPetPostStatusValue | ''>('')
const selectedCounty = ref('')
// 這張表沒有動物種類這種可分類欄位（自建貼文只有標題/描述自由文字），能篩的維度就 Status/County，
// 排序是另一個可以加的維度：依張貼時間／依更新時間（例如想看「最近有誰回報找到了」就切更新時間）
const sortBy = ref<LostPetPostSortByValue>('CreatedAt')
const sortDescending = ref(true) // 預設維持既有行為：最新張貼的在前

// 狀態選項與 label/class 對照抽到 utils/lostPetPost.ts，詳情頁要用同一份，避免兩處各刻一份
const statusOptions = lostPetPostStatusOptions
const sortByOptions: { value: LostPetPostSortByValue; label: string }[] = [
  { value: 'CreatedAt', label: '依張貼時間' },
  { value: 'UpdatedAt', label: '依更新時間' },
]
const statusLabel = lostPetPostStatusLabel
const statusClass = lostPetPostStatusClass
const formatDate = formatLostPetPostDate

// ─── 描述展開／收合 ─────────────────────────────────────────────────────

const expandedIds = reactive<Record<number, boolean>>({})

/**
 * 判斷描述是否長到會被 3 行截斷。用字數估算而非量測 DOM：卡片寬約 400px、內文 14.5px，
 * 一行約 24 個中文字，3 行約 72 字。抓 70 當門檻會有少量誤判（剛好接近門檻時多顯示一顆按鈕），
 * 但換來不必為 v-for 裡的每張卡片各掛一個 ref 去比對 scrollHeight／clientHeight。
 */
function isDescriptionClampable(description: string): boolean {
  return description.length > 70
}

// ─── 分頁 ───────────────────────────────────────────────────────────────

const {
  currentPage, visiblePages, jumpPageInput, handleJumpPage,
  changePage: paginationChangePage,
} = usePagination({
  storageKey: 'lostPetPosts.pageSize',
  totalPages: () => store.lostPetPostsPage?.totalPages,
  onChange: fetchList,
  defaultPageSize: 12,
})

function changePage(p: number) {
  paginationChangePage(p)
}

function fetchList() {
  store.fetchLostPetPosts({
    status: selectedStatus.value || undefined,
    county: selectedCounty.value || undefined,
    sortBy: sortBy.value,
    sortDescending: sortDescending.value,
    page: currentPage.value,
    pageSize: 12,
  })
}

function changeStatus(value: LostPetPostStatusValue | '') {
  selectedStatus.value = value
  currentPage.value = 1
  fetchList()
}

// 縣市／排序條件變動一律重置回第一頁再查——跟 usePagination 的 onChange 是兩條獨立的觸發路徑：
// 那個只管頁碼/每頁筆數，篩選/排序條件變動要自己重置回第一頁再查，兩者職責不同、不能共用同一個 handler
watch([selectedCounty, sortBy, sortDescending], () => {
  currentPage.value = 1
  fetchList()
})

// ─── 新增／編輯表單 ─────────────────────────────────────────────────────
// 表單欄位、驗證、送出邏輯都搬進共用元件 LostPetPostForm 了（列表頁／詳情頁／個人管理頁共用）。
// 這裡剩下的職責只有「要不要顯示表單」跟「顯示哪一筆」——editingPost 直接存整個 DTO
// 而不是只存 id：新增時是 null、編輯時是清單裡已經拿到的那筆，元件靠這個 prop 判斷新增/編輯模式

const isFormOpen = ref(false)
const editingPost = ref<LostPetPostResponseDto | null>(null)

function openCreateForm() {
  editingPost.value = null
  isFormOpen.value = true
}

function openEditForm(post: LostPetPostResponseDto) {
  editingPost.value = post
  isFormOpen.value = true
}

function closeForm() {
  isFormOpen.value = false
  editingPost.value = null
}

function handleFormSaved() {
  closeForm()
  fetchList()
}

async function handleDelete(id: number) {
  // 正在編輯的那篇被刪掉時，表單會留下一個指向「已不存在資料」的 editingPost，
  // 之後按「儲存變更」會 PUT 到一個已刪除的 id → 後端回 404 → 畫面顯示
  // 「可能不是你的貼文」這種完全誤導的訊息（實際上是自己剛剛刪掉的）。
  // 兩道處理：刪除前把後果講清楚、刪除成功後把表單一起關掉，不留孤兒狀態。
  const isEditingThisPost = editingPost.value?.id === id

  const message = isEditingThisPost
    ? '這篇正在編輯中，刪除後未儲存的編輯內容會一併消失。確定要刪除嗎？此操作無法復原。'
    : '確定要刪除這篇協尋啟事嗎？此操作無法復原。'
  if (!confirm(message)) return

  const success = await store.deleteLostPetPost(id)
  if (success) {
    if (isEditingThisPost) closeForm()
    fetchList()
  }
}

onMounted(fetchList)
</script>

<style scoped>
/* 顏色全部改用 semantic 層；狀態四選一改用 base.css 的 .segmented，
   欄位外殼走 .field-group／.field-label／.form-control。 */

/* ── 篩選列 ── */
.filter-select { min-width: 130px; }

.sort-control { display: flex; align-items: center; gap: var(--space-2); }
/* 正方形圖示鈕，高度跟旁邊的 select 一樣吃 --control-h */
.sort-dir-btn {
  width: var(--control-h); height: var(--control-h);
  display: flex; align-items: center; justify-content: center;
  border-radius: var(--radius-md);
  border: var(--border-width) solid var(--color-border);
  background: var(--color-surface);
  color: var(--color-text-dim); cursor: pointer; flex-shrink: 0;
  transition:
    border-color var(--duration-fast) var(--ease-work),
    color var(--duration-fast) var(--ease-work);
}
.sort-dir-btn:hover { border-color: var(--color-action); color: var(--color-action); }
.sort-dir-btn:focus-visible { outline: none; border-color: var(--color-action); box-shadow: var(--shadow-focus); }

/* 「我的協尋貼文」原本是純文字連結，太不顯眼（
   電腦上幾乎看不到）。做成描邊按鈕，跟旁邊的主要動作（實心的「張貼協尋啟事」）
   分出主從。⚠ 邊框從 2px 改回 1px：它跟旁邊的 Btn 排在同一列，2px 會比隔壁粗一圈，
   而且加上邊框後總高比 Btn 多 2px、底部對不齊。形狀也跟著 Btn 改成方角。 */
.my-posts-link {
  display: inline-flex; align-items: center; gap: var(--space-2);
  min-height: var(--control-h); padding: 0 var(--space-5);
  border-radius: var(--radius-md);
  border: var(--border-width) solid var(--color-action);
  background: var(--color-surface);
  color: var(--color-action);
  font-size: var(--text-sm); font-weight: var(--weight-medium);
  letter-spacing: 0.02em; text-decoration: none; white-space: nowrap;
  transition: background var(--duration-fast) var(--ease-work);
}
.my-posts-link:hover { background: var(--color-action-soft); }
.login-hint { font-size: var(--text-sm); color: var(--color-action); font-weight: var(--weight-medium); text-decoration: none; }
.login-hint:hover { text-decoration: underline; }

/* 表單面板：markup 與樣式已抽到 LostPetPostForm.vue，這裡不再重複 */

/* ── 狀態容器（載入中／錯誤／空清單） ── */
/* ── 貼文卡片格線 ── */
.post-grid {
  display: grid; grid-template-columns: repeat(auto-fill, minmax(400px, 1fr)); gap: var(--space-5);
  margin-bottom: var(--space-6);
}

.post-card {
  display: flex; flex-direction: column; gap: var(--space-2);
  background: var(--color-surface); border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--space-5);
  transition: border-color var(--duration-fast) var(--ease-work);
}
.post-card:hover { border-color: var(--color-border-strong); }

.post-card-header { display: flex; align-items: center; justify-content: space-between; gap: var(--space-2); }

/* 標籤外殼已收進 base.css 的 .badge，這裡只留語意色 */
.status-badge.searching { background: var(--warning-50); color: var(--warning-700); }
.status-badge.found { background: var(--color-action-soft-2); color: var(--color-action); }
.status-badge.withdrawn { background: var(--color-bg-sunken); color: var(--color-text-dim); }

.coord-badge {
  display: inline-flex; align-items: center; gap: var(--space-1);
  color: var(--color-action); font-size: var(--text-sm); font-weight: var(--weight-medium); text-decoration: none;
}
.coord-badge:hover { text-decoration: underline; }

/* ── 使用者自建內容的安全提醒（詐騙防範） ──
   整段紅字全粗會讓人自動略過（看起來像制式免責聲明）。改成只有左邊界與圖示是紅的、
   內文用正常字重與墨色——要讀得進去才有防詐效果。 */
.safety-notice {
  display: flex; align-items: flex-start; gap: var(--space-3);
  padding: var(--space-4) var(--space-5);
  background: var(--danger-50);
  border: var(--border-width) solid var(--danger-100);
  border-inline-start: 3px solid var(--danger-500);
  border-radius: 0 var(--radius-md) var(--radius-md) 0;
  color: var(--color-text); font-size: var(--text-sm);
  line-height: var(--leading-normal);
}
.notice-icon { font-size: var(--text-lg); color: var(--danger-500); flex-shrink: 0; line-height: var(--leading-normal); }

/* 照片（外部圖床連結）：markup 與樣式已抽到 LostPetPostPhoto.vue，這裡不再重複 */

.post-title-link { text-decoration: none; }
.post-title-link:hover .post-title { color: var(--color-action); text-decoration: underline; }
.post-title { font-size: var(--text-lg); font-weight: var(--weight-bold); color: var(--color-text); transition: color var(--duration-fast); }
.post-meta { font-size: var(--text-sm); color: var(--color-text-dim); }
.post-description {
  font-size: var(--text-base); color: var(--color-text); line-height: var(--leading-normal);
  /* line-clamp 標準版本瀏覽器支援仍在普及中，兩個都寫：有標準版本的走標準、
     沒有的（多數現況）retreat 回 -webkit- 前綴版本 */
  display: -webkit-box; line-clamp: 3; -webkit-line-clamp: 3;
  -webkit-box-orient: vertical; overflow: hidden;
  white-space: pre-wrap; /* 保留張貼者輸入的換行，特徵條列才不會被擠成一整段 */
}
/* 展開時解除行數限制；overflow 一併放開，否則 clamp 拿掉了容器還是會裁 */
.post-description.expanded {
  line-clamp: unset; -webkit-line-clamp: unset; overflow: visible;
}

.btn-expand {
  align-self: flex-start; display: inline-flex; align-items: center; gap: var(--space-1);
  padding: var(--space-1) 0; border: none; background: transparent; font-family: inherit;
  color: var(--color-action); font-size: var(--text-sm); font-weight: var(--weight-medium); cursor: pointer;
}
.btn-expand:hover { text-decoration: underline; }
.btn-expand:focus-visible { outline: 2px solid var(--color-action); outline-offset: 2px; border-radius: var(--radius-sm); }

.post-contact { display: flex; flex-wrap: wrap; gap: var(--space-3); font-size: var(--text-base); color: var(--color-text); }
.contact-item { display: inline-flex; align-items: center; gap: var(--space-1); }
.contact-missing { color: var(--color-text-dim); font-style: italic; }

.post-actions {
  display: flex; gap: var(--space-2); margin-top: auto;
  padding-top: var(--space-4); border-top: var(--border-width) solid var(--color-border);
}
</style>
