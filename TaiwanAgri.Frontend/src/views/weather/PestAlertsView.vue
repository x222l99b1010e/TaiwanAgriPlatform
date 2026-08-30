<!-- src/views/weather/PestAlertsView.vue -->
<template>
  <div class="pest-alerts-view">
    <h1>病蟲害警報</h1>

    <section class="filter-section">
      <CitySelector v-model="selectedCity" />
      <button class="btn-clear" @click="clearCity">全台</button>
      <p v-if="errorMsg" class="error-msg">{{ errorMsg }}</p>
    </section>

    <!-- 載入中 -->
    <div v-if="isLoading" class="loading-hint">載入中...</div>

    <div v-else>
      <p v-if="alerts.length === 0" class="empty-hint">查無警報資料</p>

      <div v-else>
        <!-- 警報卡片牆 -->
        <div class="alert-list">
          <div
            class="alert-card"
            v-for="a in alerts"
            :key="a.id"
            :class="{ expanded: expandedId === a.id }"
            @click="toggleExpand(a.id)"
          >
            <!-- 卡片標頭 -->
            <div class="card-top">
              <div class="card-meta">
                <span class="pub-date">{{ a.pubDate.slice(0, 10) }}</span>
                <span v-if="a.issue" class="issue-badge">{{ a.issue }}</span>
              </div>
              <span class="expand-icon mdi"
                :class="expandedId === a.id ? 'mdi-chevron-up' : 'mdi-chevron-down'"
              />
            </div>

            <div class="card-subject">{{ a.subject }}</div>

            <!-- 標籤列 -->
            <div class="tag-row">
              <span
                class="tag city-tag"
                v-for="c in a.cities"
                :key="c"
              >{{ c }}</span>
              <span
                class="tag crop-tag"
                v-for="c in a.crops"
                :key="c"
              >{{ c }}</span>
            </div>

            <!-- 展開內容 -->
            <div class="card-body" v-if="expandedId === a.id">
              <div class="section-label">警報內文</div>
              <p class="body-text">{{ a.body }}</p>

              <template v-if="a.prescription">
                <div class="section-label prescription">防治處方</div>
                <p class="body-text">{{ a.prescription }}</p>
              </template>
            </div>
          </div>
        </div>

        <!-- 分頁控制：沿用 usePagination 共用邏輯 + PagerBar 共用元件 -->
        <PagerBar
          v-if="alertsPage && alertsPage.totalPages > 1"
          :current-page="currentPage"
          :total-pages="alertsPage.totalPages"
          :total-count="alertsPage.totalCount"
          :visible-pages="visiblePages"
          :jump-page-input="jumpPageInput"
          @change="changePage"
          @update:jump-page-input="jumpPageInput = $event"
          @jump="handleJumpPage"
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { weatherApi, type PestAlertResponseDto, type PagedResult } from '@/api/weather'
import CitySelector from '@/components/CitySelector.vue'
import PagerBar from '@/components/PagerBar.vue'
import { usePagination } from '@/composables/usePagination'

const selectedCity = ref('臺北市')
const alertsPage   = ref<PagedResult<PestAlertResponseDto> | null>(null)
const isLoading    = ref(false)
const errorMsg     = ref('')
const expandedId   = ref<number | null>(null)

// template 沿用 alerts 這個名稱迭代卡片，改為由分頁結果投影
const alerts = computed(() => alertsPage.value?.items ?? [])

const {
  currentPage,
  pageSize,
  jumpPageInput,
  visiblePages,
  changePage,
  handleJumpPage,
} = usePagination({
  storageKey: 'pestAlerts.pageSize',
  totalPages: () => alertsPage.value?.totalPages,
  onChange: fetchAlerts,
})

function toggleExpand(id: number) {
  expandedId.value = expandedId.value === id ? null : id
}

function clearCity() {
  selectedCity.value = ''
  currentPage.value = 1
  fetchAlerts()
}

async function fetchAlerts() {
  isLoading.value = true
  errorMsg.value = ''
  alertsPage.value = null
  expandedId.value = null
  try {
    alertsPage.value = await weatherApi.getPestAlerts(
      selectedCity.value || undefined,
      currentPage.value,
      pageSize.value
    )
  } catch {
    errorMsg.value = '載入失敗，請稍後再試'
  } finally {
    isLoading.value = false
  }
}

// 切換城市時回到第一頁重查
watch(selectedCity, () => {
  currentPage.value = 1
  fetchAlerts()
})

onMounted(fetchAlerts)
</script>

<style scoped>
.pest-alerts-view { padding: 36px 56px; min-width: 960px; box-sizing: border-box; }

h1 { font-size: 22px; font-weight: 700; color: var(--text-primary); margin-bottom: 24px; }

.filter-section {
  display: flex; align-items: flex-end; gap: 12px; flex-wrap: wrap;
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 14px; padding: 24px; margin-bottom: 28px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
}

/* 全台按鈕 — 銀色金屬立體 */
.btn-clear {
  padding: 9px 24px; border-radius: 999px;
  border: 1px solid #9e9e9e;
  background: linear-gradient(
    180deg,
    #f5f5f5 0%,
    #e0e0e0 40%,
    #bdbdbd 100%
  );
  color: #1a2820;
  font-size: 14px; font-weight: 700; cursor: pointer;
  box-shadow:
    inset 0 1px 0 rgba(255,255,255,0.80),
    inset 0 -2px 4px rgba(0,0,0,0.15),
    inset 2px 0 6px rgba(255,255,255,0.40),
    0 2px 6px rgba(0,0,0,0.18);
  transition: all 0.15s;
}
.btn-clear:hover {
  background: linear-gradient(
    180deg,
    #ffffff 0%,
    #eeeeee 40%,
    #e0e0e0 100%
  );
  box-shadow:
    inset 0 1px 0 rgba(255,255,255,0.90),
    inset 0 -2px 4px rgba(0,0,0,0.12),
    inset 2px 0 6px rgba(255,255,255,0.50),
    0 3px 10px rgba(0,0,0,0.20);
}
.btn-clear:active {
  background: linear-gradient(
    180deg,
    #bdbdbd 0%,
    #e0e0e0 60%,
    #eeeeee 100%
  );
  box-shadow:
    inset 0 2px 6px rgba(0,0,0,0.20),
    inset 0 -1px 0 rgba(255,255,255,0.60),
    0 1px 3px rgba(0,0,0,0.12);
}

.error-msg    { font-size: 13px; color: var(--red); margin: 0; }
.empty-hint   { font-size: 14px; color: var(--text-muted); text-align: center; padding: 40px 0; }
.loading-hint { font-size: 14px; color: var(--text-muted); text-align: center; padding: 40px 0; }

.alert-list { display: flex; flex-direction: column; gap: 12px; margin-bottom: 24px; }

.alert-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 14px; padding: 20px 24px; cursor: pointer;
  transition: box-shadow 0.18s, border-color 0.18s;
  box-shadow: 0 1px 4px rgba(0,0,0,0.05);
}
.alert-card:hover { box-shadow: 0 4px 16px rgba(0,0,0,0.10); border-color: rgba(46,125,50,0.25); }
.alert-card.expanded { border-color: var(--green); background: #f6fbf6; }

.card-top { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }
.card-meta { display: flex; align-items: center; gap: 10px; }

/* 日期 */
.pub-date {
  font-size: 13px;           /* 從 12px → 13px */
  color: rgba(26,40,32,0.55);
  font-variant-numeric: tabular-nums;
  font-weight: 600;
}
/* issue badge */
.issue-badge {
  font-size: 12px;           /* 從 11px → 12px */
  padding: 3px 10px;
  border-radius: 999px;
  background: #e8f5e9; color: var(--green);
  border: 1px solid rgba(46,125,50,0.25);
  font-weight: 700;
}

.expand-icon { font-size: 18px; color: var(--text-muted); transition: color 0.15s; }
.alert-card:hover .expand-icon { color: var(--text-secondary); }

/* 主旨標題 */
.card-subject {
  font-size: 17px;           /* 從 15px → 17px */
  font-weight: 700;
  color: #1a2820;            /* 最深色，不透明 */
  margin-bottom: 12px;
  line-height: 1.5;
}

.tag-row { display: flex; flex-wrap: wrap; gap: 6px; }
/* 標籤 */
.tag { font-size: 13px; padding: 4px 12px; border-radius: 999px; border: 1px solid; font-weight: 600; }
.city-tag { background: #e3f2fd; border-color: rgba(21,101,192,0.30); color: #1565c0; }
.crop-tag { background: #e8f5e9; border-color: rgba(46,125,50,0.30); color: #2e7d32; }


.card-body { margin-top: 18px; padding-top: 18px; border-top: 1px solid var(--border); }

/* section 標籤 */
.section-label {
  font-size: 18px;
  font-weight: 700;
  color: var(--green);
  letter-spacing: 0.08em;
  text-transform: uppercase;
  margin-bottom: 10px;
  padding-bottom: 6px;
  border-bottom: 2px solid rgba(46,125,50,0.20);  /* 加底線 */
  display: block;
}
.section-label.prescription {
  color: #bf360c;
  margin-top: 18px;
  border-bottom-color: rgba(191,54,12,0.20);
}

/* 內文 */
.body-text {
  font-size: 15px;           /* 從 13.5px → 15px */
  color: rgba(26,40,32,0.82);  /* 從 text-secondary → 深一點 */
  line-height: 1.9;
  white-space: pre-wrap;
  margin: 0;
}

/* 分頁列的樣式由 PagerBar 元件自帶（scoped），此處不再重複一份 */
</style>