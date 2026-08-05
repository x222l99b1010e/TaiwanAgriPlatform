<template>
  <div class="lost-pets-view">
    <div class="page-header">
      <h2 class="section-title">遺失啟事協尋</h2>
      <p class="section-subtitle">使用者自行張貼的走失／拾獲協尋，登入後可管理自己的貼文</p>
    </div>

    <!-- 篩選列 + 張貼入口 -->
    <div class="filter-bar">
      <div class="status-tabs">
        <button
          v-for="opt in statusOptions"
          :key="opt.value"
          class="tab-btn"
          :class="{ active: selectedStatus === opt.value }"
          @click="changeStatus(opt.value)"
        >{{ opt.label }}</button>
      </div>

      <CitySelector v-model="selectedCounty" include-all />

      <div class="field-group">
        <label class="field-label">排序</label>
        <div class="sort-control">
          <select v-model="sortBy" class="filter-select">
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

      <div class="post-entry">
        <button v-if="auth.isLoggedIn" class="btn-post" @click="openCreateForm">
          <span class="mdi mdi-plus" /> 張貼協尋啟事
        </button>
        <RouterLink v-else class="login-hint" :to="{ name: 'login', query: { redirect: '/pet/lost-pets' } }">
          登入後即可張貼協尋啟事
        </RouterLink>
      </div>
    </div>

    <!-- 新增／編輯表單：inline 面板，不是彈窗（專案目前沒有 modal 元件，維持既有簡單風格） -->
    <section v-if="isFormOpen" class="form-panel" ref="formPanelRef">
      <h3 class="form-title">{{ editingId == null ? '張貼新的協尋啟事' : '編輯協尋啟事' }}</h3>

      <div class="form-grid">
        <div class="field-group span-2">
          <label class="field-label">標題 *</label>
          <input v-model="form.title" class="field-input" maxlength="100" placeholder="例如：臺中北屯走失黑色米克斯" />
        </div>

        <div class="field-group span-2">
          <label class="field-label">描述 *</label>
          <textarea v-model="form.description" class="field-textarea" maxlength="2000" rows="3"
            placeholder="特徵、走失時間地點、其他協尋資訊" />
        </div>

        <div class="field-group">
          <label class="field-label">縣市</label>
          <CitySelector v-model="form.county" include-all />
        </div>

        <div class="field-group">
          <label class="field-label">電話</label>
          <input v-model="form.phone" class="field-input" maxlength="50" placeholder="0912345678" />
        </div>

        <div class="field-group">
          <label class="field-label">Email</label>
          <input v-model="form.email" class="field-input" maxlength="254" placeholder="you@example.com" />
        </div>

        <div class="field-group">
          <label class="field-label">照片連結（選填）</label>
          <input v-model="form.photoUrl" class="field-input" placeholder="外部圖床網址，例如 Imgur 連結" />
        </div>

        <!-- 只有編輯既有貼文時才能改狀態；新增一律從「協尋中」開始（後端強制，前端表單不提供這個選項） -->
        <div v-if="editingId != null" class="field-group">
          <label class="field-label">狀態</label>
          <select v-model="form.status" class="field-select">
            <option v-for="opt in editableStatusOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
          </select>
        </div>
      </div>

      <div class="field-group span-2">
        <label class="field-label">走失／拾獲地點座標（選填，點地圖設定）</label>
        <LeafletCoordinatePicker v-model:latitude="form.latitude" v-model:longitude="form.longitude" />
      </div>

      <p v-if="formError" class="error-msg">{{ formError }}</p>
      <p v-if="store.saveLostPetPostError" class="error-msg">{{ store.saveLostPetPostError }}</p>

      <div class="form-actions">
        <button class="btn-submit" :disabled="store.isSavingLostPetPost" @click="handleSubmit">
          {{ store.isSavingLostPetPost ? '送出中...' : (editingId == null ? '送出' : '儲存變更') }}
        </button>
        <button class="btn-cancel" :disabled="store.isSavingLostPetPost" @click="closeForm">取消</button>
      </div>
    </section>

    <!-- 清單 -->
    <div v-if="store.isLoadingLostPetPosts" class="state-box">
      <div class="loading-spinner" />
      <span class="state-text">資料載入中...</span>
    </div>

    <div v-else-if="store.lostPetPostsError" class="state-box error-box">
      <span class="mdi mdi-alert-circle state-icon" />
      <span class="state-text">{{ store.lostPetPostsError }}</span>
      <button class="btn-retry" @click="fetchList">重試</button>
    </div>

    <div v-else-if="!store.lostPetPostsPage || store.lostPetPostsPage.items.length === 0" class="state-box">
      <span class="mdi mdi-dog-side state-icon" />
      <span class="state-text">目前沒有符合條件的協尋啟事</span>
    </div>

    <div v-else class="post-grid">
      <article v-for="post in store.lostPetPostsPage.items" :key="post.id" class="post-card">
        <div class="post-card-header">
          <span class="status-badge" :class="statusClass(post.status)">{{ statusLabel(post.status) }}</span>
          <span v-if="post.latitude != null && post.longitude != null" class="coord-badge" title="已標記地圖座標">
            <span class="mdi mdi-map-marker" />
          </span>
        </div>

        <h4 class="post-title">{{ post.title }}</h4>
        <p class="post-meta">{{ post.county || '未提供縣市' }}・{{ formatDate(post.createdAt) }}</p>
        <p class="post-description">{{ post.description }}</p>

        <div class="post-contact">
          <span v-if="post.phone" class="contact-item"><span class="mdi mdi-phone" /> {{ post.phone }}</span>
          <span v-if="post.email" class="contact-item"><span class="mdi mdi-email-outline" /> {{ post.email }}</span>
          <span v-if="!post.phone && !post.email" class="contact-item contact-missing">聯絡方式未提供</span>
        </div>

        <div v-if="post.isOwner" class="post-actions">
          <button class="btn-edit" @click="openEditForm(post)">
            <span class="mdi mdi-pencil-outline" /> 編輯
          </button>
          <button class="btn-delete" @click="handleDelete(post.id)">
            <span class="mdi mdi-trash-can-outline" /> 刪除
          </button>
        </div>
      </article>
    </div>

    <!-- 分頁控制：沿用 usePagination 共用邏輯 + PagerBar 共用元件 -->
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
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, nextTick, watch } from 'vue'
import CitySelector from '@/components/CitySelector.vue'
import LeafletCoordinatePicker from '@/components/LeafletCoordinatePicker.vue'
import PagerBar from '@/components/PagerBar.vue'
import { usePetStore } from '@/stores/pet'
import { useAuthStore } from '@/stores/authStore'
import { usePagination } from '@/composables/usePagination'
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

const statusOptions: { value: LostPetPostStatusValue | ''; label: string }[] = [
  { value: '',          label: '全部' },
  { value: 'Searching', label: '協尋中' },
  { value: 'Found',     label: '已找到' },
  { value: 'Withdrawn', label: '已撤回' },
]
const sortByOptions: { value: LostPetPostSortByValue; label: string }[] = [
  { value: 'CreatedAt', label: '依張貼時間' },
  { value: 'UpdatedAt', label: '依更新時間' },
]
// 編輯表單只能選這三個狀態（新增一律從 Searching 開始，後端強制，表單不提供這個選項）
const editableStatusOptions = statusOptions.filter(
  (o): o is { value: LostPetPostStatusValue; label: string } => o.value !== ''
)

function statusLabel(status: LostPetPostStatusValue): string {
  return statusOptions.find(o => o.value === status)?.label ?? status
}
function statusClass(status: LostPetPostStatusValue): string {
  return { Searching: 'searching', Found: 'found', Withdrawn: 'withdrawn' }[status]
}

function formatDate(iso: string): string {
  return iso.slice(0, 10) // "2026-08-05T12:00:00" -> "2026-08-05"
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

interface FormState {
  title: string
  description: string
  county: string
  phone: string
  email: string
  photoUrl: string
  latitude: number | null
  longitude: number | null
  status: LostPetPostStatusValue
}

function emptyForm(): FormState {
  return {
    title: '', description: '', county: '', phone: '', email: '', photoUrl: '',
    latitude: null, longitude: null, status: 'Searching',
  }
}

const isFormOpen = ref(false)
const editingId = ref<number | null>(null)
const form = reactive<FormState>(emptyForm())
const formError = ref('')
const formPanelRef = ref<HTMLElement | null>(null)

function openCreateForm() {
  editingId.value = null
  Object.assign(form, emptyForm())
  formError.value = ''
  store.saveLostPetPostError = null
  isFormOpen.value = true
  scrollToForm()
}

// 編輯直接用清單裡已經拿到的完整 DTO 填表單，不用再打一次 API
function openEditForm(post: LostPetPostResponseDto) {
  editingId.value = post.id
  Object.assign(form, {
    title: post.title,
    description: post.description,
    county: post.county,
    phone: post.phone,
    email: post.email,
    photoUrl: post.photoUrl,
    latitude: post.latitude,
    longitude: post.longitude,
    status: post.status,
  })
  formError.value = ''
  store.saveLostPetPostError = null
  isFormOpen.value = true
  scrollToForm()
}

function closeForm() {
  isFormOpen.value = false
  editingId.value = null
}

function scrollToForm() {
  nextTick(() => formPanelRef.value?.scrollIntoView({ behavior: 'smooth', block: 'start' }))
}

async function handleSubmit() {
  formError.value = ''

  if (!form.title.trim() || !form.description.trim()) {
    formError.value = '標題與描述為必填欄位'
    return
  }
  if (!form.phone.trim() && !form.email.trim()) {
    formError.value = '電話與 Email 至少填一項，才能讓拾獲者聯絡到你'
    return
  }

  const payload = {
    title: form.title,
    description: form.description,
    county: form.county || undefined,
    phone: form.phone || undefined,
    email: form.email || undefined,
    photoUrl: form.photoUrl || undefined,
    latitude: form.latitude,
    longitude: form.longitude,
  }

  if (editingId.value == null) {
    const created = await store.createLostPetPost(payload)
    if (created) {
      closeForm()
      fetchList()
    }
  } else {
    const success = await store.updateLostPetPost(editingId.value, { ...payload, status: form.status })
    if (success) {
      closeForm()
      fetchList()
    }
  }
}

async function handleDelete(id: number) {
  if (!confirm('確定要刪除這篇協尋啟事嗎？此操作無法復原。')) return
  const success = await store.deleteLostPetPost(id)
  if (success) fetchList()
}

onMounted(fetchList)
</script>

<style scoped>
.lost-pets-view { padding: 36px 56px; width: 100%; box-sizing: border-box; }

.page-header { margin-bottom: 20px; }
.section-title { font-size: 22px; font-weight: 700; color: var(--text-primary); margin-bottom: 6px; }
.section-subtitle { font-size: 13px; color: var(--text-muted); }

/* ── 篩選列 ── */
.filter-bar {
  display: flex; flex-wrap: wrap; align-items: flex-end; gap: 20px;
  margin-bottom: 20px; padding: 16px 20px;
  background: var(--surface); border: 1px solid var(--border); border-radius: 12px;
}

.status-tabs { display: flex; gap: 6px; }
.tab-btn {
  padding: 7px 16px; border-radius: 999px; border: 1px solid var(--border);
  background: transparent; color: var(--text-muted); font-size: 13px; font-weight: 600;
  cursor: pointer; transition: all 0.15s; white-space: nowrap;
}
.tab-btn:hover { border-color: var(--green); color: var(--green); }
.tab-btn.active { background: var(--green); border-color: var(--green); color: white; }

.field-group { display: flex; flex-direction: column; gap: 6px; }
.field-group.span-2 { grid-column: span 2; }
.field-label {
  font-size: 12px; color: var(--text-muted); font-weight: 600;
  letter-spacing: 0.05em; text-transform: uppercase;
}

.filter-select {
  padding: 8px 14px; border: 1px solid var(--border); border-radius: 8px;
  background: var(--surface); color: var(--text-primary); font-size: 14px;
  min-width: 130px; cursor: pointer;
}
.filter-select:focus { outline: none; border-color: var(--green); box-shadow: 0 0 0 3px rgba(46,125,50,0.12); }

.sort-control { display: flex; align-items: center; gap: 6px; }
.sort-dir-btn {
  width: 36px; height: 36px; display: flex; align-items: center; justify-content: center;
  border-radius: 8px; border: 1px solid var(--border); background: var(--surface);
  color: var(--text-secondary); cursor: pointer; flex-shrink: 0;
}
.sort-dir-btn:hover { border-color: var(--green); color: var(--green); }

.post-entry { margin-left: auto; }

.btn-post {
  display: inline-flex; align-items: center; gap: 6px;
  padding: 9px 22px; border-radius: 999px; border: 1px solid #1a5220;
  background: linear-gradient(180deg, #4caf50 0%, #2e7d32 40%, #1b5e20 100%);
  color: white; font-size: 13.5px; font-weight: 700; cursor: pointer;
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.35), 0 2px 6px rgba(0,0,0,0.20);
  transition: all 0.15s;
}
.btn-post:hover { background: linear-gradient(180deg, #66bb6a 0%, #388e3c 40%, #2e7d32 100%); }

.login-hint { font-size: 13px; color: var(--blue); font-weight: 600; text-decoration: none; }
.login-hint:hover { text-decoration: underline; }

/* ── 表單面板 ── */
.form-panel {
  background: var(--surface); border: 1px solid var(--border); border-radius: 14px;
  padding: 24px 28px; margin-bottom: 24px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
  display: flex; flex-direction: column; gap: 16px;
}
.form-title { font-size: 15px; font-weight: 700; color: var(--text-primary); }

.form-grid {
  display: grid; grid-template-columns: repeat(2, 1fr); gap: 16px 20px;
}

.field-input, .field-select, .field-textarea {
  padding: 8px 14px; border: 1px solid var(--border); border-radius: 8px;
  background: var(--surface); color: var(--text-primary); font-size: 14px;
  font-family: inherit; transition: border-color 0.18s, box-shadow 0.18s;
}
.field-textarea { resize: vertical; min-height: 72px; }
.field-input:focus, .field-select:focus, .field-textarea:focus {
  outline: none; border-color: var(--green); box-shadow: 0 0 0 3px rgba(46,125,50,0.12);
}
.field-select { cursor: pointer; }

.form-actions { display: flex; gap: 10px; }

.btn-submit {
  padding: 9px 26px; border-radius: 999px; border: 1px solid #1a5220;
  background: linear-gradient(180deg, #4caf50 0%, #2e7d32 40%, #1b5e20 100%);
  color: white; font-size: 14px; font-weight: 700; cursor: pointer;
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.35), 0 2px 6px rgba(0,0,0,0.20);
  transition: all 0.15s;
}
.btn-submit:hover:not(:disabled) { background: linear-gradient(180deg, #66bb6a 0%, #388e3c 40%, #2e7d32 100%); }
.btn-submit:disabled { background: #c8d8c8; color: #999; border-color: #b0c8b0; box-shadow: none; cursor: not-allowed; }

.btn-cancel {
  padding: 9px 22px; border-radius: 999px; border: 1px solid var(--border);
  background: transparent; color: var(--text-secondary); font-size: 14px; font-weight: 600; cursor: pointer;
}
.btn-cancel:hover:not(:disabled) { border-color: var(--border-hover); }

.error-msg { font-size: 13px; color: var(--red); font-weight: 600; }

/* ── 狀態容器（載入中／錯誤／空清單） ── */
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

/* ── 貼文卡片格線 ── */
.post-grid {
  display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 16px;
  margin-bottom: 24px;
}

.post-card {
  display: flex; flex-direction: column; gap: 8px;
  background: var(--surface); border: 1px solid var(--border); border-radius: 14px;
  padding: 18px 20px; box-shadow: 0 1px 4px rgba(0,0,0,0.05);
}

.post-card-header { display: flex; align-items: center; justify-content: space-between; }

.status-badge {
  display: inline-block; padding: 3px 12px; border-radius: 999px;
  font-size: 12px; font-weight: 700;
}
.status-badge.searching { background: #fff3e0; color: #e65100; }
.status-badge.found { background: #e8f5e9; color: var(--green); }
.status-badge.withdrawn { background: #f0f0f0; color: #757575; }

.coord-badge { color: var(--green); font-size: 16px; }

.post-title { font-size: 16px; font-weight: 700; color: var(--text-primary); }
.post-meta { font-size: 12px; color: var(--text-muted); }
.post-description {
  font-size: 13px; color: var(--text-secondary); line-height: 1.6;
  display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden;
}

.post-contact { display: flex; flex-wrap: wrap; gap: 12px; font-size: 12.5px; color: var(--text-secondary); }
.contact-item { display: inline-flex; align-items: center; gap: 4px; }
.contact-missing { color: var(--text-muted); font-style: italic; }

.post-actions { display: flex; gap: 8px; margin-top: 4px; padding-top: 12px; border-top: 1px solid var(--border); }

.btn-edit, .btn-delete {
  display: inline-flex; align-items: center; gap: 4px;
  padding: 6px 14px; border-radius: 8px; font-size: 12.5px; font-weight: 600; cursor: pointer;
  transition: all 0.15s;
}
.btn-edit { border: 1px solid var(--border); background: transparent; color: var(--text-secondary); }
.btn-edit:hover { border-color: var(--green); color: var(--green); }
.btn-delete { border: 1px solid rgba(198,40,40,0.30); background: transparent; color: var(--red); }
.btn-delete:hover { background: #fff5f5; }
</style>
