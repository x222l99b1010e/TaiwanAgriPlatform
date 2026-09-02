<!-- src/views/weather/PestAlertsView.vue -->
<template>
  <div class="page pest-alerts-view">
    <PageHeader
      title="病蟲害警報"
      subtitle="農業部發布的病蟲害警報全文，點卡片可展開內容"
    />

    <FilterCard>
      <CitySelector v-model="selectedCity" />
      <Btn variant="secondary" @click="clearCity">全台</Btn>
    </FilterCard>

    <StateBlock v-if="isLoading" state="loading" message="資料載入中..." />
    <StateBlock
      v-else-if="errorMsg"
      state="error"
      :message="errorMsg"
      retryable
      @retry="fetchAlerts"
    />
    <StateBlock
      v-else-if="alerts.length === 0"
      state="empty"
      message="查無警報資料"
      hint="這個縣市目前沒有生效中的病蟲害警報，可切換縣市或改看全台"
    />

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
              class="badge tag city-tag"
              v-for="c in a.cities"
              :key="c"
            >{{ c }}</span>
            <span
              class="badge tag crop-tag"
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
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { weatherApi, type PestAlertResponseDto, type PagedResult } from '@/api/weather'
import CitySelector from '@/components/CitySelector.vue'
import PagerBar from '@/components/PagerBar.vue'
import { usePagination } from '@/composables/usePagination'
import PageHeader from '@/components/ui/PageHeader.vue'
import FilterCard from '@/components/ui/FilterCard.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'

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
.pest-alerts-view { min-width: 960px; }
.alert-list { display: flex; flex-direction: column; gap: var(--space-3); margin-bottom: var(--space-6); }

.alert-card {
  background: var(--neutral-0); border: 1px solid var(--neutral-200);
  border-radius: var(--radius-lg); padding: var(--space-5) var(--space-6); cursor: pointer;
  transition: box-shadow var(--duration-fast), border-color var(--duration-fast);
  box-shadow: var(--shadow-sm);
}
.alert-card:hover { box-shadow: var(--shadow-md); border-color: var(--green-200); }
.alert-card.expanded { border-color: var(--green-600); background: var(--green-50); }

.card-top { display: flex; justify-content: space-between; align-items: center; margin-bottom: var(--space-2); }
.card-meta { display: flex; align-items: center; gap: var(--space-3); }

/* 日期 */
.pub-date {
  font-size: var(--text-sm);           /* 從 12px → 13px */
  color: var(--neutral-500);
  font-variant-numeric: tabular-nums;
  font-weight: var(--weight-medium);
}
/* issue badge */
.issue-badge {
  font-size: var(--text-xs);           /* 從 11px → 12px */
  padding: var(--space-1) var(--space-3);
  border-radius: var(--radius-full);
  background: var(--green-100); color: var(--green-600);
  border: 1px solid var(--green-200);
  font-weight: var(--weight-bold);
}

.expand-icon { font-size: var(--text-lg); color: var(--neutral-400); transition: color var(--duration-fast); }
.alert-card:hover .expand-icon { color: var(--neutral-500); }

/* 主旨標題 */
.card-subject {
  font-size: var(--text-lg);           /* 從 15px → 17px */
  font-weight: var(--weight-bold);
  color: var(--neutral-900);            /* 最深色，不透明 */
  margin-bottom: var(--space-3);
  line-height: var(--leading-normal);
}

.tag-row { display: flex; flex-wrap: wrap; gap: var(--space-2); }
/* 標籤外殼已收進 base.css 的 .badge，這裡只留語意色 */
.tag { border: var(--border-width) solid; }
.city-tag { background: var(--info-50); border-color: var(--info-100); color: var(--info-500); }
.crop-tag { background: var(--green-100); border-color: var(--green-200); color: var(--green-600); }


.card-body { margin-top: var(--space-5); padding-top: var(--space-5); border-top: 1px solid var(--neutral-200); }

/* section 標籤 */
.section-label {
  font-size: var(--text-lg);
  font-weight: var(--weight-bold);
  color: var(--green-600);
  letter-spacing: 0.08em;
  text-transform: uppercase;
  margin-bottom: var(--space-3);
  padding-bottom: var(--space-2);
  border-bottom: 2px solid var(--green-200);  /* 加底線 */
  display: block;
}
.section-label.prescription {
  color: var(--warning-700);
  margin-top: var(--space-5);
  border-bottom-color: var(--warning-100);
}

/* 內文 */
.body-text {
  font-size: var(--text-base);           /* 從 13.5px → 15px */
  color: var(--neutral-700);  /* 從 text-secondary → 深一點 */
  line-height: var(--leading-loose);
  white-space: pre-wrap;
  margin: 0;
}

/* 分頁列的樣式由 PagerBar 元件自帶（scoped），此處不再重複一份 */
</style>