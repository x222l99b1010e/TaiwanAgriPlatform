<!--
  src/components/ui/StateBlock.vue
  職責：資料還沒到、沒有資料、查詢失敗、還沒開始查——這四種「畫面上沒有內容可看」
        的狀態，一律用同一個區塊呈現。

  四種狀態共用同一個容器與同一組間距，差別只在圖示、顏色與有沒有重試按鈕。
  一定要有東西說明現在是什麼情況——「完全空白」或「一行灰字」在使用者眼裡
  跟「壞掉了」分不出來。
  預設圖示依狀態給，頁面要換成更貼切的（例如「今日休市」用日曆圖示）再自己傳。
-->
<template>
  <div :class="['state-block', `state-block--${state}`]" role="status">
    <div v-if="state === 'loading'" class="state-spinner" />
    <span v-else :class="['mdi', icon ?? defaultIcon, 'state-icon']" />

    <p class="state-text">
      <slot>{{ message }}</slot>
    </p>

    <p v-if="hint" class="state-hint">{{ hint }}</p>

    <Btn v-if="retryable" variant="secondary" size="sm" icon="mdi-refresh" @click="$emit('retry')">
      {{ retryLabel }}
    </Btn>

    <slot name="actions" />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import Btn from './Btn.vue'

type State = 'loading' | 'empty' | 'error' | 'hint'

/* 四種狀態各自的預設圖示。用 Record<State, string> 是為了讓「日後多一種狀態」
   直接編譯失敗——先前寫成 switch＋default，新狀態會安靜地掉進 default 拿到
   空資料的圖示，而 lint、測試、build 都不會有任何反應。
   （loading 在 template 就被 v-if 換成轉圈，取不到這裡的值，但仍要列，
   否則型別檢查沒辦法幫忙擋。） */
const DEFAULT_ICON: Record<State, string> = {
  loading: 'mdi-loading',
  empty: 'mdi-database-off-outline',
  error: 'mdi-alert-circle',
  hint: 'mdi-magnify',
}

const props = withDefaults(
  defineProps<{
    /**
     * loading＝請求進行中；empty＝查詢完成但沒有資料；
     * error＝請求失敗；hint＝還沒開始查詢，等待使用者輸入條件。
     * empty 與 hint 分開是因為兩者要說的話不同：一個是「查過了，沒有」，
     * 另一個是「還沒查」，先前有頁面把兩者都做成空白，使用者分不出差別。
     */
    state: State
    message?: string
    /** 補充說明，例如告訴使用者可以怎麼調整條件 */
    hint?: string
    /** 覆寫預設圖示（MDI class 名稱） */
    icon?: string
    retryable?: boolean
    retryLabel?: string
  }>(),
  {
    message: undefined,
    hint: undefined,
    icon: undefined,
    retryable: false,
    retryLabel: '重試',
  },
)

defineEmits<{ retry: [] }>()

const defaultIcon = computed(() => DEFAULT_ICON[props.state])
</script>

<style scoped>
.state-block {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-16) var(--space-8);
  background: var(--surface);
  border: var(--border-width) solid var(--border);
  border-radius: var(--radius-lg);
  text-align: center;
  /* 狀態切換多半是瞬間發生的，直接出現會像閃了一下；淡入加一點點上浮讓它
     看起來是「這個區塊剛長出來」。減少動態偏好由 base.css 全域關掉 */
  animation: state-in var(--duration-base) var(--ease-out);
}

@keyframes state-in {
  from { opacity: 0; transform: translateY(6px); }
  to   { opacity: 1; transform: none; }
}

.state-icon {
  font-size: var(--text-3xl);
  color: var(--neutral-400);
}

.state-text {
  font-size: var(--text-base);
  font-weight: var(--weight-normal);
  color: var(--text-secondary);
}

.state-hint {
  font-size: var(--text-sm);
  color: var(--text-muted);
}

/* ── 錯誤：整塊換色，不只是把文字改紅 ── */
.state-block--error {
  background: var(--danger-50);
  border-color: var(--danger-100);
}
.state-block--error .state-icon { color: var(--danger-500); }
.state-block--error .state-text { color: var(--danger-700); }

/* ── 提示（還沒開始查詢）：比空狀態再淡一階，避免看起來像出了問題 ── */
.state-block--hint { background: var(--surface-2); }
.state-block--hint .state-icon { color: var(--green-400); }

.state-spinner {
  width: 36px;
  height: 36px;
  border: 3px solid var(--green-200);
  border-top-color: var(--green-600);
  border-radius: var(--radius-full);
  animation: state-spin 0.8s linear infinite;
}

@keyframes state-spin { to { transform: rotate(360deg); } }
</style>
