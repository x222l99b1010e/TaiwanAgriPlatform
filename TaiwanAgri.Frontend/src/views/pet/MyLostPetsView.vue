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
  <div class="page my-lost-pets-view">
    <RouterLink to="/profile" class="back-link">
      <span class="mdi mdi-arrow-left" /> 回個人資料
    </RouterLink>

    <PageHeader
      title="我的協尋貼文"
      subtitle="只顯示你自己張貼的遺失啟事，點進去可以編輯或刪除"
    />

    <FilterCard>
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
        <Btn v-if="!isFormOpen" icon="mdi-plus" @click="isFormOpen = true">張貼新啟事</Btn>
      </div>
    </FilterCard>

    <LostPetPostForm v-if="isFormOpen" :post="null" @saved="handleFormSaved" @cancel="isFormOpen = false" />

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
      :message="selectedStatus ? '這個狀態下沒有你發布的貼文' : '你還沒有發布過任何協尋啟事'"
      hint="按上方的「張貼新啟事」可以建立一則"
    />

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
import PageHeader from '@/components/ui/PageHeader.vue'
import FilterCard from '@/components/ui/FilterCard.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'
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

.back-link {
  display: inline-flex; align-items: center; gap: var(--space-1);
  margin-bottom: var(--space-5); color: var(--text-secondary); font-size: var(--text-sm); font-weight: var(--weight-medium);
  text-decoration: none;
}
.back-link:hover { color: var(--green); }
/* ── 篩選列（跟 LostPetsView 同一套視覺語彙） ── */
.status-tabs { display: flex; gap: var(--space-2); }
.tab-btn {
  padding: var(--space-2) var(--space-4); border-radius: var(--radius-full); border: 1px solid var(--border);
  background: transparent; color: var(--text-muted); font-size: var(--text-sm); font-weight: var(--weight-medium);
  cursor: pointer; transition: all var(--duration-fast); white-space: nowrap;
}
.tab-btn:hover { border-color: var(--green); color: var(--green); }
.tab-btn.active { background: var(--green); border-color: var(--green); color: var(--neutral-0); }

.field-group { display: flex; flex-direction: column; gap: var(--space-2); }
.field-label {
  font-size: var(--text-xs); color: var(--text-muted); font-weight: var(--weight-medium);
  letter-spacing: 0.05em; text-transform: uppercase;
}
.filter-select {
  padding: var(--space-2) var(--space-4); border: 1px solid var(--border); border-radius: var(--radius-md);
  background: var(--surface); color: var(--text-primary); font-size: var(--text-base);
  min-width: 130px; cursor: pointer;
}
.filter-select:focus { outline: none; border-color: var(--green); box-shadow: var(--shadow-focus); }

.sort-control { display: flex; align-items: center; gap: var(--space-2); }
.sort-dir-btn {
  width: 36px; height: 36px; display: flex; align-items: center; justify-content: center;
  border-radius: var(--radius-md); border: 1px solid var(--border); background: var(--surface);
  color: var(--text-secondary); cursor: pointer; flex-shrink: 0;
}
.sort-dir-btn:hover { border-color: var(--green); color: var(--green); }

.post-entry { margin-left: auto; }
/* ── 狀態容器 ── */
/* ── 貼文卡片格線 ── */
.post-grid {
  display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: var(--space-5);
  margin-bottom: var(--space-6);
}
.post-card {
  display: flex; flex-direction: column; gap: var(--space-2);
  background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius-lg);
  padding: var(--space-5); box-shadow: var(--shadow-sm);
}
.post-card-header { display: flex; align-items: center; }
.status-badge {
  display: inline-block; padding: var(--space-1) var(--space-3); border-radius: var(--radius-full);
  font-size: var(--text-xs); font-weight: var(--weight-bold);
}
.status-badge.searching { background: var(--warning-50); color: var(--warning-500); }
.status-badge.found { background: var(--green-100); color: var(--green); }
.status-badge.withdrawn { background: var(--neutral-100); color: var(--neutral-500); }

.post-title-link { text-decoration: none; }
.post-title-link:hover .post-title { color: var(--green); text-decoration: underline; }
.post-title { font-size: var(--text-lg); font-weight: var(--weight-bold); color: var(--text-primary); transition: color var(--duration-fast); }
.post-meta { font-size: var(--text-sm); color: var(--text-muted); }

.btn-manage {
  display: inline-flex; align-items: center; gap: var(--space-1); align-self: flex-start;
  margin-top: var(--space-1); padding: var(--space-2) var(--space-4); border-radius: var(--radius-md);
  border: 1px solid var(--border); color: var(--text-secondary);
  font-size: var(--text-sm); font-weight: var(--weight-medium); text-decoration: none; transition: all var(--duration-fast);
}
.btn-manage:hover { border-color: var(--green); color: var(--green); }
</style>
