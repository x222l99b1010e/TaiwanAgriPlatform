<template>
  <div class="page restdays-view">
    <h1>休市日查詢</h1>

    <section class="filter-section">
      <div class="field-group">
        <label class="field-label">選擇市場</label>
        <select class="market-select" v-model="selectedMarketCode" :disabled="isLoadingMarkets">
          <option value="" disabled>請選擇市場</option>
          <option v-for="m in markets" :key="m.marketCode" :value="m.marketCode">
            {{ m.marketName }}
          </option>
        </select>
        <span v-if="isLoadingMarkets" class="loading-hint">載入中...</span>
      </div>
      <DateRangePicker v-model:startDate="startDate" v-model:endDate="endDate" />
      <button class="btn-query" :disabled="!selectedMarketCode || isLoading" @click="handleQuery">
        {{ isLoading ? '查詢中...' : '查詢休市日' }}
      </button>
      <p v-if="errorMsg" class="error-msg">{{ errorMsg }}</p>
    </section>

    <div v-if="hasQueried">
      <!-- 摘要列 -->
      <div class="summary-bar" v-if="restDays.length > 0">
        <div class="stat-card">
          <span class="stat-label">休市天數</span>
          <span class="stat-value">{{ restDays.length }}</span>
        </div>
        <div class="stat-card">
          <span class="stat-label">市場名稱</span>
          <span class="stat-value name">{{ selectedMarketName }}</span>
        </div>
      </div>

      <div class="empty-hint" v-if="restDays.length === 0 && !isLoading">
        查詢區間內無休市紀錄
      </div>

      <!-- 按月份分組 -->
      <div class="month-groups" v-if="groupedByMonth.length > 0">
        <div class="month-group" v-for="group in groupedByMonth" :key="group.label">
          <div class="month-label">
            <span class="mdi mdi-calendar-month" />
            {{ group.label }}
            <span class="month-count">{{ group.days.length }} 天</span>
          </div>
          <div class="day-row">
            <div class="rest-chip" v-for="d in group.days" :key="d.restDate">
              <span class="mdi mdi-calendar-remove chip-icon" />
              {{ formatDate(d.restDate) }}
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import DateRangePicker from '@/components/DateRangePicker.vue'
import { marketApi } from '@/api/market'
import type { RestDayResponseDto, MarketResponseDto } from '@/api/market'

const today = new Date().toISOString().split('T')[0]!
const oneYearAgo = new Date(Date.now() - 365 * 24 * 60 * 60 * 1000).toISOString().split('T')[0]!

const startDate = ref(oneYearAgo)
const endDate = ref(today)
const selectedMarketCode = ref('')
const markets = ref<MarketResponseDto[]>([])
const restDays = ref<RestDayResponseDto[]>([])
const isLoading = ref(false)
const isLoadingMarkets = ref(false)
const hasQueried = ref(false)
const errorMsg = ref('')

const selectedMarketName = computed(
  () => markets.value.find(m => m.marketCode === selectedMarketCode.value)?.marketName ?? ''
)

const groupedByMonth = computed(() => {
  const map = new Map<string, RestDayResponseDto[]>()
  for (const d of restDays.value) {
    const key = d.restDate.substring(0, 7) // "2026-01"
    if (!map.has(key)) map.set(key, [])
    map.get(key)!.push(d)
  }
  return Array.from(map.entries())
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([key, days]) => ({
      label: `${key.substring(0, 4)} 年 ${parseInt(key.substring(5, 7))} 月`,
      days,
    }))
})

function formatDate(dateStr: string) {
  const d = new Date(dateStr)
  const weekdays = ['日', '一', '二', '三', '四', '五', '六']
  return `${d.getFullYear()}/${String(d.getMonth() + 1).padStart(2, '0')}/${String(d.getDate()).padStart(2, '0')} (${weekdays[d.getDay()]})`
}

async function loadMarkets() {
  isLoadingMarkets.value = true
  try {
    markets.value = await marketApi.getMarkets('Veg')
  } catch {
    errorMsg.value = '載入市場列表失敗'
  } finally {
    isLoadingMarkets.value = false
  }
}

async function handleQuery() {
  if (!selectedMarketCode.value) return
  errorMsg.value = ''
  isLoading.value = true
  hasQueried.value = true
  restDays.value = []
  try {
    restDays.value = await marketApi.getRestDays({
      marketCode: selectedMarketCode.value,
      startDate: startDate.value,
      endDate: endDate.value,
    })
  } catch {
    errorMsg.value = '查詢失敗，請稍後再試'
  } finally {
    isLoading.value = false
  }
}

onMounted(() => loadMarkets())
</script>

<style scoped>
.restdays-view { min-width: 960px; }

h1 { font-size: 22px; font-weight: 700; color: var(--text-primary); margin-bottom: 24px; }

.filter-section {
  display: flex; align-items: flex-end; gap: 20px; flex-wrap: wrap;
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 14px; padding: 24px; margin-bottom: 28px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
}

.field-group { display: flex; flex-direction: column; gap: 6px; }
.field-label { font-size: 12px; color: var(--text-muted); font-weight: 600; letter-spacing: 0.05em; text-transform: uppercase; }

.market-select {
  padding: 8px 14px; border: 1px solid var(--border); border-radius: 8px;
  background: var(--surface); color: var(--text-primary); font-size: 14px;
  min-width: 200px; cursor: pointer;
  transition: border-color 0.18s, box-shadow 0.18s;
}
.market-select:focus { outline: none; border-color: var(--green); box-shadow: 0 0 0 3px rgba(46,125,50,0.12); }
.market-select:disabled { opacity: 0.5; cursor: not-allowed; }

.loading-hint { font-size: 12px; color: var(--text-muted); }

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

/* 摘要列 */
.summary-bar { display: flex; gap: 14px; margin-bottom: 28px; }
.stat-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 12px; padding: 16px 24px;
  display: flex; flex-direction: column; gap: 6px;
  box-shadow: 0 1px 4px rgba(0,0,0,0.05);
}
.stat-label { font-size: 12px; color: rgba(26,40,32,0.60); letter-spacing: 0.05em; text-transform: uppercase; font-weight: 600; }
.stat-value { font-size: 26px; font-weight: 700; color: #1a5c20; }
.stat-value.name { font-size: 18px; }

/* 月份分組 */
.month-groups { display: flex; flex-direction: column; gap: 28px; }

.month-label {
  display: flex; align-items: center; gap: 8px;
  font-size: 15px; font-weight: 700; color: var(--green);
  margin-bottom: 12px;
  padding-bottom: 10px;
  border-bottom: 2px solid rgba(46,125,50,0.15);
}

.month-count {
  font-size: 12px; padding: 2px 8px; border-radius: 999px;
  background: #e8f5e9; color: var(--green);
  border: 1px solid rgba(46,125,50,0.20);
  font-weight: 600; margin-left: 4px;
}

.day-row { display: flex; flex-wrap: wrap; gap: 8px; }

.rest-chip {
  display: flex; align-items: center; gap: 8px;
  padding: 10px 18px; border-radius: 10px;
  background: var(--surface); border: 1px solid var(--border);
  font-size: 14px; font-weight: 600; color: var(--text-primary);
  box-shadow: 0 1px 4px rgba(0,0,0,0.05);
  transition: box-shadow 0.15s, border-color 0.15s;
}
.rest-chip:hover { box-shadow: 0 4px 12px rgba(0,0,0,0.10); border-color: rgba(191,54,12,0.30); }
.chip-icon { font-size: 16px; color: var(--orange); }
</style>