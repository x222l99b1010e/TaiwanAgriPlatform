<template>
  <div class="page disasters-view">
    <PageHeader
      title="天災警戒紀錄"
      subtitle="農業部發布的土石流與土石流潛勢警戒，可依日期區間與縣市查詢"
    />

    <FilterCard>
      <DateRangePicker v-model:startDate="startDate" v-model:endDate="endDate" />
      <div class="field-group">
        <label class="field-label">縣市篩選</label>
        <select class="county-select" v-model="selectedCounty">
          <option value="">全台</option>
          <option v-for="county in counties" :key="county" :value="county">{{ county }}</option>
        </select>
      </div>
      <Btn icon="mdi-magnify" :loading="isLoading" @click="handleQuery">
        {{ isLoading ? '查詢中...' : '查詢天災' }}
      </Btn>
    </FilterCard>

    <StateBlock v-if="!hasQueried" state="hint" message="請設定日期區間後按下查詢" />
    <StateBlock v-else-if="isLoading" state="loading" message="資料載入中..." />
    <StateBlock
      v-else-if="errorMsg"
      state="error"
      :message="errorMsg"
      retryable
      @retry="handleQuery"
    />
    <StateBlock
      v-else-if="groupedEvents.length === 0"
      state="empty"
      icon="mdi-shield-check-outline"
      message="查詢區間內無天災警戒紀錄"
      hint="沒有紀錄是好消息；要看更長的期間可以把起始日往前調"
    />

    <div v-else>
      <!-- 摘要列 -->
      <div class="summary-bar">
        <div class="stat-card">
          <span class="stat-label">總筆數</span>
          <span class="stat-value">{{ groupedEvents.length }}</span>
        </div>
        <div class="stat-card">
          <span class="stat-label">土石流</span>
          <span class="stat-value red">{{ groupedEvents.filter(e => e.alertType === 'D').length }}</span>
        </div>
        <div class="stat-card">
          <span class="stat-label">土石流潛勢</span>
          <span class="stat-value orange">{{ groupedEvents.filter(e => e.alertType !== 'D').length }}</span>
        </div>
      </div>

      <div class="event-list">
        <div class="event-card" v-for="(event, i) in groupedEvents" :key="i">
          <div class="event-header">
            <div class="event-meta">
              <span class="badge alert-badge" :class="event.alertType === 'D' ? 'red' : 'orange'">
                {{ event.alertType === 'D' ? '土石流' : '土石流潛勢' }}
              </span>
              <span class="event-name">{{ event.disasterName }}</span>
            </div>
            <span class="event-date-range">
              {{ event.firstDate }}
              <span v-if="event.lastDate !== event.firstDate"> ～ {{ event.lastDate }}</span>
            </span>
          </div>
          <div class="county-tags">
            <span class="badge county-tag" v-for="county in event.affectedCounties" :key="county">
              {{ county }}
            </span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import DateRangePicker from '@/components/DateRangePicker.vue'
import { marketApi } from '@/api/market'
import type { DisasterResponseDto } from '@/api/market'
import PageHeader from '@/components/ui/PageHeader.vue'
import FilterCard from '@/components/ui/FilterCard.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'

const today = new Date().toISOString().split('T')[0]!
const oneYearAgo = new Date(Date.now() - 365 * 24 * 60 * 60 * 1000).toISOString().split('T')[0]!

const startDate = ref(oneYearAgo)
const endDate = ref(today)
const selectedCounty = ref('')
const rawData = ref<DisasterResponseDto[]>([])
const isLoading = ref(false)
const hasQueried = ref(false)
const errorMsg = ref('')

const counties = [
  '臺北市', '新北市', '桃園市', '臺中市', '臺南市', '高雄市',
  '基隆市', '新竹市', '嘉義市', '新竹縣', '苗栗縣', '彰化縣',
  '南投縣', '雲林縣', '嘉義縣', '屏東縣', '宜蘭縣', '花蓮縣',
  '臺東縣', '澎湖縣', '金門縣', '連江縣',
]

const groupedEvents = computed(() => {
  const map = new Map<string, {
    disasterName: string; alertType: string
    firstDate: string; lastDate: string; affectedCounties: Set<string>
  }>()
  for (const d of rawData.value) {
    const existing = map.get(d.disasterName)
    if (!existing) {
      map.set(d.disasterName, {
        disasterName: d.disasterName, alertType: d.alertType,
        firstDate: d.alertDate, lastDate: d.alertDate,
        affectedCounties: new Set(d.affectedCounties),
      })
    } else {
      if (d.alertDate < existing.firstDate) existing.firstDate = d.alertDate
      if (d.alertDate > existing.lastDate) existing.lastDate = d.alertDate
      d.affectedCounties.forEach(c => existing.affectedCounties.add(c))
    }
  }
  return Array.from(map.values())
    .map(e => ({ ...e, affectedCounties: Array.from(e.affectedCounties).sort() }))
    .sort((a, b) => b.firstDate.localeCompare(a.firstDate))
})

async function handleQuery() {
  errorMsg.value = ''
  isLoading.value = true
  hasQueried.value = true
  rawData.value = []
  try {
    rawData.value = await marketApi.getDisasters({
      startDate: startDate.value,
      endDate: endDate.value,
      counties: selectedCounty.value ? [selectedCounty.value] : undefined,
    })
  } catch {
    errorMsg.value = '查詢失敗，請稍後再試'
  } finally {
    isLoading.value = false
  }
}
</script>

<style scoped>
.disasters-view { min-width: 960px; }
.field-group { display: flex; flex-direction: column; gap: var(--space-2); }
.field-label { font-size: var(--text-xs); color: var(--text-muted); font-weight: var(--weight-medium); letter-spacing: 0.05em; text-transform: uppercase; }

.county-select {
  padding: var(--space-2) var(--space-4); border: 1px solid var(--border); border-radius: var(--radius-md);
  background: var(--surface); color: var(--text-primary); font-size: var(--text-base);
  min-width: 160px; cursor: pointer;
  transition: border-color var(--duration-fast), box-shadow var(--duration-fast);
}
.county-select:focus { outline: none; border-color: var(--green); box-shadow: var(--shadow-focus); }
.summary-bar { display: flex; gap: var(--space-4); margin-bottom: var(--space-6); }
.stat-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: var(--radius-lg); padding: var(--space-4) var(--space-6);
  display: flex; flex-direction: column; gap: var(--space-2);
  box-shadow: var(--shadow-sm);
}
.stat-label { font-size: var(--text-xs); color: var(--neutral-500); letter-spacing: 0.05em; text-transform: uppercase; font-weight: var(--weight-medium); }
.stat-value { font-size: var(--text-2xl); font-weight: var(--weight-bold); color: var(--green-800); }
.stat-value.red { color: var(--red); }
.stat-value.orange { color: var(--orange); }

.event-list { display: flex; flex-direction: column; gap: var(--space-3); }

.event-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: var(--radius-lg); padding: var(--space-5) var(--space-6);
  box-shadow: var(--shadow-sm);
  transition: box-shadow var(--duration-fast);
}
.event-card:hover { box-shadow: var(--shadow-md); }

.event-header {
  display: flex; align-items: center; justify-content: space-between;
  margin-bottom: var(--space-4);
}
.event-meta { display: flex; align-items: center; gap: var(--space-3); }
.event-name { font-size: var(--text-base); font-weight: var(--weight-bold); color: var(--text-primary); }
.event-date-range { font-size: var(--text-sm); color: var(--warning-700); font-variant-numeric: tabular-nums; font-weight: var(--weight-medium); }

/* 標籤外殼已收進 base.css 的 .badge，這裡只留語意色 */
.alert-badge.red { background: var(--danger-50); color: var(--red); border: 1px solid var(--danger-100); }
.alert-badge.orange { background: var(--warning-50); color: var(--orange); border: 1px solid var(--warning-100); }

.county-tags { display: flex; flex-wrap: wrap; gap: var(--space-2); }
.county-tag { background: var(--info-50); color: var(--blue); border: 1px solid var(--info-100); }
</style>