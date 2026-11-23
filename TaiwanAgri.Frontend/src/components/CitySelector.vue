<template>
  <div class="field-group">
    <label class="field-label" :for="selectId">{{ label }}</label>
    <select
      :id="selectId"
      class="form-control city-select"
      :value="modelValue"
      @change="emit('update:modelValue', ($event.target as HTMLSelectElement).value)"
    >
      <option v-if="includeAll" value="">{{ allLabel }}</option>
      <option v-for="city in cities" :key="city" :value="city">{{ city }}</option>
    </select>
  </div>
</template>

<script setup lang="ts">
import { useId } from 'vue'
import { TAIWAN_CITIES } from '@/constants/taiwanCities'

// includeAll：地圖／表格類篩選預設要看「全部」時開啟；既有的天氣模組固定要選單一縣市，
// 不受影響（預設 false，行為與原本一致）。
// label／allLabel 可覆寫，是因為同一個下拉在不同情境問的不是同一件事——
// 篩選頁問的是「要看哪個縣市」，通知規則表單問的是「這條規則只管哪個縣市」，
// 而「全部縣市」在後者的語意是「不限縣市」。預設值與原本相同，既有呼叫端不必改。
withDefaults(
  defineProps<{ modelValue: string; includeAll?: boolean; label?: string; allLabel?: string }>(),
  { includeAll: false, label: '選擇縣市', allLabel: '全部縣市' },
)

// label 要指向 select 才點得到、螢幕閱讀器也才唸得出這個下拉在問什麼。
// id 用 useId() 產生而不是寫死字串：同一頁可能出現兩個 CitySelector，
// 寫死會讓兩個 label 都指向第一個 select。
const selectId = useId()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const cities = TAIWAN_CITIES
</script>

<style scoped>
/* 欄位外殼走 base.css 的 .field-group／.field-label／.form-control，
   這裡只留這個元件真正不同的部分 */
.city-select { min-width: 160px; }
</style>
