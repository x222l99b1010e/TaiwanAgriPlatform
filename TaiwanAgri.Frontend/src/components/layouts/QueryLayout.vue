<!--
  src/components/layouts/QueryLayout.vue
  職責：查詢型頁面的外殼——標題、查詢條件、結果、分頁。全站數量最多的一種頁。

  為什麼要有這一層：28 條路由裡有一半以上是「填條件 → 按查詢 → 看結果」，
  之前每一頁都自己排一次，於是同一件事在不同頁的按鈕位置、間距、空狀態都不一樣。
  頁面之間真正不同的是「有哪些條件」與「結果長什麼樣」，那兩塊留給插槽。

  ⚠ 動作按鈕放在查詢條件的「頂部右側」而不是底部，這是刻意的：
  往上、往外推是伸展，體感輕鬆；往下拉回身體是收縮，體感卡。
  按鈕列同時是 sticky，捲到結果中段還按得到，不必捲回頁首。

  結果插槽沒有內容時請改放 state 插槽（StateBlock），不要自己在頁面裡寫一段
  「查無資料」——那是同一件事在每頁長得不一樣的來源。
-->
<template>
  <div class="query-layout">
    <PageHeader :title="title" :subtitle="subtitle" :title-en="titleEn">
      <template v-if="$slots.subtitle" #subtitle><slot name="subtitle" /></template>
    </PageHeader>

    <!-- sticky 沒辦法只用 CSS 判斷「現在有沒有黏住」，
         所以在黏住的位置上方放一個哨兵，看它有沒有離開視窗 -->
    <div ref="sentinel" class="query-layout__sentinel" aria-hidden="true" />

    <section class="query-filters" :class="{ 'is-stuck': stuck }">
      <div class="query-filters__bar">
        <Bilingual :zh="filterLabel" :en="filterLabelEn" class="query-filters__label" />
        <div v-if="$slots.actions" class="query-filters__actions">
          <slot name="actions" />
        </div>
      </div>
      <div v-if="$slots.filters" class="query-filters__body">
        <slot name="filters" />
      </div>
    </section>

    <div v-if="$slots.hint" class="query-layout__hint">
      <slot name="hint" />
    </div>

    <div class="query-layout__results">
      <slot name="results" />
      <slot name="state" />
    </div>

    <div v-if="$slots.pager" class="query-layout__pager">
      <slot name="pager" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, useTemplateRef } from 'vue'
import PageHeader from '@/components/ui/PageHeader.vue'
import Bilingual from '@/components/ui/Bilingual.vue'

withDefaults(
  defineProps<{
    title: string
    subtitle?: string
    /** 中英並排的英文。沒給就只顯示中文 */
    titleEn?: string
    filterLabel?: string
    filterLabelEn?: string
  }>(),
  { filterLabel: '查詢條件', filterLabelEn: 'Filters' },
)

const sentinel = useTemplateRef<HTMLElement>('sentinel')
const stuck = ref(false)
let observer: IntersectionObserver | undefined

onMounted(() => {
  // 伺服器端算圖與測試環境沒有這個 API，沒有它時就一直是未黏住的樣子
  if (!sentinel.value || typeof IntersectionObserver === 'undefined') return
  observer = new IntersectionObserver((entries) => {
    const entry = entries.at(-1)
    if (entry) stuck.value = !entry.isIntersecting
  })
  observer.observe(sentinel.value)
})

onBeforeUnmount(() => observer?.disconnect())
</script>

<style scoped>
.query-layout__sentinel {
  height: 1px;
}

.query-filters {
  position: sticky;
  top: 0;
  z-index: var(--z-sticky);
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--space-4) var(--space-6) var(--space-5);
  margin-bottom: var(--space-6);
  transition: box-shadow var(--duration-base) var(--ease-work);
}

/* 只有真的浮起來時才給陰影：卡片平貼在頁面上時靠邊框與底色差就夠了 */
.query-filters.is-stuck {
  box-shadow: var(--shadow-float);
}

.query-filters__bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-4);
  flex-wrap: wrap;
}

.query-filters__label {
  font-size: var(--text-sm);
  font-weight: var(--weight-medium);
  color: var(--color-text-dim);
}

.query-filters__actions {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  margin-left: auto;
}

.query-filters__body {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: var(--space-4) var(--space-5);
  margin-top: var(--space-4);
}

.query-layout__hint {
  margin-bottom: var(--space-6);
}

.query-layout__pager {
  margin-top: var(--space-6);
}
</style>
