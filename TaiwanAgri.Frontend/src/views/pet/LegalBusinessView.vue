<template>
  <div class="page legal-business-view">
    <PageHeader
      title="合法寵物業查詢"
      subtitle="合法寵物業者評鑑資料與農業部官方遺失啟事，皆無座標資料，僅提供表格查詢"
    />

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
      <FilterCard>
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

        <Btn
          v-if="hasActiveLegalFilters"
          variant="secondary"
          icon="mdi-filter-remove-outline"
          title="清除所有篩選條件，回到未篩選狀態"
          @click="clearLegalFilters"
        >清除篩選</Btn>

        <span v-if="store.isLoadingLegalSpecificPets" class="loading-hint">
          <span class="loading-spinner-sm" />載入中...
        </span>
      </FilterCard>

      <StateBlock
        v-if="store.legalSpecificPetsError"
        state="error"
        :message="store.legalSpecificPetsError"
        retryable
        @retry="fetchLegal"
      />
      <StateBlock
        v-else-if="store.legalSpecificPetsPage && store.legalSpecificPetsPage.items.length === 0"
        state="empty"
        icon="mdi-store-search-outline"
        message="此縣市查無合法寵物業資料"
        hint="可以把篩選條件放寬，或換一個縣市再看看"
      />

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
                <td :title="item.businessItems">{{ businessItemsLabel(item.businessItems) }}</td>
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
      <FilterCard>
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

        <Btn
          v-if="hasActiveOfficialFilters"
          variant="secondary"
          icon="mdi-filter-remove-outline"
          title="清除所有篩選條件，回到未篩選狀態"
          @click="clearOfficialFilters"
        >清除篩選</Btn>

        <span v-if="store.isLoadingOfficialLostPetPosts" class="loading-hint">
          <span class="loading-spinner-sm" />載入中...
        </span>
      </FilterCard>

      <StateBlock
        v-if="store.officialLostPetPostsError"
        state="error"
        :message="store.officialLostPetPostsError"
        retryable
        @retry="fetchOfficial"
      />
      <StateBlock
        v-else-if="store.officialLostPetPostsPage && store.officialLostPetPostsPage.items.length === 0"
        state="empty"
        icon="mdi-clipboard-search-outline"
        message="目前查無官方遺失啟事資料"
        hint="可以把篩選條件放寬，或換一個縣市再看看"
      />

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
import { ref, computed, watch, onMounted } from 'vue'
import CitySelector from '@/components/CitySelector.vue'
import PagerBar from '@/components/PagerBar.vue'
import { usePetStore } from '@/stores/pet'
import { usePagination } from '@/composables/usePagination'
import PageHeader from '@/components/ui/PageHeader.vue'
import FilterCard from '@/components/ui/FilterCard.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'
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

// 五個篩選條件疊加後，要退回未篩選狀態得逐一改回「全部」，很容易漏掉其中一個而看不懂
// 為什麼筆數還是很少。集中判斷有沒有任何條件生效，並提供一次清空的出口。
// 排序不算篩選條件（它不影響筆數，只影響順序），所以不在清除範圍內。
const hasActiveLegalFilters = computed(() =>
  Boolean(legalCounty.value || legalAnimalType.value || legalRankGrade.value ||
          legalStateFlag.value || legalBusinessItem.value)
)

function clearLegalFilters() {
  // 逐一清空即可，上面那個 watch 會偵測到變動、自動重置頁碼並重查一次
  legalCounty.value = ''
  legalAnimalType.value = ''
  legalRankGrade.value = ''
  legalStateFlag.value = ''
  legalBusinessItem.value = ''
}

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

/**
 * BusinessItems 是 "ABC" 這種代碼組合字串，直接印出來使用者看不懂是什麼意思
 * （篩選下拉選單早就有中文對照，但表格欄位漏了套用——跟評鑑等級徽章當初是同一種疏漏）。
 * 逐字元翻譯後用頓號串起；遇到對照表沒有的代碼保留原字元，不吞掉未知資料。
 */
function businessItemsLabel(items: string | null | undefined): string {
  if (!items) return '—'
  const map: Record<string, string> = { A: '繁殖', B: '買賣', C: '寄養' }
  return [...items].map(ch => map[ch] ?? ch).join('、')
}

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

// 篩選條件比合法業者那頁少，但一鍵清空的價值不在「省下幾次點擊」，而在於提供一個
// 明確的「回到未篩選狀態」出口——使用者不必逐一回想自己動過哪幾個下拉。
// 同樣不含排序（排序不影響筆數）。
const hasActiveOfficialFilters = computed(() =>
  Boolean(officialCategory.value || officialSex.value)
)

function clearOfficialFilters() {
  officialCategory.value = ''
  officialSex.value = ''
}

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
.tab-switch {
  display: flex; gap: var(--space-2); margin-bottom: var(--space-5);
  background: var(--surface-2); border: 1px solid var(--border);
  border-radius: var(--radius-lg); padding: var(--space-1); width: fit-content;
}
.tab-btn {
  padding: var(--space-2) var(--space-6); border-radius: var(--radius-md); border: none; background: transparent;
  color: var(--text-secondary); font-size: var(--text-sm); font-weight: var(--weight-medium); cursor: pointer; transition: all var(--duration-fast);
}
.tab-btn:hover { color: var(--green); }
.tab-btn.active { background: var(--green); color: var(--neutral-0); }
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
.loading-hint { display: inline-flex; align-items: center; gap: var(--space-2); color: var(--text-muted); font-size: var(--text-sm); }
.loading-hint.standalone { margin-bottom: var(--space-5); }
.loading-spinner-sm {
  width: 14px; height: 14px; border: 2px solid var(--green-200); border-top-color: var(--green);
  border-radius: 50%; animation: spin 0.8s linear infinite;
}
@keyframes spin { to { transform: rotate(360deg); } }
.table-section { display: flex; flex-direction: column; gap: var(--space-4); }
.table-wrapper {
  background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius-xl);
  box-shadow: var(--shadow-md); max-height: 600px; overflow: auto;
}

.data-table { width: 100%; min-width: 1100px; border-collapse: collapse; font-size: var(--text-sm); }
.data-table thead th {
  position: sticky; top: 0; background: var(--green-50); text-align: left; padding: var(--space-3) var(--space-4);
  font-weight: var(--weight-bold); color: var(--green-800); border-bottom: 1px solid var(--border); white-space: nowrap; z-index: var(--z-base);
}
.data-table td { padding: var(--space-3) var(--space-4); border-bottom: 1px solid var(--border); color: var(--text-primary); vertical-align: top; }
.data-table tbody tr:hover { background: var(--green-50); }
.data-table tbody tr:last-child td { border-bottom: none; }

.cell-name { font-weight: var(--weight-medium); white-space: nowrap; }
.cell-mono { font-family: monospace; font-size: var(--text-xs); color: var(--text-muted); white-space: nowrap; }
.cell-date { white-space: nowrap; font-variant-numeric: tabular-nums; }
.cell-address { max-width: 260px; font-size: var(--text-xs); }
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
  display: inline-block; padding: var(--space-1) var(--space-3); border-radius: var(--radius-lg);
  background: var(--warning-50); color: var(--warning-500); font-size: var(--text-xs); font-weight: var(--weight-bold);
  white-space: normal; word-break: break-word; line-height: var(--leading-tight);
}

.state-badge { display: inline-block; padding: var(--space-1) var(--space-3); border-radius: var(--radius-full); font-size: var(--text-xs); font-weight: var(--weight-bold); white-space: nowrap; }
.state-badge.ok { background: var(--green-100); color: var(--green); }
.state-badge.closed { background: var(--neutral-100); color: var(--neutral-500); }
.state-badge.suspended { background: var(--warning-50); color: var(--warning-500); }
.state-badge.revoked { background: var(--danger-50); color: var(--danger-500); }
.state-badge.unknown { background: var(--neutral-100); color: var(--neutral-400); }

.picture-link { display: inline-flex; align-items: center; gap: var(--space-1); font-size: var(--text-xs); color: var(--info-500); text-decoration: none; white-space: nowrap; }
.picture-link:hover { text-decoration: underline; }
</style>
