<!--
  src/components/layouts/QueryLayout.vue
  職責：查詢型頁面的外殼——標題、查詢條件、結果、分頁。全站數量最多的一種頁。

  為什麼要有這一層：28 條路由裡有一半以上是「填條件 → 按查詢 → 看結果」，
  之前每一頁都自己排一次，於是同一件事在不同頁的按鈕位置、間距、空狀態都不一樣。
  頁面之間真正不同的是「有哪些條件」與「結果長什麼樣」，那兩塊留給插槽。

  ⚠ 動作按鈕（查詢）排在查詢條件的「最後一格、跟日期同一列」，不是頂部也不是底部：
  填完條件手就停在那附近，按鈕就在旁邊，不必把視線移回卡片頂端
  （選完日期，查詢鈕就在旁邊比較直覺）。這推翻了原本
  「動作放頂部右側＋吸頂工具列」的設計——實機上那個吸頂的篩選卡太高、
  捲動時會整片蓋住下面的結果，改成不吸頂、按鈕落在條件列尾端。

  結果插槽沒有內容時請改放 state 插槽（StateBlock），不要自己在頁面裡寫一段
  「查無資料」——那是同一件事在每頁長得不一樣的來源。
-->
<template>
  <div class="query-layout">
    <PageHeader :title="title" :subtitle="subtitle" :title-en="titleEn">
      <template v-if="$slots.subtitle" #subtitle><slot name="subtitle" /></template>
    </PageHeader>

    <section class="query-filters">
      <div class="query-filters__bar">
        <Bilingual :zh="filterLabel" :en="filterLabelEn" class="query-filters__label" />
      </div>
      <div class="query-filters__body">
        <slot name="filters" />
        <div v-if="$slots.actions" class="query-filters__actions">
          <slot name="actions" />
        </div>
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
</script>

<style scoped>
.query-filters {
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--space-4) var(--space-6) var(--space-5);
  margin-bottom: var(--space-6);
}

.query-filters__bar {
  margin-bottom: var(--space-4);
}

.query-filters__label {
  font-size: var(--text-sm);
  font-weight: var(--weight-medium);
  color: var(--color-text-dim);
}

.query-filters__body {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: var(--space-4) var(--space-5);
}

/* 動作按鈕落在條件列的尾端。margin-inline-start: auto 讓它在還有空間時被推到該列
   最右側、跟欄位之間留白；欄位多到換行時它就掉到下一列的最右，仍然靠近最後一格。 */
.query-filters__actions {
  display: flex;
  align-items: flex-end;
  gap: var(--space-3);
  margin-inline-start: auto;
}

.query-layout__hint {
  margin-bottom: var(--space-6);
}

.query-layout__pager {
  margin-top: var(--space-6);
}
</style>
