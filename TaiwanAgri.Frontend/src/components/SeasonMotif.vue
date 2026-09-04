<!--
  src/components/SeasonMotif.vue
  職責：深色區背景的線稿母題，隨節氣季節分組換圖。

  四組母題只換幾何（SVG 的 path/shape），顏色一律 var(--color-motif)，
  不動任何 token——這是「節氣只進內容層與母題層，不進 token 層」定案的具體落地。

  辨識度來自「單元的形狀比例」不是輪廓（原型踩過的坑：第一版麥穗畫出來像聖誕樹，
  因為穀粒用了 26px 寬的兩條弧線）。垂穗＝兩排 rx≈3.6/ry≈9 的細長橢圓，
  旋轉 ±26°，越往上越小——這裡的四組圖都照同一種「細長橢圓／細線」的畫法，
  不用寫實輪廓，母題才會維持同一種「線稿」語言。

  只在深色區使用，內頁一律沒有；套用時記得 aria-hidden，這是裝飾不是資訊。
-->
<template>
  <svg class="season-motif" viewBox="0 0 800 400" preserveAspectRatio="xMidYMid slice" aria-hidden="true">
    <g v-if="season === 'autumn'">
      <!-- 垂穗：穗直立的莖，頂端兩排細長橢圓的穀粒往下垂、越往上越小 -->
      <g v-for="(x, i) in STALK_X" :key="`ear-${i}`" :transform="`translate(${x}, ${STALK_Y[i]!}) scale(${STALK_SCALE[i]!})`">
        <path d="M0,140 Q-4,70 0,0" fill="none" stroke="var(--color-motif)" stroke-width="1" />
        <g v-for="(gy, gi) in GRAIN_Y" :key="gi">
          <ellipse :cx="-6" :cy="gy" rx="3.6" ry="9" :transform="`rotate(-26 -6 ${gy})`" fill="var(--color-motif)" />
          <ellipse :cx="6"  :cy="gy" rx="3.6" ry="9" :transform="`rotate(26 6 ${gy})`"   fill="var(--color-motif)" />
        </g>
      </g>
    </g>

    <g v-else-if="season === 'summer'">
      <!-- 抽穗：穗剛長出、還沒被穀粒的重量壓彎，穀粒貼著莖往外撐開而不是下垂 -->
      <g v-for="(x, i) in STALK_X" :key="`spike-${i}`" :transform="`translate(${x}, ${STALK_Y[i]!}) scale(${STALK_SCALE[i]!})`">
        <path d="M0,140 L0,0" fill="none" stroke="var(--color-motif)" stroke-width="1" />
        <g v-for="(gy, gi) in GRAIN_Y" :key="gi">
          <ellipse :cx="-5" :cy="gy" rx="3" ry="8" :transform="`rotate(-14 -5 ${gy})`" fill="var(--color-motif)" />
          <ellipse :cx="5"  :cy="gy" rx="3" ry="8" :transform="`rotate(14 5 ${gy})`"   fill="var(--color-motif)" />
        </g>
      </g>
    </g>

    <g v-else-if="season === 'spring'">
      <!-- 秧苗：細直葉，往上，還沒抽穗——只有莖，沒有穀粒叢 -->
      <g v-for="(x, i) in STALK_X" :key="`seedling-${i}`" :transform="`translate(${x}, ${STALK_Y[i]! + 60}) scale(${STALK_SCALE[i]!})`">
        <path d="M0,80 Q-10,30 -3,0"  fill="none" stroke="var(--color-motif)" stroke-width="1" />
        <path d="M0,80 L0,-6"          fill="none" stroke="var(--color-motif)" stroke-width="1" />
        <path d="M0,80 Q10,34 4,-2"   fill="none" stroke="var(--color-motif)" stroke-width="1" />
      </g>
    </g>

    <g v-else>
      <!-- 冬：田壟／等高線，收割後的田——沒有莖，換成水平的等高線 -->
      <path
        v-for="(row, i) in WINTER_ROWS"
        :key="i"
        :d="row"
        fill="none"
        stroke="var(--color-motif)"
        stroke-width="1"
      />
    </g>
  </svg>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { Season } from '@/utils/solarTerms'

defineProps<{ season: Season }>()

// 六株分布在 800 寬的畫布上，位置與縮放故意不規則，才不會看起來像貼上去的花紋磚
const STALK_X     = [40, 190, 330, 480, 620, 750]
const STALK_Y     = [230, 180, 260, 200, 240, 190]
const STALK_SCALE = [0.9, 1.1, 0.85, 1.15, 0.95, 1.05]
// 每株 5 排穀粒，由下往上：ry 遞減＝越往上越小（越上越小）
const GRAIN_Y = [0, -16, -30, -42, -52]

const WINTER_ROWS = computed(() => {
  // 五條略帶弧度的水平線，模擬收割後田壟的等高線，垂直間距不等寬避免規律感
  const ys = [70, 140, 205, 265, 320]
  return ys.map((y, i) => {
    const bow = 14 + (i % 3) * 6
    return `M0,${y} Q400,${y - bow} 800,${y}`
  })
})
</script>

<style scoped>
.season-motif {
  width: 100%;
  height: 100%;
  display: block;
}
</style>
