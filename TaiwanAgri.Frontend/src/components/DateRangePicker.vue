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
  display: flex; align-items: flex-end; gap: 12px; flex-wrap: wrap;
}

.date-group { display: flex; flex-direction: column; gap: 4px; }

.date-group label {
  font-size: 12px; font-weight: 600;
  color: var(--text-muted);
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.date-group input[type="date"] {
  padding: 7px 10px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--surface);
  color: var(--text-primary);
  font-size: 14px;
  width: 160px;
  transition: border-color 0.18s, box-shadow 0.18s;
}
.date-group input[type="date"]:focus {
  outline: none;
  border-color: var(--green);
  box-shadow: 0 0 0 3px rgba(46,125,50,0.12);
}

.separator { font-size: 18px; color: var(--text-muted); padding-bottom: 6px; }

.shortcuts { display: flex; gap: 6px; flex-wrap: wrap; padding-bottom: 2px; }

.shortcut-btn {
  padding: 6px 14px;
  border-radius: 999px;
  border: 1px solid var(--border);
  background: var(--surface);
  color: var(--text-secondary);
  font-size: 12px;
  cursor: pointer;
  transition: all 0.18s;
}
.shortcut-btn:hover {
  border-color: var(--green);
  color: var(--green);
  background: var(--green-50);
}

.shortcut-btn.active {
  border-color: var(--green);
  color: var(--green);
  background: var(--green-100);
  font-weight: 600;
}
</style>