<!-- src/components/CitySelector.vue -->
<template>
  <div class="field-group">
    <label class="field-label">選擇縣市</label>
    <select
      class="city-select"
      :value="modelValue"
      @change="emit('update:modelValue', ($event.target as HTMLSelectElement).value)"
    >
      <option v-for="city in cities" :key="city" :value="city">{{ city }}</option>
    </select>
  </div>
</template>

<script setup lang="ts">
defineProps<{ modelValue: string }>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const cities = [
  '臺北市', '新北市', '桃園市', '臺中市', '臺南市', '高雄市',
  '基隆市', '新竹市', '嘉義市', '新竹縣', '苗栗縣', '彰化縣',
  '南投縣', '雲林縣', '嘉義縣', '屏東縣', '宜蘭縣', '花蓮縣',
  '臺東縣', '澎湖縣', '金門縣', '連江縣',
]
</script>

<style scoped>
.field-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.field-label {
  font-size: 12px;
  color: rgba(170, 185, 205, 0.5);
  font-weight: 600;
  letter-spacing: 0.05em;
  text-transform: uppercase;
}

.city-select {
  padding: 8px 14px;
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.05);
  color: rgba(215, 225, 240, 0.88);
  font-size: 14px;
  min-width: 160px;
  cursor: pointer;
  transition: border-color 0.18s;
}

.city-select option {
  background: #1a2e1f;
  color: rgba(215, 225, 240, 0.88);
}

.city-select:focus {
  outline: none;
  border-color: rgba(125, 216, 160, 0.45);
}
</style>