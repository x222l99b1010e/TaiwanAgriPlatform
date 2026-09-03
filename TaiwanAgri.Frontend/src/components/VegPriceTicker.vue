<!--
  src/components/VegPriceTicker.vue
  職責：導覽列正下方的今日菜價橫幅。台北一市場、十種民生蔬菜的今日均價。

  ── 這一版（P3）改了什麼、為什麼 ──
  1. **從亮綠色帶改成深色資料帶。** 原本用的是 --green-100/200/300 那組舊色階，
     整條薄荷綠橫在夜土色導覽列與暖米白內容之間，是畫面上唯一不屬於秋田色盤的東西。
     改成 --color-deep-2 之後它變成導覽列的下半截，深色區一次結束，再交給淺色內文——
     深淺只換一次，而不是深、亮綠、淺換三次。
  2. **分隔改用細直線，不用「•」。** 圓點在滾動時會被誤讀成資料的一部分
     （價格後面跟著一個點）；直線是純粹的分隔，而且與導覽列的節奏一致。
  3. **兩端做漸隱。** 沒有漸隱時，品項會在標籤與日期的邊界上被直接切斷，
     看起來像沒寫完；漸隱讓它是「滑進來、滑出去」。
  4. **滑鼠移上去就停。** 一直在動的價格是讀不到的——想看某一項就得追著它跑。
     這是這條橫幅唯一真正的可用性缺陷，restyle 順手一起修。
  5. **左邊的標籤是連結**，指向今日菜價完整頁；橫幅本身只放得下十項，
     使用者看到有興趣的東西時要有地方去。
-->
<template>
  <div class="ticker-bar" v-if="store.todayVegPrices.length > 0">
    <RouterLink to="/food-safety/today-veg" class="ticker-label">
      <span class="mdi mdi-sprout label-icon" />
      <span class="label-zh">今日菜價</span>
      <span class="label-en" aria-hidden="true">TODAY</span>
      <span class="mdi mdi-chevron-right label-arrow" />
    </RouterLink>

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
        </span>
      </div>
    </div>

    <div class="ticker-date">
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
/* 高度用 --control-h：這條帶子的高度跟按鈕、輸入框是同一把尺，
   不再是自己一個 44px。 */
.ticker-bar {
  display: flex;
  align-items: stretch;
  height: var(--control-h);
  background: var(--color-deep-2);
  border-bottom: var(--border-width) solid var(--color-deep-border);
  overflow: hidden;
  flex-shrink: 0;
}

/* ── 左側標籤（同時是往完整頁的連結） ── */
.ticker-label {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  padding: 0 var(--space-5) 0 var(--bleed-padding-x);
  color: var(--color-on-deep);
  text-decoration: none;
  white-space: nowrap;
  border-right: var(--border-width) solid var(--color-deep-border);
  transition: background var(--duration-fast) var(--ease-work);
}
.ticker-label:hover { background: var(--white-a12); }
.ticker-label:focus-visible { outline: 2px solid var(--color-action-on-deep); outline-offset: -2px; }

.label-zh {
  font-size: var(--text-sm);
  font-weight: var(--weight-bold);
  letter-spacing: 0.05em;
}

/* 中英並排樣式 A：英文 12px／600／字距 .12em／全大寫，窄螢幕整個收掉 */
.label-en {
  font-family: var(--font-num);
  font-size: 12px;
  font-weight: 600;
  letter-spacing: var(--tracking-label);
  color: var(--color-on-deep-dim);
}

.label-icon { font-size: var(--text-lg); color: var(--color-action-on-deep); }
.label-arrow {
  font-size: var(--text-base);
  color: var(--color-on-deep-dim);
  transition: transform var(--duration-fast) var(--ease-work);
}
.ticker-label:hover .label-arrow { transform: translateX(2px); }

/* ── 滾動軌道 ──
   兩端漸隱，讓品項是滑進滑出而不是被切斷。mask 只影響這一層，
   標籤與日期兩塊不在裡面所以不受影響。 */
.ticker-track-wrapper {
  flex: 1;
  overflow: hidden;
  min-width: 0;
  display: flex;
  align-items: center;
  mask-image: linear-gradient(
    to right,
    transparent 0,
    #000 var(--space-8),
    #000 calc(100% - var(--space-8)),
    transparent 100%
  );
}

.ticker-track {
  display: flex;
  align-items: center;
  white-space: nowrap;
  animation: ticker-scroll linear infinite;
  will-change: transform;
}

/* 滑過去就停：一直在動的數字讀不到。用 :hover 掛在 wrapper 上而不是 track 上，
   因為 track 是滾動中的元素，滑鼠很容易落在兩個品項之間的空隙。 */
.ticker-track-wrapper:hover .ticker-track { animation-play-state: paused; }

@keyframes ticker-scroll {
  from { transform: translateX(0); }
  to   { transform: translateX(-50%); }
}

/* ── 每一個品項 ──
   分隔線用 ::after 畫，不用文字的「•」：圓點會被讀成價格的一部分。 */
.ticker-item {
  position: relative;
  display: inline-flex;
  align-items: baseline;
  gap: var(--space-2);
  padding: 0 var(--space-6);
}

.ticker-item::after {
  content: '';
  position: absolute;
  right: 0;
  top: 50%;
  transform: translateY(-50%);
  width: 1px;
  height: 14px;
  background: var(--color-deep-border);
}

.ticker-crop {
  font-size: var(--text-sm);
  color: var(--color-on-deep-dim);
  font-weight: var(--weight-normal);
}

/* 價格是這條橫幅唯一的主角：拿最亮的字色、數字字型、也拿最大的字級——
   品名（dim、text-sm）與價格（亮、text-xl）之間的級距拉開，滾動時才有主次，
   不會整條看起來一樣重（owner 2026-09-03：這排沒層次）。整條刻意維持深色不調亮：
   調亮會變成導覽列下方第二條亮帶，跟內容搶注意力，層次靠對比做、不靠整體變亮。 */
.ticker-price {
  font-family: var(--font-num);
  font-size: var(--text-xl);
  font-weight: var(--weight-bold);
  color: var(--color-on-deep);
  font-variant-numeric: tabular-nums;
  letter-spacing: -0.01em;
}

.ticker-unit {
  font-size: var(--text-2xs);
  color: var(--color-on-deep-dim);
}

/* ── 右側日期 ── */
.ticker-date {
  display: flex;
  align-items: center;
  padding: 0 var(--bleed-padding-x) 0 var(--space-5);
  font-family: var(--font-num);
  font-size: var(--text-xs);
  color: var(--color-on-deep-dim);
  white-space: nowrap;
  border-left: var(--border-width) solid var(--color-deep-border);
  font-variant-numeric: tabular-nums;
  letter-spacing: 0.02em;
}

/* 窄螢幕：英文標籤與日期先收掉，中文與價格是主體 */
@media (max-width: 640px) {
  .label-en { display: none; }
  .ticker-date { display: none; }
}
</style>
