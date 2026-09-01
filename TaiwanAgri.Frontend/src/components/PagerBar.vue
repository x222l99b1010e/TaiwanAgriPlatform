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

      <div class="jump-to-page">
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
defineProps<{
  currentPage: number
  totalPages: number
  totalCount: number
  visiblePages: number[]
  /** 跳頁輸入框目前的值，由呼叫端的 usePagination().jumpPageInput 透過 v-model 傳入 */
  jumpPageInput: number | null
}>()

const emit = defineEmits<{
  change: [page: number]
  'update:jumpPageInput': [value: number | null]
  /** 使用者按 Enter 或點 Go：呼叫端接到後執行 usePagination().handleJumpPage() */
  jump: []
}>()

/** input 清空時 value 是空字串，轉成 null 比轉成 NaN 更符合 jumpPageInput 的型別語意 */
function toNullableNumber(raw: string): number | null {
  return raw === '' ? null : Number(raw)
}
</script>

<style scoped>
.pagination-bar { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: var(--space-3); }
.pagination-info { font-size: var(--text-xs); color: var(--text-muted); }
.pagination-controls { display: flex; align-items: center; gap: var(--space-1); }
.page-btn {
  min-width: 32px; height: 32px; padding: 0 var(--space-2); border-radius: var(--radius-md);
  border: 1px solid var(--border); background: var(--surface); color: var(--text-primary);
  font-size: var(--text-sm); cursor: pointer; display: flex; align-items: center; justify-content: center;
}
.page-btn:hover:not(:disabled) { border-color: var(--green); color: var(--green); }
.page-btn:disabled { opacity: 0.4; cursor: not-allowed; }
.page-btn.active { background: var(--green); border-color: var(--green); color: var(--neutral-0); }

.jump-to-page {
  display: flex; align-items: center; gap: var(--space-1);
  margin-left: var(--space-2); padding-left: var(--space-2); border-left: 1px solid var(--border);
}
.jump-label { font-size: var(--text-xs); color: var(--text-muted); white-space: nowrap; }
.jump-input {
  width: 50px; padding: var(--space-1) var(--space-2); border-radius: var(--radius-md);
  border: 1px solid var(--border); font-size: var(--text-sm); text-align: center; outline: none;
}
.jump-input:focus { border-color: var(--green); }
.jump-btn {
  padding: var(--space-1) var(--space-3); border-radius: var(--radius-md); border: 1px solid var(--green);
  background: var(--green); color: var(--neutral-0); font-size: var(--text-xs); font-weight: var(--weight-bold); cursor: pointer;
}
.jump-btn:hover { background: var(--green-hover); }
</style>
