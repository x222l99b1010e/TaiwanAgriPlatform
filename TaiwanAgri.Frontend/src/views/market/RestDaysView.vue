<template>
  <div class="page restdays-view">
    <PageHeader
      title="休市日查詢"
      subtitle="各農產品批發市場的休市日期，請先選擇市場再查詢"
    />

    <FilterCard>
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
      <Btn
        icon="mdi-magnify"
        :loading="isLoading"
        :disabled="!selectedMarketCode"
        @click="handleQuery"
      >{{ isLoading ? '查詢中...' : '查詢休市日' }}</Btn>
    </FilterCard>

    <StateBlock v-if="!hasQueried" state="hint" message="請選擇市場與日期區間後按下查詢" />
    <StateBlock v-else-if="isLoading" state="loading" message="資料載入中..." />
    <StateBlock
      v-else-if="errorMsg"
      state="error"
      :message="errorMsg"
      retryable
      @retry="handleQuery"
    />
    <StateBlock
      v-else-if="restDays.length === 0"
      state="empty"
      icon="mdi-calendar-remove"
      message="查詢區間內無休市紀錄"
      hint="這個市場在所選期間內每天都有交易"
    />

    <div v-else>
      <!-- 摘要列 -->
      <div class="summary-bar">
        <div class="stat-card">
          <span class="stat-label">休市天數</span>
          <span class="stat-value">{{ restDays.length }}</span>
        </div>
        <div class="stat-card">
          <span class="stat-label">市場名稱</span>
          <span class="stat-value name">{{ selectedMarketName }}</span>
        </div>
      </div>

      <!-- 按月份分組 -->
      <div class="month-groups">
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
import PageHeader from '@/components/ui/PageHeader.vue'
import FilterCard from '@/components/ui/FilterCard.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'

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
.field-group { display: flex; flex-direction: column; gap: 6px; }
.field-label { font-size: var(--text-xs); color: var(--text-muted); font-weight: 600; letter-spacing: 0.05em; text-transform: uppercase; }

.market-select {
  padding: var(--space-2) 14px; border: 1px solid var(--border); border-radius: var(--radius-md);
  background: var(--surface); color: var(--text-primary); font-size: 14px;
  min-width: 200px; cursor: pointer;
  transition: border-color 0.18s, box-shadow 0.18s;
}
.market-select:focus { outline: none; border-color: var(--green); box-shadow: 0 0 0 3px rgba(46,125,50,0.12); }
.market-select:disabled { opacity: 0.5; cursor: not-allowed; }

.loading-hint { font-size: var(--text-xs); color: var(--text-muted); }
/* 摘要列 */
.summary-bar { display: flex; gap: 14px; margin-bottom: 28px; }
.stat-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: var(--radius-lg); padding: var(--space-4) var(--space-6);
  display: flex; flex-direction: column; gap: 6px;
  box-shadow: 0 1px 4px rgba(0,0,0,0.05);
}
.stat-label { font-size: var(--text-xs); color: var(--neutral-500); letter-spacing: 0.05em; text-transform: uppercase; font-weight: 600; }
.stat-value { font-size: 26px; font-weight: var(--weight-bold); color: var(--green-800); }
.stat-value.name { font-size: var(--text-lg); }

/* 月份分組 */
.month-groups { display: flex; flex-direction: column; gap: 28px; }

.month-label {
  display: flex; align-items: center; gap: var(--space-2);
  font-size: var(--text-base); font-weight: var(--weight-bold); color: var(--green);
  margin-bottom: var(--space-3);
  padding-bottom: 10px;
  border-bottom: 2px solid var(--green-100);
}

.month-count {
  font-size: var(--text-xs); padding: 2px var(--space-2); border-radius: var(--radius-full);
  background: var(--green-100); color: var(--green);
  border: 1px solid var(--green-200);
  font-weight: 600; margin-left: var(--space-1);
}

.day-row { display: flex; flex-wrap: wrap; gap: var(--space-2); }

.rest-chip {
  display: flex; align-items: center; gap: var(--space-2);
  padding: 10px 18px; border-radius: 10px;
  background: var(--surface); border: 1px solid var(--border);
  font-size: 14px; font-weight: 600; color: var(--text-primary);
  box-shadow: 0 1px 4px rgba(0,0,0,0.05);
  transition: box-shadow 0.15s, border-color 0.15s;
}
.rest-chip:hover { box-shadow: 0 4px 12px rgba(0,0,0,0.10); border-color: rgba(191,54,12,0.30); }
.chip-icon { font-size: 16px; color: var(--orange); }
</style>