<template>
  <div class="page today-veg-view">
    <PageHeader title="今日民生蔬菜均價" title-en="TODAY'S VEG">
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

    <div v-else class="price-grid">
      <article
        class="price-card"
        v-for="item in store.todayVegPrices"
        :key="item.cropCode"
      >
        <h2 class="crop-name">{{ item.cropName }}</h2>

        <div class="avg-price-row">
          <span class="price-value">{{ item.avgPrice.toFixed(1) }}</span>
          <span class="price-unit">元／公斤</span>
        </div>

        <!-- 下價 → 上價的區間帶，均價落在中間某個位置。
             原本這三個數字是三格並排的方塊，讀者要自己在腦中算「均價偏高還是偏低」；
             畫成一條帶子之後，價差有多大、均價偏哪一邊，一眼就看得出來，
             而且十張卡片並排時可以互相比較「哪一種菜今天的價差特別大」。 -->
        <div class="range">
          <div class="range-track">
            <span class="range-marker" :style="{ insetInlineStart: markerPct(item) }" />
          </div>
          <div class="range-labels">
            <span class="range-end">下 {{ item.lowerPrice.toFixed(1) }}</span>
            <span class="range-mid">中 {{ item.middlePrice.toFixed(1) }}</span>
            <span class="range-end">上 {{ item.upperPrice.toFixed(1) }}</span>
          </div>
        </div>

        <p class="card-footer">交易量 {{ item.transQuantity.toLocaleString() }} 公斤</p>
      </article>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useFoodSafetyStore } from '@/stores/foodSafety'
import type { PriceResponseDto } from '@/api/market'
import PageHeader from '@/components/ui/PageHeader.vue'
import StateBlock from '@/components/ui/StateBlock.vue'

const store = useFoodSafetyStore()

// 從資料裡取出交易日（所有卡片同一天，取第一筆即可）
const latestDate = computed(() =>
  store.todayVegPrices.length > 0 ? store.todayVegPrices[0]?.transDate : null
)

/** 均價在「下價～上價」這條帶子上的位置。
 *  上下價相同時（單一成交價）除數會是 0，這時直接放中間，不要讓它變成 NaN%
 *  ——CSS 收到 NaN% 會整條規則失效，marker 會掉回帶子最左邊，看起來像資料有問題。 */
function markerPct(item: PriceResponseDto): string {
  const span = item.upperPrice - item.lowerPrice
  if (span <= 0) return '50%'
  const pct = ((item.avgPrice - item.lowerPrice) / span) * 100
  // 夾在 0–100：均價理論上一定落在上下價之間，但這是外部資料，不保證
  return `${Math.min(100, Math.max(0, pct))}%`
}

onMounted(() => {
  store.fetchTodayVegPrices()
})
</script>

<style scoped>
/* 顏色全部改用 semantic 層（style tile §九）；卡片不給陰影，靠 1px 邊框與底色差。 */
.data-date {
  font-weight: var(--weight-medium);
  color: var(--color-text);
}

/* 原本是固定兩欄、每張卡片 32px 內距的大卡片，十種菜要捲三屏才看得完。
   改成自動填滿的窄卡片：一屏就看得到全部，才有「互相比較」的可能。 */
.price-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: var(--space-4);
}

.price-card {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--space-5);
  transition: border-color var(--duration-fast) var(--ease-work);
}
.price-card:hover { border-color: var(--color-border-strong); }

.crop-name {
  font-size: var(--text-base);
  font-weight: var(--weight-bold);
  line-height: var(--leading-tight);
  color: var(--color-text);
}

.avg-price-row { display: flex; align-items: baseline; gap: var(--space-2); }

.price-value {
  font-family: var(--font-num);
  font-size: var(--text-3xl);
  font-weight: var(--weight-bold);
  line-height: 1;
  color: var(--color-text);
  font-variant-numeric: tabular-nums;
  letter-spacing: -0.02em;
}

.price-unit { font-size: var(--text-xs); color: var(--color-text-dim); }

/* ── 價格區間帶 ── */
.range { display: flex; flex-direction: column; gap: var(--space-2); }

.range-track {
  position: relative;
  height: 6px;
  border-radius: var(--radius-full);
  /* 兩端深、中間淡：帶子本身要看得出是「一段範圍」而不是進度條 */
  background: linear-gradient(
    to right,
    var(--color-border-strong),
    var(--color-border),
    var(--color-border-strong)
  );
}

/* 均價的位置。用 --color-brand（品牌綠本體）不是動作色：這是資料標記不是可點的東西。
   外面那圈卡片底色的描邊，是為了讓標記落在帶子兩端時仍分得出來。 */
.range-marker {
  position: absolute;
  top: 50%;
  width: 10px;
  height: 10px;
  margin-inline-start: -5px;
  transform: translateY(-50%);
  border-radius: var(--radius-full);
  background: var(--color-brand);
  border: 2px solid var(--color-surface);
}

.range-labels {
  display: flex;
  justify-content: space-between;
  gap: var(--space-2);
  font-family: var(--font-num);
  font-size: var(--text-2xs);
  color: var(--color-text-dim);
  font-variant-numeric: tabular-nums;
}
.range-mid { color: var(--color-text); font-weight: var(--weight-medium); }
.range-end { white-space: nowrap; }

.card-footer {
  margin-top: auto;
  padding-top: var(--space-3);
  border-top: var(--border-width) solid var(--color-border);
  font-family: var(--font-num);
  font-size: var(--text-2xs);
  color: var(--color-text-dim);
  font-variant-numeric: tabular-nums;
}
</style>
