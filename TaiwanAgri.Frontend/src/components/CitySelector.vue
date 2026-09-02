<template>
  <div class="field-group">
    <label class="field-label">選擇縣市</label>
    <select
      class="city-select"
      :value="modelValue"
      @change="emit('update:modelValue', ($event.target as HTMLSelectElement).value)"
    >
      <option v-if="includeAll" value="">全部縣市</option>
      <option v-for="city in cities" :key="city" :value="city">{{ city }}</option>
    </select>
  </div>
</template>

<script setup lang="ts">
// includeAll：地圖／表格類篩選預設要看「全部」時開啟；既有的天氣模組固定要選單一縣市，
// 不受影響（預設 false，行為與原本一致）
withDefaults(defineProps<{ modelValue: string; includeAll?: boolean }>(), {
  includeAll: false,
})
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const cities = [
  '臺北市','新北市','桃園市','臺中市','臺南市','高雄市',
  '基隆市','新竹市','嘉義市','新竹縣','苗栗縣','彰化縣',
  '南投縣','雲林縣','嘉義縣','屏東縣','宜蘭縣','花蓮縣',
  '臺東縣','澎湖縣','金門縣','連江縣',
]
</script>

<style scoped>
.field-group { display: flex; flex-direction: column; gap: var(--space-2); }

.field-label {
  font-size: var(--text-xs);
  color: var(--neutral-400);
  font-weight: var(--weight-medium);
  letter-spacing: 0.05em;
  text-transform: uppercase;
}

.city-select {
  padding: var(--space-2) var(--space-4);
  border: 1px solid var(--neutral-200);
  border-radius: var(--radius-md);
  background: var(--neutral-0);
  color: var(--neutral-900);
  font-size: var(--text-base);
  min-width: 160px;
  cursor: pointer;
  transition: border-color var(--duration-fast), box-shadow var(--duration-fast);
}
.city-select:focus {
  outline: none;
  border-color: var(--green-600);
  box-shadow: var(--shadow-focus);
}
</style>
