<template>
  <div class="prices-view">
    <h1>作物行情查詢</h1>

    <section class="filter-section">
      <div class="filter-row">
        <MarketFilter />
      </div>
      <div class="filter-bottom">
        <DateRangePicker v-model:startDate="startDate" v-model:endDate="endDate" />
        <div class="action-row">
          <button
            class="btn-query"
            :disabled="store.selectedCropCodes.length === 0 || isLoading"
            @click="handleQuery"
          >{{ isLoading ? '查詢中...' : '查詢價格' }}</button>
          <button v-if="prices.length > 0" class="btn-export" @click="handleExportCsv">匯出 CSV</button>
          <button v-if="store.selectedCropCodes.length > 0" class="btn-clear" @click="store.$patch({ selectedCropCodes: [] })">清空作物</button>
        </div>
        <p v-if="validationMsg" class="validation-msg">{{ validationMsg }}</p>
      </div>
    </section>

    <div class="bottom-grid" v-if="hasQueried">
      <section class="chart-section" v-if="prices.length > 0">
        <PriceChart :prices="prices" :disasters="rawDisasters" />
      </section>
      <section class="chart-section empty-section" v-else-if="!isLoading">
        <p class="result-hint">查無資料，請確認篩選條件後重試</p>
      </section>

      <section class="disaster-section">
        <div class="disaster-header">
          <span class="disaster-title">天災警戒紀錄</span>
          <span class="disaster-count" v-if="disasterEvents.length > 0">{{ disasterEvents.length }} 件</span>
        </div>
        <div class="disaster-empty" v-if="disasterEvents.length === 0">查詢區間內無天災警戒紀錄</div>
        <div class="disaster-list" v-else>
          <div class="disaster-item" v-for="(event, i) in disasterEvents" :key="i">
            <div class="disaster-date-range">
              {{ event.firstDate }}
              <span v-if="event.lastDate !== event.firstDate"> ～ {{ event.lastDate }}</span>
            </div>
            <div class="disaster-name">
              <span class="alert-badge" :class="event.alertType === 'D' ? 'red' : 'orange'">
                {{ event.alertType === 'D' ? '土石流' : '土石流潛勢' }}
              </span>
              {{ event.disasterName }}
            </div>
            <div class="disaster-counties">{{ event.affectedCounties.join('、') }}</div>
          </div>
        </div>
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import MarketFilter from '@/components/MarketFilter.vue'
import DateRangePicker from '@/components/DateRangePicker.vue'
import PriceChart from '@/components/PriceChart.vue'
import { useMarketStore } from '@/stores/market'
import { marketApi } from '@/api/market'
import type { PriceResponseDto, DisasterResponseDto } from '@/api/market'
import { exportPricesToCsv } from '@/utils/exportCsv'

const store = useMarketStore()
const today = new Date().toISOString().split('T')[0]!
const oneYearAgo = new Date(Date.now() - 365 * 24 * 60 * 60 * 1000).toISOString().split('T')[0]!

const startDate = ref(oneYearAgo)
const endDate = ref(today)
const prices = ref<PriceResponseDto[]>([])
const rawDisasters = ref<DisasterResponseDto[]>([])
const isLoading = ref(false)
const hasQueried = ref(false)

const disasterEvents = computed(() => {
  const map = new Map<string, {
    disasterName: string; alertType: string
    firstDate: string; lastDate: string; affectedCounties: Set<string>
  }>()
  for (const d of rawDisasters.value) {
    const existing = map.get(d.disasterName)
    if (!existing) {
      map.set(d.disasterName, {
        disasterName: d.disasterName, alertType: d.alertType,
        firstDate: d.alertDate, lastDate: d.alertDate,
        affectedCounties: new Set(d.affectedCounties)
      })
    } else {
      if (d.alertDate < existing.firstDate) existing.firstDate = d.alertDate
      if (d.alertDate > existing.lastDate) existing.lastDate = d.alertDate
      d.affectedCounties.forEach(c => existing.affectedCounties.add(c))
    }
  }
  return Array.from(map.values())
    .map(e => ({ ...e, affectedCounties: Array.from(e.affectedCounties).sort() }))
    .sort((a, b) => a.firstDate.localeCompare(b.firstDate))
})

const validationMsg = computed(() => {
  if (store.selectedCropCodes.length === 0) return '請至少選擇一種作物'
  if (!startDate.value || !endDate.value) return '請選擇日期範圍'
  if (startDate.value > endDate.value) return '開始日期不能晚於結束日期'
  return ''
})

async function handleQuery() {
  if (validationMsg.value) return
  isLoading.value = true
  hasQueried.value = true
  prices.value = []
  rawDisasters.value = []
  try {
    const [priceResult, disasterResult] = await Promise.all([
      marketApi.getPrices({
        marketType: store.marketType,
        cropCodes: store.selectedCropCodes,
        marketCode: store.selectedMarketCode ?? undefined,
        startDate: startDate.value,
        endDate: endDate.value,
      }),
      marketApi.getDisasters({ startDate: startDate.value, endDate: endDate.value }),
    ])
    prices.value = priceResult
    rawDisasters.value = disasterResult
  } catch (e) {
    console.error('查詢失敗', e)
  } finally {
    isLoading.value = false
  }
}

function handleExportCsv() {
  if (prices.value.length === 0) return
  exportPricesToCsv(prices.value)
}
</script>

<style scoped>
.prices-view { width: 100%; min-width: 960px; padding: 36px 56px; box-sizing: border-box; }

h1 { font-size: 22px; font-weight: 700; color: var(--text-primary); margin-bottom: 24px; }

.filter-section {
  display: flex; flex-direction: column; gap: 16px;
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 14px; padding: 28px; margin-bottom: 24px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
}
.filter-bottom { display: flex; align-items: flex-start; gap: 28px; flex-wrap: wrap; }

.bottom-grid {
  display: grid; grid-template-columns: 1fr 280px;
  gap: 24px; align-items: start;
}

.chart-section {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 14px; padding: 28px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
}
.empty-section { display: flex; align-items: center; justify-content: center; min-height: 200px; }

.disaster-section {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 14px; padding: 22px;
  max-height: 600px; overflow-y: auto;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
  scrollbar-width: thin; scrollbar-color: rgba(0,0,0,0.15) transparent;
}
.disaster-header {
  display: flex; align-items: center; justify-content: space-between;
  margin-bottom: 14px; padding-bottom: 12px;
  border-bottom: 1px solid var(--border);
}
.disaster-title { font-size: 12px; color: var(--text-secondary); letter-spacing: 0.06em; text-transform: uppercase; font-weight: 700; }
.disaster-count { font-size: 12px; padding: 2px 9px; background: rgba(191,54,12,0.10); border: 1px solid rgba(191,54,12,0.25); border-radius: 999px; color: #bf360c; font-weight: 700; }
.disaster-empty { font-size: 12px; color: var(--text-muted); text-align: center; padding: 24px 0; }
.disaster-list { display: flex; flex-direction: column; gap: 12px; }
.disaster-item { padding: 12px 14px; background: #fff8f6; border: 1px solid rgba(191,54,12,0.12); border-radius: 10px; display: flex; flex-direction: column; gap: 5px; }
.disaster-date-range { font-size: 12px; color: #bf360c; font-variant-numeric: tabular-nums; font-weight: 600; }
.disaster-name { font-size: 14px; color: var(--text-primary); font-weight: 600; display: flex; align-items: center; gap: 6px; }
.alert-badge { font-size: 10px; padding: 1px 6px; border-radius: 4px; flex-shrink: 0; }
.alert-badge.red { background: rgba(198,40,40,0.10); color: var(--red); border: 1px solid rgba(198,40,40,0.20); }
.alert-badge.orange { background: rgba(191,54,12,0.10); color: var(--orange); border: 1px solid rgba(191,54,12,0.20); }
.disaster-counties { font-size: 12px; color: var(--text-secondary); line-height: 1.6; }

.action-row { display: flex; align-items: center; gap: 10px; }

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
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.45), inset 0 -2px 4px rgba(0,0,0,0.20), 0 3px 10px rgba(0,0,0,0.22);
}
.btn-query:active:not(:disabled) {
  background: linear-gradient(180deg, #1b5e20 0%, #2e7d32 60%, #388e3c 100%);
  box-shadow: inset 0 2px 6px rgba(0,0,0,0.35), 0 1px 3px rgba(0,0,0,0.15);
}
.btn-query:disabled { background: #c8d8c8; color: #999; border-color: #b0c8b0; box-shadow: none; cursor: not-allowed; }

.btn-export {
  padding: 9px 20px; border-radius: 999px;
  border: 1px solid #005f6b;
  background: linear-gradient(180deg, #00bcd4 0%, #0097a7 40%, #006978 100%);
  color: white; font-size: 13.5px; font-weight: 700; cursor: pointer;
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.35), inset 0 -2px 4px rgba(0,0,0,0.25), 0 2px 6px rgba(0,0,0,0.18);
  transition: all 0.15s;
}
.btn-export:hover {
  background: linear-gradient(180deg, #26c6da 0%, #00acc1 40%, #0097a7 100%);
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.45), 0 3px 10px rgba(0,0,0,0.22);
}
.btn-export:active {
  background: linear-gradient(180deg, #006978 0%, #0097a7 60%, #00acc1 100%);
  box-shadow: inset 0 2px 6px rgba(0,0,0,0.35), 0 1px 3px rgba(0,0,0,0.15);
}

.btn-clear {
  padding: 9px 18px; border-radius: 999px;
  border: 1px solid #6a1010;
  background: linear-gradient(180deg, #ff6f43 0%, #e64a19 40%, #bf360c 100%);
  color: white; font-size: 13.5px; font-weight: 700; cursor: pointer;
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.35), inset 0 -2px 4px rgba(0,0,0,0.25), 0 2px 6px rgba(0,0,0,0.18);
  transition: all 0.15s;
}
.btn-clear:hover {
  background: linear-gradient(180deg, #ff8a65 0%, #f4511e 40%, #e64a19 100%);
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.45), 0 3px 10px rgba(0,0,0,0.22);
}
.btn-clear:active {
  background: linear-gradient(180deg, #bf360c 0%, #e64a19 60%, #f4511e 100%);
  box-shadow: inset 0 2px 6px rgba(0,0,0,0.35), 0 1px 3px rgba(0,0,0,0.15);
}

.validation-msg { font-size: 13px; color: var(--red); }
.result-hint { font-size: 13px; color: var(--text-muted); text-align: center; }
</style>