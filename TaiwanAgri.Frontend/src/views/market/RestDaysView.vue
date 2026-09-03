<template>
  <div class="page restdays-view">
    <QueryLayout
      title="休市日查詢"
      title-en="MARKET CLOSURES"
      subtitle="各農產品批發市場的休市日期，請先選擇市場再查詢"
    >
      <template #actions>
        <Btn
          icon="mdi-magnify"
          :loading="isLoading"
          :disabled="!selectedMarketCode"
          @click="handleQuery"
        >{{ isLoading ? '查詢中...' : '查詢休市日' }}</Btn>
      </template>

      <template #filters>
        <div class="field-group">
          <label class="field-label" for="market-select">選擇市場</label>
          <div class="select-wrap">
            <select
              id="market-select"
              class="form-control market-select"
              v-model="selectedMarketCode"
              :disabled="isLoadingMarkets"
            >
              <option value="" disabled>請選擇市場</option>
              <option v-for="m in markets" :key="m.marketCode" :value="m.marketCode">
                {{ m.marketName }}
              </option>
            </select>
            <span v-if="isLoadingMarkets" class="loading-hint">載入中...</span>
          </div>
        </div>
        <DateRangePicker v-model:startDate="startDate" v-model:endDate="endDate" />
      </template>

      <template #results>
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
              <span class="stat-value stat-value--text">{{ selectedMarketName }}</span>
            </div>
          </div>

          <!-- 按月份分組，一個月一張月曆——比一排排 chip 更容易看出「連續休了幾天」，
               不用自己在腦中換算某個日期是星期幾 -->
          <div class="month-groups">
            <MonthCalendar
              v-for="group in groupedByMonth"
              :key="`${group.year}-${group.month}`"
              :year="group.year"
              :month="group.month"
              :marked-dates="group.markedDates"
            />
          </div>
        </div>
      </template>
    </QueryLayout>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import DateRangePicker from '@/components/DateRangePicker.vue'
import { marketApi } from '@/api/market'
import type { RestDayResponseDto, MarketResponseDto } from '@/api/market'
import QueryLayout from '@/components/layouts/QueryLayout.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'
import MonthCalendar from '@/components/MonthCalendar.vue'

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
      year: Number(key.substring(0, 4)),
      month: Number(key.substring(5, 7)),
      markedDates: new Set(days.map(d => d.restDate)),
    }))
})

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
/* 欄位與摘要列的外殼已收進 base.css，這裡只留這一頁真正不同的部分 */
.restdays-view { min-width: 960px; }

.select-wrap { display: flex; align-items: center; gap: var(--space-3); }
.market-select { min-width: 200px; }
.loading-hint { font-size: var(--text-xs); color: var(--color-text-dim); }

/* 月曆網格：一個月一張卡片，卡片外殼在 MonthCalendar 元件自己身上 */
.month-groups {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: var(--space-6);
}
</style>