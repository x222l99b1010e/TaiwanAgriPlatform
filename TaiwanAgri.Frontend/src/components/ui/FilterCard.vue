<!--
  src/components/ui/FilterCard.vue
  職責：查詢條件區的外殼——白底卡片、邊框、圓角、陰影、內距。

  只統一外殼，內部排列仍由各頁自己決定：各頁的查詢條件本來就不一樣，
  為了共用而把不同需求壓成同一種版面，得到的會是每一頁都不好用。

  layout 提供兩種常見排法，涵蓋現況絕大多數頁面：
  - row（預設）：一排控制項橫向排列、底部對齊，換行時自動掉到下一列。
  - stack：多列結構（例如上排篩選、下排日期與動作）自己排，元件只給垂直間距。
-->
<template>
  <section :class="['filter-card', `filter-card--${layout}`]">
    <slot />
  </section>
</template>

<script setup lang="ts">
withDefaults(
  defineProps<{
    layout?: 'row' | 'stack'
  }>(),
  { layout: 'row' },
)
</script>

<style scoped>
.filter-card {
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  /* 卡片不給陰影：style tile §三「卡片邊界只用 1px 邊框＋底色差」，
     陰影降級成只給真的浮在頁面上方的東西（下拉、對話框、吸頂後的工具列）。
     暖米白底與卡片面的明度差本來就足以讓卡片浮起來，再加陰影只會糊掉邊界。 */
  padding: var(--space-5) var(--space-6);
  margin-bottom: var(--space-6);
}

/* align-items: flex-end 讓「有 label 的欄位」與「沒有 label 的按鈕」底部切齊——
   先前各頁用 center 對齊時，按鈕會浮在欄位的中間高度 */
.filter-card--row {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: var(--space-4) var(--space-5);
}

.filter-card--stack {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}
</style>
