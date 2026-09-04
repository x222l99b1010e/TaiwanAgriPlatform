<!--
  src/components/ui/PageHeader.vue
  職責：頁面最上方的標題區（標題＋一句話說明＋可選的右側動作）。

  每一頁都要有一句說明自己在做什麼的副標——同一層級的頁面有的解釋、有的不解釋，
  使用者就得自己猜這頁能做什麼。

  標題一律用 h1：一個頁面只該有一個 h1，混用 h1／h2 會讓螢幕閱讀器的標題層級
  在頁與頁之間跳來跳去。

  titleEn 是選填的中英並排：給了才會在中文右邊出現英文標籤，沒給就只有中文。
  同一個中文名稱在全站只該有一種英文譯法，所以定譯集中維護，不要各頁自己翻。
-->
<template>
  <header class="page-header">
    <div class="page-header-text">
      <h1 class="page-title"><Bilingual :zh="title" :en="titleEn" /></h1>
      <!-- 副標允許插槽，因為有幾頁要在說明裡塞動態內容（最新交易日、查詢區間天數） -->
      <p v-if="subtitle || $slots.subtitle" class="page-subtitle">
        <slot name="subtitle">{{ subtitle }}</slot>
      </p>
    </div>

    <div v-if="$slots.actions" class="page-header-actions">
      <slot name="actions" />
    </div>
  </header>
</template>

<script setup lang="ts">
import Bilingual from '@/components/ui/Bilingual.vue'

defineProps<{
  title: string
  subtitle?: string
  titleEn?: string
}>()
</script>

<style scoped>
.page-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-6);
  margin-bottom: var(--space-6);
}

.page-title {
  font-size: var(--text-xl);
  font-weight: var(--weight-bold);
  line-height: var(--leading-tight);
  color: var(--color-text);
}

.page-subtitle {
  margin-top: var(--space-2);
  /* 說明文字限寬：頁面容器有 1400px，一行說明拉滿寬會超過可讀行長 */
  max-width: var(--container-md);
  font-size: var(--text-sm);
  font-weight: var(--weight-normal);
  line-height: var(--leading-normal);
  color: var(--color-text-dim);
}

/* align-items: flex-end 讓「有 label 的控制項（縣市下拉）」與「沒有 label 的按鈕（全台）」
   底部切齊——先前用 center 對齊時，按鈕會浮在下拉的中間高度、跟它對不齊（owner 回報）。 */
.page-header-actions {
  display: flex;
  align-items: flex-end;
  gap: var(--space-3);
  flex-shrink: 0;
}
</style>
