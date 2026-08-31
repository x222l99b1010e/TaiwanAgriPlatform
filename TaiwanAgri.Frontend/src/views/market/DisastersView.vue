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
              <span class="alert-badge" :class="event.alertType === 'D' ? 'red' : 'orange'">
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
            <span class="county-tag" v-for="county in event.affectedCounties" :key="county">
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
.field-group { display: flex; flex-direction: column; gap: 6px; }
.field-label { font-size: 12px; color: var(--text-muted); font-weight: 600; letter-spacing: 0.05em; text-transform: uppercase; }

.county-select {
  padding: 8px 14px; border: 1px solid var(--border); border-radius: 8px;
  background: var(--surface); color: var(--text-primary); font-size: 14px;
  min-width: 160px; cursor: pointer;
  transition: border-color 0.18s, box-shadow 0.18s;
}
.county-select:focus { outline: none; border-color: var(--green); box-shadow: 0 0 0 3px rgba(46,125,50,0.12); }
.summary-bar { display: flex; gap: 14px; margin-bottom: 24px; }
.stat-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 12px; padding: 16px 24px;
  display: flex; flex-direction: column; gap: 6px;
  box-shadow: 0 1px 4px rgba(0,0,0,0.05);
}
.stat-label { font-size: 12px; color: rgba(26,40,32,0.60); letter-spacing: 0.05em; text-transform: uppercase; font-weight: 600; }
.stat-value { font-size: 26px; font-weight: 700; color: #1a5c20; }
.stat-value.red { color: var(--red); }
.stat-value.orange { color: var(--orange); }

.event-list { display: flex; flex-direction: column; gap: 12px; }

.event-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 14px; padding: 20px 24px;
  box-shadow: 0 1px 4px rgba(0,0,0,0.05);
  transition: box-shadow 0.18s;
}
.event-card:hover { box-shadow: 0 4px 16px rgba(0,0,0,0.10); }

.event-header {
  display: flex; align-items: center; justify-content: space-between;
  margin-bottom: 14px;
}
.event-meta { display: flex; align-items: center; gap: 10px; }
.event-name { font-size: 16px; font-weight: 700; color: var(--text-primary); }
.event-date-range { font-size: 13px; color: #bf360c; font-variant-numeric: tabular-nums; font-weight: 600; }

.alert-badge { font-size: 11px; padding: 3px 9px; border-radius: 6px; flex-shrink: 0; font-weight: 700; }
.alert-badge.red { background: rgba(198,40,40,0.10); color: var(--red); border: 1px solid rgba(198,40,40,0.20); }
.alert-badge.orange { background: rgba(191,54,12,0.10); color: var(--orange); border: 1px solid rgba(191,54,12,0.20); }

.county-tags { display: flex; flex-wrap: wrap; gap: 6px; }
.county-tag {
  font-size: 12px; padding: 3px 10px; border-radius: 999px;
  background: #e3f2fd; color: var(--blue);
  border: 1px solid rgba(21,101,192,0.20); font-weight: 600;
}
</style>