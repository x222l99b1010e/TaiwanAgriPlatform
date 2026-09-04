<!--
  src/components/layouts/DetailLayout.vue
  職責：詳情型頁面的外殼——返回、標題、摘要、分區內容。

  詳情頁跟查詢頁的差別在「使用者是從哪裡來的」：他一定是從某個清單點進來的，
  所以第一件要給的東西是回去的路，不是標題。返回列固定在標題上方，
  不放在頁尾——看完內容才發現不知道怎麼回去，就得按瀏覽器的上一頁。

  內文限寬用 --container-sm：詳情頁是拿來讀的，一行拉到 1400px 會讀不動。
  需要整寬的東西（地圖、圖表、圖片牆）放 wide 插槽，那一塊不限寬。
-->
<template>
  <article class="detail-layout">
    <nav class="detail-layout__back">
      <RouterLink :to="backTo" class="detail-layout__back-link">
        <span class="mdi mdi-arrow-left" aria-hidden="true" />
        {{ backLabel }}
      </RouterLink>
    </nav>

    <PageHeader :title="title" :subtitle="subtitle" :title-en="titleEn">
      <template v-if="$slots.actions" #actions><slot name="actions" /></template>
      <template v-if="$slots.subtitle" #subtitle><slot name="subtitle" /></template>
    </PageHeader>

    <div v-if="$slots.summary" class="detail-layout__summary">
      <slot name="summary" />
    </div>

    <div v-if="$slots.wide" class="detail-layout__wide">
      <slot name="wide" />
    </div>

    <div class="detail-layout__body">
      <slot />
    </div>
  </article>
</template>

<script setup lang="ts">
import { RouterLink, type RouteLocationRaw } from 'vue-router'
import PageHeader from '@/components/ui/PageHeader.vue'

withDefaults(
  defineProps<{
    title: string
    subtitle?: string
    titleEn?: string
    /** 從哪個清單來的就回哪裡去 */
    backTo: RouteLocationRaw
    backLabel?: string
  }>(),
  { backLabel: '返回列表' },
)
</script>

<style scoped>
.detail-layout__back {
  margin-bottom: var(--space-4);
}

.detail-layout__back-link {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  font-size: var(--text-sm);
  color: var(--color-action);
  text-decoration: none;
  transition:
    color var(--duration-fast) var(--ease-work),
    transform var(--duration-fast) var(--ease-work);
}

/* 返回是往左，所以 hover 也往左移——方向跟動作一致，手感才對得上 */
.detail-layout__back-link:hover {
  color: var(--color-action-hover);
  transform: translateX(calc(-1 * var(--space-1)));
}

.detail-layout__summary {
  margin-bottom: var(--space-8);
}

.detail-layout__wide {
  margin-bottom: var(--space-8);
}

.detail-layout__body {
  max-width: var(--container-sm);
  display: flex;
  flex-direction: column;
  gap: var(--space-8);
}
</style>
