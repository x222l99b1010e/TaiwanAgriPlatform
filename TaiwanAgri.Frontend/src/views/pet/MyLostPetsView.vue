<!--
  src/views/pet/MyLostPetsView.vue
  職責：「我發布的遺失啟事」個人管理頁 /profile/lost-pets（不掛週次分支新增）。
  owner 2026-08-09 指出的缺口：公開列表頁一旦貼文數量變多，使用者很難翻找到自己發過的那幾篇；
  這頁用後端新增的 OnlyMine 篩選，只查自己的貼文，不用在幾千篇公開清單裡大海撈針。

  編輯／刪除刻意不做在這頁的卡片上——那個動作已經在詳情頁做好了（LostPetPostForm 原地編輯），
  這裡的卡片只負責「找到是哪一篇」，點進去之後才是「動它」，兩個頁面各司其職，不重複兩份
  編輯入口互相打架。新增貼文則直接用同一份共用表單，這裡也能直接張貼，不用先跳到公開列表頁。
-->
<template>
  <div class="my-lost-pets-view">
    <RouterLink to="/profile" class="back-link">
      <span class="mdi mdi-arrow-left" /> 回個人資料
    </RouterLink>

    <div class="page-header">
      <h2 class="section-title">我的協尋貼文</h2>
      <p class="section-subtitle">只顯示你自己張貼的遺失啟事，點進去可以編輯或刪除</p>
    </div>

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
        <button v-if="!isFormOpen" class="btn-post" @click="isFormOpen = true">
          <span class="mdi mdi-plus" /> 張貼新啟事
        </button>
      </div>
    </div>

    <LostPetPostForm v-if="isFormOpen" :post="null" @saved="handleFormSaved" @cancel="isFormOpen = false" />

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
      <span class="state-text">
        {{ selectedStatus ? '這個狀態下沒有你發布的貼文' : '你還沒有發布過任何協尋啟事' }}
      </span>
    </div>

    <div v-else class="post-grid">
      <article v-for="post in store.lostPetPostsPage.items" :key="post.id" class="post-card">
        <div class="post-card-header">
          <span class="status-badge" :class="statusClass(post.status)">{{ statusLabel(post.status) }}</span>
        </div>

        <LostPetPostPhoto :photo-url="post.photoUrl" :title="post.title" />

        <RouterLink :to="`/pet/lost-pets/${post.id}`" class="post-title-link">
          <h4 class="post-title">{{ post.title }}</h4>
        </RouterLink>
        <p class="post-meta">
          {{ post.county || '未提供縣市' }}・張貼於 {{ formatDate(post.createdAt) }}
          <template v-if="post.updatedAt !== post.createdAt">（{{ formatDate(post.updatedAt) }} 更新）</template>
        </p>

        <RouterLink :to="`/pet/lost-pets/${post.id}`" class="btn-manage">
          <span class="mdi mdi-pencil-outline" /> 查看／編輯
        </RouterLink>
      </article>
    </div>

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
import { ref, onMounted, watch } from 'vue'
import LostPetPostPhoto from '@/components/LostPetPostPhoto.vue'
import LostPetPostForm from '@/components/LostPetPostForm.vue'
import PagerBar from '@/components/PagerBar.vue'
import { usePetStore } from '@/stores/pet'
import { usePagination } from '@/composables/usePagination'
import {
  lostPetPostStatusOptions, lostPetPostStatusLabel, lostPetPostStatusClass, formatLostPetPostDate,
} from '@/utils/lostPetPost'
import type { LostPetPostStatusValue, LostPetPostSortByValue } from '@/api/pet'

const store = usePetStore()

const selectedStatus = ref<LostPetPostStatusValue | ''>('')
// 這頁沒有縣市篩選——自己發過的貼文通常沒幾篇，不像公開列表頁動輒上千筆需要縣市縮小範圍
const sortBy = ref<LostPetPostSortByValue>('CreatedAt')
const sortDescending = ref(true)

const statusOptions = lostPetPostStatusOptions
const sortByOptions: { value: LostPetPostSortByValue; label: string }[] = [
  { value: 'CreatedAt', label: '依張貼時間' },
  { value: 'UpdatedAt', label: '依更新時間' },
]

const statusLabel = lostPetPostStatusLabel
const statusClass = lostPetPostStatusClass
const formatDate = formatLostPetPostDate

const isFormOpen = ref(false)

const {
  currentPage, visiblePages, jumpPageInput, handleJumpPage,
  changePage: paginationChangePage,
} = usePagination({
  storageKey: 'myLostPetPosts.pageSize',
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
    onlyMine: true,
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

watch([sortBy, sortDescending], () => {
  currentPage.value = 1
  fetchList()
})

function handleFormSaved() {
  isFormOpen.value = false
  currentPage.value = 1
  fetchList()
}

onMounted(fetchList)
</script>

<style scoped>
.my-lost-pets-view { padding: 36px 56px; width: 100%; box-sizing: border-box; }

.back-link {
  display: inline-flex; align-items: center; gap: 4px;
  margin-bottom: 20px; color: var(--text-secondary); font-size: 13.5px; font-weight: 600;
  text-decoration: none;
}
.back-link:hover { color: var(--green); }

.page-header { margin-bottom: 20px; }
.section-title { font-size: 22px; font-weight: 700; color: var(--text-primary); margin-bottom: 6px; }
.section-subtitle { font-size: 13px; color: var(--text-muted); }

/* ── 篩選列（跟 LostPetsView 同一套視覺語彙） ── */
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

/* ── 狀態容器 ── */
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
  display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 18px;
  margin-bottom: 24px;
}
.post-card {
  display: flex; flex-direction: column; gap: 8px;
  background: var(--surface); border: 1px solid var(--border); border-radius: 14px;
  padding: 18px 20px; box-shadow: 0 1px 4px rgba(0,0,0,0.05);
}
.post-card-header { display: flex; align-items: center; }
.status-badge {
  display: inline-block; padding: 3px 12px; border-radius: 999px;
  font-size: 12px; font-weight: 700;
}
.status-badge.searching { background: #fff3e0; color: #e65100; }
.status-badge.found { background: #e8f5e9; color: var(--green); }
.status-badge.withdrawn { background: #f0f0f0; color: #757575; }

.post-title-link { text-decoration: none; }
.post-title-link:hover .post-title { color: var(--green); text-decoration: underline; }
.post-title { font-size: 17px; font-weight: 700; color: var(--text-primary); transition: color 0.15s; }
.post-meta { font-size: 13px; color: var(--text-muted); }

.btn-manage {
  display: inline-flex; align-items: center; gap: 4px; align-self: flex-start;
  margin-top: 4px; padding: 6px 14px; border-radius: 8px;
  border: 1px solid var(--border); color: var(--text-secondary);
  font-size: 12.5px; font-weight: 600; text-decoration: none; transition: all 0.15s;
}
.btn-manage:hover { border-color: var(--green); color: var(--green); }
</style>
