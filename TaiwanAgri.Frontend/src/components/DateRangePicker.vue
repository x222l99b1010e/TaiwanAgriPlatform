<template>
  <div class="date-range-picker">
    <div class="date-group">
      <label>開始日期</label>
      <input type="date" :value="startDate" :max="endDate || today"
        @change="emit('update:startDate', ($event.target as HTMLInputElement).value)" />
    </div>
    <span class="separator">～</span>
    <div class="date-group">
      <label>結束日期</label>
      <input type="date" :value="endDate" :min="startDate" :max="today"
        @change="emit('update:endDate', ($event.target as HTMLInputElement).value)" />
    </div>
    <div class="shortcuts">
      <button
        v-for="s in shortcuts"
        :key="s.label"
        class="shortcut-btn"
        :class="{ active: activeShortcut === s.days }"
        @click="applyShortcut(s.days)"
      >
        {{ s.label }}
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'

withDefaults(defineProps<{ startDate?: string; endDate?: string }>(), {
  startDate: '', endDate: '',
})
const emit = defineEmits<{
  'update:startDate': [value: string]
  'update:endDate': [value: string]
}>()

const today = computed(() => new Date().toISOString().split('T')[0])
const shortcuts = [
  { label: '近 30 天', days: 30 },
  { label: '近 90 天', days: 90 },
  { label: '近 180 天', days: 180 },
  { label: '近 365 天', days: 365 },
]

const activeShortcut = ref<number | null>(null)

function applyShortcut(days: number) {
  activeShortcut.value = days
  const end = new Date()
  const start = new Date()
  start.setDate(end.getDate() - days)
  const fmt = (d: Date) => d.toISOString().split('T')[0] ?? ''
  emit('update:endDate', fmt(end))
  emit('update:startDate', fmt(start))
}
</script>

<style scoped>
.date-range-picker {
  display: flex; align-items: flex-end; gap: var(--space-3); flex-wrap: wrap;
}

.date-group { display: flex; flex-direction: column; gap: var(--space-1); }

.date-group label {
  font-size: var(--text-xs); font-weight: var(--weight-medium);
  color: var(--color-text-dim);
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

/* 高度吃 --control-h、垂直 padding 為 0：這個輸入框幾乎永遠跟一顆查詢按鈕排在
   同一列，兩者高度必須由同一個 token 決定。先前是 padding 撐出來的 37.2px 對上
   按鈕的 41.4px，底部對齊了還是差 4px。
   日期是數字，用 --font-num 才會等寬，換日期時欄位寬度不會抖。 */
.date-group input[type="date"] {
  min-height: var(--control-h);
  padding: 0 var(--space-3);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  color: var(--color-text);
  font-family: var(--font-num);
  font-size: var(--text-sm);
  font-variant-numeric: tabular-nums;
  width: 152px;
  transition:
    border-color var(--duration-fast) var(--ease-work),
    box-shadow var(--duration-fast) var(--ease-work);
}
.date-group input[type="date"]:hover { border-color: var(--color-border-strong); }
.date-group input[type="date"]:focus {
  outline: none;
  border-color: var(--color-action);
  box-shadow: var(--shadow-focus);
}

.separator {
  font-size: var(--text-lg); color: var(--color-text-dim);
  /* 有 label 的欄位比沒 label 的高一個 label 高度，波浪號要往下對到輸入框的中線 */
  padding-bottom: var(--space-2);
}

.shortcuts { display: flex; gap: var(--space-2); flex-wrap: wrap; }

/* 快捷鍵是 chip 不是動作按鈕，所以維持藥丸形。
   全站的分工：**方角（--radius-md）＝按下去會做事的動作按鈕，
   藥丸（--radius-full）＝切換條件的 chip**——形狀本身就在說明它是哪一種東西。 */
.shortcut-btn {
  min-height: var(--control-h-sm);
  padding: 0 var(--space-4);
  border-radius: var(--radius-full);
  border: var(--border-width) solid var(--color-border);
  background: var(--color-surface);
  color: var(--color-text-dim);
  font-family: inherit;
  font-size: var(--text-xs);
  cursor: pointer;
  transition:
    border-color var(--duration-fast) var(--ease-work),
    color var(--duration-fast) var(--ease-work),
    background var(--duration-fast) var(--ease-work);
}
.shortcut-btn:hover {
  border-color: var(--color-action);
  color: var(--color-action);
  background: var(--color-action-soft);
}
.shortcut-btn:focus-visible {
  outline: none;
  border-color: var(--color-action);
  box-shadow: var(--shadow-focus);
}

.shortcut-btn.active {
  border-color: var(--color-action);
  color: var(--color-action);
  background: var(--color-action-soft-2);
  font-weight: var(--weight-medium);
}
</style>