<template>
  <div class="disasters-view">
    <h1>天災警戒紀錄</h1>

    <section class="filter-section">
      <DateRangePicker v-model:startDate="startDate" v-model:endDate="endDate" />
      <div class="filter-right">
        <div class="field-group">
          <label class="field-label">縣市篩選</label>
          <select class="county-select" v-model="selectedCounty">
            <option value="">全台</option>
            <option v-for="county in counties" :key="county" :value="county">{{ county }}</option>
          </select>
        </div>
        <button class="btn-query" :disabled="isLoading" @click="handleQuery">
          {{ isLoading ? '查詢中...' : '查詢天災' }}
        </button>
      </div>
      <p v-if="errorMsg" class="error-msg">{{ errorMsg }}</p>
    </section>

    <div v-if="hasQueried">
      <!-- 摘要列 -->
      <div class="summary-bar" v-if="groupedEvents.length > 0">
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

      <div class="empty-hint" v-if="groupedEvents.length === 0 && !isLoading">
        查詢區間內無天災警戒紀錄
      </div>

      <div class="event-list" v-else>
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
.disasters-view { width: 100%; min-width: 960px; padding: 36px 56px; box-sizing: border-box; }

h1 { font-size: 22px; font-weight: 700; color: var(--text-primary); margin-bottom: 24px; }

.filter-section {
  display: flex; align-items: flex-end; gap: 20px; flex-wrap: wrap;
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 14px; padding: 24px; margin-bottom: 28px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
}
.filter-right { display: flex; align-items: flex-end; gap: 14px; }

.field-group { display: flex; flex-direction: column; gap: 6px; }
.field-label { font-size: 12px; color: var(--text-muted); font-weight: 600; letter-spacing: 0.05em; text-transform: uppercase; }

.county-select {
  padding: 8px 14px; border: 1px solid var(--border); border-radius: 8px;
  background: var(--surface); color: var(--text-primary); font-size: 14px;
  min-width: 160px; cursor: pointer;
  transition: border-color 0.18s, box-shadow 0.18s;
}
.county-select:focus { outline: none; border-color: var(--green); box-shadow: 0 0 0 3px rgba(46,125,50,0.12); }

.btn-query {
  padding: 9px 26px; border-radius: 999px;
  border: 1px solid #1a5220;
  background: linear-gradient(180deg, #4caf50 0%, #2e7d32 40%, #1b5e20 100%);
  color: white; font-size: 14px; font-weight: 700; cursor: pointer;
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.35), inset 0 -2px 4px rgba(0,0,0,0.25), 0 2px 6px rgba(0,0,0,0.20);
  transition: all 0.15s;
}
.btn-query:hover:not(:disabled) {
  background: linear-gradient(180deg, #66bb6a 0%, #388e3c 40%, #2e7d32 100%);
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.45), 0 3px 10px rgba(0,0,0,0.22);
}
.btn-query:active:not(:disabled) {
  background: linear-gradient(180deg, #1b5e20 0%, #2e7d32 60%, #388e3c 100%);
  box-shadow: inset 0 2px 6px rgba(0,0,0,0.35), 0 1px 3px rgba(0,0,0,0.15);
}
.btn-query:disabled { background: #c8d8c8; color: #999; border-color: #b0c8b0; box-shadow: none; cursor: not-allowed; }

.error-msg { font-size: 13px; color: var(--red); margin: 0; }
.empty-hint { font-size: 14px; color: var(--text-muted); text-align: center; padding: 60px 0; }

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