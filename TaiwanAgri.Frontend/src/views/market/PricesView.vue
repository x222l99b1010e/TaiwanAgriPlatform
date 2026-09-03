<template>
  <div class="page prices-view">
    <QueryLayout
      title="作物行情查詢"
      title-en="CROP PRICES"
      subtitle="蔬菜、水果、花卉在各批發市場的每日交易均價，可同時比較多項作物並疊上同期天災警戒"
    >
      <!-- 動作列改放 QueryLayout 的頂部右側插槽。原本是 FilterCard layout="stack"
           底下自己手寫一層 .filter-bottom，那一層的 align-items 是 flex-start，
           讓按鈕比左邊的日期輸入框高 23px（決策 59.十一）——繞過共用元件自己排，
           就會繞過共用元件已經修好的東西。 -->
      <template #actions>
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
      </template>

      <template #filters>
        <div class="filter-stack">
          <MarketFilter />
          <DateRangePicker v-model:startDate="startDate" v-model:endDate="endDate" />
          <p v-if="validationMsg" class="validation-msg">
            <span class="mdi mdi-alert-circle-outline" />{{ validationMsg }}
          </p>
        </div>
      </template>

      <template #results>
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
          <section class="chart-section card card--lg" v-if="prices.length > 0">
            <PriceChart :prices="prices" :disasters="rawDisasters" />
          </section>
          <StateBlock
            v-else
            state="empty"
            message="查無價格資料"
            hint="請確認作物、市場與日期區間後重試"
          />

          <!-- 天災警戒是圖表的註腳而不是另一份資料：圖上那幾條標註線代表什麼，
               在這裡才看得到名稱與縣市，所以兩者並排、不做成分頁 -->
          <section class="disaster-section card">
            <div class="disaster-header">
              <span class="field-label">天災警戒紀錄</span>
              <span class="badge disaster-count" v-if="disasterEvents.length > 0">
                {{ disasterEvents.length }} 件
              </span>
            </div>
            <div class="disaster-empty" v-if="disasterEvents.length === 0">查詢區間內無天災警戒紀錄</div>
            <div class="disaster-list" v-else>
              <div class="disaster-item" v-for="(event, i) in disasterEvents" :key="i">
                <div class="disaster-date-range">
                  {{ event.firstDate }}
                  <span v-if="event.lastDate !== event.firstDate"> ～ {{ event.lastDate }}</span>
                </div>
                <div class="disaster-name">
                  <span class="badge alert-badge" :class="event.alertType === 'D' ? 'red' : 'orange'">
                    {{ event.alertType === 'D' ? '土石流' : '土石流潛勢' }}
                  </span>
                  {{ event.disasterName }}
                </div>
                <div class="disaster-counties">{{ event.affectedCounties.join('、') }}</div>
              </div>
            </div>
          </section>
        </div>
      </template>
    </QueryLayout>
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
import QueryLayout from '@/components/layouts/QueryLayout.vue'
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
/* 顏色全部改用 semantic 層，不再引用待刪的 --neutral- 舊色階（style tile §九）。
   卡片外殼改用 base.css 的 .card／.card--lg，這裡只留這一頁真正不同的部分。 */
.prices-view { min-width: 960px; }

/* 這一頁的查詢條件是兩段式（作物選擇是一整塊、日期是一列），所以在 filters 插槽裡
   直接堆疊。QueryLayout 的 __body 本身是 flex-end 的橫列，兩者不衝突：
   這裡包一層自己的縱向容器，橫列規則只作用在這一個子元素上。 */
.filter-stack { display: flex; flex-direction: column; gap: var(--space-5); width: 100%; }

.validation-msg {
  display: flex; align-items: center; gap: var(--space-2);
  font-size: var(--text-sm); color: var(--danger-700);
}

.bottom-grid {
  display: grid; grid-template-columns: 1fr 280px;
  gap: var(--space-6); align-items: start;
}

.disaster-section {
  max-height: 600px; overflow-y: auto;
  scrollbar-width: thin; scrollbar-color: var(--color-border-strong) transparent;
}
.disaster-header {
  display: flex; align-items: center; justify-content: space-between;
  margin-bottom: var(--space-4); padding-bottom: var(--space-3);
  border-bottom: var(--border-width) solid var(--color-border);
}
.disaster-count { background: var(--warning-50); border: var(--border-width) solid var(--warning-100); color: var(--warning-700); }
.disaster-empty { font-size: var(--text-xs); color: var(--color-text-dim); text-align: center; padding: var(--space-6) 0; }
.disaster-list { display: flex; flex-direction: column; gap: var(--space-3); }
/* 左側那道橘色粗邊是刻意的：一整格淺橘底在暖米白卡片上幾乎看不出來
   （兩者明度太近），改成「淺底＋左邊界」之後，一眼就數得出有幾件 */
.disaster-item {
  padding: var(--space-3) var(--space-4);
  background: var(--warning-50);
  border-inline-start: 3px solid var(--color-accent-2-fill);
  border-radius: 0 var(--radius-md) var(--radius-md) 0;
  display: flex; flex-direction: column; gap: var(--space-1);
}
.disaster-date-range {
  font-family: var(--font-num); font-size: var(--text-xs); color: var(--warning-700);
  font-variant-numeric: tabular-nums; font-weight: var(--weight-medium);
}
.disaster-name { font-size: var(--text-base); color: var(--color-text); font-weight: var(--weight-medium); display: flex; align-items: center; gap: var(--space-2); }
/* 標籤外殼已收進 base.css 的 .badge，這裡只留語意色 */
.alert-badge.red { background: var(--danger-50); color: var(--danger-500); border: var(--border-width) solid var(--danger-100); }
.alert-badge.orange { background: var(--color-surface); color: var(--warning-700); border: var(--border-width) solid var(--warning-100); }
.disaster-counties { font-size: var(--text-xs); color: var(--color-text-dim); line-height: var(--leading-normal); }
</style>