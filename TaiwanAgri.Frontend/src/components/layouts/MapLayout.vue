<!--
  src/components/layouts/MapLayout.vue
  職責：地圖型頁面的外殼——左邊地圖與圖例，右邊清單。

  地圖型頁面的篩選方式跟查詢型不一樣：條件是點在圖上點出來的，不是填表填出來的，
  所以這個樣板沒有查詢按鈕，取而代之的是「現在篩了什麼」那一列（filterSummary）。
  使用者要看得到自己剛剛點了哪個縣市、以及怎麼取消。

  ⚠ 地圖的外框與資料點一定要用同一組投影算出來。外框自己畫得「像台灣」、
  資料點由經緯度投影，兩者一定對不準——北部幾個點會落在海裡。

  版面是「地圖在上、清單在下」的上下疊（owner 2026-09-03：左右排時清單被擠成窄欄，
  一則警報的內文擠成細長一條很難讀）。清單改成滿版之後每張卡片都寬，長內文一行放得下。
  先看得到地圖才知道能點，所以地圖在上。
-->
<template>
  <div class="map-layout">
    <PageHeader :title="title" :subtitle="subtitle" :title-en="titleEn">
      <template v-if="$slots.actions" #actions><slot name="actions" /></template>
      <template v-if="$slots.subtitle" #subtitle><slot name="subtitle" /></template>
    </PageHeader>

    <div v-if="$slots.filterSummary" class="map-layout__summary">
      <slot name="filterSummary" />
    </div>

    <div class="map-layout__cols">
      <section class="map-layout__map" :aria-label="mapLabel">
        <div class="map-layout__canvas"><slot name="map" /></div>
        <div v-if="$slots.legend" class="map-layout__legend"><slot name="legend" /></div>
      </section>

      <section class="map-layout__list" :aria-label="listLabel">
        <slot name="list" />
        <slot name="state" />
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
import PageHeader from '@/components/ui/PageHeader.vue'

withDefaults(
  defineProps<{
    title: string
    subtitle?: string
    titleEn?: string
    mapLabel?: string
    listLabel?: string
  }>(),
  { mapLabel: '地圖', listLabel: '查詢結果' },
)
</script>

<style scoped>
.map-layout__summary {
  margin-bottom: var(--space-4);
}

.map-layout__cols {
  display: flex;
  flex-direction: column;
  gap: var(--space-6);
}

.map-layout__map,
.map-layout__list {
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  overflow: hidden;
}

.map-layout__canvas {
  /* 地圖高度用視窗比例而不是固定 px：固定高度在筆電上會只剩一條縫。
     ⚠ 這裡要用 height 不能只用 min-height——地圖庫（Leaflet 之類）掛載的容器
     一律用 height:100% 撐滿這一層，而百分比高度只認父層的「明確高度」，
     min-height 不算，撐出來的容器高度會是 0，地圖會整片空白。 */
  height: min(56vh, 560px);
}

.map-layout__legend {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-3) var(--space-5);
  padding: var(--space-4) var(--space-5);
  border-top: var(--border-width) solid var(--color-border);
}

.map-layout__list {
  /* 清單滿版、順著頁面往下捲（不再自己開一個內捲區）——上下疊之後地圖在上面，
     不需要把地圖釘住，清單就讓它一路讀下去，長度由分頁控制。 */
  padding: var(--space-5) var(--space-6);
}
</style>
