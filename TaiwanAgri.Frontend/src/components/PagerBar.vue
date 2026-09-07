<!--
  src/components/PagerBar.vue
  職責：分頁列 UI（頁碼按鈕、上一頁/下一頁/第一頁/最後一頁、跳頁輸入框），純顯示元件不含狀態邏輯。
  狀態邏輯（目前頁、每頁筆數、視窗頁碼、跳頁輸入值）仍由 usePagination composable 管理，
  這個元件只負責把那份狀態畫出來、把事件轉發回去——跳頁用的 jumpPageInput／handleJumpPage
  直接重用 usePagination 既有的邏輯（跟 ViolationWallView／OrganicCertView 同一份），
  不重新寫一套，這樣需要多張表格的頁面（例如 LegalBusinessView 兩個分頁籤各一份分頁）
  也不用複製貼上同一段 template。
-->
<template>
  <div class="pagination-bar">
    <span class="pagination-info">
      共 {{ totalCount }} 筆，第 {{ currentPage }} / {{ totalPages }} 頁
    </span>
    <div class="pagination-controls">
      <!-- 每頁筆數是選填的：只有資料量大到需要調整的頁面才傳 pageSizeOptions。
           先前 ViolationWallView 與 OrganicCertView 因為這個元件沒有這一格，
           整條分頁列自己重寫了一份——共用元件少一個選項，代價是兩份重複的 template。 -->
      <div v-if="pageSizeOptions?.length" class="page-size-group">
        <span class="jump-label">每頁</span>
        <select
          class="form-control page-size-select"
          :value="pageSize"
          @change="emit('update:pageSize', Number(($event.target as HTMLSelectElement).value))"
        >
          <option v-for="n in pageSizeOptions" :key="n" :value="n">{{ n }} 筆</option>
        </select>
      </div>

      <button class="page-btn" :disabled="currentPage <= 1" title="第一頁" @click="emit('change', 1)">
        <span class="mdi mdi-page-first" />
      </button>
      <button class="page-btn" :disabled="currentPage <= 1" @click="emit('change', currentPage - 1)">
        <span class="mdi mdi-chevron-left" />
      </button>
      <button
        v-for="p in visiblePages" :key="p" class="page-btn" :class="{ active: p === currentPage }"
        @click="emit('change', p)"
      >{{ p }}</button>
      <button class="page-btn" :disabled="currentPage >= totalPages" @click="emit('change', currentPage + 1)">
        <span class="mdi mdi-chevron-right" />
      </button>
      <button class="page-btn" :disabled="currentPage >= totalPages" title="最後一頁" @click="emit('change', totalPages)">
        <span class="mdi mdi-page-last" />
      </button>

      <div v-if="!hideJump" class="jump-to-page">
        <span class="jump-label">跳至</span>
        <input
          :value="jumpPageInput"
          type="number" min="1" :max="totalPages" class="jump-input"
          @input="emit('update:jumpPageInput', toNullableNumber(($event.target as HTMLInputElement).value))"
          @keyup.enter="emit('jump')"
        />
        <span class="jump-label">頁</span>
        <button class="jump-btn" @click="emit('jump')">Go</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
withDefaults(
  defineProps<{
    currentPage: number
    totalPages: number
    totalCount: number
    visiblePages: number[]
    /** 跳頁輸入框目前的值，由呼叫端的 usePagination().jumpPageInput 透過 v-model 傳入 */
    jumpPageInput?: number | null
    /** 每頁筆數。只有同時給 pageSizeOptions 時那一格才會出現 */
    pageSize?: number
    /** 可選的每頁筆數。不給就不顯示這一格——多數頁面的資料量不需要讓使用者調 */
    pageSizeOptions?: number[]
    /**
     * 隱藏「跳至第 N 頁」那一格。用在「一頁裡有多個各自分頁的表格」的場合
     * （農藥查詢）：那裡沒有共用的 jumpPageInput 狀態，也不需要跳頁，只要頁碼按鈕與每頁筆數。
     */
    hideJump?: boolean
  }>(),
  { jumpPageInput: null, pageSize: undefined, pageSizeOptions: undefined, hideJump: false },
)

const emit = defineEmits<{
  change: [page: number]
  'update:jumpPageInput': [value: number | null]
  'update:pageSize': [value: number]
  /** 使用者按 Enter 或點 Go：呼叫端接到後執行 usePagination().handleJumpPage() */
  jump: []
}>()

/** input 清空時 value 是空字串，轉成 null 比轉成 NaN 更符合 jumpPageInput 的型別語意 */
function toNullableNumber(raw: string): number | null {
  return raw === '' ? null : Number(raw)
}
</script>

<style scoped>
/* 顏色全部改用 semantic 層；所有控制項的高度吃 --control-h-sm，
   一整條分頁列因此是同一個高度，不再是「按鈕 32、輸入框 27、下拉 26」各自為政。 */
.pagination-bar { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: var(--space-3); }
.pagination-info { font-size: var(--text-xs); color: var(--color-text-dim); font-variant-numeric: tabular-nums; }
.pagination-controls { display: flex; align-items: center; gap: var(--space-1); }

.page-btn {
  min-width: var(--control-h-sm); height: var(--control-h-sm);
  padding: 0 var(--space-2); border-radius: var(--radius-md);
  border: var(--border-width) solid var(--color-border);
  background: var(--color-surface); color: var(--color-text);
  font-family: var(--font-num); font-size: var(--text-sm);
  font-variant-numeric: tabular-nums;
  cursor: pointer; display: flex; align-items: center; justify-content: center;
  transition:
    background var(--duration-fast) var(--ease-work),
    border-color var(--duration-fast) var(--ease-work),
    color var(--duration-fast) var(--ease-work);
}
.page-btn:hover:not(:disabled) { border-color: var(--color-action); color: var(--color-action); background: var(--color-action-soft); }
.page-btn:focus-visible { outline: none; border-color: var(--color-action); box-shadow: var(--shadow-focus); }
.page-btn:disabled { opacity: 0.4; cursor: not-allowed; }
/* 目前頁是實心的：一整排相同的方框裡，只有填色分得夠開 */
.page-btn.active {
  background: var(--color-action); border-color: var(--seed-700);
  color: var(--color-on-action); font-weight: var(--weight-medium);
}

.page-size-group {
  display: flex; align-items: center; gap: var(--space-2);
  margin-right: var(--space-2); padding-right: var(--space-2);
  border-right: var(--border-width) solid var(--color-border);
}
.page-size-select { min-height: var(--control-h-sm); font-size: var(--text-xs); }

.jump-to-page {
  display: flex; align-items: center; gap: var(--space-1);
  margin-left: var(--space-2); padding-left: var(--space-2);
  border-left: var(--border-width) solid var(--color-border);
}
.jump-label { font-size: var(--text-xs); color: var(--color-text-dim); white-space: nowrap; }
.jump-input {
  width: 52px; min-height: var(--control-h-sm); padding: 0 var(--space-2);
  border-radius: var(--radius-md);
  border: var(--border-width) solid var(--color-border);
  background: var(--color-surface); color: var(--color-text);
  font-family: var(--font-num); font-size: var(--text-sm);
  text-align: center; outline: none;
  transition: border-color var(--duration-fast) var(--ease-work), box-shadow var(--duration-fast) var(--ease-work);
}
.jump-input:focus { border-color: var(--color-action); box-shadow: var(--shadow-focus); }
.jump-btn {
  min-height: var(--control-h-sm); padding: 0 var(--space-3);
  border-radius: var(--radius-md);
  border: var(--border-width) solid var(--seed-700);
  background: var(--color-action); color: var(--color-on-action);
  font-family: inherit; font-size: var(--text-xs); font-weight: var(--weight-bold);
  cursor: pointer;
  transition: background var(--duration-fast) var(--ease-work);
}
.jump-btn:hover { background: var(--color-action-hover); }
.jump-btn:focus-visible { outline: none; box-shadow: var(--shadow-focus); }
</style>
