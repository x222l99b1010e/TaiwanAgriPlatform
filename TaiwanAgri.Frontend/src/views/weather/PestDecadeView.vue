<!-- src/views/weather/PestDecadeView.vue -->
<!--
  這一頁原本的主軸是「旬密度趨勢」：一張統計卡顯示最高密度、一張折線圖畫各城市的密度變化。
  實測發現上游從未提供那個欄位的值（我方 136 筆與上游單頁 500 筆全部為 null），
  所以統計卡永遠顯示 0、折線圖永遠是一條貼著底的平線——兩者都不是壞掉，但看起來就是壞掉。

  處理方式是「刪掉假的、留下真的、講清楚為什麼」：縣市、鄉鎮、期別是真實資料，
  所以表格保留（兩個空欄顯示「—」讀起來是「這筆沒有值」，不像壞掉）；
  以密度為主軸的統計卡與折線圖移除；理由用一段提示講在頁面上，完整推導寫在 README。
-->
<template>
  <div class="page pest-view">
    <QueryLayout
      title="病蟲害旬報查詢"
      title-en="PEST DECADE REPORT"
      subtitle="依害蟲名稱查詢各縣市鄉鎮的旬別通報紀錄"
    >
      <template #actions>
        <Btn
          icon="mdi-magnify"
          :loading="isLoading"
          :disabled="!selectedPest"
          @click="handleQuery"
        >{{ isLoading ? '查詢中...' : '查詢' }}</Btn>
      </template>

      <template #filters>
        <div class="field-group">
          <label class="field-label" for="pest-select">選擇害蟲</label>
          <select
            id="pest-select"
            v-model="selectedPest"
            class="form-control pest-select"
            :disabled="isLoadingNames"
          >
            <option v-if="isLoadingNames" value="">載入中...</option>
            <option
              v-for="name in pestNames"
              :key="name"
              :value="name"
            >{{ name }}</option>
          </select>
        </div>
      </template>

      <template #hint>
        <HintBox title="關於「平均密度」與「全島比例」兩欄">
          這兩個數值欄位上游未提供值——實測我方 136 筆與上游單頁 500 筆全部為空，
          因此表格中一律顯示「—」。本頁呈現的是通報分布（哪些縣市鄉鎮在哪一旬有通報紀錄），
          不是密度趨勢。詳細的探勘過程寫在專案 README 的「已知限制」。
        </HintBox>
      </template>

      <template #results>
        <StateBlock v-if="!hasQueried" state="hint" message="請選擇害蟲後按下查詢" />
        <StateBlock v-else-if="isLoading" state="loading" message="資料載入中..." />
        <StateBlock
          v-else-if="errorMsg"
          state="error"
          :message="errorMsg"
          retryable
          @retry="handleQuery"
        />
        <StateBlock
          v-else-if="records.length === 0"
          state="empty"
          message="查無資料"
          hint="這種害蟲沒有旬報統計紀錄，可換一種害蟲再查"
        />

        <div v-else>
          <!-- 摘要：只留真的算得出來的三項 -->
          <div class="summary-bar">
            <div class="stat-card">
              <span class="stat-label">害蟲名稱</span>
              <span class="stat-value stat-value--text">{{ selectedPest }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-label">城市數</span>
              <span class="stat-value">{{ cityCount }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-label">資料筆數</span>
              <span class="stat-value">{{ records.length }}</span>
            </div>
          </div>

          <!-- 明細表格 -->
          <div class="table-wrap">
            <table class="data-table">
              <thead>
                <tr>
                  <th>城市</th>
                  <th>鄉鎮</th>
                  <th class="num">年</th>
                  <th class="num">月</th>
                  <th>旬</th>
                  <th class="num">平均密度</th>
                  <th class="num">全島比例</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(r, i) in pagedRecords" :key="i">
                  <td class="city-cell">{{ r.city }}</td>
                  <td class="town-cell">{{ r.town }}</td>
                  <td class="num">{{ r.year }}</td>
                  <td class="num">{{ r.month }}</td>
                  <td>{{ tenDaysLabel(r.tenDays) }}</td>
                  <td class="num">{{ r.average ?? '—' }}</td>
                  <td class="num">{{ r.proportionIsland != null ? (r.proportionIsland * 100).toFixed(1) + '%' : '—' }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <PagerBar
            v-if="totalPages > 1"
            class="decade-pager"
            :current-page="currentPage"
            :total-pages="totalPages"
            :total-count="records.length"
            :visible-pages="visiblePages"
            :jump-page-input="jumpPageInput"
            :page-size="pageSize"
            :page-size-options="[50, 100, 200]"
            @change="changePage"
            @update:page-size="setPageSize"
            @update:jump-page-input="jumpPageInput = $event"
            @jump="handleJumpPage"
          />
        </div>
      </template>
    </QueryLayout>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { weatherApi, type PestDecadeResponseDto } from '@/api/weather'
import QueryLayout from '@/components/layouts/QueryLayout.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import HintBox from '@/components/ui/HintBox.vue'
import Btn from '@/components/ui/Btn.vue'
import PagerBar from '@/components/PagerBar.vue'
import { usePagination } from '@/composables/usePagination'

// ── 狀態 ─────────────────────────────────────────────
const pestNames      = ref<string[]>([])
const selectedPest   = ref('')
const records        = ref<PestDecadeResponseDto[]>([])
const isLoadingNames = ref(false)
const isLoading      = ref(false)
const hasQueried     = ref(false)
const errorMsg       = ref('')

// ── 統計 ─────────────────────────────────────────────
const cityCount = computed(() =>
  new Set(records.value.map(r => r.city)).size
)

// ── 前端分頁 ──────────────────────────────────────────
// 一種害蟲橫跨全台鄉鎮 × 多個旬別，列數常常上百，整頁列出來會很長。資料已全在 records
// 記憶體裡，換頁只重切片、不重打 API，跟雨量頁同一套做法（onChange 因此是空的）。
const {
  pageSize, currentPage, jumpPageInput, visiblePages, totalPages,
  changePage, handleJumpPage, setPageSize,
} = usePagination({
  storageKey: 'pestDecade.pageSize',
  pageSizeOptions: [50, 100, 200],
  defaultPageSize: 50,
  totalCount: () => records.value.length,
  onChange: () => {},
})
const pagedRecords = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  return records.value.slice(start, start + pageSize.value)
})

// ── 旬標籤 ───────────────────────────────────────────
function tenDaysLabel(n: number) {
  return n === 1 ? '上旬' : n === 2 ? '中旬' : '下旬'
}

// ── 初始化：載入害蟲清單 ──────────────────────────────
onMounted(async () => {
  isLoadingNames.value = true
  try {
    pestNames.value = await weatherApi.getPestNames()
    if (pestNames.value.length) selectedPest.value = pestNames.value[0]!
  } catch {
    errorMsg.value = '載入害蟲清單失敗'
  } finally {
    isLoadingNames.value = false
  }
})

// ── 查詢 ──────────────────────────────────────────────
async function handleQuery() {
  if (!selectedPest.value) return
  isLoading.value = true
  hasQueried.value = true
  errorMsg.value = ''
  records.value = []
  try {
    records.value = await weatherApi.getPestDecade(selectedPest.value)
    currentPage.value = 1   // 新查詢回到第一頁，否則會停在上次的頁碼看不到資料
  } catch {
    errorMsg.value = '查詢失敗，請稍後再試'
  } finally {
    isLoading.value = false
  }
}
</script>

<style scoped>
/* 顏色全部改用 semantic 層；欄位、摘要列與卡片外殼已收進 base.css，
   這裡只留這一頁真正不同的部分。 */
.pest-view { min-width: 960px; }

.pest-select { min-width: 200px; }
.stat-card { min-width: 130px; }

/* 資料收在一個有高度上限的 data grid：內部自己捲、表頭吸頂，配合下方分頁，
   整頁不會被上百列撐得很長（跟雨量頁一致）。 */
.table-wrap {
  max-height: min(58vh, 620px);
  overflow: auto;
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}
.decade-pager { margin-top: var(--space-4); }
/* 表格外殼已收進 base.css 的 .data-table，這裡只留這一頁真正不同的部分 */

.city-cell  { font-weight: var(--weight-bold); color: var(--color-text); }
.town-cell  { color: var(--color-text-dim); }
</style>
