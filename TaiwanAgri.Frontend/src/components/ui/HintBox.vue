<!--
  src/components/ui/HintBox.vue
  職責：說明用的提示框——淺色底＋同色系邊框＋深色文字，用來放「查詢說明」
        「線段中斷代表什麼」這類對使用者解釋現況的文字。

  收斂範圍刻意只到「提示框」為止：顏色剛好一樣的行內 chip／badge 不收進來。
  它們共用顏色但不共用結構（chip 有自己的選取態與排列邏輯），
  包進來會讓這個元件同時背兩種職責。

  tone 直接對應語意色的 50／100／500 三階，所以「同樣是提示」在任何一頁都長一樣；
  要換提示框的長相只改這個檔。
-->
<template>
  <div :class="['hint-box', `hint-box--${tone}`]">
    <span :class="['mdi', icon ?? defaultIcon, 'hint-box-icon']" />
    <div class="hint-box-body">
      <p v-if="title" class="hint-box-title">{{ title }}</p>
      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

type Tone = 'info' | 'success' | 'warning'

/* 用 Record<Tone, string> 而不是 switch：日後 Tone 多一階時，這個物件會因為少一個
   key 直接編譯失敗。switch 要達到同樣效果得刻意不寫 default，但那又會被
   eslint 的 vue/return-in-computed-property 擋下。 */
const DEFAULT_ICON: Record<Tone, string> = {
  info: 'mdi-information-outline',
  success: 'mdi-check-circle-outline',
  warning: 'mdi-alert-outline',
}

const props = withDefaults(
  defineProps<{
    /** info＝中性說明；success＝已完成的好消息；warning＝要留意但不是錯誤 */
    tone?: Tone
    /** 有標題時是「說明區塊」（標題＋條列），沒有時是一句話的行內提示 */
    title?: string
    /** 覆寫預設圖示（MDI class 名稱） */
    icon?: string
  }>(),
  { tone: 'info', title: undefined, icon: undefined },
)

const defaultIcon = computed(() => DEFAULT_ICON[props.tone])
</script>

<style scoped>
.hint-box {
  display: flex;
  align-items: flex-start;
  gap: var(--space-2);
  padding: var(--space-3) var(--space-4);
  border: var(--border-width) solid;
  border-radius: var(--radius-md);
  font-size: var(--text-sm);
  font-weight: var(--weight-medium);
  line-height: var(--leading-normal);
}

/* 圖示跟著第一行文字的行高對齊。用 align-items: center 的話，
   多行提示的圖示會掉到整段的中間高度，看起來像浮在旁邊 */
.hint-box-icon {
  font-size: var(--text-lg);
  line-height: var(--leading-normal);
  flex-shrink: 0;
}

.hint-box-body {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  min-width: 0;
}

.hint-box-title {
  font-weight: var(--weight-bold);
}

.hint-box--info {
  background: var(--info-50);
  border-color: var(--info-100);
  color: var(--info-500);
}

.hint-box--success {
  background: var(--seed-100);
  border-color: var(--seed-200);
  color: var(--color-action);
}

.hint-box--warning {
  background: var(--warning-50);
  border-color: var(--warning-100);
  color: var(--warning-700);
}
</style>
