<template>
  <div class="ticker-bar" v-if="store.todayVegPrices.length > 0">
    <div class="ticker-label">
      <span class="mdi mdi-sprout label-icon" />
      今日菜價
    </div>
    <div class="ticker-track-wrapper">
      <div class="ticker-track" :style="{ animationDuration: `${animationDuration}s` }">
        <span
          class="ticker-item"
          v-for="(item, i) in displayItems"
          :key="i"
        >
          <span class="ticker-crop">{{ item.cropName }}</span>
          <span class="ticker-price">{{ item.avgPrice.toFixed(1) }}</span>
          <span class="ticker-unit">元/kg</span>
          <span class="ticker-dot">•</span>
        </span>
      </div>
    </div>
    <div class="ticker-date" v-if="store.todayVegPrices.length > 0">
      <span class="mdi mdi-calendar-check date-icon" />
      {{ store.todayVegPrices[0]?.transDate }}
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useFoodSafetyStore } from '@/stores/foodSafety'

const store = useFoodSafetyStore()

onMounted(() => {
  store.fetchTodayVegPrices()
})

const displayItems = computed(() => [
  ...store.todayVegPrices,
  ...store.todayVegPrices,
])

const animationDuration = computed(() =>
  store.todayVegPrices.length * 3.5
)
</script>

<style scoped>
.ticker-bar {
  display: flex;
  align-items: center;
  height: 44px;
  background: var(--green-100);
  border-bottom: 2px solid var(--green-300);
  overflow: hidden;
  flex-shrink: 0;
}

/* ── 左側標籤 ── */
.ticker-label {
  display: flex;
  align-items: center;
  gap: 7px;
  padding: 0 22px;
  font-size: 13px;
  font-weight: 800;
  color: var(--green-800);
  white-space: nowrap;
  border-right: 2px solid var(--green-300);
  height: 100%;
  background: var(--green-200);
  letter-spacing: 0.05em;
}

.label-icon {
  font-size: 17px;
  color: var(--green-600);
}

/* ── 滾動軌道 ── */
.ticker-track-wrapper {
  flex: 1;
  overflow: hidden;
  height: 100%;
  display: flex;
  align-items: center;
}

.ticker-track {
  display: flex;
  align-items: center;
  white-space: nowrap;
  animation: ticker-scroll linear infinite;
  will-change: transform;
}

@keyframes ticker-scroll {
  from { transform: translateX(0); }
  to   { transform: translateX(-50%); }
}

/* ── 每一個品項 ── */
.ticker-item {
  display: inline-flex;
  align-items: baseline;
  gap: 5px;
  padding: 0 24px;
}

.ticker-crop {
  font-size: 14px;
  color: var(--green-700);
  font-weight: 600;
}

.ticker-price {
  font-size: 17px;
  font-weight: 900;
  color: var(--green-800);
  font-variant-numeric: tabular-nums;
}

.ticker-unit {
  font-size: 12px;
  color: var(--green-500);
  font-weight: 500;
}

.ticker-dot {
  font-size: 11px;
  color: var(--green-300);
  padding-left: 4px;
}

/* ── 右側日期 ── */
.ticker-date {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 0 20px;
  font-size: 12px;
  color: var(--green-600);
  font-weight: 600;
  white-space: nowrap;
  border-left: 2px solid var(--green-300);
  height: 100%;
  font-variant-numeric: tabular-nums;
  background: var(--green-200);
}

.date-icon {
  font-size: 14px;
  color: var(--green-400);
}
</style>