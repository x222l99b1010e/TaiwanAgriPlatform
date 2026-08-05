<template>
  <div class="legal-business-view">
    <div class="page-header">
      <h2 class="section-title">合法寵物業查詢</h2>
      <p class="section-subtitle">合法寵物業者評鑑資料與農業部官方遺失啟事，皆無座標資料，僅提供表格查詢</p>
    </div>

    <div class="tab-switch">
      <button class="tab-btn" :class="{ active: activeTab === 'legal' }" @click="switchTab('legal')">
        合法寵物業查詢
      </button>
      <button class="tab-btn" :class="{ active: activeTab === 'official' }" @click="switchTab('official')">
        官方遺失啟事
      </button>
    </div>

    <!-- ── Tab 1：合法寵物業查詢 ── -->
    <section v-if="activeTab === 'legal'">
      <div class="filter-bar">
        <CitySelector v-model="legalCounty" include-all />

        <div class="field-group">
          <label class="field-label">動物類型</label>
          <select v-model="legalAnimalType" class="filter-select">
            <option v-for="opt in legalAnimalTypeOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
          </select>
        </div>

        <div class="field-group">
          <label class="field-label">評鑑等級</label>
          <select v-model="legalRankGrade" class="filter-select">
            <option v-for="opt in legalRankGradeOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
          </select>
        </div>

        <div class="field-group">
          <label class="field-label">營業狀態</label>
          <select v-model="legalStateFlag" class="filter-select">
            <option v-for="opt in legalStateFlagOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
          </select>
        </div>

        <div class="field-group">
          <label class="field-label">業務項目</label>
          <select v-model="legalBusinessItem" class="filter-select">
            <option v-for="opt in legalBusinessItemOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
          </select>
        </div>

        <div class="field-group">
          <label class="field-label">排序</label>
          <div class="sort-control">
            <select v-model="legalSortBy" class="filter-select">
              <option v-for="opt in legalSortByOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
            </select>
            <button
              type="button" class="sort-dir-btn" :title="legalSortDescending ? '降冪，點擊切換升冪' : '升冪，點擊切換降冪'"
              @click="legalSortDescending = !legalSortDescending"
            >
              <span class="mdi" :class="legalSortDescending ? 'mdi-sort-descending' : 'mdi-sort-ascending'" />
            </button>
          </div>
        </div>

        <span v-if="store.isLoadingLegalSpecificPets" class="loading-hint">
          <span class="loading-spinner-sm" />載入中...
        </span>
      </div>

      <div v-if="store.legalSpecificPetsError" class="state-box error-box">
        <span class="mdi mdi-alert-circle state-icon" />
        <span class="state-text">{{ store.legalSpecificPetsError }}</span>
        <button class="btn-retry" @click="fetchLegal">重試</button>
      </div>

      <div v-else-if="store.legalSpecificPetsPage && store.legalSpecificPetsPage.items.length === 0" class="state-box">
        <span class="mdi mdi-store-search-outline state-icon" />
        <span class="state-text">此縣市查無合法寵物業資料</span>
      </div>

      <div v-else-if="store.legalSpecificPetsPage" class="table-section">
        <div class="table-wrapper">
          <table class="data-table legal-table">
            <colgroup>
              <col class="col-name" />
              <col class="col-county" />
              <col class="col-business" />
              <col class="col-animal-type" />
              <col class="col-address" />
              <col class="col-permit-number" />
              <col class="col-permit-date" />
              <col class="col-owner" />
              <col class="col-rank" />
              <col class="col-state" />
            </colgroup>
            <thead>
              <tr>
                <th>業者名稱</th>
                <th>縣市</th>
                <th>業務項目</th>
                <th>動物類型</th>
                <th>地址</th>
                <th>許可證字號</th>
                <th>許可證效期</th>
                <th>負責人</th>
                <th>評鑑等級</th>
                <th>營業狀態</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in store.legalSpecificPetsPage.items" :key="item.id">
                <td class="cell-name">{{ item.name }}</td>
                <td>{{ item.county }}</td>
                <td>{{ item.businessItems || '—' }}</td>
                <td>{{ animalTypeLabel(item.animalType) }}</td>
                <td class="cell-address" :title="item.address">{{ item.address }}</td>
                <td class="cell-mono">{{ item.permitNumber || '—' }}</td>
                <td class="cell-date">{{ item.permitValidDate ?? '—' }}</td>
                <td>{{ item.ownerName || item.responsibleStaffName || '—' }}</td>
                <td>
                  <span class="rank-badge" :title="item.rankText || undefined">{{ rankGradeLabel(item.rankGrade) }}</span>
                </td>
                <td>
                  <span class="state-badge" :class="stateClass(item.stateFlag)">{{ stateLabel(item.stateFlag) }}</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <PagerBar
          :total-pages="store.legalSpecificPetsPage.totalPages"
          :total-count="store.legalSpecificPetsPage.totalCount"
          :current-page="legalPage.currentPage.value"
          :visible-pages="legalPage.visiblePages.value"
          :jump-page-input="legalPage.jumpPageInput.value"
          @change="legalPage.changePage"
          @update:jump-page-input="legalPage.jumpPageInput.value = $event"
          @jump="legalPage.handleJumpPage"
        />
      </div>
    </section>

    <!-- ── Tab 2：官方遺失啟事 ── -->
    <section v-else>
      <div class="filter-bar">
        <div class="field-group">
          <label class="field-label">動物類別</label>
          <select v-model="officialCategory" class="filter-select">
            <option v-for="opt in officialCategoryOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
          </select>
        </div>

        <div class="field-group">
          <label class="field-label">性別</label>
          <select v-model="officialSex" class="filter-select">
            <option v-for="opt in officialSexOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
          </select>
        </div>

        <div class="field-group">
          <label class="field-label">排序</label>
          <div class="sort-control">
            <select v-model="officialSortBy" class="filter-select">
              <option v-for="opt in officialSortByOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
            </select>
            <button
              type="button" class="sort-dir-btn" :title="officialSortDescending ? '降冪，點擊切換升冪' : '升冪，點擊切換降冪'"
              @click="officialSortDescending = !officialSortDescending"
            >
              <span class="mdi" :class="officialSortDescending ? 'mdi-sort-descending' : 'mdi-sort-ascending'" />
            </button>
          </div>
        </div>

        <span v-if="store.isLoadingOfficialLostPetPosts" class="loading-hint">
          <span class="loading-spinner-sm" />載入中...
        </span>
      </div>

      <div v-if="store.officialLostPetPostsError" class="state-box error-box">
        <span class="mdi mdi-alert-circle state-icon" />
        <span class="state-text">{{ store.officialLostPetPostsError }}</span>
        <button class="btn-retry" @click="fetchOfficial">重試</button>
      </div>

      <div v-else-if="store.officialLostPetPostsPage && store.officialLostPetPostsPage.items.length === 0" class="state-box">
        <span class="mdi mdi-clipboard-search-outline state-icon" />
        <span class="state-text">目前查無官方遺失啟事資料</span>
      </div>

      <div v-else-if="store.officialLostPetPostsPage" class="table-section">
        <div class="table-wrapper">
          <table class="data-table">
            <thead>
              <tr>
                <th>寵物名稱</th>
                <th>類別</th>
                <th>性別</th>
                <th>品種</th>
                <th>毛色</th>
                <th>走失時間</th>
                <th>走失地點</th>
                <th>飼主</th>
                <th>聯絡電話</th>
                <th>照片</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in store.officialLostPetPostsPage.items" :key="item.id">
                <td class="cell-name">{{ item.petName || '未提供' }}</td>
                <td>{{ categoryLabel(item.category) }}</td>
                <td>{{ sexLabel(item.sex) }}</td>
                <td>{{ item.variety || '—' }}</td>
                <td>{{ item.coat || '—' }}</td>
                <td class="cell-date">{{ item.lostTime }}</td>
                <td class="cell-address" :title="item.lostPlace">{{ item.lostPlace }}</td>
                <td>{{ item.feederName || '—' }}</td>
                <td class="cell-mono">{{ item.phoneNum || '—' }}</td>
                <td>
                  <a v-if="isUrl(item.pictureUrl)" :href="item.pictureUrl" target="_blank" rel="noopener noreferrer"
                    class="picture-link"><span class="mdi mdi-image-outline" /> 查看</a>
                  <span v-else class="cell-muted">—</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <PagerBar
          :total-pages="store.officialLostPetPostsPage.totalPages"
          :total-count="store.officialLostPetPostsPage.totalCount"
          :current-page="officialPage.currentPage.value"
          :visible-pages="officialPage.visiblePages.value"
          :jump-page-input="officialPage.jumpPageInput.value"
          @change="officialPage.changePage"
          @update:jump-page-input="officialPage.jumpPageInput.value = $event"
          @jump="officialPage.handleJumpPage"
        />
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import CitySelector from '@/components/CitySelector.vue'
import PagerBar from '@/components/PagerBar.vue'
import { usePetStore } from '@/stores/pet'
import { usePagination } from '@/composables/usePagination'
import type {
  LegalPetAnimalType, LegalPetRankGrade, LegalPetStateFlag, LegalSpecificPetSortByValue,
  AnimalKind, OfficialLostPetPostSortByValue,
} from '@/api/pet'

const store = usePetStore()

const activeTab = ref<'legal' | 'official'>('legal')
// 只有第一次切到某個分頁籤才打 API，之後切回去用已經快取在 store 裡的資料，不重複查詢
const hasFetchedOfficial = ref(false)

function switchTab(tab: 'legal' | 'official') {
  activeTab.value = tab
  if (tab === 'official' && !hasFetchedOfficial.value) {
    hasFetchedOfficial.value = true
    fetchOfficial()
  }
}

// ─── Tab 1：合法寵物業查詢 ──────────────────────────────────────────────

const legalCounty = ref('')
const legalAnimalType = ref<LegalPetAnimalType | ''>('')
const legalRankGrade = ref<LegalPetRankGrade | ''>('')
const legalStateFlag = ref<LegalPetStateFlag | ''>('')
const legalBusinessItem = ref('')
const legalSortBy = ref<LegalSpecificPetSortByValue>('Name')
const legalSortDescending = ref(false)

const legalAnimalTypeOptions: { value: LegalPetAnimalType | ''; label: string }[] = [
  { value: '',     label: '全部種類' },
  { value: 'Dog',  label: '狗' },
  { value: 'Cat',  label: '貓' },
  { value: 'Both', label: '貓狗皆可' },
  { value: 'Other', label: '其他' },
]
const legalRankGradeOptions: { value: LegalPetRankGrade | ''; label: string }[] = [
  { value: '',          label: '全部等級' },
  { value: 'Excellent', label: '優等' },
  { value: 'GradeA',    label: '甲等' },
  { value: 'GradeB',    label: '乙等' },
  { value: 'GradeC',    label: '丙等' },
  { value: 'Unknown',   label: '未評鑑' },
]
const legalStateFlagOptions: { value: LegalPetStateFlag | ''; label: string }[] = [
  { value: '',           label: '全部狀態' },
  { value: 'Operating',  label: '營業中' },
  { value: 'Closed',     label: '歇業' },
  { value: 'Suspended',  label: '停業' },
  { value: 'Revoked',    label: '廢止' },
  { value: 'Unknown',    label: '不明' },
]
// BusinessItems 是 "ABC" 這種代碼組合字串（A=繁殖 B=買賣 C=寄養），比對用 Contains 單字元篩選
const legalBusinessItemOptions: { value: string; label: string }[] = [
  { value: '', label: '全部項目' },
  { value: 'A', label: '繁殖' },
  { value: 'B', label: '買賣' },
  { value: 'C', label: '寄養' },
]
const legalSortByOptions: { value: LegalSpecificPetSortByValue; label: string }[] = [
  { value: 'Name',            label: '依名稱' },
  { value: 'PermitValidDate', label: '依許可證效期' },
  { value: 'RankGrade',       label: '依評鑑等級' },
]

const legalPage = usePagination({
  storageKey: 'legalSpecificPets.pageSize',
  totalPages: () => store.legalSpecificPetsPage?.totalPages,
  onChange: fetchLegal,
  defaultPageSize: 20,
})

function fetchLegal() {
  store.fetchLegalSpecificPets({
    county: legalCounty.value || undefined,
    animalType: legalAnimalType.value || undefined,
    rankGrade: legalRankGrade.value || undefined,
    stateFlag: legalStateFlag.value || undefined,
    businessItem: legalBusinessItem.value || undefined,
    sortBy: legalSortBy.value,
    sortDescending: legalSortDescending.value,
    page: legalPage.currentPage.value,
    pageSize: legalPage.pageSize.value,
  })
}

// 篩選或排序條件變動一律重置回第一頁再查——複合條件是疊加 Where 子句，前端只要把目前選到的
// 條件一次送出即可，不需要為每個欄位各寫一次查詢函式
watch(
  [legalCounty, legalAnimalType, legalRankGrade, legalStateFlag, legalBusinessItem, legalSortBy, legalSortDescending],
  () => {
    legalPage.currentPage.value = 1
    fetchLegal()
  }
)

function animalTypeLabel(t: string): string {
  return { Dog: '狗', Cat: '貓', Both: '貓狗皆可', Other: '其他' }[t] ?? t
}
function stateLabel(flag: string): string {
  return { Operating: '營業中', Closed: '歇業', Suspended: '停業', Revoked: '廢止', Unknown: '不明' }[flag] ?? flag
}
function stateClass(flag: string): string {
  return { Operating: 'ok', Closed: 'closed', Suspended: 'suspended', Revoked: 'revoked', Unknown: 'unknown' }[flag] ?? 'unknown'
}
/**
 * 評鑑等級一律顯示這個翻譯過的標籤，不要 fallback 到原始 rankGrade 字串（後端沒有全域註冊
 * JsonStringEnumConverter，item.rankGrade 雖然已經是字串，但值是 "Excellent"/"GradeB" 這種
 * 沒翻譯的 enum 名稱，直接顯示會跟篩選下拉選單的中文選項對不起來）。原始 rankText（官方原始
 * 用字，可能是「特優」這種跟標準分級表不同的措辭）改放 title 提示，不丟資訊但不當主要顯示文字
 */
function rankGradeLabel(grade: string): string {
  return { Excellent: '優等', GradeA: '甲等', GradeB: '乙等', GradeC: '丙等', Unknown: '未評鑑' }[grade] ?? grade
}

// ─── Tab 2：官方遺失啟事 ────────────────────────────────────────────────

const officialCategory = ref<AnimalKind | ''>('')
const officialSex = ref<'Male' | 'Female' | 'Other' | 'Unknown' | ''>('')
const officialSortBy = ref<OfficialLostPetPostSortByValue>('LostTime')
const officialSortDescending = ref(true) // 預設維持既有行為：最新走失的在前

const officialCategoryOptions: { value: AnimalKind | ''; label: string }[] = [
  { value: '',      label: '全部類別' },
  { value: 'Dog',   label: '狗' },
  { value: 'Cat',   label: '貓' },
  { value: 'Other', label: '其他' },
]
const officialSexOptions: { value: 'Male' | 'Female' | 'Other' | 'Unknown' | ''; label: string }[] = [
  { value: '',        label: '全部性別' },
  { value: 'Male',    label: '公' },
  { value: 'Female',  label: '母' },
  { value: 'Other',   label: '其他' },
  { value: 'Unknown', label: '不明' },
]
const officialSortByOptions: { value: OfficialLostPetPostSortByValue; label: string }[] = [
  { value: 'LostTime', label: '依走失時間' },
  { value: 'Category', label: '依類別' },
  { value: 'Sex',       label: '依性別' },
]

const officialPage = usePagination({
  storageKey: 'officialLostPetPosts.pageSize',
  totalPages: () => store.officialLostPetPostsPage?.totalPages,
  onChange: fetchOfficial,
  defaultPageSize: 20,
})

function fetchOfficial() {
  store.fetchOfficialLostPetPosts({
    category: officialCategory.value || undefined,
    sex: officialSex.value || undefined,
    sortBy: officialSortBy.value,
    sortDescending: officialSortDescending.value,
    page: officialPage.currentPage.value,
    pageSize: officialPage.pageSize.value,
  })
}

watch(
  [officialCategory, officialSex, officialSortBy, officialSortDescending],
  () => {
    officialPage.currentPage.value = 1
    fetchOfficial()
  }
)

function categoryLabel(c: string): string {
  return { Dog: '狗', Cat: '貓', Other: '其他' }[c] ?? c
}
function sexLabel(s: string): string {
  return { Male: '公', Female: '母', Other: '其他', Unknown: '不明' }[s] ?? s
}
function isUrl(value: string): boolean {
  return /^https?:\/\//i.test(value)
}

onMounted(fetchLegal)
</script>

<style scoped>
.legal-business-view { padding: 36px 56px; width: 100%; box-sizing: border-box; }

.page-header { margin-bottom: 20px; }
.section-title { font-size: 22px; font-weight: 700; color: var(--text-primary); margin-bottom: 6px; }
.section-subtitle { font-size: 13px; color: var(--text-muted); }

.tab-switch {
  display: flex; gap: 6px; margin-bottom: 20px;
  background: var(--surface-2); border: 1px solid var(--border);
  border-radius: 10px; padding: 4px; width: fit-content;
}
.tab-btn {
  padding: 8px 22px; border-radius: 8px; border: none; background: transparent;
  color: var(--text-secondary); font-size: 13.5px; font-weight: 600; cursor: pointer; transition: all 0.15s;
}
.tab-btn:hover { color: var(--green); }
.tab-btn.active { background: var(--green); color: white; }

.filter-bar {
  display: flex; align-items: flex-end; gap: 16px; margin-bottom: 20px; flex-wrap: wrap;
  padding: 16px 20px; background: var(--surface); border: 1px solid var(--border); border-radius: 12px;
}

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

.loading-hint { display: inline-flex; align-items: center; gap: 8px; color: var(--text-muted); font-size: 13px; }
.loading-hint.standalone { margin-bottom: 20px; }
.loading-spinner-sm {
  width: 14px; height: 14px; border: 2px solid #c8e6c9; border-top-color: var(--green);
  border-radius: 50%; animation: spin 0.8s linear infinite;
}
@keyframes spin { to { transform: rotate(360deg); } }

.state-box {
  display: flex; flex-direction: column; align-items: center; gap: 12px;
  padding: 56px 32px; background: var(--surface); border: 1px solid var(--border); border-radius: 16px;
}
.state-icon { font-size: 36px; color: #aaa; }
.state-text { font-size: 15px; color: var(--text-muted); }
.error-box { background: #fff5f5; border-color: #ffcdd2; color: #c62828; }
.btn-retry {
  padding: 8px 24px; border-radius: 999px; border: 1.5px solid #c62828;
  background: transparent; color: #c62828; font-size: 13px; font-weight: 600; cursor: pointer;
}
.btn-retry:hover { background: #fff5f5; }

.table-section { display: flex; flex-direction: column; gap: 16px; }
.table-wrapper {
  background: var(--surface); border: 1px solid var(--border); border-radius: 16px;
  box-shadow: 0 2px 8px rgba(46,125,50,0.06); max-height: 600px; overflow: auto;
}

.data-table { width: 100%; min-width: 1100px; border-collapse: collapse; font-size: 13px; }
.data-table thead th {
  position: sticky; top: 0; background: #f1f8f1; text-align: left; padding: 12px 16px;
  font-weight: 700; color: #1b5e20; border-bottom: 1px solid var(--border); white-space: nowrap; z-index: 1;
}
.data-table td { padding: 12px 16px; border-bottom: 1px solid var(--border); color: var(--text-primary); vertical-align: top; }
.data-table tbody tr:hover { background: #fafdf9; }
.data-table tbody tr:last-child td { border-bottom: none; }

.cell-name { font-weight: 600; white-space: nowrap; }
.cell-mono { font-family: monospace; font-size: 12px; color: var(--text-muted); white-space: nowrap; }
.cell-date { white-space: nowrap; font-variant-numeric: tabular-nums; }
.cell-address { max-width: 260px; font-size: 12px; }
.cell-muted { color: var(--text-muted); }

/* 合法寵物業表格：table-layout: fixed 讓 colgroup 的欄寬真正生效（不然瀏覽器只會把它當參考值，
   還是照內容自動調整）；固定寬度後，內容比欄寬長的儲存格靠 white-space/wrap 決定要不要換行。
   業者名稱／評鑑等級這兩欄的內容長度落差很大（評鑑等級有時是簡短代碼，有時是「已搬遷至新址，
   請洽新址辦理註銷許可」這種長句），一律改成允許自動換行＋加寬，不要用 nowrap——
   nowrap 在固定欄寬表格裡不會讓欄位變寬，只會讓文字溢出、視覺上蓋到隔壁欄位 */
.legal-table { min-width: 1320px; table-layout: fixed; }
.legal-table .col-name         { width: 160px; }
.legal-table .col-county       { width: 100px; }
.legal-table .col-business     { width: 90px; }
.legal-table .col-animal-type  { width: 90px; }
.legal-table .col-address      { width: 280px; }
.legal-table .col-permit-number{ width: 170px; }
.legal-table .col-permit-date  { width: 110px; }
.legal-table .col-owner        { width: 90px; }
.legal-table .col-rank         { width: 170px; }
.legal-table .col-state        { width: 90px; }
.legal-table .cell-address { max-width: none; white-space: normal; word-break: break-word; }
.legal-table .cell-mono { white-space: normal; word-break: break-all; }
.legal-table .cell-name { white-space: normal; word-break: break-word; }

.rank-badge {
  /* rankText 有時是簡短代碼（GradeB）、有時是長句（已搬遷至新址，請洽新址辦理註銷許可），
     不能用 nowrap；改成允許換行的圓角色塊，寬度跟著 col-rank 走 */
  display: inline-block; padding: 3px 10px; border-radius: 12px;
  background: #fff8e1; color: #f57f17; font-size: 12px; font-weight: 700;
  white-space: normal; word-break: break-word; line-height: 1.4;
}

.state-badge { display: inline-block; padding: 3px 10px; border-radius: 999px; font-size: 12px; font-weight: 700; white-space: nowrap; }
.state-badge.ok { background: #e8f5e9; color: var(--green); }
.state-badge.closed { background: #f0f0f0; color: #757575; }
.state-badge.suspended { background: #fff3e0; color: #e65100; }
.state-badge.revoked { background: #ffebee; color: #c62828; }
.state-badge.unknown { background: #f0f0f0; color: #9e9e9e; }

.picture-link { display: inline-flex; align-items: center; gap: 4px; font-size: 12px; color: #1565c0; text-decoration: none; white-space: nowrap; }
.picture-link:hover { text-decoration: underline; }
</style>
