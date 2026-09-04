<template>
  <div class="page disasters-view">
    <QueryLayout
      title="天災警戒紀錄"
      title-en="DISASTER ALERTS"
      subtitle="農業部發布的土石流與土石流潛勢警戒，可依日期區間與縣市查詢"
    >
      <template #actions>
        <Btn icon="mdi-magnify" :loading="isLoading" @click="handleQuery">
          {{ isLoading ? '查詢中...' : '查詢天災' }}
        </Btn>
      </template>

      <template #filters>
        <DateRangePicker v-model:startDate="startDate" v-model:endDate="endDate" />
        <div class="field-group">
          <label class="field-label" for="county-select">縣市篩選</label>
          <select id="county-select" class="form-control county-select" v-model="selectedCounty">
            <option value="">全台</option>
            <option v-for="county in counties" :key="county" :value="county">{{ county }}</option>
          </select>
        </div>
      </template>

      <template #results>
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
              <span class="stat-value stat-value--danger">{{ groupedEvents.filter(e => e.alertType === 'D').length }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-label">土石流潛勢</span>
              <span class="stat-value stat-value--warning">{{ groupedEvents.filter(e => e.alertType !== 'D').length }}</span>
            </div>
          </div>

          <!-- 每一筆是「一段時間內的一場警戒」，所以日期獨立成左欄、事件內容在右欄：
               清單依日期新到舊排序，左欄對齊之後才掃得出時間的疏密。 -->
          <div class="event-list">
            <article
              class="event-card"
              :class="event.alertType === 'D' ? 'is-danger' : 'is-warning'"
              v-for="(event, i) in groupedEvents"
              :key="i"
            >
              <div class="event-date">
                <span class="event-date__from">{{ event.firstDate }}</span>
                <span v-if="event.lastDate !== event.firstDate" class="event-date__to">
                  ～ {{ event.lastDate }}
                </span>
                <span class="event-date__days">持續 {{ event.days }} 天</span>
              </div>

              <div class="event-body">
                <div class="event-meta">
                  <span class="badge alert-badge" :class="event.alertType === 'D' ? 'red' : 'orange'">
                    {{ event.alertType === 'D' ? '土石流' : '土石流潛勢' }}
                  </span>
                  <span class="event-name">{{ event.disasterName }}</span>
                </div>
                <div class="county-tags">
                  <span class="badge county-tag" v-for="county in event.affectedCounties" :key="county">
                    {{ county }}
                  </span>
                </div>
              </div>
            </article>
          </div>
        </div>
      </template>
    </QueryLayout>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import DateRangePicker from '@/components/DateRangePicker.vue'
import { marketApi } from '@/api/market'
import type { DisasterResponseDto } from '@/api/market'
import QueryLayout from '@/components/layouts/QueryLayout.vue'
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
    .map(e => ({
      ...e,
      affectedCounties: Array.from(e.affectedCounties).sort(),
      // 含頭含尾：同一天發布又解除也算警戒了一天，所以 +1
      days: Math.round(
        (Date.parse(e.lastDate) - Date.parse(e.firstDate)) / 86_400_000,
      ) + 1,
    }))
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
/* 顏色全部改用 semantic 層；欄位與摘要列的外殼已收進 base.css
   的 .field-group／.field-label／.form-control／.summary-bar／.stat-*，
   這裡只留這一頁真正不同的部分。 */
.disasters-view { min-width: 960px; }

.county-select { min-width: 160px; }

/* 兩個數字用語意色而不是動作綠：它們是「有多嚴重」不是「可以點」 */
.stat-value--danger  { color: var(--danger-500); }
.stat-value--warning { color: var(--warning-700); }

.event-list { display: flex; flex-direction: column; gap: var(--space-3); }

/* 日期獨立成左欄。原本日期是右上角的一行小字，而這一頁的資料本質是時間序列——
   日期對齊之後，掃一眼就看得出哪一段時間警戒特別密集。
   等級不用底色只用左邊界：整張卡片上淺紅／淺橘會讓一整頁看起來像全部都出事了。 */
.event-card {
  display: grid;
  grid-template-columns: 128px 1fr;
  gap: var(--space-6);
  align-items: start;
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-inline-start-width: 3px;
  border-radius: var(--radius-lg);
  padding: var(--space-5) var(--space-6);
  transition: border-color var(--duration-fast) var(--ease-work);
}
.event-card.is-danger  { border-inline-start-color: var(--danger-500); }
.event-card.is-warning { border-inline-start-color: var(--color-accent-2-fill); }
.event-card:hover { border-color: var(--color-border-strong); }
.event-card.is-danger:hover  { border-inline-start-color: var(--danger-500); }
.event-card.is-warning:hover { border-inline-start-color: var(--color-accent-2-fill); }

.event-date {
  display: flex; flex-direction: column; gap: var(--space-1);
  font-family: var(--font-num);
  font-variant-numeric: tabular-nums;
}
.event-date__from { font-size: var(--text-sm); font-weight: var(--weight-bold); color: var(--color-text); }
.event-date__to   { font-size: var(--text-sm); color: var(--color-text-dim); }
.event-date__days { font-size: var(--text-2xs); color: var(--color-text-dim); }

.event-body { display: flex; flex-direction: column; gap: var(--space-4); min-width: 0; }
.event-meta { display: flex; align-items: center; gap: var(--space-3); flex-wrap: wrap; }
.event-name { font-size: var(--text-base); font-weight: var(--weight-bold); color: var(--color-text); }

/* 標籤外殼已收進 base.css 的 .badge，這裡只留語意色 */
.alert-badge.red { background: var(--danger-50); color: var(--danger-500); border: var(--border-width) solid var(--danger-100); }
.alert-badge.orange { background: var(--warning-50); color: var(--warning-700); border: var(--border-width) solid var(--warning-100); }

.county-tags { display: flex; flex-wrap: wrap; gap: var(--space-2); }
/* 縣市是「這件事發生在哪裡」，屬於中性資訊，不該長得像藍色的狀態標籤 */
.county-tag {
  background: var(--color-bg-sunken);
  color: var(--color-text-dim);
  border: var(--border-width) solid var(--color-border);
  font-weight: var(--weight-normal);
}
</style>