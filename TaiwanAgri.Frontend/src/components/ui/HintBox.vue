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
/* callout：中性底 ＋ 左側色條 ＋ 該色圖示，不是整塊染色的色塊（owner 回報滿版色塊很醜）。
   ──資訊本身一律用可讀的墨色，顏色只出現在色條、圖示與標題上。

   P3 第二輪（owner 2026-09-04）：原本 warning 只有 3px 細條，「細心才看得到」。
   要更醒目、又不退回色塊，設計上的解法是**加焦點**而不是**加填色**：
   ① 色條加粗（--hint-bar，warning/success 5px）② 圖示改成有底色的圓形徽章
   ③ 標題染成該語氣的深色。三個線索疊起來，一眼就掃到，但底仍是淺色不是重色塊。 */
.hint-box {
  display: flex;
  align-items: flex-start;
  gap: var(--space-3);
  padding: var(--space-4) var(--space-5);
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-inline-start: var(--hint-bar, 3px) solid var(--hint-accent);
  border-radius: var(--radius-md);
  font-size: var(--text-sm);
  line-height: var(--leading-normal);
  color: var(--color-text);
}

/* 圖示跟著第一行文字的行高對齊。用 align-items: center 的話，
   多行提示的圖示會掉到整段的中間高度，看起來像浮在旁邊 */
.hint-box-icon {
  font-size: var(--text-xl);
  line-height: var(--leading-normal);
  color: var(--hint-accent);
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
  color: var(--hint-title, var(--color-text));
}

/* info 維持安靜：它是中性說明，不該跟 warning 一樣搶眼——只換色條與圖示顏色 */
.hint-box--info { --hint-accent: var(--info-500); }

/* success／warning 是「要被看到」的語氣：色條加粗、底邊框染成同色系、
   圖示變成圓形徽章、標題染深色。 */
.hint-box--success {
  --hint-accent: var(--color-brand);
  --hint-bar: 5px;
  --hint-title: var(--color-action-hover);
  --hint-badge-bg: var(--seed-100);
  background: var(--seed-50);
  border-color: var(--seed-200);
}
.hint-box--warning {
  --hint-accent: var(--warning-500);
  --hint-bar: 5px;
  --hint-title: var(--warning-700);
  --hint-badge-bg: var(--warning-100);
  background: var(--warning-50);
  border-color: var(--warning-100);
}

/* 圓形圖示徽章：只在 success／warning 出現。把細長的字型圖示換成一塊有底色的圓，
   眼睛先被這個「有顏色的點」抓到，再讀旁邊的字——這是 callout 常見的視線引導。
   info 沒有這一條，維持純圖示。 */
.hint-box--success .hint-box-icon,
.hint-box--warning .hint-box-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 30px;
  height: 30px;
  border-radius: var(--radius-full);
  background: var(--hint-badge-bg);
  font-size: var(--text-lg);
}
</style>
