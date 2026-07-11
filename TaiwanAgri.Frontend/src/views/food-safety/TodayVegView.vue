<template>
  <div class="today-veg-view">
    <div class="page-header">
      <h2 class="section-title">今日民生蔬菜均價</h2>
      <p class="section-subtitle">
        資料來源：台北一果菜市場（市場代號 109）
        <span v-if="latestDate" class="data-date">｜ 最新交易日：{{ latestDate }}</span>
      </p>
    </div>

    <!-- 載入中 -->
    <div v-if="store.isLoadingTodayVeg" class="state-box">
      <div class="loading-spinner" />
      <span class="state-text">資料載入中...</span>
    </div>

    <!-- 錯誤 -->
    <div v-else-if="store.todayVegError" class="state-box error-box">
      <span class="mdi mdi-alert-circle state-icon" />
      <span class="state-text">{{ store.todayVegError }}</span>
      <button class="btn-retry" @click="store.fetchTodayVegPrices()">重試</button>
    </div>

    <!-- 無資料 -->
    <div v-else-if="store.todayVegPrices.length === 0 && store.todayVegHasFetched" class="state-box">
      <span class="mdi mdi-calendar-remove state-icon" />
      <span class="state-text">今日無菜價資料（可能為休市日）</span>
    </div>

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
.today-veg-view {
  padding: 40px 48px;
  width: 100%;
  box-sizing: border-box;
}

/* ── 頁首 ── */
.page-header { margin-bottom: 32px; }

.section-title {
  font-size: 24px;
  font-weight: 800;
  color: #1b5e20;
  margin-bottom: 8px;
  letter-spacing: -0.01em;
}

.section-subtitle {
  font-size: 13px;
  color: #555;
}

.data-date {
  font-weight: 600;
  color: #2e7d32;
}

/* ── 狀態容器（載入／錯誤／無資料） ── */
.state-box {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 14px;
  padding: 64px 32px;
  background: #f9faf9;
  border: 1px solid #dce8dc;
  border-radius: 16px;
}

.state-icon {
  font-size: 36px;
  color: #aaa;
}

.state-text {
  font-size: 15px;
  color: #666;
}

.error-box .state-icon { color: #c62828; }
.error-box .state-text { color: #c62828; }

.loading-spinner {
  width: 36px;
  height: 36px;
  border: 3px solid #c8e6c9;
  border-top-color: #2e7d32;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin { to { transform: rotate(360deg); } }

.btn-retry {
  padding: 8px 24px;
  border-radius: 999px;
  border: 1.5px solid #c62828;
  background: transparent;
  color: #c62828;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.15s;
}
.btn-retry:hover { background: #fff5f5; }

/* ── 卡片格狀：一列兩張 ── */
.price-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 24px;
}

/* ── 單張卡片 ── */
.price-card {
  background: #fff;
  border: 1.5px solid #c8e6c9;
  border-radius: 18px;
  padding: 28px 32px;
  box-shadow: 0 4px 16px rgba(46, 125, 50, 0.08);
  display: flex;
  flex-direction: column;
  gap: 16px;
  transition: box-shadow 0.18s, transform 0.18s;
}

.price-card:hover {
  box-shadow: 0 8px 28px rgba(46, 125, 50, 0.16);
  transform: translateY(-2px);
}

/* ── 卡片頂部：圖示 + 作物名稱 ── */
.card-header {
  display: flex;
  align-items: center;
  gap: 10px;
}

.crop-icon {
  font-size: 22px;
  color: #388e3c;
}

.crop-name {
  font-size: 20px;
  font-weight: 800;
  color: #1a2e1a;
  letter-spacing: -0.01em;
}

/* ── 均價大字 ── */
.avg-price-row {
  display: flex;
  align-items: baseline;
  gap: 6px;
}

.price-value {
  font-size: 48px;
  font-weight: 900;
  color: #2e7d32;
  font-variant-numeric: tabular-nums;
  line-height: 1;
}

.price-unit {
  font-size: 15px;
  color: #666;
  font-weight: 500;
}

/* ── 分隔線 ── */
.divider {
  height: 1px;
  background: #e8f5e9;
}

/* ── 上中下價三欄 ── */
.price-detail-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
}

.price-detail-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  background: #f1f8f1;
  border-radius: 10px;
  padding: 10px 8px;
}

.detail-label {
  font-size: 11px;
  color: #777;
  font-weight: 600;
  letter-spacing: 0.04em;
}

.detail-value {
  font-size: 18px;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}

.detail-value.upper { color: #c62828; }
.detail-value.middle { color: #2e7d32; }
.detail-value.lower { color: #1565c0; }

/* ── 卡片底部：交易日 ── */
.card-footer {
  display: none;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: #999;
  font-variant-numeric: tabular-nums;
}

.footer-icon {
  font-size: 14px;
  color: #bbb;
}
</style>