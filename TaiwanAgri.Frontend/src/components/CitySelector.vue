<template>
  <div class="field-group">
    <label class="field-label" :for="selectId">選擇縣市</label>
    <select
      :id="selectId"
      class="form-control city-select"
      :value="modelValue"
      @change="emit('update:modelValue', ($event.target as HTMLSelectElement).value)"
    >
      <option v-if="includeAll" value="">全部縣市</option>
      <option v-for="city in cities" :key="city" :value="city">{{ city }}</option>
    </select>
  </div>
</template>

<script setup lang="ts">
import { useId } from 'vue'

// includeAll：地圖／表格類篩選預設要看「全部」時開啟；既有的天氣模組固定要選單一縣市，
// 不受影響（預設 false，行為與原本一致）
withDefaults(defineProps<{ modelValue: string; includeAll?: boolean }>(), {
  includeAll: false,
})

// label 要指向 select 才點得到、螢幕閱讀器也才唸得出這個下拉在問什麼。
// id 用 useId() 產生而不是寫死字串：同一頁可能出現兩個 CitySelector，
// 寫死會讓兩個 label 都指向第一個 select。
const selectId = useId()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const cities = [
  '臺北市','新北市','桃園市','臺中市','臺南市','高雄市',
  '基隆市','新竹市','嘉義市','新竹縣','苗栗縣','彰化縣',
  '南投縣','雲林縣','嘉義縣','屏東縣','宜蘭縣','花蓮縣',
  '臺東縣','澎湖縣','金門縣','連江縣',
]
</script>

<style scoped>
/* 欄位外殼走 base.css 的 .field-group／.field-label／.form-control，
   這裡只留這個元件真正不同的部分 */
.city-select { min-width: 160px; }
</style>
