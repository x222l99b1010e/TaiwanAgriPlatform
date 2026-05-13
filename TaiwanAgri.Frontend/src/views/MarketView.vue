<template>
  <div class="market-view">
    <h1>農產品市場資訊</h1>

    <!-- 篩選區（全寬） -->
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
          >
            {{ isLoading ? '查詢中...' : '查詢價格' }}
          </button>
          <!-- 按鈕列，放在查詢按鈕旁邊 -->
          <button
            v-if="prices.length > 0"
            class="btn-export"
            @click="handleExportCsv"
          >匯出 CSV</button>
          <button
            v-if="store.selectedCropCodes.length > 0"
            class="btn-clear"
            @click="store.$patch({ selectedCropCodes: [] })"
          >
            清空作物
          </button>
        </div>
        <p v-if="validationMsg" class="validation-msg">{{ validationMsg }}</p>
      </div>
    </section>

    <!-- 下方：圖表 + 天災並排 -->
    <div class="bottom-grid" v-if="hasQueried">

      <!-- 圖表區 -->
      <section class="chart-section" v-if="prices.length > 0">
        <PriceChart :prices="prices" :disasters="rawDisasters" />
      </section>
      <section class="chart-section" v-else-if="!isLoading">
        <p class="result-hint">查無資料，請確認篩選條件後重試</p>
      </section>

      <!-- 天災面板 -->
      <section class="disaster-section">
        <div class="disaster-header">
          <span class="disaster-title">天災警戒紀錄</span>
          <span class="disaster-count" v-if="disasterEvents.length > 0">
            {{ disasterEvents.length }} 件
          </span>
        </div>
        <div class="disaster-empty" v-if="disasterEvents.length === 0">
          查詢區間內無天災警戒紀錄
        </div>
        <div class="disaster-list" v-else>
          <div
            class="disaster-item"
            v-for="(event, i) in disasterEvents"
            :key="i"
          >
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
            <div class="disaster-counties">
              {{ event.affectedCounties.join('、') }}
            </div>
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

// 天災事件：按 disasterName 合併，算出首末日期
const disasterEvents = computed(() => {
  const map = new Map<string, {
    disasterName: string
    alertType: string
    firstDate: string
    lastDate: string
    affectedCounties: Set<string>
  }>()

  for (const d of rawDisasters.value) {
    const existing = map.get(d.disasterName)
    if (!existing) {
      map.set(d.disasterName, {
        disasterName: d.disasterName,
        alertType: d.alertType,
        firstDate: d.alertDate,
        lastDate: d.alertDate,
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
    // 兩支 API 同時打，不需要等第一支才打第二支
    const [priceResult, disasterResult] = await Promise.all([
      marketApi.getPrices({
        marketType: store.marketType,
        cropCodes: store.selectedCropCodes,
        marketCode: store.selectedMarketCode ?? undefined,
        startDate: startDate.value,
        endDate: endDate.value,
      }),
      marketApi.getDisasters({
        startDate: startDate.value,
        endDate: endDate.value,
      }),
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
  if (prices.value.length === 0) return  // store.prices → prices.value
  exportPricesToCsv(prices.value)
}
</script>

<style scoped>
.market-view {
  width: 100%;           /* 改回 100%，配合 App.vue 的 #app 設定 */
  min-width: 960px;      /* 加這行：低於這個寬度改為橫向捲軸，不讓版面破版 */
  padding: 36px 56px;
  box-sizing: border-box;
}

h1 {
  font-size: 22px;
  font-weight: 700;
  color: #7DD8CF;
  margin-bottom: 24px;
}

/* 篩選區：全寬 */
.filter-section {
  display: flex;
  flex-direction: column;
  gap: 16px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.10);
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  border-radius: 14px;
  padding: 28px;
  margin-bottom: 24px;
  width: 100%;
  box-sizing: border-box;
  min-width: 0;        /* overflow: hidden 改成這個 */
}

.filter-bottom {
  display: flex;
  align-items: flex-start;
  gap: 28px;
  flex-wrap: wrap;
}

/* 下方並排 Grid */
.bottom-grid {
  display: grid;
  grid-template-columns: 1fr 280px;
  gap: 24px;
  align-items: start;
  width: 100%;
  box-sizing: border-box;
}

/* 圖表區 */
.chart-section {
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid rgba(255, 255, 255, 0.09);
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  border-radius: 14px;
  padding: 28px;
  width: 100%;
  box-sizing: border-box;
}

/* 天災面板 */
.disaster-section {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.10);
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  border-radius: 14px;
  padding: 22px;
  max-height: 600px;
  overflow-y: auto;
  scrollbar-width: thin;
  scrollbar-color: rgba(255, 120, 80, 0.2) transparent;
  align-self: start;
}

.disaster-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 14px;
  padding-bottom: 12px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
}

.disaster-title {
  font-size: 12px;
  color: rgba(170, 185, 205, 0.5);
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.disaster-count {
  font-size: 11px;
  padding: 2px 8px;
  background: rgba(255, 120, 80, 0.1);
  border: 1px solid rgba(255, 120, 80, 0.25);
  border-radius: 999px;
  color: rgba(255, 150, 100, 0.8);
}

.disaster-empty {
  font-size: 12px;
  color: rgba(170, 185, 205, 0.35);
  text-align: center;
  padding: 24px 0;
}

.disaster-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.disaster-item {
  padding: 12px 14px;
  background: rgba(255, 120, 80, 0.04);
  border: 1px solid rgba(255, 120, 80, 0.12);
  border-radius: 10px;
  display: flex;
  flex-direction: column;
  gap: 5px;
}

.disaster-date-range {
  font-size: 11px;
  color: rgba(255, 150, 100, 0.7);
  font-variant-numeric: tabular-nums;
}

.disaster-name {
  font-size: 13px;
  color: rgba(215, 225, 240, 0.88);
  display: flex;
  align-items: center;
  gap: 6px;
  font-weight: 500;
}

.alert-badge {
  font-size: 10px;
  padding: 1px 6px;
  border-radius: 4px;
  flex-shrink: 0;
}

.alert-badge.red {
  background: rgba(220, 80, 60, 0.15);
  color: rgba(255, 120, 100, 0.85);
  border: 1px solid rgba(220, 80, 60, 0.25);
}

.alert-badge.orange {
  background: rgba(255, 160, 60, 0.12);
  color: rgba(255, 180, 90, 0.85);
  border: 1px solid rgba(255, 160, 60, 0.25);
}

.disaster-counties {
  font-size: 11px;
  color: rgba(170, 185, 205, 0.5);
  line-height: 1.6;
}

/* 按鈕列 */
.action-row {
  display: flex;
  align-items: center;
  gap: 10px;
}

/* ── 查詢按鈕（主要行動） ── */
.btn-query {
  padding: 9px 26px;
  border-radius: 999px;
  border: 1px solid rgba(125, 216, 207, 0.35);
  background: linear-gradient(
    180deg,
    rgba(125, 216, 207, 0.22) 0%,
    rgba(125, 216, 207, 0.10) 100%
  );
  color: #7DD8CF;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  letter-spacing: 0.02em;
  box-shadow:
    inset 0 1px 0 rgba(255, 255, 255, 0.12),
    0 4px 16px rgba(125, 216, 207, 0.12),
    0 2px 6px rgba(0, 0, 0, 0.3);
  transition: all 0.2s cubic-bezier(0.34, 1.56, 0.64, 1);
}

.btn-query:hover:not(:disabled) {
  background: linear-gradient(
    180deg,
    rgba(125, 216, 207, 0.32) 0%,
    rgba(125, 216, 207, 0.16) 100%
  );
  border-color: rgba(125, 216, 207, 0.6);
  box-shadow:
    inset 0 1px 0 rgba(255, 255, 255, 0.18),
    0 6px 20px rgba(125, 216, 207, 0.2),
    0 2px 8px rgba(0, 0, 0, 0.3);
  transform: translateY(-1px);
}

.btn-query:active {
  transform: translateY(0);
  box-shadow:
    inset 0 1px 0 rgba(0, 0, 0, 0.1),
    0 2px 6px rgba(0, 0, 0, 0.25);
}

.btn-query:disabled {
  border-color: rgba(255, 255, 255, 0.06);
  background: rgba(255, 255, 255, 0.04);
  color: rgba(180, 195, 210, 0.25);
  box-shadow: none;
  cursor: not-allowed;
}

/* ── 清空作物（輔助，橘色調） ── */
.btn-clear {
  padding: 9px 18px;
  border-radius: 999px;
  border: 1px solid rgba(240, 150, 100, 0.28);
  background: linear-gradient(
    180deg,
    rgba(240, 150, 100, 0.13) 0%,
    rgba(240, 150, 100, 0.05) 100%
  );
  color: rgba(240, 170, 120, 0.82);
  font-size: 13.5px;
  cursor: pointer;
  letter-spacing: 0.01em;
  box-shadow:
    inset 0 1px 0 rgba(255, 255, 255, 0.08),
    0 3px 10px rgba(0, 0, 0, 0.22);
  transition: all 0.2s cubic-bezier(0.34, 1.56, 0.64, 1);
}

.btn-clear:hover {
  background: linear-gradient(
    180deg,
    rgba(240, 150, 100, 0.22) 0%,
    rgba(240, 150, 100, 0.10) 100%
  );
  border-color: rgba(240, 150, 100, 0.5);
  box-shadow:
    inset 0 1px 0 rgba(255, 255, 255, 0.12),
    0 5px 14px rgba(240, 150, 100, 0.14),
    0 2px 6px rgba(0, 0, 0, 0.25);
  transform: translateY(-1px);
}

.btn-clear:active {
  transform: translateY(0);
}

.validation-msg {
  font-size: 13px;
  color: rgba(240, 100, 100, 0.8);
  margin: 0;
}

.result-hint {
  font-size: 13px;
  color: rgba(170, 185, 205, 0.45);
  text-align: center;
  padding: 20px 0;
}

/* ── 匯出 CSV（次要行動） ── */
.btn-export {
  padding: 9px 20px;
  border-radius: 999px;
  border: 1px solid rgba(100, 170, 220, 0.3);
  background: linear-gradient(
    180deg,
    rgba(100, 170, 220, 0.16) 0%,
    rgba(100, 170, 220, 0.07) 100%
  );
  color: rgba(140, 195, 235, 0.88);
  font-size: 13.5px;
  font-weight: 500;
  cursor: pointer;
  letter-spacing: 0.02em;
  box-shadow:
    inset 0 1px 0 rgba(255, 255, 255, 0.10),
    0 3px 10px rgba(0, 0, 0, 0.25);
  transition: all 0.2s cubic-bezier(0.34, 1.56, 0.64, 1);
}

.btn-export:hover {
  background: linear-gradient(
    180deg,
    rgba(100, 170, 220, 0.25) 0%,
    rgba(100, 170, 220, 0.12) 100%
  );
  border-color: rgba(100, 170, 220, 0.55);
  box-shadow:
    inset 0 1px 0 rgba(255, 255, 255, 0.15),
    0 5px 16px rgba(100, 170, 220, 0.15),
    0 2px 8px rgba(0, 0, 0, 0.28);
  transform: translateY(-1px);
}

.btn-export:active {
  transform: translateY(0);
  box-shadow: inset 0 1px 0 rgba(0,0,0,0.1), 0 2px 4px rgba(0,0,0,0.2);
}

</style>