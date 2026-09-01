<template>
  <div class="page today-veg-view">
    <PageHeader title="今日民生蔬菜均價">
      <template #subtitle>
        資料來源：台北一果菜市場（市場代號 109）
        <span v-if="latestDate" class="data-date">｜ 最新交易日：{{ latestDate }}</span>
      </template>
    </PageHeader>

    <StateBlock v-if="store.isLoadingTodayVeg" state="loading" message="資料載入中..." />
    <StateBlock
      v-else-if="store.todayVegError"
      state="error"
      :message="store.todayVegError"
      retryable
      @retry="store.fetchTodayVegPrices()"
    />
    <StateBlock
      v-else-if="store.todayVegPrices.length === 0 && store.todayVegHasFetched"
      state="empty"
      icon="mdi-calendar-remove"
      message="今日無菜價資料"
      hint="可能是休市日，明天再回來看看"
    />

    <!-- 資料卡片：一列兩張 -->
    <div v-else class="price-grid">
      <div
        class="price-card"
        v-for="item in store.todayVegPrices"
        :key="item.cropCode"
      >
        <div class="card-header">
          <span class="mdi mdi-sprout crop-icon" />
          <span class="crop-name">{{ item.cropName }}</span>
        </div>

        <div class="avg-price-row">
          <span class="price-value">{{ item.avgPrice.toFixed(1) }}</span>
          <span class="price-unit">元／公斤</span>
        </div>

        <div class="divider" />

        <div class="price-detail-grid">
          <div class="price-detail-item">
            <span class="detail-label">上價</span>
            <span class="detail-value upper">{{ item.upperPrice.toFixed(1) }}</span>
          </div>
          <div class="price-detail-item">
            <span class="detail-label">中價</span>
            <span class="detail-value middle">{{ item.middlePrice.toFixed(1) }}</span>
          </div>
          <div class="price-detail-item">
            <span class="detail-label">下價</span>
            <span class="detail-value lower">{{ item.lowerPrice.toFixed(1) }}</span>
          </div>
        </div>

        <div class="card-footer">
          <span class="mdi mdi-calendar-check footer-icon" />
          {{ item.transDate }}
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useFoodSafetyStore } from '@/stores/foodSafety'
import PageHeader from '@/components/ui/PageHeader.vue'
import StateBlock from '@/components/ui/StateBlock.vue'

const store = useFoodSafetyStore()

// 從資料裡取出交易日（所有卡片同一天，取第一筆即可）
const latestDate = computed(() =>
  store.todayVegPrices.length > 0 ? store.todayVegPrices[0]?.transDate : null
)

onMounted(() => {
  store.fetchTodayVegPrices()
})
</script>

<style scoped>
/* ── 頁首 ── */
.data-date {
  font-weight: var(--weight-medium);
  color: var(--green-600);
}

/* ── 卡片格狀：一列兩張 ── */
.price-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: var(--space-6);
}

/* ── 單張卡片 ── */
.price-card {
  background: var(--neutral-0);
  border: 1.5px solid var(--green-200);
  border-radius: var(--radius-xl);
  padding: var(--space-8);
  box-shadow: var(--shadow-md);
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  transition: box-shadow var(--duration-fast), transform var(--duration-fast);
}

.price-card:hover {
  box-shadow: var(--shadow-lg);
  transform: translateY(-2px);
}

/* ── 卡片頂部：圖示 + 作物名稱 ── */
.card-header {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}

.crop-icon {
  font-size: var(--text-xl);
  color: var(--green-500);
}

.crop-name {
  font-size: var(--text-lg);
  font-weight: var(--weight-bold);
  color: var(--green-900);
  letter-spacing: -0.01em;
}

/* ── 均價大字 ── */
.avg-price-row {
  display: flex;
  align-items: baseline;
  gap: var(--space-2);
}

.price-value {
  font-size: var(--text-3xl);
  font-weight: var(--weight-bold);
  color: var(--green-600);
  font-variant-numeric: tabular-nums;
  line-height: 1;
}

.price-unit {
  font-size: var(--text-base);
  color: var(--neutral-600);
  font-weight: var(--weight-medium);
}

/* ── 分隔線 ── */
.divider {
  height: 1px;
  background: var(--green-100);
}

/* ── 上中下價三欄 ── */
.price-detail-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: var(--space-2);
}

.price-detail-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-1);
  background: var(--green-50);
  border-radius: var(--radius-lg);
  padding: var(--space-3) var(--space-2);
}

.detail-label {
  font-size: var(--text-2xs);
  color: var(--neutral-500);
  font-weight: var(--weight-medium);
  letter-spacing: 0.04em;
}

.detail-value {
  font-size: var(--text-lg);
  font-weight: var(--weight-bold);
  font-variant-numeric: tabular-nums;
}

.detail-value.upper { color: var(--danger-500); }
.detail-value.middle { color: var(--green-600); }
.detail-value.lower { color: var(--info-500); }

/* ── 卡片底部：交易日 ── */
.card-footer {
  display: none;
  align-items: center;
  gap: var(--space-2);
  font-size: var(--text-xs);
  color: var(--neutral-400);
  font-variant-numeric: tabular-nums;
}

.footer-icon {
  font-size: var(--text-base);
  color: var(--neutral-400);
}
</style>