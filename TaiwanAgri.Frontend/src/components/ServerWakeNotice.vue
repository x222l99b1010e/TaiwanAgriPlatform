<!--
  src/components/ServerWakeNotice.vue
  職責：有請求等太久時，在畫面下方說明現在卡在哪一段（伺服器喚醒中／正在喚醒資料庫／仍在查詢中）。
        判斷邏輯在 api/serverWake.ts，這裡只負責呈現。

  掛在 App.vue 而不是各頁：等待發生在任何一頁的任何一個請求上，說明只需要一份。
  「已等待 N 秒」每秒更新，所以放在 aria-live 區塊之外——放在裡面的話，
  螢幕報讀軟體每秒都會念一次。
-->
<template>
  <Transition name="wake">
    <div v-if="message" class="wake-notice">
      <div class="wake-spinner" aria-hidden="true" />
      <div class="wake-body">
        <p class="wake-title" role="status" aria-live="polite">{{ message.title }}</p>
        <p class="wake-hint">
          {{ message.hint }}<span aria-hidden="true">・已等待 {{ serverWake.state.waitedSeconds }} 秒</span>
        </p>
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { describeWake, serverWake } from '@/api/serverWake'

const message = computed(() =>
  serverWake.state.phase === 'idle' ? null : describeWake(serverWake.state.phase),
)
</script>

<style scoped>
/* 浮在頁面上方的一層，所以可以用 --shadow-float（base.css 只准浮動層用陰影）。
   置中貼底：不擋導覽列，也不蓋住各頁最上方的查詢條件。 */
.wake-notice {
  position: fixed;
  left: 50%;
  bottom: var(--space-6);
  z-index: var(--z-toast);
  transform: translateX(-50%);
  display: flex;
  align-items: center;
  gap: var(--space-3);
  width: max-content;
  max-width: calc(100vw - 2 * var(--space-4));
  padding: var(--space-3) var(--space-5);
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-float);
  font-size: var(--text-sm);
  line-height: var(--leading-tight);
  color: var(--color-text);
}

/* 跟 StateBlock 的載入轉圈同一個樣式，縮成這一行的高度 */
.wake-spinner {
  flex-shrink: 0;
  width: 20px;
  height: 20px;
  border: 2px solid var(--seed-200);
  border-top-color: var(--color-action);
  border-radius: var(--radius-full);
  animation: wake-spin 0.8s linear infinite;
}

.wake-body {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  min-width: 0;
}

.wake-title {
  font-weight: var(--weight-bold);
}

.wake-hint {
  color: var(--color-text-dim);
  font-variant-numeric: tabular-nums;
}

@keyframes wake-spin { to { transform: rotate(360deg); } }

.wake-enter-active,
.wake-leave-active {
  transition: opacity var(--duration-base) var(--ease-work), transform var(--duration-base) var(--ease-work);
}
.wake-enter-from,
.wake-leave-to {
  opacity: 0;
  transform: translate(-50%, var(--space-2));
}
</style>
