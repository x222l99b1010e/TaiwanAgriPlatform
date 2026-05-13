<!-- src/components/DateRangePicker.vue -->
<!-- 職責：日期範圍選擇器，可重複使用於 GetPrices / GetDisasters / GetRestDays -->
<!-- 父元件透過 v-model:startDate / v-model:endDate 取得選擇的值 -->

<template>
  <div class="date-range-picker">

    <div class="date-group">
      <label>開始日期</label>
      <input
        type="date"
        :value="startDate"
        :max="endDate || today"
        @change="emit('update:startDate', ($event.target as HTMLInputElement).value)"
      />
    </div>

    <span class="separator">～</span>

    <div class="date-group">
      <label>結束日期</label>
      <input
        type="date"
        :value="endDate"
        :min="startDate"
        :max="today"
        @change="emit('update:endDate', ($event.target as HTMLInputElement).value)"
      />
    </div>

    <!-- 快捷選擇 -->
    <div class="shortcuts">
      <button
        v-for="shortcut in shortcuts"
        :key="shortcut.label"
        class="shortcut-btn"
        @click="applyShortcut(shortcut.days)"
      >
        {{ shortcut.label }}
      </button>
    </div>

  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

// ─── Props（父元件傳進來的值） ────────────────────────────────────────────────
const props = withDefaults(defineProps<{
  startDate?: string //yyyy-MM-dd
  endDate?: string //yyyy-MM-dd
}>(), {
  startDate: '',
  endDate: '',
})

// ─── Emits（通知父元件更新值） ────────────────────────────────────────────────
// v-model:startDate / v-model:endDate 的實作方式
const emit = defineEmits<{
  'update:startDate': [value: string]
  'update:endDate': [value: string]
}>()

// ─── 今天的日期（yyyy-MM-dd 格式） ────────────────────────────────────────────
const today = computed(() => {
  return new Date().toISOString().split('T')[0]
})

// ─── 快捷按鈕設定 ─────────────────────────────────────────────────────────────
const shortcuts = [
  { label: '近 30 天', days: 30 },
  { label: '近 90 天', days: 90 },
  { label: '近 180 天', days: 180 },
  { label: '近 365 天', days: 365 },
]

function applyShortcut(days: number) {
  const end = new Date()
  const start = new Date()
  start.setDate(end.getDate() - days)

  const fmt = (d: Date) => d.toISOString().split('T')[0] ?? ''  // 加 ?? ''

  emit('update:endDate', fmt(end))
  emit('update:startDate', fmt(start))
}
</script>

<style scoped>
.date-range-picker {
  display: flex;
  align-items: flex-end;
  gap: 12px;
  flex-wrap: wrap;
}

.date-group {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.date-group label {
  font-size: 13px;
  font-weight: 600;
  color: #aaa;
}

.date-group input[type="date"] {
  padding: 6px 8px;
  border: 1px solid #444;
  border-radius: 4px;
  background: #1e1e1e;
  color: #e0e0e0;
  font-size: 14px;
  width: 160px;
}

.separator {
  font-size: 18px;
  color: #888;
  padding-bottom: 6px;
}

/* 快捷按鈕 */
.shortcuts {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
  padding-bottom: 2px;
}

.shortcut-btn {
  padding: 6px 14px;
  border-radius: 999px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  background: linear-gradient(
    180deg,
    rgba(255, 255, 255, 0.07) 0%,
    rgba(255, 255, 255, 0.03) 100%
  );
  color: rgba(190, 205, 220, 0.65);
  font-size: 12px;
  cursor: pointer;
  box-shadow:
    inset 0 1px 0 rgba(255, 255, 255, 0.07),
    0 2px 6px rgba(0, 0, 0, 0.2);
  transition: all 0.2s cubic-bezier(0.34, 1.56, 0.64, 1);
}

.shortcut-btn:hover {
  border-color: rgba(125, 216, 207, 0.3);
  color: rgba(125, 216, 207, 0.85);
  background: linear-gradient(
    180deg,
    rgba(125, 216, 207, 0.12) 0%,
    rgba(125, 216, 207, 0.05) 100%
  );
  box-shadow:
    inset 0 1px 0 rgba(255, 255, 255, 0.1),
    0 4px 12px rgba(125, 216, 207, 0.1),
    0 2px 6px rgba(0, 0, 0, 0.22);
  transform: translateY(-1px);
}

.shortcut-btn:active {
  transform: translateY(0);
}
</style>
