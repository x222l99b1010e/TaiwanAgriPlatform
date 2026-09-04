<!--
  src/components/ui/Bilingual.vue
  職責：中英並排的標題。中文是主體，英文是裝飾。

  兩種排法：
  - inline（預設）：同一行，英文在右。用在導覽列模組名、頁面標題、區塊小標。
  - stacked：英文在中文正下方，字級是中文的 0.42 倍。用在深色頁首帶與首頁 hero。

  英文一律 aria-hidden：它不承載中文沒有的資訊，讓螢幕閱讀器唸兩次只是干擾。
  inline 版在 640px 以下整個收掉——並排在窄螢幕會把中文擠掉，而中文才是主體。
  英文不換行，翻譯太長就換一個短的，不要讓它折行。
-->
<template>
  <span :class="['bilingual', `bilingual--${layout}`, { 'bilingual--deep': tone === 'deep' }]">
    <span class="bilingual__zh">{{ zh }}</span>
    <span v-if="en" class="bilingual__en" aria-hidden="true">{{ en }}</span>
  </span>
</template>

<script setup lang="ts">
withDefaults(
  defineProps<{
    zh: string
    en?: string
    layout?: 'inline' | 'stacked'
    /** deep＝這段字壓在深色底上，換一組對深色底夠亮的次要文字色 */
    tone?: 'light' | 'deep'
  }>(),
  { layout: 'inline', tone: 'light' },
)
</script>

<style scoped>
.bilingual--inline {
  display: inline-flex;
  align-items: baseline;
  gap: var(--space-3);
}

.bilingual--stacked {
  display: block;
}

.bilingual__en {
  font-family: var(--font-num);
  color: var(--color-text-dim);
  white-space: nowrap;
}

.bilingual--deep .bilingual__en {
  color: var(--color-on-deep-dim);
}

/* inline 的英文是固定 12px 的標籤，不隨中文字級縮放——
   它在不同字級的標題旁邊要看起來是同一種東西 */
.bilingual--inline .bilingual__en {
  font-size: 12px;
  font-weight: 600;
  letter-spacing: var(--tracking-label);
  text-transform: uppercase;
}

/* stacked 的英文是副標，跟著中文字級走 */
.bilingual--stacked .bilingual__en {
  display: block;
  margin-top: var(--space-3);
  font-size: 0.42em;
  font-weight: var(--weight-normal);
  line-height: var(--leading-tight);
}

@media (max-width: 640px) {
  .bilingual--inline .bilingual__en {
    display: none;
  }
}
</style>
