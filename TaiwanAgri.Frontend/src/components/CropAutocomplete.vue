<!--
  src/components/CropAutocomplete.vue
  職責：作物名稱的自動完成輸入框（農場設定、監看清單共用）。
  打字列出建議；↓／↑ 在建議裡移動、Enter 選取、Esc 收起，滑鼠點選也可以。

  原本兩頁各寫一份，而且只能用滑鼠點（方向鍵、Enter 選不到），抽成一份一起補上鍵盤操作。
  標記照 WAI-ARIA 的 combobox 模式：輸入框 role="combobox"、建議清單 role="listbox"，
  反白的那一筆用 aria-activedescendant 指過去——焦點一直留在輸入框，可以邊打字邊選。
  過濾與方向鍵的邏輯在 utils/cropAutocomplete.ts，有單元測試。
-->
<template>
  <div class="crop-autocomplete">
    <input
      :id="inputId"
      v-model="text"
      class="form-control crop-autocomplete__input"
      role="combobox"
      aria-autocomplete="list"
      :aria-expanded="isOpen"
      :aria-controls="listId"
      :aria-activedescendant="activeIndex >= 0 ? optionId(activeIndex) : undefined"
      :placeholder="placeholder"
      :disabled="disabled"
      autocomplete="off"
      @input="onInput"
      @keydown="onKeydown"
      @blur="close"
    />
    <ul v-show="isOpen" :id="listId" class="crop-autocomplete__list" role="listbox">
      <!-- mousedown.prevent：點選項時不讓輸入框失焦，否則 blur 先把清單收起來、click 就落空 -->
      <li
        v-for="(crop, i) in suggestions"
        :id="optionId(i)"
        :key="crop.cropCode"
        role="option"
        :aria-selected="i === activeIndex"
        class="crop-autocomplete__item"
        :class="{ 'is-active': i === activeIndex }"
        @mousedown.prevent="choose(crop)"
        @mousemove="activeIndex = i"
      >
        {{ crop.cropName }}
        <span class="crop-autocomplete__code">{{ crop.cropCode }}</span>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, useId, watch } from 'vue'
import { cropSuggestions, nextActiveIndex, type CropOption } from '@/utils/cropAutocomplete'

const props = withDefaults(defineProps<{
  options: CropOption[]
  /** 已選的作物代碼，不再出現在建議裡 */
  excludeCodes?: string[]
  placeholder?: string
  disabled?: boolean
  /** 給頁面上的 <label for> 對到輸入框（點標籤會聚焦、報讀軟體念得出欄位名稱） */
  inputId?: string
}>(), {
  excludeCodes: () => [],
  placeholder: '',
  disabled: false,
  inputId: undefined,
})

const emit = defineEmits<{ select: [crop: CropOption] }>()

// 同一頁可能有兩個這個元件，id 用 useId() 產生（比照 CitySelector）
const uid = useId()
const listId = `${uid}-list`
const optionId = (i: number) => `${uid}-opt-${i}`

const text = ref('')
const isOpen = ref(false)
const activeIndex = ref(-1)

const suggestions = computed(() => cropSuggestions(props.options, text.value, props.excludeCodes))

function onInput() {
  activeIndex.value = -1
  isOpen.value = suggestions.value.length > 0
}

function close() {
  isOpen.value = false
  activeIndex.value = -1
}

function choose(crop: CropOption) {
  emit('select', crop)
  text.value = ''
  close()
}

function onKeydown(e: KeyboardEvent) {
  const count = suggestions.value.length
  if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
    if (count === 0) return
    e.preventDefault() // 不讓游標跳到輸入框頭尾
    isOpen.value = true
    activeIndex.value = nextActiveIndex(activeIndex.value, count, e.key === 'ArrowDown' ? 1 : -1)
  } else if (e.key === 'Enter') {
    if (!isOpen.value) return
    // 沒有反白、但只剩一筆建議時，Enter 直接選它
    const pick = activeIndex.value >= 0
      ? suggestions.value[activeIndex.value]
      : count === 1 ? suggestions.value[0] : undefined
    if (pick) {
      e.preventDefault()
      choose(pick)
    }
  } else if (e.key === 'Escape' && isOpen.value) {
    e.preventDefault()
    close()
  }
}

// 清單超過可視高度時，方向鍵移到看不見的那一筆要把它捲進來
watch(activeIndex, async i => {
  if (i < 0) return
  await nextTick()
  document.getElementById(optionId(i))?.scrollIntoView({ block: 'nearest' })
})
</script>

<style scoped>
.crop-autocomplete { position: relative; }
.crop-autocomplete__input { width: 100%; box-sizing: border-box; }

/* 這一層是真的浮在頁面上方的浮動層，所以准用陰影（浮動層是陰影的唯一例外） */
.crop-autocomplete__list {
  position: absolute; top: calc(100% + var(--space-1)); left: 0; right: 0;
  margin: 0; list-style: none;
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-float);
  z-index: var(--z-dropdown); max-height: 240px; overflow-y: auto;
  padding: var(--space-1);
}

.crop-autocomplete__item {
  display: flex; justify-content: space-between; align-items: center; gap: var(--space-3);
  padding: var(--space-2) var(--space-3);
  border-radius: var(--radius-sm);
  cursor: pointer; font-size: var(--text-sm); color: var(--color-text);
}
/* 滑鼠移過與方向鍵反白是同一個狀態（mousemove 會改 activeIndex），只畫一種樣式 */
.crop-autocomplete__item.is-active { background: var(--color-action-soft); }

.crop-autocomplete__code {
  font-family: var(--font-num); font-size: var(--text-xs); color: var(--color-text-dim);
}
</style>
