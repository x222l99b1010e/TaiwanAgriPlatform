<!--
  src/components/ui/StateBlock.vue
  職責：資料還沒到、沒有資料、查詢失敗、還沒開始查——這四種「畫面上沒有內容可看」
        的狀態，一律用同一個區塊呈現。

  盤點記錄的現況是空狀態三種做法（卡片／純文字／完全空白）、載入中三種做法
  （spinner／純文字「載入中...」／沒有提示）、錯誤兩種做法（卡片＋重試鈕／
  塞在篩選列裡的一行紅字）。這不只是外觀不一致：「完全空白」與「一行灰字」在
  使用者眼裡跟「壞掉了」很難分辨，因為畫面沒有任何東西說明現在是什麼情況。

  四種狀態共用同一個容器與同一組間距，差別只在圖示、顏色與有沒有重試按鈕。
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

const props = withDefaults(
  defineProps<{
    /**
     * loading＝請求進行中；empty＝查詢完成但沒有資料；
     * error＝請求失敗；hint＝還沒開始查詢，等待使用者輸入條件。
     * empty 與 hint 分開是因為兩者要說的話不同：一個是「查過了，沒有」，
     * 另一個是「還沒查」，先前有頁面把兩者都做成空白，使用者分不出差別。
     */
    state: 'loading' | 'empty' | 'error' | 'hint'
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

const defaultIcon = computed(() => {
  switch (props.state) {
    case 'error':
      return 'mdi-alert-circle'
    case 'hint':
      return 'mdi-magnify'
    default:
      return 'mdi-database-off-outline'
  }
})
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
