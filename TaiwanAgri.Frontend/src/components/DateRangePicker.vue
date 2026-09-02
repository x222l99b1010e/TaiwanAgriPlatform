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
  color: var(--neutral-400);
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.date-group input[type="date"] {
  padding: var(--space-2) var(--space-3);
  border: 1px solid var(--neutral-200);
  border-radius: var(--radius-md);
  background: var(--neutral-0);
  color: var(--neutral-900);
  font-size: var(--text-base);
  width: 160px;
  transition: border-color var(--duration-fast), box-shadow var(--duration-fast);
}
.date-group input[type="date"]:focus {
  outline: none;
  border-color: var(--green-600);
  box-shadow: var(--shadow-focus);
}

.separator { font-size: var(--text-lg); color: var(--neutral-400); padding-bottom: var(--space-2); }

.shortcuts { display: flex; gap: var(--space-2); flex-wrap: wrap; padding-bottom: var(--space-1); }

.shortcut-btn {
  padding: var(--space-2) var(--space-4);
  border-radius: var(--radius-full);
  border: 1px solid var(--neutral-200);
  background: var(--neutral-0);
  color: var(--neutral-500);
  font-size: var(--text-xs);
  cursor: pointer;
  transition: all var(--duration-fast);
}
.shortcut-btn:hover {
  border-color: var(--green-600);
  color: var(--green-600);
  background: var(--green-50);
}

.shortcut-btn.active {
  border-color: var(--green-600);
  color: var(--green-600);
  background: var(--green-100);
  font-weight: var(--weight-medium);
}
</style>