<!--
  src/views/pet/ShelterDetailView.vue
  職責：收容所詳情頁 /pet/shelter-map/:shelterId（不掛週次分支新增）。
  地圖 popup 原本用 POPUP_ANIMAL_LIMIT=50 截斷動物清單——調高數字解決不了問題（
  「寫 50 隻跟寫總數沒差異，因為都只看得到前 50 隻」），這頁才是真正的解法：用既有 PagerBar
  機制分頁列出該所全部動物，popup 回歸「快速預覽」定位，長清單瀏覽交給這頁。

  版面採 datagrid（沿用 LegalBusinessView 的表格＋篩選＋排序＋清除篩選慣例），
  不用卡片格線：大所（如 150 隻以上）用卡片格線要捲很多頁才看得完
  一輪，資料表格才是這種「同結構、多筆數、要橫向比較欄位」清單的正確形狀
  （跟合法寵物業查詢／官方遺失啟事當初選表格是同一個判準）。
-->
<template>
  <div class="page shelter-detail-view">
    <RouterLink to="/pet/shelter-map" class="back-link">
      <span class="mdi mdi-arrow-left" /> 回收容動物地圖
    </RouterLink>

    <QueryLayout
      :title="shelterHeader ? `${shelterHeader.name}（${shelterHeader.county}）` : '收容所詳情'"
      :subtitle="shelterHeader?.address"
    >
      <template #actions>
        <span v-if="store.isLoadingShelterAnimalsByShelter" class="loading-hint">
          <span class="loading-spinner-sm" />載入中...
        </span>
        <span v-else-if="page" class="stat-text">共 {{ page.totalCount }} 隻在養動物</span>
        <Btn
          v-if="hasActiveFilters"
          variant="secondary"
          icon="mdi-filter-remove-outline"
          title="清除所有篩選條件，回到未篩選狀態"
          @click="clearFilters"
        >清除篩選</Btn>
      </template>

      <!-- 篩選列：頁首（收容所名稱）先抓到就先顯示，篩選列在資料還沒回來前也能先操作，
           不必等第一次查詢完成——跟 ShelterMapView 的篩選列同一個設計慣例 -->
      <template #filters>
        <div class="field-group">
          <label class="field-label" for="shelter-kind">動物種類</label>
          <select id="shelter-kind" v-model="kind" class="form-control filter-select">
            <option v-for="opt in kindOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
          </select>
        </div>

        <div class="field-group">
          <label class="field-label" for="shelter-sex">性別</label>
          <select id="shelter-sex" v-model="sex" class="form-control filter-select">
            <option v-for="opt in sexOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
          </select>
        </div>

        <div class="field-group">
          <label class="field-label" for="shelter-sort">排序</label>
          <div class="sort-control">
            <select id="shelter-sort" v-model="sortBy" class="form-control filter-select">
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

      <template #results>
        <StateBlock
          v-if="store.shelterAnimalsByShelterError"
          state="error"
          :message="store.shelterAnimalsByShelterError"
          retryable
          @retry="fetchPage"
        />

        <!-- shelterId 打錯、或這間收容所目前剛好沒有在養動物、或篩選條件太窄，三種情況後端都回傳空頁；
             有沒有下篩選條件決定文案要不要提「調整篩選」這個出口 -->
        <StateBlock
          v-else-if="page && page.totalCount === 0"
          state="empty"
          icon="mdi-home-search-outline"
          :message="hasActiveFilters ? '此篩選條件下查無在養動物' : '查無此收容所，或目前沒有在養動物資料'"
          :hint="hasActiveFilters ? '可以按上方的「清除篩選」看全部' : undefined"
        />

        <div v-else-if="page" class="table-section">
          <div class="table-wrapper">
            <table class="data-table">
              <colgroup>
                <col class="col-id" />
                <col class="col-kind" />
                <col class="col-sex" />
                <col class="col-age" />
                <col class="col-sterilization" />
                <col class="col-variety" />
                <col class="col-colour" />
                <col class="col-place" />
                <col class="col-date" />
                <col class="col-album" />
              </colgroup>
              <thead>
                <tr>
                  <th>編號</th>
                  <th>種類</th>
                  <th>性別</th>
                  <th>年齡</th>
                  <th>結紮</th>
                  <th>品種</th>
                  <th>毛色</th>
                  <th>拾獲地點</th>
                  <th>建檔日期</th>
                  <th>相簿</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="a in page.items" :key="a.id">
                  <td class="cell-mono">
                    <RouterLink :to="`/pet/shelter-map/animals/${a.id}`" class="animal-link">{{ a.animalSubId }}</RouterLink>
                  </td>
                  <td>{{ animalKindLabel(a.kind) }}</td>
                  <td>{{ animalSexLabel(a.sex) }}</td>
                  <td>{{ a.age || '—' }}</td>
                  <td>{{ sterilizationLabel(a.sterilization) }}</td>
                  <td>{{ a.variety || '—' }}</td>
                  <td>{{ a.colour || '—' }}</td>
                  <td class="cell-address" :title="a.foundPlace">{{ a.foundPlace || '—' }}</td>
                  <td class="cell-date">{{ a.createdTime }}</td>
                  <td>
                    <a v-if="isDisplayableAlbumLink(a.albumFile)" :href="a.albumFile" target="_blank" rel="noopener noreferrer" class="album-link">
                      <span class="mdi mdi-image-multiple-outline" /> 查看
                    </a>
                    <span v-else class="cell-muted">—</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <PagerBar
            v-if="page.totalPages > 1"
            :current-page="currentPage"
            :total-pages="page.totalPages"
            :total-count="page.totalCount"
            :visible-pages="visiblePages"
            :jump-page-input="jumpPageInput"
            @change="changePage"
            @update:jump-page-input="jumpPageInput = $event"
            @jump="handleJumpPage"
          />
        </div>
      </template>
    </QueryLayout>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import PagerBar from '@/components/PagerBar.vue'
import { usePetStore } from '@/stores/pet'
import { usePagination } from '@/composables/usePagination'
import { animalKindLabel, animalSexLabel, isDisplayableAlbumLink, sterilizationLabel } from '@/utils/shelterAnimal'
import type { AnimalKind, AnimalSex, ShelterAnimalSortByValue } from '@/api/pet'
import QueryLayout from '@/components/layouts/QueryLayout.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'

// id 由 router 的 props 函式模式轉成 number（見 router/index.ts）
const props = defineProps<{ shelterId: number }>()

const store = usePetStore()
const page = computed(() => store.shelterAnimalsByShelterPage)

// 收容所名稱／地址／縣市不是這支端點獨立回傳的欄位，而是每一筆動物 DTO 都帶著的展示資訊
// （沿用地圖端點原本的設計：Include(Shelter) 一次帶出，不用再查一次 Shelter 表）。
// 用 ref 快取「上一次成功拿到的頁首」而不是直接 computed 讀 page.items[0]：篩選條件收得太窄、
// 這一頁剛好 0 筆時，頁首（收容所名稱／地址）應該繼續顯示，不能因為篩選結果空了就跟著消失——
// 「查無資料」講的是動物清單、不代表這間收容所的身分資訊也不見了
const shelterHeader = ref<{ name: string; address: string; county: string } | null>(null)
watch(page, (p) => {
  const first = p?.items[0]
  if (first) {
    shelterHeader.value = { name: first.shelterName, address: first.shelterAddress, county: first.county }
  }
})

// ─── 篩選狀態 ───────────────────────────────────────────────────────────

const kind = ref<AnimalKind | ''>('')
const sex = ref<AnimalSex | ''>('')
const sortBy = ref<ShelterAnimalSortByValue>('CreatedTime')
const sortDescending = ref(true) // 預設維持後端既有行為：最新拾獲的在前

const kindOptions: { value: AnimalKind | ''; label: string }[] = [
  { value: '',      label: '全部種類' },
  { value: 'Dog',   label: '狗' },
  { value: 'Cat',   label: '貓' },
  { value: 'Other', label: '其他' },
]
const sexOptions: { value: AnimalSex | ''; label: string }[] = [
  { value: '',        label: '全部性別' },
  { value: 'Male',    label: '公' },
  { value: 'Female',  label: '母' },
  { value: 'Other',   label: '其他' },
  { value: 'Unknown', label: '不明' },
]
const sortByOptions: { value: ShelterAnimalSortByValue; label: string }[] = [
  { value: 'CreatedTime', label: '依拾獲時間' },
  { value: 'AnimalSubId', label: '依編號' },
]

// 排序不算篩選條件（它不影響筆數，只影響順序），清除範圍只含 Kind／Sex——
// 跟 LegalBusinessView／ShelterMapView 的既有慣例一致
const hasActiveFilters = computed(() => Boolean(kind.value || sex.value))

function clearFilters() {
  kind.value = ''
  sex.value = ''
}

// ─── 分頁與查詢 ───────────────────────────────────────────────────────────

const { currentPage, visiblePages, jumpPageInput, handleJumpPage, changePage: paginationChangePage } = usePagination({
  storageKey: 'shelterAnimalsByShelter.pageSize',
  totalPages: () => page.value?.totalPages,
  onChange: fetchPage,
  defaultPageSize: 20,
})

function changePage(p: number) {
  paginationChangePage(p)
}

function fetchPage() {
  store.fetchShelterAnimalsByShelter(props.shelterId, {
    kind: kind.value || undefined,
    sex: sex.value || undefined,
    sortBy: sortBy.value,
    sortDescending: sortDescending.value,
    page: currentPage.value,
    pageSize: 20,
  })
}

onMounted(fetchPage)

// 篩選或排序條件變動一律重置回第一頁再查（跟 LegalBusinessView 同一個慣例）
watch([kind, sex, sortBy, sortDescending], () => {
  currentPage.value = 1
  fetchPage()
})

// 同一個元件被重用去看「另一間收容所」時（例如使用者從這頁的相簿連結晃了一圈又點別的收容所連結
// 回來），shelterId 改變要重置回第一頁、清空上一間的頁首快取，重新查
watch(() => props.shelterId, () => {
  shelterHeader.value = null
  currentPage.value = 1
  fetchPage()
})
</script>

<style scoped>
/* 顏色全部改用 semantic 層；返回連結與欄位外殼已收進 base.css。 */
.filter-select { min-width: 130px; }

.sort-control { display: flex; align-items: center; gap: var(--space-2); }
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

.loading-hint { display: inline-flex; align-items: center; gap: var(--space-2); color: var(--color-text-dim); font-size: var(--text-sm); }
.loading-spinner-sm {
  width: 14px; height: 14px; border: 2px solid var(--seed-200); border-top-color: var(--color-action);
  border-radius: var(--radius-full); animation: spin 0.8s linear infinite;
}
@keyframes spin { to { transform: rotate(360deg); } }
.stat-text { font-size: var(--text-sm); color: var(--color-text-dim); white-space: nowrap; }

/* ── datagrid（沿用 LegalBusinessView 的表格慣例：高一點的可捲動容器，表頭黏頂） ── */
.table-section { display: flex; flex-direction: column; gap: var(--space-4); }
.table-wrapper {
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  max-height: 720px; overflow: auto;
}
/* 表格外殼已收進 base.css 的 .data-table，這裡只留這一頁真正不同的部分 */
.data-table { min-width: 1020px; table-layout: fixed; }

.col-id            { width: 130px; }
.col-kind          { width: 70px; }
.col-sex           { width: 60px; }
.col-age           { width: 90px; }
.col-sterilization { width: 80px; }
.col-variety       { width: 130px; }
.col-colour        { width: 100px; }
.col-place         { width: 220px; }
.col-date          { width: 110px; }
.col-album         { width: 80px; }

.cell-mono { font-family: var(--font-num); font-size: var(--text-xs); color: var(--color-text-dim); white-space: normal; word-break: break-all; }
.animal-link { color: var(--color-action); font-weight: var(--weight-bold); text-decoration: none; }
.animal-link:hover { text-decoration: underline; }
.cell-date { white-space: nowrap; font-family: var(--font-num); font-variant-numeric: tabular-nums; }
.cell-address { white-space: normal; word-break: break-word; font-size: var(--text-xs); }
.cell-muted { color: var(--color-text-dim); }

.album-link { display: inline-flex; align-items: center; gap: var(--space-1); font-size: var(--text-xs); color: var(--color-action); text-decoration: none; white-space: nowrap; }
.album-link:hover { text-decoration: underline; }
</style>
