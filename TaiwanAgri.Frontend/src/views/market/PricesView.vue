<template>
  <div class="page prices-view">
    <PageHeader
      title="作物行情查詢"
      subtitle="蔬菜、水果、花卉在各批發市場的每日交易均價，可同時比較多項作物並疊上同期天災警戒"
    />

    <FilterCard layout="stack">
      <MarketFilter />
      <div class="filter-bottom">
        <DateRangePicker v-model:startDate="startDate" v-model:endDate="endDate" />
        <div class="action-row">
          <Btn
            icon="mdi-magnify"
            :loading="isLoading"
            :disabled="store.selectedCropCodes.length === 0"
            @click="handleQuery"
          >{{ isLoading ? '查詢中...' : '查詢價格' }}</Btn>
          <Btn
            v-if="prices.length > 0"
            variant="secondary"
            icon="mdi-file-chart"
            @click="handleExportCsv"
          >匯出 CSV</Btn>
          <Btn
            v-if="store.selectedCropCodes.length > 0"
            variant="secondary"
            icon="mdi-filter-remove-outline"
            @click="store.$patch({ selectedCropCodes: [] })"
          >清空作物</Btn>
        </div>
        <p v-if="validationMsg" class="validation-msg">{{ validationMsg }}</p>
      </div>
    </FilterCard>

    <StateBlock v-if="!hasQueried" state="hint" message="請選擇作物與日期區間後按下查詢" />
    <StateBlock v-else-if="isLoading" state="loading" message="資料載入中..." />
    <StateBlock
      v-else-if="errorMsg"
      state="error"
      :message="errorMsg"
      retryable
      @retry="handleQuery"
    />

    <div class="bottom-grid" v-else>
      <section class="chart-section" v-if="prices.length > 0">
        <PriceChart :prices="prices" :disasters="rawDisasters" />
      </section>
      <StateBlock
        v-else
        state="empty"
        message="查無價格資料"
        hint="請確認作物、市場與日期區間後重試"
      />

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
import PageHeader from '@/components/ui/PageHeader.vue'
import FilterCard from '@/components/ui/FilterCard.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'

const store = useMarketStore()
const today = new Date().toISOString().split('T')[0]!
const oneYearAgo = new Date(Date.now() - 365 * 24 * 60 * 60 * 1000).toISOString().split('T')[0]!

const startDate = ref(oneYearAgo)
const endDate = ref(today)
const prices = ref<PriceResponseDto[]>([])
const rawDisasters = ref<DisasterResponseDto[]>([])
const isLoading = ref(false)
const hasQueried = ref(false)
// 先前查詢失敗只 console.error，畫面上完全沒有任何訊息——使用者只會看到「查無資料」，
// 分不出是真的沒有資料還是請求失敗。改成跟其他頁一樣走 StateBlock 的錯誤狀態。
const errorMsg = ref('')

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
  errorMsg.value = ''
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
    errorMsg.value = '查詢失敗，請稍後再試'
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
.prices-view { min-width: 960px; }
.filter-bottom { display: flex; align-items: flex-start; gap: 28px; flex-wrap: wrap; }

.bottom-grid {
  display: grid; grid-template-columns: 1fr 280px;
  gap: var(--space-6); align-items: start;
}

.chart-section {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 14px; padding: 28px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
}
.disaster-section {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 14px; padding: 22px;
  max-height: 600px; overflow-y: auto;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
  scrollbar-width: thin; scrollbar-color: var(--neutral-300) transparent;
}
.disaster-header {
  display: flex; align-items: center; justify-content: space-between;
  margin-bottom: 14px; padding-bottom: var(--space-3);
  border-bottom: 1px solid var(--border);
}
.disaster-title { font-size: var(--text-xs); color: var(--text-secondary); letter-spacing: 0.06em; text-transform: uppercase; font-weight: var(--weight-bold); }
.disaster-count { font-size: var(--text-xs); padding: 2px 9px; background: var(--warning-50); border: 1px solid var(--warning-100); border-radius: var(--radius-full); color: var(--warning-700); font-weight: var(--weight-bold); }
.disaster-empty { font-size: var(--text-xs); color: var(--text-muted); text-align: center; padding: var(--space-6) 0; }
.disaster-list { display: flex; flex-direction: column; gap: var(--space-3); }
.disaster-item { padding: var(--space-3) 14px; background: var(--warning-50); border: 1px solid var(--warning-50); border-radius: 10px; display: flex; flex-direction: column; gap: 5px; }
.disaster-date-range { font-size: var(--text-xs); color: var(--warning-700); font-variant-numeric: tabular-nums; font-weight: 600; }
.disaster-name { font-size: 14px; color: var(--text-primary); font-weight: 600; display: flex; align-items: center; gap: 6px; }
.alert-badge { font-size: 10px; padding: 1px 6px; border-radius: var(--radius-sm); flex-shrink: 0; }
.alert-badge.red { background: var(--danger-50); color: var(--red); border: 1px solid var(--danger-100); }
.alert-badge.orange { background: var(--warning-50); color: var(--orange); border: 1px solid var(--warning-100); }
.disaster-counties { font-size: var(--text-xs); color: var(--text-secondary); line-height: var(--leading-normal); }

.action-row { display: flex; align-items: center; gap: 10px; }
.validation-msg { font-size: var(--text-sm); color: var(--red); }
</style>