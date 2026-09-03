<!--
  src/components/MonthCalendar.vue
  職責：一個月份的月曆網格，標出哪幾天有事（休市、警報生效日這類「這天是特殊日」）。

  只負責畫格子跟標記，不管資料從哪裡來——markedDates 是呼叫端算好的
  Set<"YYYY-MM-DD">，這個元件不知道也不需要知道那是休市日還是別的。
-->
<template>
  <div class="month-calendar">
    <div class="month-calendar__title">
      {{ year }} 年 {{ month }} 月
      <span v-if="markedCount > 0" class="month-calendar__count">{{ markedCount }} 天</span>
    </div>

    <div class="month-calendar__weekday-row">
      <span v-for="w in WEEKDAYS" :key="w" class="month-calendar__weekday">{{ w }}</span>
    </div>

    <div class="month-calendar__grid">
      <div
        v-for="(cell, i) in flatCells"
        :key="i"
        class="month-calendar__cell"
        :class="{ 'is-blank': cell.day === null, 'is-marked': cell.date !== null && markedDates.has(cell.date) }"
      >
        <span v-if="cell.day !== null" class="month-calendar__day">{{ cell.day }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { getMonthGrid } from '@/utils/calendar'

const props = defineProps<{
  year: number
  /** 1～12 */
  month: number
  /** "YYYY-MM-DD" 集合，落在集合裡的日期會標成特殊日 */
  markedDates: Set<string>
}>()

const flatCells = computed(() => getMonthGrid(props.year, props.month).flat())
const markedCount = computed(
  () => flatCells.value.filter(c => c.date !== null && props.markedDates.has(c.date)).length
)

const WEEKDAYS = ['日', '一', '二', '三', '四', '五', '六']
</script>

<style scoped>
.month-calendar {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--space-5);
  box-shadow: var(--shadow-sm);
}

.month-calendar__title {
  display: flex;
  align-items: baseline;
  gap: var(--space-2);
  font-size: var(--text-base);
  font-weight: var(--weight-bold);
  color: var(--color-text);
  margin-bottom: var(--space-3);
}

.month-calendar__count {
  font-size: var(--text-xs);
  padding: var(--space-1) var(--space-2);
  border-radius: var(--radius-full);
  background: var(--warning-50);
  color: var(--warning-700);
  font-weight: var(--weight-medium);
}

.month-calendar__weekday-row,
.month-calendar__grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
}

.month-calendar__weekday {
  text-align: center;
  padding-block: var(--space-2);
  font-size: var(--text-xs);
  font-weight: var(--weight-medium);
  color: var(--color-text-dim);
}

.month-calendar__cell {
  aspect-ratio: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: var(--radius-md);
}

.month-calendar__day {
  font-variant-numeric: tabular-nums;
  font-size: var(--text-sm);
  color: var(--color-text);
}

.month-calendar__cell.is-marked {
  background: var(--warning-50);
}
.month-calendar__cell.is-marked .month-calendar__day {
  font-weight: var(--weight-bold);
  color: var(--warning-700);
}

/* 留白格：不畫格線也不用可以互動的樣子，純粹是對齊星期幾用的空間 */
.month-calendar__cell.is-blank { visibility: hidden; }
</style>
